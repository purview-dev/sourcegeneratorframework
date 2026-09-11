using Microsoft.CodeAnalysis;
using Purview.SourceGeneratorFramework.Helpers;

namespace Purview.SourceGeneratorFramework.TestGenerators;

public sealed class DiagnosticTestGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var targets = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			new TypeIdentity("TestAttribute", null),
			static (ctx, _) =>
			{
				// ForAttributeWithMetadataName already resolves TargetSymbol.
				var name = ctx.TargetSymbol.Name;
				var diagnostic = ReportableDiagnostic.Create(
					new DiagnosticDescriptor(
						"TEST001",
						"Test diagnostic",
						$"Processed {name}",
						"Test",
						DiagnosticSeverity.Info,
						isEnabledByDefault: true
					),
					isBlocking: false,
					Location.None
				);
				return GeneratorResult<TargetInfo>.Create(new TargetInfo(name), diagnostic);
			}
		);

		var generationContext = IncrementalPipeline.DefaultGenerationContextValueProvider(
			context,
			new GenerationSettings("DiagnosticTestGenerator", "1.0.0")
		);

		context.RegisterSourceOutput(
			targets,
			generationContext,
			static (spc, target, ctx) =>
			{
				var writer = ctx.CreateCodeWriter();
				writer.Line($"partial class {target.Name} {{ }}");
				spc.AddSource($"{target.Name}.g.cs", writer.ToString());
			}
		);
	}
}
