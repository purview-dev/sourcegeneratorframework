namespace Purview.SourceGeneratorFramework.Generators.Model;

/// <summary>
/// Describes the generated type-library class for a <c>[GenerateTypeLibrary]</c> spec.
/// </summary>
sealed record TypeLibraryModel(
	string Specifier,
	string SpecClassName,
	string? SpecNamespace,
	TypeDeclarationAccessibility? SpecAccessibility,
	string? Namespace,
	string ClassName,
	string? Documentation,
	EquatableArray<TypeLibraryNamespaceNode> Namespaces
);

/// <summary>
/// Describes a nested namespace class in the generated type library, mirroring the namespace hierarchy
/// of the declared members.
/// </summary>
sealed record TypeLibraryNamespaceNode(
	string Name,
	string NamespaceValue,
	EquatableArray<TypeLibraryMemberModel> Members,
	EquatableArray<TypeLibraryNamespaceNode> Children
);

/// <summary>
/// Describes a single member declared in the type library.
/// </summary>
sealed record TypeLibraryMemberModel(
	string MemberName,
	string? TypeName,
	string? Namespace,
	int GenericArity,
	string? ReferenceInitializer,
	bool IsTypeReference,
	string? Documentation,
	bool IncludeInGetTypes = false
)
{
	/// <summary>
	/// Gets whether the member's value comes from a copied initializer expression rather than a simple
	/// <c>new("Name", "Namespace")</c> identity.
	/// </summary>
	public bool IsReference => ReferenceInitializer is not null;
}
