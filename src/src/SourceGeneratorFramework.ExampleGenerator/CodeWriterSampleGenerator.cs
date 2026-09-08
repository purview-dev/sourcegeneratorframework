namespace Purview.SourceGeneratorFramework.Examples;

/// <summary>
/// Generates a sample class for every type annotated with the <c>GenerateCodeWriterSampleAttribute</c>,
/// demonstrating the structured <see cref="CodeWriter"/> API.
/// </summary>
[Generator]
public partial class CodeWriterSampleGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the generator pipeline.
	/// </summary>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context
			.RegisterEmbeddedAttribute<CodeWriterSampleGenerator>()
			.RegisterPostInitializationOutput(CodeWriterSampleEmitter.EmitAttribute);

		var targets = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			ExampleGenerator.PurviewTypeLibrary.GenerateCodeWriterSampleAttribute,
			static (ctx, ct) =>
			{
				if (ctx.SemanticModel.GetDeclaredSymbol(ctx.TargetNode, ct) is not INamedTypeSymbol symbol)
					return default;

				// The generator will emit a sample class for every type annotated with the attribute, so the target is the
				return new CodeWriterSampleTarget(
					TypeName: symbol.Name,
					Namespace: symbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
						? containingNamespace.ToDisplayString()
						: null
				);
			},
			trackingName: "ForAttribute_GenerateCodeWriterSample"
		);

		context.RegisterSourceOutput(targets, static (spc, target) => CodeWriterSampleEmitter.Execute(spc, target));
	}
}

/// <summary>
/// Identifies a type annotated with the <c>GenerateCodeWriterSampleAttribute</c>.
/// </summary>
readonly record struct CodeWriterSampleTarget(string TypeName, string? Namespace);
