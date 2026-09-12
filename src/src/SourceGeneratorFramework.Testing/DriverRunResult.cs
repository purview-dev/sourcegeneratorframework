using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.SourceGeneratorFramework.Logging;
using Purview.SourceGeneratorFramework.Testing.Models;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// The result of a source generator test run.
/// </summary>
/// <param name="AllSyntaxTrees">All syntax trees generated during the run.</param>
/// <param name="PrimarySyntaxTrees">
/// The primary syntax trees generated during the run, excluding anything detailed by <see cref="SourceGeneratorTestOptions.ExcludeGeneratedSourceHintNames"/>.
/// <para>
/// This is useful for excluding generated source such as attribute markers.
/// </para>
/// </param>
/// <param name="LogEntries">The log entries generated during the run.</param>
/// <param name="AnalyzerResult">The result of the analyzer compilation run, if any.</param>
/// <param name="CompilationResult">The result of the compilation run.</param>
/// <param name="DriverResult">The result of the generator driver run.</param>
public sealed record class DriverRunResult(
	GeneratorDriverRunResult DriverResult,
	CompilationRunResult CompilationResult,
	AnalyzerCompilationRunResult? AnalyzerResult,
	ImmutableArray<SyntaxTree> AllSyntaxTrees,
	ImmutableArray<SyntaxTree> PrimarySyntaxTrees,
	ImmutableArray<LogEntry> LogEntries
) : IDisposable
{
	/// <summary>
	/// Unloads the compiled test assembly and releases the metadata view, when one was produced.
	/// </summary>
	/// <remarks>
	/// Only disposes the compiled output; the <see cref="Compilation"/> and generated trees remain usable.
	/// The result (and the <see cref="Assembly"/>/metadata it exposes) must not be used after disposal.
	/// </remarks>
	public void Dispose() => CompilationResult.Emitted?.Dispose();

	/// <summary>
	/// Throws <see cref="DriverRunValidationException"/> containing all generation exceptions,
	/// compilation errors, emit errors, and generator log errors found in the run.
	/// </summary>
	public void EnsureValid()
	{
		var generationExceptions = DriverResult
			.Results.Where(result => result.Exception is not null)
			.Select(result => new GeneratorFailure(
				result.Generator.GetType().FullName ?? result.Generator.GetType().Name,
				result.Exception!
			))
			.ToList();

		var compilationErrors = CompilationResult
			.Compilation.GetDiagnostics()
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToList();
		var logErrors = LogEntries.Where(e => e.Type == SourceGenLogLevel.Fatal).ToList();
		HashSet<string> compilationErrorKeys = [.. compilationErrors.Select(GetDiagnosticKey)];
		var emitErrors = CompilationResult
			.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
			.Where(diagnostic => !compilationErrorKeys.Contains(GetDiagnosticKey(diagnostic)))
			.ToList();

		if (
			generationExceptions.Count == 0
			&& compilationErrors.Count == 0
			&& emitErrors.Count == 0
			&& logErrors.Count == 0
		)
			return;

		throw new DriverRunValidationException(
			this,
			generationExceptions,
			compilationErrors,
			emitErrors,
			logErrors,
			AllSyntaxTrees,
			CompilationResult.Compilation.SyntaxTrees
		);
	}

	static string GetDiagnosticKey(Diagnostic diagnostic) =>
		$"{diagnostic.Id}|{diagnostic.Location.SourceTree?.FilePath}|{diagnostic.Location.SourceSpan.Start}|{diagnostic.Location.SourceSpan.Length}|{diagnostic.GetMessage(CultureInfo.InvariantCulture)}";

	/// <summary>
	/// Gets the source text of a generated tree with the specified hint name.
	/// </summary>
	/// <param name="hintName">The hint name of the generated tree - this can be the whole of end of the hint name.</param>
	/// <param name="matchMode">The mode to use when matching the hint name.</param>
	/// <returns>The source text of the generated tree, or <see langword="null"/> if not found.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="hintName"/> is <see langword="null"/> or whitespace.</exception>
	/// <remarks>
	/// The <paramref name="hintName"/> is matched using <see cref="StringComparison.Ordinal"/>.
	/// </remarks>
	public string? GetSource(string hintName, HintNameMatchMode matchMode = HintNameMatchMode.Suffix)
	{
		if (string.IsNullOrWhiteSpace(hintName))
			throw new ArgumentException("Value cannot be null or whitespace.", nameof(hintName));

		var hasExtension = hintName.EndsWith(".cs", StringComparison.Ordinal);
		var predicate = matchMode switch
		{
			HintNameMatchMode.Suffix => new Func<string, bool>(s =>
			{
				if (!hasExtension)
				{
					var postSuffix = ".cs";
					if (
						s.EndsWith(hintName + postSuffix, StringComparison.Ordinal)
						|| s.EndsWith(hintName + ".g" + postSuffix, StringComparison.Ordinal)
					)
						return true;
				}

				return s.EndsWith(hintName, StringComparison.Ordinal);
			}),
			HintNameMatchMode.Partial => new Func<string, bool>(s => s.Contains(hintName, StringComparison.Ordinal)),
			HintNameMatchMode.Exact => new Func<string, bool>(s => s.Equals(hintName, StringComparison.Ordinal)),
			_ => throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Invalid match mode."),
		};

		// Find the generated source with the specified hint name
		return DriverResult
			.Results.SelectMany(static r => r.GeneratedSources)
			.Where(s => predicate(s.HintName))
			.Select(static s => s.SourceText.ToString())
			.SingleOrDefault();
	}

	/// <summary>
	/// Gets the source text of the first primary generated tree.
	/// </summary>
	public string GetSource()
	{
		var tree = PrimarySyntaxTrees.FirstOrDefault();
		return tree?.GetText().ToString() ?? string.Empty;
	}

	/// <summary>
	/// Finds a generated tree whose file path ends with the specified suffix.
	/// </summary>
	/// <param name="filePathSuffix">The suffix to match.</param>
	/// <returns>The matching syntax tree, or <see langword="null"/> if none is found.</returns>
	public SyntaxTree? GetGeneratedTree(string filePathSuffix) =>
		AllSyntaxTrees.FirstOrDefault(tree => tree.FilePath.EndsWith(filePathSuffix, StringComparison.Ordinal));

	/// <summary>
	/// Gets the symbol for a type by its metadata name.
	/// </summary>
	/// <param name="identity">The identity of the type.</param>
	/// <returns>The symbol for the type, or <see langword="null"/> if the type is not found.</returns>
	public ISymbol? GetTypeByMetadataName(TypeIdentity identity) =>
		CompilationResult.Compilation.GetTypeByMetadataName(identity.MetadataFullName);

	/// <summary>
	/// Gets the symbol for a type by its metadata name.
	/// </summary>
	/// <param name="fullyQualifiedMetadataName">The fully qualified metadata name of the type.</param>
	/// <returns>The symbol for the type, or <see langword="null"/> if the type is not found.</returns>
	public ISymbol? GetTypeByMetadataName(string fullyQualifiedMetadataName) =>
		CompilationResult.Compilation.GetTypeByMetadataName(fullyQualifiedMetadataName);
}

