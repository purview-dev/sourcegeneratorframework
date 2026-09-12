using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.SourceGeneratorFramework.Logging;
using Purview.SourceGeneratorFramework.Testing.Models;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Executes a source generator against a test compilation and returns the result.
/// </summary>
public sealed class SourceGeneratorTestRunner<TGenerator>
	where TGenerator : class, IIncrementalGenerator, new()
{
	/// <summary>
	/// Runs the generator against the provided source using the specified options.
	/// </summary>
	public Task<DriverRunResult> RunAsync(
		string source,
		SourceGeneratorTestOptions? options = null,
		CancellationToken cancellationToken = default
	) => RunAsync([source], options, cancellationToken);

	/// <summary>
	/// Runs the generator against the provided sources using the specified options.
	/// </summary>
	public async Task<DriverRunResult> RunAsync(
		IEnumerable<string> sources,
		SourceGeneratorTestOptions? options = null,
		CancellationToken cancellationToken = default
	)
	{
		options ??= new();
		if (options.AnalyzerOptions is not null && options.CompilationWithAnalyzersOptions is not null)
		{
			throw new ArgumentException(
				$"{nameof(options.AnalyzerOptions)} and {nameof(options.CompilationWithAnalyzersOptions)} cannot be provided at the same time.",
				nameof(options)
			);
		}

		ConcurrentBag<LogEntry> logEntries = [];

		if (!options.AdditionalSources.IsDefaultOrEmpty)
			sources = sources.Concat(options.AdditionalSources);

		var syntaxTrees = sources
			.Select(source =>
				CSharpSyntaxTree.ParseText(
					PrepareSource(source, options),
					encoding: System.Text.Encoding.UTF8,
					options: new CSharpParseOptions(options.LanguageVersion),
					cancellationToken: cancellationToken
				)
			)
			.ToImmutableArray();
		var references = SourceGeneratorHelpers.ResolveReferences(options, typeof(TGenerator).Assembly);
		var compilation = SourceGeneratorHelpers.CreateCompilation(syntaxTrees, references, options);
		TGenerator generator = new();
		var loggingSessionId = options.EnableLogging ? Guid.NewGuid().ToString("N") : null;
		using var loggingRegistration = ConfigureLogging(loggingSessionId, options, logEntries);

		var driver = CreateDriver(generator, options, loggingSessionId);
		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out var outputCompilation,
			out _,
			cancellationToken
		);

		var analyzerCompilationRun = await GetAnalyzerResultsAsync(options, outputCompilation, cancellationToken);
		var result = driver.GetRunResult();

		Assembly? assembly = null;
		ImmutableArray<Diagnostic> compilationDiagnostics = [];
		EmittedAssembly? emitted = null;
		if (options.CompileToAssembly)
			emitted = CompileToAssembly(outputCompilation, cancellationToken);

		if (emitted is not null)
		{
			assembly = emitted.Assembly;
			compilationDiagnostics = emitted.Diagnostics;
		}

		CompilationRunResult compilationResult = new(outputCompilation, assembly, compilationDiagnostics)
		{
			Emitted = emitted,
		};

		var excludedGeneratedSource = ExcludeGeneratedSources(result, options.ExcludeGeneratedSourceHintNames);

		return new(
			result,
			compilationResult,
			analyzerCompilationRun,
			result.GeneratedTrees,
			excludedGeneratedSource,
			[.. logEntries]
		);
	}

	/// <summary>
	/// Runs the generator against a sequence of source sets using a single shared driver, capturing each run's
	/// tracked incremental steps so tests can prove which pipeline stages were recomputed (<c>Modified</c>) and
	/// which were reused (<c>Cached</c>/<c>Unchanged</c>) between runs.
	/// </summary>
	public Task<IncrementalCacheResult> RunIncrementalAsync(
		IEnumerable<IncrementalRunInput> inputs,
		SourceGeneratorTestOptions? options = null,
		CancellationToken cancellationToken = default
	) => Task.FromResult(RunIncremental(inputs, options, cancellationToken));

	/// <summary>
	/// Runs the generator against the same source set twice using a single shared driver, the common case for
	/// proving that an unchanged input is fully cached on the second run.
	/// </summary>
	public Task<IncrementalCacheResult> RunIncrementalAsync(
		IEnumerable<string> sources,
		SourceGeneratorTestOptions? options = null,
		CancellationToken cancellationToken = default
	) =>
		RunIncrementalAsync(
			[new IncrementalRunInput(sources), new IncrementalRunInput(sources)],
			options,
			cancellationToken
		);

	/// <summary>
	/// Runs the generator incrementally over the supplied source sets and returns each run's tracked steps.
	/// </summary>
	public IncrementalCacheResult RunIncremental(
		IEnumerable<IncrementalRunInput> inputs,
		SourceGeneratorTestOptions? options = null,
		CancellationToken cancellationToken = default
	)
	{
		var materializedInputs = inputs?.ToList() ?? throw new ArgumentNullException(nameof(inputs));
		if (materializedInputs.Count == 0)
			throw new ArgumentException("At least one run is required.", nameof(inputs));

		options ??= new();
		if (options.AnalyzerOptions is not null && options.CompilationWithAnalyzersOptions is not null)
		{
			throw new ArgumentException(
				$"{nameof(options.AnalyzerOptions)} and {nameof(options.CompilationWithAnalyzersOptions)} cannot be provided at the same time.",
				nameof(options)
			);
		}

		TGenerator generator = new();
		var loggingSessionId = options.EnableLogging ? Guid.NewGuid().ToString("N") : null;
		var driver = CreateDriver(generator, options, loggingSessionId);
		var references = SourceGeneratorHelpers.ResolveReferences(options, typeof(TGenerator).Assembly);
		var runs = ImmutableArray.CreateBuilder<IncrementalCacheRun>(materializedInputs.Count);
		Dictionary<string, Compilation> compilationCache = new(StringComparer.Ordinal);

		foreach (var input in materializedInputs)
		{
			// Reuse the compilation for identical source sets so Roslyn can report the unchanged pipeline
			// stages as cached rather than modified on the second run.
			var key = string.Join("\u0001", input.Sources.Select(source => PrepareSource(source, options)));
			if (!compilationCache.TryGetValue(key, out var compilation))
			{
				compilation = BuildCompilation(input.Sources, options, references, cancellationToken);
				compilationCache[key] = compilation;
			}

			if (input.AnalyzerConfig is not null)
			{
				var analyzerOptions = BuildAnalyzerConfig(options, loggingSessionId, input.AnalyzerConfig);
				driver = driver.WithUpdatedAnalyzerConfigOptions(
					new TestAnalyzerConfigOptionsProvider(analyzerOptions)
				);
			}

			driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cancellationToken);
			runs.Add(CaptureRun(driver));
		}

		return new(runs.ToImmutable());
	}

	static CSharpCompilation BuildCompilation(
		IEnumerable<string> sources,
		SourceGeneratorTestOptions options,
		ImmutableArray<MetadataReference> references,
		CancellationToken cancellationToken
	)
	{
		if (!options.AdditionalSources.IsDefaultOrEmpty)
			sources = sources.Concat(options.AdditionalSources);

		var syntaxTrees = sources
			.Select(source =>
				CSharpSyntaxTree.ParseText(
					PrepareSource(source, options),
					encoding: System.Text.Encoding.UTF8,
					options: new CSharpParseOptions(options.LanguageVersion),
					cancellationToken: cancellationToken
				)
			)
			.ToImmutableArray();

		return SourceGeneratorHelpers.CreateCompilation(syntaxTrees, references, options);
	}

	static IncrementalCacheRun CaptureRun(GeneratorDriver driver)
	{
		var runResult = driver.GetRunResult().Results.FirstOrDefault(run => run.Generator is not null);
		var steps = runResult.Generator is null
#pragma warning disable IDE0301
			? ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>>.Empty
#pragma warning restore IDE0301
			: runResult.TrackedSteps;

		return new(runResult, steps);
	}

	static async Task<AnalyzerCompilationRunResult?> GetAnalyzerResultsAsync(
		SourceGeneratorTestOptions options,
		Compilation outputCompilation,
		CancellationToken cancellationToken
	)
	{
		if (options.AnalyzerTypes.IsDefaultOrEmpty)
			return null;

		var analyzers = options
			.AnalyzerTypes.Select(static type =>
			{
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
				if (type.GetConstructors().All(c => c.GetParameters().Length > 0))
				{
					throw new ArgumentException(
						$"Analyzer type {type.FullName} must have a parameterless constructor.",
						nameof(options.AnalyzerTypes)
					);
				}

				if (!typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
				{
					throw new ArgumentException(
						$"Analyzer type {type.FullName} must be a DiagnosticAnalyzer.",
						nameof(options.AnalyzerTypes)
					);
				}
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

				return (Activator.CreateInstance(type) as DiagnosticAnalyzer)!;
			})
			.ToImmutableArray();

		var compilationWithAnalyzers = options.CompilationWithAnalyzersOptions is not null
			? outputCompilation.WithAnalyzers(analyzers, options.CompilationWithAnalyzersOptions)
			: outputCompilation.WithAnalyzers(analyzers, options.AnalyzerOptions);

		var analyzerDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
		return new(compilationWithAnalyzers, analyzerDiagnostics);
	}

	static string PrepareSource(string source, SourceGeneratorTestOptions options) =>
		SourceGeneratorHelpers.PrepareSource(source, options);

	static GeneratorDriver CreateDriver(
		TGenerator generator,
		SourceGeneratorTestOptions options,
		string? loggingSessionId
	)
	{
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[generator.AsSourceGenerator()],
			additionalTexts: options.AdditionalText,
			parseOptions: new(options.LanguageVersion),
			driverOptions: new GeneratorDriverOptions(
				IncrementalGeneratorOutputKind.None,
				trackIncrementalGeneratorSteps: true
			)
		);

		var analyzerOptions = BuildAnalyzerConfig(options, loggingSessionId);
		if (analyzerOptions.Count > 0)
			driver = driver.WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(analyzerOptions));

		return driver;
	}

	static Dictionary<string, string> BuildAnalyzerConfig(
		SourceGeneratorTestOptions options,
		string? loggingSessionId,
		IEnumerable<(string Key, string Value)>? overrides = null
	)
	{
		Dictionary<string, string> analyzerOptions = new(options.AnalyzerConfigOptions)
		{
			[SourceGeneratorBuildProperties.ValidateCodeWriterScopes] = options.ValidateCodeWriterScopes.ToString(),
			[SourceGeneratorBuildProperties.EnableLogging] = options.EnableLogging.ToString(),
		};

		foreach (var pair in options.AnalyzerConfigOptions)
		{
			if (!pair.Key.StartsWith(SourceGeneratorBuildProperties.BuildProperty, StringComparison.Ordinal))
				analyzerOptions[SourceGeneratorBuildProperties.BuildProperty + pair.Key] = pair.Value;
		}

		if (loggingSessionId is not null)
		{
			analyzerOptions[SourceGeneratorBuildProperties.LoggingSessionId] = loggingSessionId;
		}

		if (options.DisableSourceGeneratorPropertyName is not null && options.DisableSourceGeneratorValue is not null)
		{
			var disablePropertyName = options.DisableSourceGeneratorPropertyName;
			if (!disablePropertyName.StartsWith(SourceGeneratorBuildProperties.BuildProperty, StringComparison.Ordinal))
				disablePropertyName = SourceGeneratorBuildProperties.BuildProperty + disablePropertyName;

			analyzerOptions[disablePropertyName] = options.DisableSourceGeneratorValue.Value.ToString();
		}

		if (overrides is not null)
		{
			foreach (var (key, value) in overrides)
				analyzerOptions[key] = value;
		}

		return analyzerOptions;
	}

	static LoggingRegistrations? ConfigureLogging(
		string? loggingSessionId,
		SourceGeneratorTestOptions options,
		ConcurrentBag<LogEntry> logEntries
	)
	{
		if (loggingSessionId is null)
			return null;

		void Sink(string message, int level)
		{
			var type = (SourceGenLogLevel)level;
			options.TestOutput.WriteLine($"[{type}] {message}");

			logEntries.Add(new(type, message));
		}

		List<IDisposable> registrations = [SourceGenLogging.RegisterSinkCore(loggingSessionId, Sink)];

		// Self-contained generators may embed the framework logging types. Register the test sink
		// explicitly with that private copy rather than relying on process-wide shared state.
		var embeddedRegistration = GetEmbeddedSinkRegistration(typeof(TGenerator).Assembly);
		if (embeddedRegistration is not null)
			registrations.Add(embeddedRegistration(loggingSessionId, Sink));

		return new(registrations);
	}

	static readonly ConcurrentDictionary<
		Assembly,
		Func<string, Action<string, int>, IDisposable>?
	> EmbeddedSinkRegistrationCache = new();

	static Func<string, Action<string, int>, IDisposable>? GetEmbeddedSinkRegistration(Assembly generatorAssembly)
	{
		return EmbeddedSinkRegistrationCache.GetOrAdd(
			generatorAssembly,
			static assembly =>
			{
				var embeddedLoggingType = assembly.GetType(
					"Purview.SourceGeneratorFramework.Logging.SourceGenLogging",
					throwOnError: false
				);
				if (embeddedLoggingType is null || embeddedLoggingType == typeof(SourceGenLogging))
					return null;

				var registerSink = embeddedLoggingType.GetMethod(
					"RegisterSinkCore",
					BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
				);

				return registerSink is null
					? null
					: (Func<string, Action<string, int>, IDisposable>)
						Delegate.CreateDelegate(typeof(Func<string, Action<string, int>, IDisposable>), registerSink);
			}
		);
	}

	sealed class LoggingRegistrations(List<IDisposable> registrations) : IDisposable
	{
		List<IDisposable>? _registrations = registrations;

		public void Dispose()
		{
			var registrationsToDispose = Interlocked.Exchange(ref _registrations, null);
			if (registrationsToDispose is null)
				return;

			for (var index = registrationsToDispose.Count - 1; index >= 0; index--)
				registrationsToDispose[index].Dispose();
		}
	}

	static EmittedAssembly CompileToAssembly(Compilation compilation, CancellationToken cancellationToken)
	{
		using MemoryStream assemblyStream = new();
		var emitResult = compilation.Emit(assemblyStream, cancellationToken: cancellationToken);

		return new EmittedAssembly(emitResult.Success ? assemblyStream.ToArray() : null, compilation)
		{
			Diagnostics = emitResult.Diagnostics,
		};
	}

	static ImmutableArray<SyntaxTree> ExcludeGeneratedSources(
		GeneratorDriverRunResult result,
		ImmutableArray<string> exclude
	)
	{
		return exclude.IsEmpty
			? result.GeneratedTrees
			:
			[
				.. result.GeneratedTrees.Where(tree =>
					!exclude.Any(attr =>
						tree.FilePath.EndsWith(attr, StringComparison.Ordinal)
						|| tree.FilePath.EndsWith(attr + ".g.cs", StringComparison.Ordinal)
						|| tree.FilePath.EndsWith(attr + ".cs", StringComparison.Ordinal)
					)
				),
			];
	}
}
