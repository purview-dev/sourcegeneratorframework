using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Executes a code refactoring against a test document and returns the refactored source.
/// </summary>
public sealed class RefactoringTestRunner<TRefactoring> : RoslynTestRunner
	where TRefactoring : CodeRefactoringProvider, new()
{
	/// <summary>
	/// Runs the refactoring against one source file.
	/// </summary>
	public Task<RefactorTestResult> RunAsync(
		string source,
		RefactorTestOptions? options = null,
		CancellationToken cancellationToken = default
	) => RunAsync([source], options, cancellationToken);

	/// <summary>
	/// Runs the refactoring against the supplied source files.
	/// </summary>
	public async Task<RefactorTestResult> RunAsync(
		IEnumerable<string> sources,
		RefactorTestOptions? options = null,
		CancellationToken cancellationToken = default
	)
	{
		options ??= new();
		using var testProject = CreateProject(sources, options, typeof(TRefactoring).Assembly);
		var compilation =
			await testProject.Project.GetCompilationAsync(cancellationToken)
			?? throw new InvalidOperationException("Unable to create the test compilation.");

		var document =
			testProject.Project.Solution.GetDocument(testProject.DocumentIds[0])
			?? throw new InvalidOperationException("Unable to locate the test document.");

		var span = ResolveSpan(options, compilation);

		List<CodeAction> actions = [];
		CodeRefactoringContext context = new(document, span, actions.Add, cancellationToken);
		await new TRefactoring().ComputeRefactoringsAsync(context);

		var action =
			(
				options.EquivalenceKey is null
					? actions.ElementAtOrDefault(options.CodeActionIndex)
					: actions.FirstOrDefault(candidate => candidate.EquivalenceKey == options.EquivalenceKey)
			) ?? throw new InvalidOperationException("The requested refactoring was not registered.");

		var operations = await action.GetOperationsAsync(cancellationToken);
		var changedSolution =
			operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution
			?? throw new InvalidOperationException("The refactoring did not produce an ApplyChangesOperation.");

		var fixedSources = ImmutableDictionary.CreateBuilder<string, string>();
		foreach (var documentId in testProject.DocumentIds)
		{
			var changedDocument = changedSolution.GetDocument(documentId);
			if (changedDocument is null)
				continue;

			fixedSources[changedDocument.Name] = (await changedDocument.GetTextAsync(cancellationToken)).ToString();
		}

		return new([.. actions], fixedSources.ToImmutable(), changedSolution, compilation);
	}

	static TextSpan ResolveSpan(RefactorTestOptions options, Compilation compilation)
	{
		if (options.Span is { } explicitSpan)
			return explicitSpan;

		if (options.NodeSelector is not null)
		{
			CodeQuery query = new([.. compilation.SyntaxTrees], compilation);

			return options.NodeSelector(query).Span;
		}

		throw new InvalidOperationException(
			"RefactorTestOptions requires a Span or NodeSelector to determine the refactoring trigger."
		);
	}
}
