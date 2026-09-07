using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Purview.SourceGeneratorFramework.Logging;

namespace Purview.SourceGeneratorFramework.Helpers;

/// <summary>
/// Helpers for building common incremental source generator pipelines.
/// </summary>
public static class IncrementalPipeline
{
	/// <summary>
	/// Creates a value provider that reads an MSBuild property to determine whether the generator is disabled.
	/// </summary>
	public static IncrementalValueProvider<bool> IsDisabledValueProvider(
		IncrementalGeneratorInitializationContext context,
		string propertyName
	) => PropertyValueProvider(context, propertyName, value => bool.TryParse(value, out var isDisabled) && isDisabled);

	/// <summary>
	/// Creates a value provider that reads an MSBuild property.
	/// </summary>
	public static IncrementalValueProvider<T> PropertyValueProvider<T>(
		IncrementalGeneratorInitializationContext context,
		string propertyName,
		Func<string?, T> converter
	)
	{
		if (string.IsNullOrWhiteSpace(propertyName))
			throw new ArgumentException("Property name cannot be null or whitespace.", nameof(propertyName));
		if (converter == null)
			throw new ArgumentNullException(nameof(converter));

		var msbuildPropertyValue = propertyName;
		if (!propertyName.StartsWith(SourceGeneratorBuildProperties.BuildProperty, StringComparison.Ordinal))
			msbuildPropertyValue = SourceGeneratorBuildProperties.BuildProperty + propertyName;

		// Use the bare property name for the tracking name so cache tests reference the human-readable
		// stage (for example GetMSBuildPropertyValue_EmitServiceInfo) rather than the build_property.* key.
		var trackingName = propertyName.StartsWith(
			SourceGeneratorBuildProperties.BuildProperty,
			StringComparison.Ordinal
		)
			? propertyName.Substring(SourceGeneratorBuildProperties.BuildProperty.Length)
			: propertyName;

		// All valid...
		return context
			.AnalyzerConfigOptionsProvider.Select(
				(options, _) =>
				{
					options.GlobalOptions.TryGetValue(msbuildPropertyValue, out var value);

					return converter(value);
				}
			)
			.WithTrackingName($"GetMSBuildPropertyValue_{trackingName}");
	}

	/// <summary>
	/// Creates a value provider that builds a generation context from the compilation, framework
	/// build properties, an optional generator-disable property, and any registered logging sink.
	/// </summary>
	/// <param name="context">The generator initialization context.</param>
	/// <param name="settings">The generation settings.</param>
	/// <param name="factory">A factory that creates a generation context from the compilation, settings, and optional logger.</param>
	public static IncrementalValueProvider<
		GenerationContext<TCapabilities>
	> GenerationContextValueProvider<TCapabilities>(
		IncrementalGeneratorInitializationContext context,
		GenerationSettings settings,
		Func<Compilation, GenerationSettings, ISourceGenLogger?, CancellationToken, TCapabilities> factory
	)
		where TCapabilities : class, IGenerationCapabilities
	{
		if (settings is null)
			throw new ArgumentNullException(nameof(settings));
		if (factory is null)
			throw new ArgumentNullException(nameof(factory));

		// Combine the compilation and generation configuration into a single value provider, then transform it into a generation context.
		return context
			.CompilationProvider.Combine(
				GenerationConfigurationValueProvider(context, settings.DisabledSourceGenMSBuildProperty)
			)
			.Select(
				(input, cancellationToken) =>
				{
					cancellationToken.ThrowIfCancellationRequested();

					var compilation = input.Left;
					var configuration = input.Right;
					var logger = configuration.IsLoggingEnabled
						? SourceGenLogging.CreateLogger(configuration.LoggingSessionId)
						: null;

					logger?.Info(
						$"Creating generation context ({typeof(TCapabilities)}) for compilation '{input.Left.AssemblyName}'."
					);

					// Honour an explicitly configured nullable-context value first, falling back to the
					// compilation's nullable annotations state only when one was not specified.
					var resolvedSettings = settings with
					{
						ValidateCodeWriterScopes = configuration.ValidateCodeWriterScopes,
						IsSourceGeneratorDisabled = configuration.IsSourceGeneratorDisabled,
						IsLoggingEnabled = logger is not null,
						IsNullableContextEnabled =
							settings.IsNullableContextEnabled ?? IsNullableContextEnabled(compilation),
						LanguageVersion = configuration.LanguageVersion ?? settings.LanguageVersion,
					};

					var capabilities = factory(compilation, resolvedSettings, logger, cancellationToken);

					return new GenerationContext<TCapabilities>(capabilities, resolvedSettings, logger);
				}
			)
			.WithTrackingName($"GetGenerationContext_{typeof(TCapabilities).Name}");
	}

	/// <summary>
	/// Determines whether the compilation has nullable annotations enabled, which is the case when
	/// the compilation options allow nullable annotations. Returns <see langword="null"/> when the
	/// compilation is not a C# compilation or the state cannot be determined.
	/// </summary>
	public static bool? IsNullableContextEnabled(Compilation compilation)
	{
		if (compilation is not CSharpCompilation csharpCompilation)
			return null;

		// Nullable annotations are enabled when the compilation's nullable context options are either
		return csharpCompilation.Options.NullableContextOptions
			is NullableContextOptions.Annotations
				or NullableContextOptions.Enable;
	}

