using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Describes a generated class, struct, record, interface, enum, or delegate declaration.
/// </summary>
public sealed record TypeDeclarationOptions
{
	/// <summary>
	/// Initializes a type declaration description.
	/// </summary>
	/// <param name="name">The generated type name without generic parameters.</param>
	/// <param name="accessibility">The optional accessibility modifier, or <see langword="null"/> to omit accessibility.</param>
	public TypeDeclarationOptions(string name, TypeDeclarationAccessibility? accessibility = null)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Type name cannot be null or whitespace.", nameof(name));

		Name = name;
		Accessibility = accessibility;
	}

	/// <summary>
	/// Initializes a type declaration description from a <see cref="TypeIdentity"/>.
	/// </summary>
	/// <param name="type">The type value object.</param>
	/// <param name="accessibility">The optional accessibility modifier, or <see langword="null"/> to omit accessibility.</param>
	public TypeDeclarationOptions(TypeIdentity type, TypeDeclarationAccessibility? accessibility = null)
	{
		Name = type.Name;
		Accessibility = accessibility;
	}

	/// <summary>
	/// Initializes a type declaration description from a nullable <see cref="TypeIdentity"/>. This overload
	/// binds when a nullable identity is supplied so it is not ambiguously resolved to the
	/// <see cref="string"/>-based constructor via the implicit string conversion.
	/// </summary>
	/// <param name="type">The type value object.</param>
	/// <param name="accessibility">The optional accessibility modifier, or <see langword="null"/> to omit accessibility.</param>
	public TypeDeclarationOptions(TypeIdentity? type, TypeDeclarationAccessibility? accessibility = null)
	{
		if (type is null || type.Value.Name is null)
			throw new ArgumentException("Type name cannot be null or whitespace.", nameof(type));

		Name = type.Value.Name;
		Accessibility = accessibility;
	}

	/// <summary>
	/// Gets the generated type name without generic parameters.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the declaration kind. The default is <see cref="TypeDeclarationKind.Class"/>.
	/// </summary>
	public TypeDeclarationKind Kind { get; init; } = TypeDeclarationKind.Class;

	/// <summary>
	/// Gets the accessibility modifier, or <see langword="null"/> to omit accessibility.
	/// </summary>
	public TypeDeclarationAccessibility? Accessibility { get; init; }

	/// <summary>
	/// Gets whether the <c>partial</c> modifier is emitted. The default is <see langword="true"/>.
	/// </summary>
	public bool IsPartial { get; init; } = true;

	/// <summary>
	/// Gets whether the <c>sealed</c> modifier is emitted for a class or record class.
	/// The default is <see langword="true"/>. This option is ignored for struct declarations.
	/// </summary>
	public bool IsSealed { get; init; } = true;

	/// <summary>
	/// Gets whether the <c>abstract</c> modifier is emitted for a class or record class.
	/// </summary>
	/// <remarks>
	/// Abstract declarations take precedence over the default <see cref="IsSealed"/> value.
	/// </remarks>
	public bool IsAbstract { get; init; }

	/// <summary>
	/// Gets whether the <c>static</c> modifier is emitted for a class declaration.
	/// </summary>
	/// <remarks>
	/// Static classes cannot declare a base type, implement interfaces, or declare
	/// primary-constructor parameters. <see cref="IsSealed"/> is ignored when this value is
	/// <see langword="true"/>.
	/// </remarks>
	public bool IsStatic { get; init; }

	/// <summary>
	/// Gets whether the <c>readonly</c> modifier is emitted for a struct or record struct.
	/// </summary>
	public bool IsReadOnly { get; init; }

	/// <summary>
	/// Gets whether the <c>ref</c> modifier is emitted, producing a <c>ref struct</c> declaration.
	/// Only valid for <see cref="TypeDeclarationKind.Struct"/>.
	/// </summary>
	public bool IsRefStruct { get; init; }

	/// <summary>
	/// Gets the optional base class or base record type.
	/// </summary>
	/// <remarks>
	/// Struct and record struct declarations cannot specify a base type.
	/// </remarks>
	public TypeReference? BaseType { get; init; }

	/// <summary>
	/// Gets the optional enum underlying integral type.
	/// </summary>
	public TypeReference? EnumUnderlyingType { get; init; }

	/// <summary>
	/// Gets the delegate return type.
	/// </summary>
	public TypeReference? DelegateReturnType { get; init; }

	/// <summary>
	/// Gets the complete delegate parameter declarations.
	/// </summary>
	public ImmutableArray<ParameterDeclarationOptions> DelegateParameters { get; init; } = [];

	/// <summary>
	/// Gets the interfaces implemented by the generated type, or inherited by an interface.
	/// </summary>
	public ImmutableArray<TypeReference> Interfaces { get; init; } = [];

	/// <summary>
	/// Gets the generic type parameters and their constraints.
	/// </summary>
	public ImmutableArray<GenericTypeParameterOptions> GenericTypes { get; init; } = [];

	/// <summary>
	/// Gets the primary-constructor parameters written after the type name and generic parameters.
	/// </summary>
	/// <remarks>
	/// Each entry is emitted verbatim as a complete parameter declaration.
	/// </remarks>
	public ImmutableArray<ParameterDeclarationOptions> PrimaryConstructorParameters { get; init; } = [];

	/// <summary>
	/// If <see  langword="true" />, the primary-constructor parameters are emitted on separate lines with one parameter per line.
	/// </summary>
	public bool ConstructorParametersOnSeparateLines { get; init; }

	/// <summary>
	/// Gets the attributes applied to the generated type.
	/// </summary>
	public ImmutableArray<AttributeDeclarationOptions> Attributes { get; init; } = [];

	/// <summary>
	/// Gets whether to emit <see cref="EmbeddedAttribute"/> on the type.
	/// When <see langword="null"/>, <c>AttributeClass</c> and <c>Enum</c> enable it and other
	/// type-writing APIs leave it disabled. Set this explicitly to <see langword="false"/> to opt a
	/// generated attribute or enum out of embedding.
	/// </summary>
	public bool? IncludeEmbeddedAttribute { get; init; }

	/// <summary>
	/// Gets whether to process any generated attributes, such as <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> and <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/>.
	/// <see cref="IncludeEmbeddedAttribute"/> is ignored if this is <see langword="false"/>.
	/// When <see langword="null"/>, the value is inherited from <see cref="CodeWriter.DefaultIncludeGeneratedAttributes"/>.
	/// </summary>
	public bool? IncludeGeneratedAttributes { get; init; }
}
