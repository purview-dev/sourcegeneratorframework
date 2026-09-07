using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
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
