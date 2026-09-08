using System.Collections.Immutable;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Describes one member assignment in a generated object-initializer expression.
/// </summary>
/// <param name="Name">The member name.</param>
/// <param name="Value">The assigned value expression.</param>
public readonly record struct ObjectInitializerMemberOptions(string Name, string? Value);

/// <summary>
/// Describes a generated object-creation expression.
/// </summary>
public readonly record struct ObjectCreationOptions
{
	/// <summary>
	/// Creates an object-creation expression.
	/// </summary>
	/// <param name="reference">The type to instantiate.</param>
	/// <param name="arguments">The constructor arguments; strings are implicitly supported.</param>
	public ObjectCreationOptions(TypeReference reference, params MethodCallArgumentOptions[] arguments)
	{
		if (reference.IsNullOrEmpty())
			throw new ArgumentException("Object-creation type cannot be empty.", nameof(reference));

		Reference = reference;
		Arguments = arguments is null ? [] : [.. arguments];
	}

	/// <summary>
	/// Creates a target-typed object-creation expression that omits the type, such as <c>new(...)</c>.
	/// The emitted expression is valid only where the target type is known, such as an assignment to a
	/// typed local, field, property, or parameter.
	/// </summary>
	/// <param name="arguments">The constructor arguments; strings are implicitly supported.</param>
	public ObjectCreationOptions(params MethodCallArgumentOptions[] arguments)
	{
		Reference = TypeReference.Empty;
		Arguments = arguments is null ? [] : [.. arguments];
	}

	/// <summary>
	/// Gets the type to instantiate.
	/// </summary>
	public TypeReference Reference { get; }

	/// <summary>
	/// Gets the constructor arguments.
	/// </summary>
	public ImmutableArray<MethodCallArgumentOptions> Arguments { get; }

	/// <summary>
	/// Gets whether constructor arguments are written one per line.
	/// </summary>
	public bool WriteArgumentsOnSeparateLines { get; init; }

	/// <summary>
	/// Gets the object-initializer member assignments written after the constructor arguments.
	/// </summary>
	public ImmutableArray<ObjectInitializerMemberOptions> InitializerMembers { get; init; }

	/// <summary>
	/// Gets whether object-initializer members are written one per line with a trailing comma.
	/// The default is <see langword="true"/>.
	/// </summary>
	public bool WriteInitializerMembersOnSeparateLines { get; init; } = true;
}