	static IncrementalValueProvider<GenerationConfiguration> GenerationConfigurationValueProvider(
		IncrementalGeneratorInitializationContext context,
		string? disablePropertyName
	) =>
		context
			.AnalyzerConfigOptionsProvider.Select(
				(options, _) =>
				{
					options.GlobalOptions.TryGetValue(
						SourceGeneratorBuildProperties.ValidateCodeWriterScopes,
						out var scopeValidationValue
					);
					options.GlobalOptions.TryGetValue(
						SourceGeneratorBuildProperties.EnableLogging,
						out var loggingEnabledValue
					);
					options.GlobalOptions.TryGetValue(
						SourceGeneratorBuildProperties.LoggingSessionId,
						out var loggingSessionId
					);
					if (
						!options.GlobalOptions.TryGetValue(
							SourceGeneratorBuildProperties.LanguageVersion,
							out var languageVersionValue
						)
					)
					{
						options.GlobalOptions.TryGetValue("build_property.LangVersion", out languageVersionValue);
					}

					string? disabledValue = null;
					if (!string.IsNullOrWhiteSpace(disablePropertyName))
					{
						var propertyName = disablePropertyName!.StartsWith(
							SourceGeneratorBuildProperties.BuildProperty,
							StringComparison.Ordinal
						)
							? disablePropertyName
							: SourceGeneratorBuildProperties.BuildProperty + disablePropertyName;
						options.GlobalOptions.TryGetValue(propertyName, out disabledValue);
					}

					return new GenerationConfiguration(
						ValidateCodeWriterScopes: bool.TryParse(scopeValidationValue, out var validateScopes)
							&& validateScopes,
						IsSourceGeneratorDisabled: bool.TryParse(disabledValue, out var isDisabled) && isDisabled,
						IsLoggingEnabled: bool.TryParse(loggingEnabledValue, out var loggingEnabled) && loggingEnabled,
						LoggingSessionId: loggingSessionId,
						LanguageVersion: TryParseLanguageVersion(languageVersionValue)
					);
				}
			)
			.WithTrackingName("GetGenerationConfiguration");

	/// <summary>
	/// Parses an MSBuild <c>LangVersion</c> value into a <see cref="LanguageVersion"/>, handling the
	/// numeric forms (<c>14</c>/<c>14.0</c> → <see cref="LanguageVersion.CSharp14"/>) and the
	/// <c>latest</c>/<c>latestMajor</c>/<c>preview</c> keywords in addition to named enum values.
	/// </summary>
	internal static LanguageVersion? TryParseLanguageVersion(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		// The netstandard2.0 reference surface does not surface the [NotNullWhen(false)] annotation,
		// so the guard does not narrow the nullable annotation.
		var trimmed = value!.Trim();

		if (
			trimmed.Equals("latest", StringComparison.OrdinalIgnoreCase)
			|| trimmed.Equals("latestMajor", StringComparison.OrdinalIgnoreCase)
		)
			return LanguageVersion.LatestMajor;

		if (trimmed.Equals("preview", StringComparison.OrdinalIgnoreCase))
			return LanguageVersion.Preview;

		// The MSBuild LangVersion property is commonly numeric ("14", "13.0"). Enum.TryParse binds a
		// numeric string to the integer value cast to the enum — "14" becomes the undefined
		// (LanguageVersion)14 rather than LanguageVersion.CSharp14 (1400) — so numeric forms are
		// mapped explicitly before named parsing.
		var numericText = trimmed;
		if (numericText.EndsWith(".0", StringComparison.Ordinal))
			numericText = numericText.Substring(0, numericText.Length - 2);

		if (int.TryParse(numericText, out var major))
			return (LanguageVersion)(major * 100);

		// Finally, try to parse the value as a named enum value.
		return Enum.TryParse(trimmed, ignoreCase: true, out LanguageVersion parsed) ? parsed : null;
	}

	/// <summary>
	/// Creates a values provider for syntax nodes annotated with a specific attribute.
	/// </summary>
	public static IncrementalValuesProvider<TOutput> ForAttributeWithMetadataName<TOutput>(
		IncrementalGeneratorInitializationContext context,
		TypeIdentity attributeType,
		Func<GeneratorAttributeSyntaxContext, CancellationToken, TOutput> transform,
		Func<SyntaxNode, CancellationToken, bool>? predicate = null,
		string? trackingName = null
	)
		where TOutput : notnull
	{
		if (transform is null)
			throw new ArgumentNullException(nameof(transform));

		predicate ??= static (_, _) => true;

		return context
			.SyntaxProvider.ForAttributeWithMetadataName(attributeType.MetadataFullName, predicate, transform)
			.WithTrackingName(trackingName ?? $"ForAttribute_{attributeType.Name}");
	}

	readonly record struct GenerationConfiguration(
		bool ValidateCodeWriterScopes,
		bool IsSourceGeneratorDisabled,
		bool IsLoggingEnabled,
		string? LoggingSessionId,
		LanguageVersion? LanguageVersion
	);
}
