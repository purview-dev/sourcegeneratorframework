#pragma warning disable CS1591
using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Purview.SourceGeneratorFramework;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class SourceGeneratorTestOptionsExtensions
{
	extension<TOptions>(TOptions options)
		where TOptions : SourceGeneratorTestOptions
	{
		/// <summary>
		/// Creates a new options snapshot with the specified analyzer-config options added to the existing set.
		/// </summary>
		/// <param name="configOptions">The analyzer-config options to add.</param>
		/// <returns>A new <see cref="SourceGeneratorTestOptions"/> instance with the specified options added.</returns>
		public TOptions WithAnalyzerConfigOptions(params (string, string)[] configOptions)
		{
			if (configOptions is null || configOptions.Length == 0)
				return options;

			var analyzerConfigOptions = options.AnalyzerConfigOptions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			foreach (var (key, value) in configOptions)
			{
				analyzerConfigOptions[key] = value;
			}

			return options with
			{
				AnalyzerConfigOptions = analyzerConfigOptions.ToImmutableDictionary(),
			};
		}

		/// <summary>
		/// Creates a new options snapshot with the specified analyzer-config options added to the existing set.
		/// </summary>
		/// <param name="configOptions">The analyzer-config options to add.</param>
		/// <returns>A new <see cref="SourceGeneratorTestOptions"/> instance with the specified options added.</returns>
		public TOptions WithAnalyzerConfigOptions(IEnumerable<(string, string)> configOptions)
		{
			if (configOptions is null)
				return options;

			var analyzerConfigOptions = options.AnalyzerConfigOptions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			foreach (var (key, value) in configOptions)
			{
				analyzerConfigOptions[key] = value;
			}

			return options with
			{
				AnalyzerConfigOptions = analyzerConfigOptions.ToImmutableDictionary(),
			};
		}

		/// <summary>
		/// Creates a new options snapshot with the specified additional assembly types added to the existing set.
		/// </summary>
		/// <param name="additionalAssemblyTypes">The additional assembly types to add.</param>
		/// <returns>A new <see cref="SourceGeneratorTestOptions"/> instance with the specified assembly types added.</returns>
		public TOptions WithAdditionalAssemblyTypes(params Type[] additionalAssemblyTypes)
		{
			if (additionalAssemblyTypes is null || additionalAssemblyTypes.Length == 0)
				return options;

			var assemblyTypes = options.AdditionalAssemblyTypes.ToList();
			assemblyTypes.AddRange(additionalAssemblyTypes);

			return options with
			{
				AdditionalAssemblyTypes = [.. assemblyTypes],
			};
		}

		/// <summary>
		/// Creates a new options snapshot with the specified additional assembly types added to the existing set.
		/// </summary>
		/// <param name="additionalAssemblyTypes">The additional assembly types to add.</param>
		/// <returns>A new <see cref="SourceGeneratorTestOptions"/> instance with the specified assembly types added.</returns>
		public TOptions WithAdditionalAssemblyTypes(IEnumerable<Type> additionalAssemblyTypes)
		{
			if (additionalAssemblyTypes is null)
				return options;

			var assemblyTypes = options.AdditionalAssemblyTypes.ToList();
			assemblyTypes.AddRange(additionalAssemblyTypes);

			return options with
			{
				AdditionalAssemblyTypes = [.. assemblyTypes],
			};
		}

		public TOptions WithExcludeGeneratedSourceHintNames(params string[] sourceHintNames) =>
			sourceHintNames is null || sourceHintNames.Length == 0
				? options
				: (
					options with
					{
						ExcludeGeneratedSourceHintNames = options.ExcludeGeneratedSourceHintNames.AddRange(
							sourceHintNames
						),
					}
				);

		/// <summary>
		/// Creates a new options snapshot with additional namespaces appended.
		/// </summary>
		public TOptions WithExcludeGeneratedSourceHintNames(IEnumerable<string> sourceHintNames) =>
			sourceHintNames is null
				? options
				: (
					options with
					{
						ExcludeGeneratedSourceHintNames = options.ExcludeGeneratedSourceHintNames.AddRange(
							sourceHintNames
						),
					}
				);

		/// <summary>
		/// Creates a new options snapshot with additional namespaces appended.
		/// </summary>
		public TOptions WithAdditionalNamespaces(params string[] additionalNamespaces) =>
			additionalNamespaces is null || additionalNamespaces.Length == 0
				? options
				: (options with { AdditionalNamespaces = options.AdditionalNamespaces.AddRange(additionalNamespaces) });

		/// <summary>
		/// Creates a new options snapshot with additional namespaces appended.
		/// </summary>
		public TOptions WithAdditionalNamespaces(IEnumerable<string> additionalNamespaces) =>
			additionalNamespaces is null
				? options
				: (options with { AdditionalNamespaces = options.AdditionalNamespaces.AddRange(additionalNamespaces) });

		/// <summary>
		/// Creates a new options snapshot with additional namespaces appended.
		/// </summary>
		public TOptions WithAdditionalNamespaces(params TypeIdentity[] identities) =>
			identities is null || identities.Length == 0
				? options
				: (
					options with
					{
						AdditionalNamespaces = options.AdditionalNamespaces.AddRange(
							identities.Where(m => !m.IsGlobalNamespace).Select(x => x.Namespace!)
						),
					}
				);

		/// <summary>
		/// Creates a new options snapshot with additional namespaces appended.
		/// </summary>
		public TOptions WithAdditionalNamespaces(IEnumerable<TypeIdentity> identities) =>
			identities is null
				? options
				: (
					options with
					{
						AdditionalNamespaces = options.AdditionalNamespaces.AddRange(
							identities.Where(m => !m.IsGlobalNamespace).Select(x => x.Namespace!)
						),
					}
				);

		/// <summary>
		/// Creates a new options snapshot with additional metadata references appended.
		/// </summary>
		public TOptions WithAdditionalReferences(params MetadataReference[] additionalReferences) =>
			additionalReferences is null || additionalReferences.Length == 0
				? options
				: (options with { AdditionalReferences = options.AdditionalReferences.AddRange(additionalReferences) });

		/// <summary>
		/// Creates a new options snapshot with additional metadata references appended.
		/// </summary>
		public TOptions WithAdditionalReferences(IEnumerable<MetadataReference> additionalReferences) =>
			additionalReferences is null || !additionalReferences.Any()
				? options
				: (options with { AdditionalReferences = options.AdditionalReferences.AddRange(additionalReferences) });

		/// <summary>
		/// Creates a new options snapshot with additional source files appended.
		/// </summary>
		public TOptions WithAdditionalSources(params string[] additionalSources) =>
			additionalSources is null || additionalSources.Length == 0
				? options
				: (options with { AdditionalSources = options.AdditionalSources.AddRange(additionalSources) });

		/// <summary>
		/// Creates a new options snapshot with additional source files appended.
		/// </summary>
		public TOptions WithAdditionalSources(IEnumerable<string> additionalSources) =>
			additionalSources is null || !additionalSources.Any()
				? options
				: (options with { AdditionalSources = options.AdditionalSources.AddRange(additionalSources) });

		/// <summary>
		/// Creates a new options snapshot with additional source files appended.
		/// </summary>
		public TOptions WithAdditionalSources(params SourceText[] additionalSources) =>
			additionalSources is null || additionalSources.Length == 0
				? options
				: (
					options with
					{
						AdditionalSources = options.AdditionalSources.AddRange(
							additionalSources.Select(x => x.ToString())
						),
					}
				);

		/// <summary>
		/// Creates a new options snapshot with additional source files appended.
		/// </summary>
		public TOptions WithAdditionalSources(IEnumerable<SourceText> additionalSources) =>
			additionalSources is null
				? options
				: (
					options with
					{
						AdditionalSources = options.AdditionalSources.AddRange(
							additionalSources.Select(x => x.ToString())
						),
					}
				);

		/// <summary>
		/// Creates a new options snapshot with additional text files appended.
		/// </summary>
		public TOptions WithAdditionalText(params AdditionalText[] additionalText) =>
			additionalText is null || additionalText.Length == 0
				? options
				: (options with { AdditionalText = options.AdditionalText.AddRange(additionalText) });

		/// <summary>
		/// Creates a new options snapshot with additional text files appended.
		/// </summary>
		public TOptions WithAdditionalText(IEnumerable<AdditionalText> additionalText) =>
			additionalText is null
				? options
				: (options with { AdditionalText = options.AdditionalText.AddRange(additionalText) });

		/// <summary>
		/// Creates a new options snapshot with analyzer types appended.
		/// </summary>
		public TOptions WithAnalyzers(params Type[] analyzerTypes)
		{
			if (analyzerTypes is null || analyzerTypes.Length == 0)
				return options;

			foreach (var analyzerType in analyzerTypes)
			{
				if (analyzerType is null || !typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType))
				{
					throw new ArgumentException(
						$"All analyzer types must derive from {nameof(DiagnosticAnalyzer)}.",
						nameof(analyzerTypes)
					);
				}
			}

			return options with
			{
				AnalyzerTypes = options.AnalyzerTypes.AddRange(analyzerTypes),
			};
		}

		/// <summary>
		/// Creates a new options snapshot with analyzer types appended.
		/// </summary>
		public TOptions WithAnalyzers(IEnumerable<Type> analyzerTypes)
		{
			if (analyzerTypes is null)
				return options;

			foreach (var analyzerType in analyzerTypes)
			{
				if (analyzerType is null || !typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType))
				{
					throw new ArgumentException(
						$"All analyzer types must derive from {nameof(DiagnosticAnalyzer)}.",
						nameof(analyzerTypes)
					);
				}
			}

			return options with
			{
				AnalyzerTypes = options.AnalyzerTypes.AddRange(analyzerTypes),
			};
		}

		/// <summary>
		/// Creates a new options snapshot using the specified analyzer options.
		/// </summary>
		/// <remarks>
		/// This clears <see cref="SourceGeneratorTestOptions.CompilationWithAnalyzersOptions"/>.
		/// </remarks>
		public TOptions WithAnalyzerOptions(AnalyzerOptions? analyzerOptions) =>
			options with
			{
				AnalyzerOptions = analyzerOptions,
				CompilationWithAnalyzersOptions = null,
			};

		/// <summary>
		/// Creates a new options snapshot using the specified compilation-with-analyzers options.
		/// </summary>
		/// <remarks>
		/// This clears <see cref="SourceGeneratorTestOptions.AnalyzerOptions"/>.
		/// </remarks>
		public TOptions WithCompilationWithAnalyzersOptions(
			CompilationWithAnalyzersOptions? compilationWithAnalyzersOptions
		) => options with { AnalyzerOptions = null, CompilationWithAnalyzersOptions = compilationWithAnalyzersOptions };
	}

	extension<TOptions>(TOptions options)
		where TOptions : CodeFixTestOptions
	{
		/// <summary>
		/// Creates a new code-fix options snapshot selecting a code action by index.
		/// </summary>
		/// <remarks>
		/// This clears <see cref="CodeFixTestOptions.EquivalenceKey"/>.
		/// </remarks>
		[System.Diagnostics.CodeAnalysis.SuppressMessage(
			"Usage",
			"CA1512:Use ArgumentOutOfRangeException throw helper",
			Justification = "The testing package supports targets where ThrowIfNegative is unavailable."
		)]
		public TOptions WithCodeActionIndex(int codeActionIndex) =>
			codeActionIndex < 0
				? throw new ArgumentOutOfRangeException(nameof(codeActionIndex))
				: (options with { CodeActionIndex = codeActionIndex, EquivalenceKey = null });

		/// <summary>
		/// Creates a new code-fix options snapshot selecting a code action by equivalence key.
		/// </summary>
		public TOptions WithCodeActionEquivalenceKey(string equivalenceKey) =>
			string.IsNullOrWhiteSpace(equivalenceKey)
				? throw new ArgumentException("Value cannot be null or whitespace.", nameof(equivalenceKey))
				: (options with { EquivalenceKey = equivalenceKey });
	}
}
