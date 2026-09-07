namespace Purview.SourceGeneratorFramework.Generators.Helpers;

static class GeneratorTypeLibrary
{
	const string GeneratorsNamespace = "Purview.SourceGeneratorFramework.Generators";

	public static readonly TypeIdentity TypeValueObject = TypeIdentity.Create<TypeIdentity>();

	public static readonly TypeIdentity TypeReferenceValueObject = TypeIdentity.Create<TypeReference>();

	public static class Attirbutes
	{
		public static readonly TypeIdentity GenerateAttribute = new(nameof(GenerateAttribute), GeneratorsNamespace);

		public static readonly TypeIdentity PropertyAttribute = new(nameof(PropertyAttribute), GeneratorsNamespace);

		public static readonly TypeIdentity ArgumentAttribute = new(nameof(ArgumentAttribute), GeneratorsNamespace);

		public static readonly TypeIdentity NestedModelAttribute = new(
			nameof(NestedModelAttribute),
			GeneratorsNamespace
		);

		public static readonly TypeIdentity ExcludeAttribute = new(nameof(ExcludeAttribute), GeneratorsNamespace);

		public static readonly TypeIdentity GenericTypeArgumentAttribute = new(
			nameof(GenericTypeArgumentAttribute),
			GeneratorsNamespace
		);

		public static readonly TypeIdentity GenerateTypeLibraryAttribute = new(
			nameof(GenerateTypeLibraryAttribute),
			GeneratorsNamespace
		);

		public static readonly TypeIdentity TypeRefAttribute = new(nameof(TypeRefAttribute), GeneratorsNamespace);
	}

	public static class System
	{
		public static readonly TypeIdentity Action = TypeIdentity.Create<Action>();

		public static readonly TypeIdentity Object = TypeIdentity.Create<object>();

		public static readonly TypeIdentity String = TypeIdentity.Create<string>();

		public static readonly TypeIdentity Int32 = TypeIdentity.Create<int>();
	}

	public static class CodeAnalysis
	{
		public static readonly TypeIdentity IIncrementalGenerator =
			TypeIdentity.Create<Microsoft.CodeAnalysis.IIncrementalGenerator>();

		public static readonly TypeIdentity ISourceGenerator =
			TypeIdentity.Create<Microsoft.CodeAnalysis.ISourceGenerator>();

		public static readonly TypeIdentity EmbeddedAttribute =
			TypeIdentity.Create<Microsoft.CodeAnalysis.EmbeddedAttribute>();
	}
}
