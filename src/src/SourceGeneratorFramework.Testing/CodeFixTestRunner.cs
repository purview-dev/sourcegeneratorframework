using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Executes an analyzer and applies one code fix to a test document.
/// </summary>
public sealed class CodeFixTestRunner<TAnalyzer, TCodeFix> : RoslynTestRunner
	where TAnalyzer : DiagnosticAnalyzer, new()
	where TCodeFix : CodeFixProvider, new()
{
	/// <summary>
	/// Runs the analyzer and applies a registered code action.
	/// </summary>
	public async Task<CodeFixTestResult> RunAsync(
		string source,
		CodeFixTestOptions? options = null,
		CancellationToken cancellationToken = default
	)
	{
		options ??= new();
		using var testProject = CreateProject([source], options, typeof(TAnalyzer).Assembly);
		var compilation =
			await testProject.Project.GetCompilationAsync(cancellationToken)
			?? throw new InvalidOperationException("Unable to create the test compilation.");
		var diagnostics = await WithAnalyzers(compilation, [new TAnalyzer()], options)
			.GetAnalyzerDiagnosticsAsync(cancellationToken);
		var diagnostic =
			diagnostics.FirstOrDefault(diagnostic => diagnostic.Location.IsInSource)
			?? throw new InvalidOperationException("The analyzer did not report a source diagnostic.");
		var document = testProject.Project.Solution.GetDocument(testProject.DocumentIds[0])!;
		var actions = RegisterActions(document, diagnostic, cancellationToken);
		var action = SelectAction(actions, options);

		var operations = await action.GetOperationsAsync(cancellationToken);
		var changedSolution =
			operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution
			?? throw new InvalidOperationException("The code action did not produce an ApplyChangesOperation.");
		var changedDocument =
			changedSolution.GetDocument(document.Id)
			?? throw new InvalidOperationException("The code action removed the source document.");
		var fixedSource = (await changedDocument.GetTextAsync(cancellationToken)).ToString();

		return new(diagnostics, [.. actions], fixedSource, compilation, changedSolution);
	}

	/// <summary>
	/// Runs the analyzer and applies the registered code action to every diagnostic in the project using the
	/// code fix's <see cref="FixAllProvider"/>, then returns each document's fixed source.
	/// </summary>
	public async Task<CodeFixFixAllResult> RunFixAllAsync(
		IEnumerable<string> sources,
		CodeFixTestOptions? options = null,
		CancellationToken cancellationToken = default
	)
	{
		options ??= new();
		using var testProject = CreateProject(sources, options, typeof(TAnalyzer).Assembly);
		var compilation =
			await testProject.Project.GetCompilationAsync(cancellationToken)
			?? throw new InvalidOperationException("Unable to create the test compilation.");
		var diagnostics = await WithAnalyzers(compilation, [new TAnalyzer()], options)
			.GetAnalyzerDiagnosticsAsync(cancellationToken);
		var sourceDiagnostics = diagnostics.Where(diagnostic => diagnostic.Location.IsInSource).ToImmutableArray();
		if (sourceDiagnostics.IsEmpty)
			throw new InvalidOperationException("The analyzer did not report a source diagnostic.");

		var firstDiagnostic = sourceDiagnostics[0];
		var document =
			await GetDocumentForDiagnosticAsync(testProject, firstDiagnostic, cancellationToken)
			?? throw new InvalidOperationException("Unable to locate the document containing the diagnostic.");
		var actions = RegisterActions(document, firstDiagnostic, cancellationToken);
		var action = SelectAction(actions, options);

		TCodeFix codeFixProvider = new();
		var fixAllAction =
			await RunFixAllProviderAsync(document, codeFixProvider, action, sourceDiagnostics, cancellationToken)
			?? throw new InvalidOperationException("The FixAllProvider did not produce a code action.");

		var operations = await fixAllAction.GetOperationsAsync(cancellationToken);
		var changedSolution =
			operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution
			?? throw new InvalidOperationException("The fix-all action did not produce an ApplyChangesOperation.");

		var fixedSources = ImmutableDictionary.CreateBuilder<string, string>();
		foreach (var documentId in testProject.DocumentIds)
		{
			var changedDocument = changedSolution.GetDocument(documentId);
			if (changedDocument is null)
				continue;

			fixedSources[changedDocument.Name] = (await changedDocument.GetTextAsync(cancellationToken)).ToString();
		}

		return new(diagnostics, [.. actions], fixedSources.ToImmutable(), changedSolution);
	}

	static async Task<CodeAction?> RunFixAllProviderAsync(
		Document document,
		TCodeFix codeFixProvider,
		CodeAction action,
		ImmutableArray<Diagnostic> diagnostics,
		CancellationToken cancellationToken
	)
	{
		var fixAllProvider =
			codeFixProvider.GetFixAllProvider()
			?? throw new InvalidOperationException("The code fix provider has no FixAllProvider.");

		FixAllContext fixAllContext = new(
			document,
			codeFixProvider,
			FixAllScope.Project,
			action.EquivalenceKey ?? string.Empty,
			codeFixProvider.FixableDiagnosticIds,
			new TestDiagnosticProvider(diagnostics),
			cancellationToken
		);

		return await fixAllProvider.GetFixAsync(fixAllContext);
	}

	static ImmutableArray<CodeAction> RegisterActions(
		Document document,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	)
	{
		List<CodeAction> actions = [];
		CodeFixContext context = new(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
		new TCodeFix().RegisterCodeFixesAsync(context).GetAwaiter().GetResult();

		return [.. actions];
	}

	static CodeAction SelectAction(ImmutableArray<CodeAction> actions, CodeFixTestOptions options)
	{
		var action = options.EquivalenceKey is null
			? actions.ElementAtOrDefault(options.CodeActionIndex)
			: actions.FirstOrDefault(action => action.EquivalenceKey == options.EquivalenceKey);
		action = action ?? throw new InvalidOperationException("The requested code action was not registered.");

		return action;
	}

	static async Task<Document?> GetDocumentForDiagnosticAsync(
		TestProject testProject,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	)
	{
		var tree = diagnostic.Location.SourceTree;
		if (tree is null)
			return null;

		foreach (var documentId in testProject.DocumentIds)
		{
			var document = testProject.Project.Solution.GetDocument(documentId);
			if (document is null)
				continue;

			var documentTree = await document.GetSyntaxTreeAsync(cancellationToken);

			if (documentTree == tree || string.Equals(documentTree?.FilePath, tree.FilePath, StringComparison.Ordinal))
				return document;
		}

		return null;
	}

	sealed class TestDiagnosticProvider(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
	{
		public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
			Document document,
			CancellationToken cancellationToken
		)
		{
			var tree = await document.GetSyntaxTreeAsync(cancellationToken);

			return diagnostics
				.Where(diagnostic =>
					diagnostic.Location.SourceTree == tree
					|| string.Equals(diagnostic.Location.SourceTree?.FilePath, tree?.FilePath, StringComparison.Ordinal)
				)
				.ToList();
		}

		public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
			Project project,
			CancellationToken cancellationToken
		) => Task.FromResult<IEnumerable<Diagnostic>>([]);

		public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
			Project project,
			CancellationToken cancellationToken
		) => Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
	}
}
