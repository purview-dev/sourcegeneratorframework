using System.Collections.Immutable;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Describes an ordinary instance or static constructor declaration.
/// </summary>
public readonly record struct ConstructorDeclarationOptions
{
	/// <summary>
	/// Initializes a constructor declaration from its containing type name.
	/// </summary>
	/// <param name="name">The name of the containing type.</param>
	/// <param name="accessibility">The optional accessibility modifier, or <see langword="null"/> to omit accessibility.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
	public ConstructorDeclarationOptions(string name, TypeDeclarationAccessibility? accessibility = null)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Type name cannot be null or whitespace.", nameof(name));

		Reference = new TypeReference(new TypeIdentity(name, null));
		Accessibility = accessibility;
	}

	/// <summary>
	/// Initializes a constructor declaration from its containing type.
	/// </summary>
	/// <param name="type">The containing type. Only its unqualified declaration name is used.</param>
	/// <param name="accessibility">The optional accessibility modifier, or <see langword="null"/> to omit accessibility.</param>
	public ConstructorDeclarationOptions(TypeIdentity type, TypeDeclarationAccessibility? accessibility = null)
		: this(type.AsTypeReference(), accessibility) { }

	/// <summary>
	/// Initializes a constructor declaration from a nullable containing type. This overload binds when a
	/// nullable identity is supplied so it is not ambiguously resolved to the <see cref="string"/>-based
	/// constructor via the implicit string conversion.
	/// </summary>
	/// <param name="type">The containing type. Only its unqualified declaration name is used.</param>
	/// <param name="accessibility">The optional accessibility modifier, or <see langword="null"/> to omit accessibility.</param>
	public ConstructorDeclarationOptions(TypeIdentity? type, TypeDeclarationAccessibility? accessibility = null)
		: this(type is null || type.Value.Name is null ? TypeIdentity.Empty : type.Value, accessibility) { }

	/// <summary>
	/// Initializes a constructor declaration from its containing type reference.
	/// </summary>
	/// <param name="reference">The containing type reference.</param>
	/// <param name="accessibility">The optional accessibility modifier, or <see langword="null"/> to omit accessibility.</param>
	public ConstructorDeclarationOptions(TypeReference reference, TypeDeclarationAccessibility? accessibility = null)
	{
		Reference = reference;
		Accessibility = accessibility;
	}

	/// <summary>
	/// Gets the structured containing type reference.
	/// </summary>
	public TypeReference Reference { get; }

	/// <summary>
	/// Gets the optional accessibility modifier, or <see langword="null"/> to omit accessibility.
	/// </summary>
	public TypeDeclarationAccessibility? Accessibility { get; init; }

	/// <summary>
	/// Gets whether a static constructor is emitted.
	/// </summary>
	/// <remarks>
	/// Static constructors cannot declare parameters or an initializer.
	/// </remarks>
	public bool IsStatic { get; init; }

	/// <summary>
	/// Gets the constructor parameters.
	/// </summary>
	/// <remarks>
	/// Each entry is emitted verbatim as a complete parameter declaration.
	/// </remarks>
	public ImmutableArray<ParameterDeclarationOptions> Parameters { get; init; }

	/// <summary>
	/// Gets attributes applied to the constructor.
	/// </summary>
	public ImmutableArray<AttributeDeclarationOptions> Attributes { get; init; }

	/// <summary>
	/// Gets whether the parameters are written on separate lines, with each parameter indented.
	/// </summary>
	public bool WriteParametersOnSeparateLines { get; init; }

	/// <summary>
	/// Gets the optional constructor initializer without the leading colon.
	/// </summary>
	/// <example><c>base(connectionString)</c> or <c>this("Default")</c>.</example>
	public string? Initializer { get; init; }

	/// <summary>
	/// Gets whether to emit generated attributes such as <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> and
	/// <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/>.
	/// When <see langword="null"/>, the value is inherited from <see cref="CodeWriter.DefaultIncludeGeneratedAttributes"/>.
	/// </summary>
	public bool? IncludeGeneratedAttributes { get; init; }
}
