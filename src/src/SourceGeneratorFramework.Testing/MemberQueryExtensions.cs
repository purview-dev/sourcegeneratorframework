using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Member and attribute query extensions that chain from a <see cref="CodeQueryResult{T}"/> obtained from a
/// <see cref="CodeQuery"/>, for example <c>query.GetClass("Service").HasMethod("Add", intType)</c> or
/// <c>query.GetRecord("Person").HasConstructor(stringType)</c>.
/// </summary>
/// <remarks>
/// The originating query is carried by the <see cref="CodeQueryResult{T}"/>, so these extensions need no
/// separate <c>CodeQuery</c> argument. Type matching (<c>Matches</c>) resolves through that query's
/// compilation.
/// </remarks>
public static class MemberQueryExtensions
{
	// ---------------------------------------------------------------------------------------------
	// Properties
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets a property declared on the type, optionally matching its type.
	/// </summary>
	public static CodeQueryResult<PropertyDeclarationSyntax> GetProperty<T>(
		this CodeQueryResult<T> type,
		string name,
		TypeReference? propertyType = null
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// If the property is not found, throw an exception with a descriptive message.
		return type.TryGetProperty(name, out var property, propertyType)
			? new(type.Query, property!)
			: throw new SyntaxNotFoundException(
				$"No property named '{name}' was found on '{type.Node.Identifier.ValueText}'."
			);
	}

