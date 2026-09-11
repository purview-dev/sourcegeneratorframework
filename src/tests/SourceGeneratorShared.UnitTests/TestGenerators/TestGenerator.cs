using Microsoft.CodeAnalysis;
using Purview.SourceGeneratorFramework.Helpers;

namespace Purview.SourceGeneratorFramework.TestGenerators;

public sealed class TestGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var isDisabled = IncrementalPipeline.IsDisabledValueProvider(context, "DisableTestGenerator");
		var targets = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			new TypeIdentity("TestAttribute", null),
			static (ctx, _) =>
				// ForAttributeWithMetadataName already resolves TargetSymbol.
				new TargetInfo(ctx.TargetSymbol.Name),
			predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax
		);

		// Combine each target with the global disable flag so output stays per-item and only the
		// affected target invalidates on change, rather than collecting everything into one aggregate.
		context.RegisterSourceOutput(
			targets.CombineWith(isDisabled, static (target, disabled, _) => (target, disabled)),
			static (spc, pair) =>
			{
				var (target, isDisabled) = pair;
				if (isDisabled)
					return;

				spc.AddSource($"{target.Name}.g.cs", $"partial class {target.Name} {{ }}");
			}
		);
	}
}