/// <summary>
/// The result of a compilation run, including the compilation, the resulting assembly (if successful), and any diagnostics produced during compilation.
/// </summary>
/// <param name="Compilation">The compilation that was run.</param>
/// <param name="Assembly">The resulting assembly, or <see langword="null"/> if the compilation failed.</param>
/// <param name="Diagnostics">The diagnostics produced during the compilation.</param>
public sealed record class CompilationRunResult(
	Compilation Compilation,
	Assembly? Assembly,
	ImmutableArray<Diagnostic> Diagnostics
)
{
	/// <summary>
	/// The emitted output produced when <see cref="SourceGeneratorTestOptions.CompileToAssembly"/> was enabled,
	/// or <see langword="null"/> otherwise.
	/// </summary>
	internal EmittedAssembly? Emitted { get; init; }

	/// <summary>
	/// Gets a metadata-only view of the emitted assembly, or <see langword="null"/> when compilation to an
	/// assembly was not enabled or failed.
	/// </summary>
	/// <remarks>
	/// The view is created lazily on first access and never executes code. It shares the lifetime of the run
	/// result: dispose the <see cref="DriverRunResult"/> to release it.
	/// </remarks>
	public MetadataLoadContext? Metadata => Emitted?.Metadata;

	/// <summary>
	/// Gets the emitted assembly loaded within the metadata-only view, or <see langword="null"/> when
	/// compilation to an assembly was not enabled or failed. Reflects over the assembly without executing code.
	/// </summary>
	public Assembly? MetadataAssembly => Emitted?.MetadataAssembly;
}

/// <summary>
/// The result of a compilation run with analyzers applied, including the compilation and any diagnostics produced during compilation.
/// </summary>
/// <param name="Compilation">The compilation that was run, with analyzers applied.</param>
/// <param name="Diagnostics">The diagnostics produced just by the analyzers defined by <see cref="SourceGeneratorTestOptions.AnalyzerTypes"/>.</param>
public sealed record class AnalyzerCompilationRunResult(
	CompilationWithAnalyzers Compilation,
	ImmutableArray<Diagnostic> Diagnostics
);

/// <summary>
/// Specifies how to match hint names when retrieving generated source code from a <see cref="DriverRunResult"/>.
/// </summary>
public enum HintNameMatchMode
{
	/// <summary>
	/// Match the hint name by suffix.
	/// </summary>
	/// <remarks>
	/// Note this will automatically check for <c>.cs</c> if it's excluded.
	/// </remarks>
	Suffix,

	/// <summary>
	/// Match the hint name by partial match.
	/// </summary>
	Partial,

	/// <summary>
	/// Match the hint name exactly.
	/// </summary>
	Exact,
}