	/// <summary>
	/// Determines whether the type declares a property with the given name, optionally matching its type.
	/// </summary>
	public static bool HasProperty<T>(this CodeQueryResult<T> type, string name, TypeReference? propertyType = null)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the TryGetProperty method to check for the property without throwing an exception.
		return type.TryGetProperty(name, out _, propertyType);
	}

	/// <summary>
	/// Attempts to get a property declared on the type, optionally matching its type.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public static bool TryGetProperty<T>(
		this CodeQueryResult<T> type,
		string name,
		out PropertyDeclarationSyntax? property,
		TypeReference? propertyType = null
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The property name cannot be null or whitespace.", nameof(name));

		foreach (var candidate in type.Node.Members.OfType<PropertyDeclarationSyntax>())
		{
			if (candidate.Identifier.ValueText != name)
				continue;

			if (propertyType is not null && !type.Query.Matches(candidate.Type, propertyType))
				continue;

			property = candidate;
			return true;
		}

		property = null;
		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Indexers
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets an indexer declared on the type, optionally matching its type and index parameters.
	/// </summary>
	public static CodeQueryResult<IndexerDeclarationSyntax> GetIndexer<T>(
		this CodeQueryResult<T> type,
		TypeReference? indexerType = null,
		params TypeReference[]? indexParameters
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// If the indexer is not found, throw an exception with a descriptive message.
		return type.TryGetIndexer(out var indexer, indexerType, indexParameters)
			? new(type.Query, indexer!)
			: throw new SyntaxNotFoundException(
				$"No matching indexer was found on '{type.Node.Identifier.ValueText}'."
			);
	}

	/// <summary>
	/// Determines whether the type declares an indexer, optionally matching its type and index parameters.
	/// </summary>
	public static bool HasIndexer<T>(
		this CodeQueryResult<T> type,
		TypeReference? indexerType = null,
		params TypeReference[]? indexParameters
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the TryGetIndexer method to check for the indexer without throwing an exception.
		return type.TryGetIndexer(out _, indexerType, indexParameters);
	}

	/// <summary>
	/// Attempts to get an indexer declared on the type, optionally matching its type and index parameters.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public static bool TryGetIndexer<T>(
		this CodeQueryResult<T> type,
		out IndexerDeclarationSyntax? indexer,
		TypeReference? indexerType = null,
		params TypeReference[]? indexParameters
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		var expected = indexParameters ?? [];
		foreach (var candidate in type.Node.Members.OfType<IndexerDeclarationSyntax>())
		{
			if (indexerType is not null && !type.Query.Matches(candidate.Type, indexerType))
				continue;

			if (expected.Length > 0 && !IndexerParametersMatch(type.Query, candidate, expected))
				continue;

			indexer = candidate;
			return true;
		}

		indexer = null;
		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Methods
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets a method declared on the type, optionally matching its parameter types.
	/// </summary>
	public static CodeQueryResult<MethodDeclarationSyntax> GetMethod<T>(
		this CodeQueryResult<T> type,
		string name,
		params TypeReference[]? parameters
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// If the method is not found, throw an exception with a descriptive message.
		return type.TryGetMethod(name, out var method, parameters)
			? new(type.Query, method!)
			: throw new SyntaxNotFoundException(
				$"No method named '{name}' was found on '{type.Node.Identifier.ValueText}'."
			);
	}

	/// <summary>
	/// Determines whether the type declares a method with the given name, optionally matching its parameter types.
	/// </summary>
	public static bool HasMethod<T>(this CodeQueryResult<T> type, string name, params TypeReference[]? parameters)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the TryGetMethod method to check for the method without throwing an exception.
		return type.TryGetMethod(name, out _, parameters);
	}

	/// <summary>
	/// Attempts to get a method declared on the type, optionally matching its parameter types.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public static bool TryGetMethod<T>(
		this CodeQueryResult<T> type,
		string name,
		out MethodDeclarationSyntax? method,
		params TypeReference[]? parameters
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The method name cannot be null or whitespace.", nameof(name));

		var expected = parameters ?? [];
		foreach (var candidate in type.Node.Members.OfType<MethodDeclarationSyntax>())
		{
			if (candidate.Identifier.ValueText != name)
				continue;

			if (expected.Length > 0 && !type.Query.HasParameters(candidate, expected))
				continue;

			method = candidate;
			return true;
		}

		method = null;
		return false;
	}

	/// <summary>
	/// Determines whether the type declares a method with the given name and return type.
	/// </summary>
	public static bool HasMethodReturnType<T>(this CodeQueryResult<T> type, string name, TypeReference returnType)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The method name cannot be null or whitespace.", nameof(name));
		if (returnType is null)
			throw new ArgumentNullException(nameof(returnType));

		foreach (var candidate in type.Node.Members.OfType<MethodDeclarationSyntax>())
		{
			if (candidate.Identifier.ValueText != name)
				continue;

			if (candidate.ReturnType is { } returnTypeSyntax && type.Query.Matches(returnTypeSyntax, returnType))
				return true;
		}

		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Constructors
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets a constructor declared on the type, optionally matching its parameter types.
	/// </summary>
	public static CodeQueryResult<ConstructorDeclarationSyntax> GetConstructor<T>(
		this CodeQueryResult<T> type,
		params TypeReference[]? parameters
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// If the constructor is not found, throw an exception with a descriptive message.
		return type.TryGetConstructor(out var constructor, parameters)
			? new(type.Query, constructor!)
			: throw new SyntaxNotFoundException($"No constructor was found on '{type.Node.Identifier.ValueText}'.");
	}

	/// <summary>
	/// Determines whether the type declares a constructor, optionally matching its parameter types.
	/// </summary>
	public static bool HasConstructor<T>(this CodeQueryResult<T> type, params TypeReference[]? parameters)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the TryGetConstructor method to check for the constructor without throwing an exception.
		return type.TryGetConstructor(out _, parameters);
	}

	/// <summary>
	/// Attempts to get a constructor declared on the type, optionally matching its parameter types.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public static bool TryGetConstructor<T>(
		this CodeQueryResult<T> type,
		out ConstructorDeclarationSyntax? constructor,
		params TypeReference[]? parameters
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		var expected = parameters ?? [];
		foreach (var candidate in type.Node.Members.OfType<ConstructorDeclarationSyntax>())
		{
			if (expected.Length > 0 && !type.Query.HasParameters(candidate, expected))
				continue;

			constructor = candidate;
			return true;
		}

		constructor = null;
		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Attributes
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets all attribute applications on or within the node.
	/// </summary>
	public static ImmutableArray<CodeQueryResult<AttributeSyntax>> GetAttributes<T>(this CodeQueryResult<T> node)
		where T : SyntaxNode
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		// Delegate to the query's GetAttributes method, passing the node.
		return node.Query.GetAttributes(node.Node);
	}

	/// <summary>
	/// Determines whether the node has an attribute with the specified name.
	/// </summary>
	/// <remarks>
	/// The name may be supplied with or without the <c>Attribute</c> suffix.
	/// </remarks>
	public static bool HasAttribute<T>(this CodeQueryResult<T> node, string name)
		where T : SyntaxNode
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		// Delegate to the query's HasAttribute method, passing the node and name.
		return node.Query.HasAttribute(node.Node, name);
	}

	/// <summary>
	/// Gets the first attribute with the specified name, or <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// The name may be supplied with or without the <c>Attribute</c> suffix.
	/// </remarks>
	public static CodeQueryResult<AttributeSyntax>? GetAttribute<T>(this CodeQueryResult<T> node, string name)
		where T : SyntaxNode
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		// Delegate to the query's GetAttribute method, passing the node and name.
		return node.Query.GetAttribute(node.Node, name);
	}

	// ---------------------------------------------------------------------------------------------
	// Nested types
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets a nested type declaration (class, struct, interface or record) declared directly by the type,
	/// optionally matching its generic arity.
	/// </summary>
	public static CodeQueryResult<TypeDeclarationSyntax> GetNestedType<T>(
		this CodeQueryResult<T> type,
		string name,
		int? arity = null
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// If the nested type is not found, throw an exception with a descriptive message.
		return type.TryGetNestedType(name, out var nested, arity)
			? new(type.Query, nested!)
			: throw new SyntaxNotFoundException(
				$"No nested type named '{name}' was found on '{type.Node.Identifier.ValueText}'."
			);
	}

	/// <summary>
	/// Determines whether the type declares a nested type with the given name, optionally matching its
	/// generic arity.
	/// </summary>
	public static bool HasNestedType<T>(this CodeQueryResult<T> type, string name, int? arity = null)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the TryGetNestedType method to check for the nested type without throwing an exception.
		return type.TryGetNestedType(name, out _, arity);
	}

	/// <summary>
	/// Attempts to get a nested type declaration declared directly by the type, optionally matching its
	/// generic arity.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public static bool TryGetNestedType<T>(
		this CodeQueryResult<T> type,
		string name,
		out TypeDeclarationSyntax? nested,
		int? arity = null
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The nested type name cannot be null or whitespace.", nameof(name));

		foreach (var candidate in type.Node.Members.OfType<TypeDeclarationSyntax>())
		{
			if (candidate.Identifier.ValueText != name)
				continue;

			if (arity is not null && (candidate.TypeParameterList?.Parameters.Count ?? 0) != arity)
				continue;

			nested = candidate;
			return true;
		}

		nested = null;
		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Accessibility
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines whether the member's effective accessibility matches the given value. When the member has no
	/// accessibility modifier, the C# default is applied (for example <see cref="Accessibility.Private"/> for a
	/// class member or nested type, and <see cref="Accessibility.Internal"/> for a top-level type).
	/// </summary>
	public static bool HasAccessibility<T>(this CodeQueryResult<T> member, Accessibility accessibility)
		where T : MemberDeclarationSyntax
	{
		if (member is null)
			throw new ArgumentNullException(nameof(member));

		// Use AccessibilityFacts to resolve the effective accessibility, applying C# defaults.
		return AccessibilityFacts.GetEffectiveAccessibility(member.Node) == accessibility;
	}

	/// <summary>
	/// Determines whether the property has a getter whose effective accessibility matches the given value.
	/// </summary>
	public static bool HasGetterAccessibility(
		this CodeQueryResult<PropertyDeclarationSyntax> property,
		Accessibility accessibility
	)
	{
		if (property is null)
			throw new ArgumentNullException(nameof(property));

		foreach (var accessor in property.Node.AccessorList?.Accessors ?? [])
		{
			if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
			{
				// The accessor inherits the property's accessibility when it has no modifier of its own.
				return AccessibilityFacts.GetEffectiveAccessibility(accessor, property.Node) == accessibility;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether the property has a setter whose effective accessibility matches the given value.
	/// </summary>
	public static bool HasSetterAccessibility(
		this CodeQueryResult<PropertyDeclarationSyntax> property,
		Accessibility accessibility
	)
	{
		if (property is null)
			throw new ArgumentNullException(nameof(property));

		foreach (var accessor in property.Node.AccessorList?.Accessors ?? [])
		{
			if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
			{
				// The accessor inherits the property's accessibility when it has no modifier of its own.
				return AccessibilityFacts.GetEffectiveAccessibility(accessor, property.Node) == accessibility;
			}
		}

		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Base types
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines whether the type declares the given base type in its base list, resolved through the query's
	/// compilation.
	/// </summary>
	public static bool HasBaseType<T>(this CodeQueryResult<T> type, TypeReference baseType)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (baseType is null)
			throw new ArgumentNullException(nameof(baseType));

		foreach (var baseTypeSyntax in type.Node.BaseList?.Types ?? [])
		{
			if (type.Query.Matches(baseTypeSyntax.Type, baseType))
				return true;
		}

		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Generic type parameters
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines whether the type declares a type parameter with the given name.
	/// </summary>
	public static bool HasGenericTypeParameter<T>(this CodeQueryResult<T> type, string typeParameter)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (string.IsNullOrWhiteSpace(typeParameter))
			throw new ArgumentException("The type parameter name cannot be null or whitespace.", nameof(typeParameter));

		// Check the type's TypeParameterList for a parameter with the specified name.
		return (type.Node.TypeParameterList?.Parameters ?? []).Any(parameter =>
			parameter.Identifier.ValueText == typeParameter
		);
	}

	/// <summary>
	/// Determines whether the type declares type parameters with all of the given names.
	/// </summary>
	public static bool HasGenericTypeParameters<T>(this CodeQueryResult<T> type, params string[] typeParameters)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (typeParameters is null)
			throw new ArgumentNullException(nameof(typeParameters));

		var declared = (type.Node.TypeParameterList?.Parameters ?? []).Select(parameter =>
			parameter.Identifier.ValueText
		);

		return typeParameters.All(declared.Contains);
	}

	// ---------------------------------------------------------------------------------------------
	// Namespaces
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines whether the node is declared within the specified namespace.
	/// </summary>
	public static bool IsInNamespace<T>(this CodeQueryResult<T> node, string @namespace)
		where T : SyntaxNode
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		// Delegate to the query's namespace check.
		return node.Query.IsInNamespace(node.Node, @namespace);
	}

	/// <summary>
	/// Determines whether the node is declared in the global namespace (no enclosing namespace declaration).
	/// </summary>
	public static bool IsInGlobalNamespace<T>(this CodeQueryResult<T> node)
		where T : SyntaxNode
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		// Delegate to the query's namespace check.
		return node.Query.IsInGlobalNamespace(node.Node);
	}

	static bool IndexerParametersMatch(CodeQuery query, IndexerDeclarationSyntax indexer, TypeReference[] expected)
	{
		var parameters = indexer.ParameterList.Parameters;
		if (parameters.Count != expected.Length)
			return false;

		for (var index = 0; index < parameters.Count; index++)
		{
			var typeSyntax = parameters[index].Type;
			if (typeSyntax is null || !query.Matches(typeSyntax, expected[index]))
				return false;
		}

		return true;
	}
}
