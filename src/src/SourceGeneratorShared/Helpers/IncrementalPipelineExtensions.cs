using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Purview.SourceGeneratorFramework.Logging;

namespace Purview.SourceGeneratorFramework.Helpers;

[EditorBrowsable(EditorBrowsableState.Never)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible")]
public static class IncrementalPipelineExtensions
{
	extension(IncrementalPipeline)
	{
		/// <summary>
		/// Creates a value provider that builds a generation context from the compilation, framework
		/// build properties, an optional generator-disable property, and any registered logging sink.
		/// </summary>
		/// <param name="context">The generator initialization context.</param>
		/// <param name="factory">A factory that creates a generation context from the compilation, settings, and optional logger.</param>
		/// <param name="disablePropertyName">An optional MSBuild property name that disables the generator when set to true.</param>
		public static IncrementalValueProvider<GenerationContext<TCapabilities>> GenerationContextValueProvider<
			TCapabilities,
			TGenerator
		>(
			IncrementalGeneratorInitializationContext context,
			Func<Compilation, GenerationSettings, ISourceGenLogger?, CancellationToken, TCapabilities> factory,
			string? disablePropertyName = null
		)
			where TCapabilities : class, IGenerationCapabilities =>
			IncrementalPipeline.GenerationContextValueProvider(
				context,
				GenerationSettings.Create<TGenerator>(disablePropertyName),
				factory
			);

		/// <summary>
		/// Creates a value provider that builds a default generation context from the compilation and
		/// resolved framework configuration.
		/// </summary>
		/// <param name="context">The generator initialization context.</param>
		/// <param name="settings">The generation settings.</param>
		public static IncrementalValueProvider<
			GenerationContext<EmptyCapabilities>
		> DefaultGenerationContextValueProvider(
			IncrementalGeneratorInitializationContext context,
			GenerationSettings settings
		) =>
			IncrementalPipeline.GenerationContextValueProvider(
				context,
				settings,
				static (_, _, _, _) => EmptyCapabilities.Instance
			);

		/// <summary>
		/// Creates a value provider that builds a default generation context from the compilation and
		/// resolved framework configuration.
		/// </summary>
		/// <param name="context">The generator initialization context.</param>
		/// <param name="disableSourceGenMSBuildProperty">The MSBuild property that can be used to disable the source generator.</param>
		/// <typeparam name="TGenerator">The type of the source generator.</typeparam>
		public static IncrementalValueProvider<
			GenerationContext<EmptyCapabilities>
		> DefaultGenerationContextValueProvider<TGenerator>(
			IncrementalGeneratorInitializationContext context,
			string? disableSourceGenMSBuildProperty = null
		) =>
			IncrementalPipeline.GenerationContextValueProvider(
				context,
				GenerationSettings.Create<TGenerator>(disableSourceGenMSBuildProperty),
				static (_, _, _, _) => EmptyCapabilities.Instance
			);

		/// <summary>
		/// Registers a source output action that receives a generation context and the generator result,
		/// and produces source files and diagnostics from the successful results. This keeps generator
		/// <see cref="IIncrementalGenerator.Initialize"/> methods thin and enforces the rule that diagnostics are reported in the source output stage.
		/// </summary>
		/// <typeparam name="TOutput">The type of the generator result.</typeparam>
		/// <param name="context">The generator initialization context.</param>
		/// <param name="outputs">The incremental values provider for the generator results.</param>
		/// <param name="contextProvider">The incremental value provider for the generation context.</param>
		/// <param name="generate">The action to generate source files and report diagnostics.</param>
		/// <param name="trackingName">An optional tracking name for the source output.</param>
		public static void RegisterSourceOutput<TOutput>(
			IncrementalGeneratorInitializationContext context,
			IncrementalValuesProvider<GeneratorResult<TOutput>> outputs,
			IncrementalValueProvider<GenerationContext<EmptyCapabilities>> contextProvider,
			Action<SourceProductionContext, TOutput, GenerationContext<EmptyCapabilities>> generate,
			string? trackingName = null
		)
			where TOutput : notnull => context.RegisterSourceOutput(outputs, contextProvider, generate, trackingName);
	}
}
