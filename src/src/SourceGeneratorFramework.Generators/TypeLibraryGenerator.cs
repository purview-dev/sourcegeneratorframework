using Microsoft.CodeAnalysis;
using Purview.SourceGeneratorFramework.Generators.Helpers;
using Purview.SourceGeneratorFramework.Logging;

namespace Purview.SourceGeneratorFramework.Generators;

[Generator]
public sealed class TypeLibraryGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context
			.RegisterEmbeddedAttribute<TypeLibraryGenerator>()
			.RegisterPostInitializationOutput(context =>
			{
				foreach (var (hintName, source) in SourceEmitter.TypeLibraryEmit())
					context.AddSource(hintName, source);
			});

		var targetPipeline = TypeLibraryModelLibrary.GetTypeLibraryTargetPipeline(context);
		var contextPipeline = IncrementalPipeline.DefaultGenerationContextValueProvider<TypeLibraryGenerator>(
			context,
			PropertyLibrary.DisableTypeLibraryGenerator
		);

		var outputPipeline = targetPipeline.CombineWithContext(contextPipeline);

		context.RegisterSourceOutput(
			outputPipeline,
			static (spc, outputContext) =>
			{
				var (target, generationContext) = outputContext;

				if (generationContext.Settings.IsSourceGeneratorDisabled)
				{
					generationContext.Info("TypeLibraryGenerator is disabled.");
					return;
				}

				// Validation diagnostics (TLB0001-TLB0010) are reported by the analyzers; the generator
				// only skips processing targets that carry a blocking error.
				if (!target.ShouldProcess)
					return;

				GenerateTypeLibrary(spc, target.Value, generationContext);
			}
		);
	}

	static void GenerateTypeLibrary(
		SourceProductionContext spc,
		TypeLibraryModel target,
		GenerationContext<EmptyCapabilities> generationContext
	)
	{
		generationContext.Debug("Generating type library '{0}'", target.ClassName);

		spc.AddSource(SourceEmitter.GetTypeLibraryHintName(target), SourceEmitter.EmitTypeLibrary(target));
		spc.AddSource(SourceEmitter.GetTypeLibraryUsageHintName(target), SourceEmitter.EmitTypeRefUsagePartial(target));
	}
}
