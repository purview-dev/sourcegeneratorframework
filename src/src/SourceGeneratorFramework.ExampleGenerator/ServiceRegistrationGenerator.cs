namespace Purview.SourceGeneratorFramework.Examples;

/// <summary>
/// Generates service registration extension methods for types annotated with <see cref="GenerateServiceAttribute"/>.
/// </summary>
[Generator]
public partial class ServiceRegistrationGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the generator pipeline.
	/// </summary>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context
			.RegisterEmbeddedAttribute<ServiceRegistrationGenerator>()
			.RegisterPostInitializationOutput(ServiceRegistrationEmitter.EmitAttributeAndEnum);

		var emitServiceInfo = IncrementalPipeline.PropertyValueProvider(
			context,
			PropertyLibrary.EmitServiceRegistrationInfo,
			value => bool.TryParse(value, out var result) && result
		);

		var generationContext = IncrementalPipeline.DefaultGenerationContextValueProvider<ServiceRegistrationGenerator>(
			context,
			PropertyLibrary.DisableServiceRegistrationGenerator
		);

		var targets = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			ExampleGenerator.PurviewTypeLibrary.GenerateServiceAttribute,
			CreateServiceTarget
		);

		var model = generationContext
			.CollectWith(
				targets,
				static (ctx, targetsArray, _) =>
					new ServiceRegistrationGenerationModel(ctx, new EquatableArray<ServiceTarget>(targetsArray))
			)
			.CombineWith(emitServiceInfo, (m, emit, _) => m with { EmitServiceInfo = emit });

		context.RegisterSourceOutput(model, static (spc, m) => ServiceRegistrationEmitter.Execute(spc, m));
	}

	static ServiceTarget CreateServiceTarget(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
	{
		// ForAttributeWithMetadataName already resolves TargetSymbol; calling
		// SemanticModel.GetDeclaredSymbol again would re-run the same symbol resolution on every
		// pipeline rerun, so the pre-resolved symbol is used directly.
		var symbol = ctx.TargetSymbol;
		if (symbol is null)
			return ServiceTarget.Empty;

		var attributeData = ctx.Attributes.FirstOrDefault(a =>
			ExampleGenerator.PurviewTypeLibrary.GenerateServiceAttribute.Equals(a.AttributeClass)
		);
		if (attributeData is null)
			return ServiceTarget.Empty;

		var model = GenerateServiceAttributeData.FromAttributeData(attributeData);
		if (!model.Exists)
			return ServiceTarget.Empty;

		var lifetime = model.Lifetime ?? ExampleGenerator.PurviewTypeLibrary.ServiceLifetime.StaticMember("Singleton");
		var memberName = lifetime.Substring(lifetime.LastIndexOf('.') + 1);

		return new ServiceTarget(
			TypeName: TypeHelpers.ToFullyQualifiedDisplayString(symbol),
			ClassName: symbol.Name,
			Name: model.Name ?? symbol.Name,
			LifetimeMemberName: memberName
		);
	}
}
