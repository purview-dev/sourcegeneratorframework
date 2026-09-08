using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Testing;

public sealed partial class CodeQuery
{
	// ---------------------------------------------------------------------------------------------
	// Operators
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets the first operator declaration matching the given token, such as <c>==</c>, optionally matching
	/// its parameter types.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No operator matched.</exception>
	public CodeQueryResult<OperatorDeclarationSyntax> GetOperator(
		string operatorToken,
		params TypeReference[]? parameters
	) =>
		TryGetOperator(operatorToken, out var @operator, parameters)
			? new(this, @operator!)
			: throw new SyntaxNotFoundException(
				$"No operator '{operatorToken}' was found in the {ScopeDescription()}{(parameters is { Length: > 0 } ? " with the specified parameters" : "")}."
			);

	/// <summary>
	/// Determines whether an operator declaration with the given token exists, optionally matching its
	/// parameter types.
	/// </summary>
	public bool HasOperator(string operatorToken, params TypeReference[]? parameters) =>
		TryGetOperator(operatorToken, out _, parameters);

	/// <summary>
	/// Attempts to get the first operator declaration matching the given token, optionally matching its
	/// parameter types.
	/// </summary>
	/// <remarks>
	/// Parameter types are resolved through the query's <see cref="Compilation"/> with the same semantics as
	/// <see cref="HasParameters"/>: nullable <i>value</i> types are significant while nullable <i>reference</i>
	/// annotations are metadata. When <paramref name="parameters"/> is <see langword="null"/> or empty, the
	/// token is matched alone.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public bool TryGetOperator(
		string operatorToken,
		out OperatorDeclarationSyntax? @operator,
		params TypeReference[]? parameters
	)
	{
		if (string.IsNullOrWhiteSpace(operatorToken))
			throw new ArgumentException("The operator token cannot be null or whitespace.", nameof(operatorToken));

		var expected = parameters ?? [];
		foreach (var tree in Trees)
		{
			foreach (var candidate in RootOf(tree).DescendantNodes().OfType<OperatorDeclarationSyntax>())
			{
				if (candidate.OperatorToken.ValueText != operatorToken)
					continue;

				if (expected.Length > 0 && !HasParameters(candidate, expected))
					continue;

				@operator = candidate;
				return true;
			}
		}

		@operator = null;
		return false;
	}

	/// <summary>
	/// Gets the first conversion operator matching the given keyword, such as <c>implicit</c> or
	/// <c>explicit</c>, optionally matching its parameter type.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No conversion operator matched.</exception>
	public CodeQueryResult<ConversionOperatorDeclarationSyntax> GetConversionOperator(
		string keyword,
		params TypeReference[]? parameters
	) =>
		TryGetConversionOperator(keyword, out var conversion, parameters)
			? new(this, conversion!)
			: throw new SyntaxNotFoundException(
				$"No '{keyword}' conversion operator was found in the {ScopeDescription()}{(parameters is { Length: > 0 } ? " with the specified parameters" : "")}."
			);

	/// <summary>
	/// Determines whether a conversion operator with the given keyword exists, optionally matching its
	/// parameter type.
	/// </summary>
	public bool HasConversionOperator(string keyword, params TypeReference[]? parameters) =>
		TryGetConversionOperator(keyword, out _, parameters);

	/// <summary>
	/// Attempts to get the first conversion operator matching the given keyword, optionally matching its
	/// parameter type.
	/// </summary>
	/// <remarks>
	/// Parameter types are resolved through the query's <see cref="Compilation"/> with the same semantics as
	/// <see cref="HasParameters"/>: nullable <i>value</i> types are significant while nullable <i>reference</i>
	/// annotations are metadata. When <paramref name="parameters"/> is <see langword="null"/> or empty, the
	/// keyword is matched alone.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public bool TryGetConversionOperator(
		string keyword,
		out ConversionOperatorDeclarationSyntax? conversion,
		params TypeReference[]? parameters
	)
	{
		if (keyword is not ("implicit" or "explicit"))
			throw new ArgumentException("The keyword must be 'implicit' or 'explicit'.", nameof(keyword));

		var expected = parameters ?? [];
		foreach (var tree in Trees)
		{
			foreach (var candidate in RootOf(tree).DescendantNodes().OfType<ConversionOperatorDeclarationSyntax>())
			{
				if (candidate.ImplicitOrExplicitKeyword.ValueText != keyword)
					continue;

				if (expected.Length > 0 && !HasParameters(candidate, expected))
					continue;

				conversion = candidate;
				return true;
			}
		}

		conversion = null;
		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Indexers
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets an indexer declaration whose parameters match the given types.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No indexer matched.</exception>
	public CodeQueryResult<IndexerDeclarationSyntax> GetIndexer(params TypeReference[] parameters) =>
		TryGetIndexer(out var indexer, parameters)
			? new(this, indexer!)
			: throw new SyntaxNotFoundException($"No indexer was found in the {ScopeDescription()}.");

	/// <summary>
	/// Determines whether an indexer declaration with the given parameter types exists.
	/// </summary>
	public bool HasIndexer(params TypeReference[] parameters) => TryGetIndexer(out _, parameters);

	/// <summary>
	/// Attempts to get an indexer declaration whose parameters match the given types.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public bool TryGetIndexer(out IndexerDeclarationSyntax? indexer, params TypeReference[] parameters)
	{
		var expected = parameters ?? [];
		foreach (var tree in Trees)
		{
			foreach (var candidate in RootOf(tree).DescendantNodes().OfType<IndexerDeclarationSyntax>())
			{
				if (expected.Length == 0 || MatchesParameters(candidate.ParameterList, expected))
				{
					indexer = candidate;
					return true;
				}
			}
		}

		indexer = null;
		return false;
	}

	bool MatchesParameters(BracketedParameterListSyntax parameterList, TypeReference[] expected)
	{
		var parameters = parameterList.Parameters;
		if (parameters.Count != expected.Length)
			return false;

		for (var index = 0; index < parameters.Count; index++)
		{
			var typeSyntax = parameters[index].Type;
			if (typeSyntax is null || !Matches(typeSyntax, expected[index]))
				return false;
		}

		return true;
	}

	// ---------------------------------------------------------------------------------------------
	// Attributes
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets all attribute applications on or within the given node.
	/// </summary>
	public ImmutableArray<CodeQueryResult<AttributeSyntax>> GetAttributes(SyntaxNode node)
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		// Note: This intentionally returns attributes on the node itself and any nested nodes, such as parameters.
		return
		[
			.. node.DescendantNodes()
				.OfType<AttributeSyntax>()
				.Select(attribute => new CodeQueryResult<AttributeSyntax>(this, attribute)),
		];
	}

	/// <summary>
	/// Determines whether the given node has an attribute with the specified name.
	/// </summary>
	/// <remarks>
	/// The name may be supplied with or without the <c>Attribute</c> suffix.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
	public bool HasAttribute(SyntaxNode node, string name)
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The attribute name cannot be null or whitespace.", nameof(name));

		foreach (var attribute in node.DescendantNodes().OfType<AttributeSyntax>())
		{
			if (MatchesAttributeName(attribute, name))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Gets the first attribute with the specified name, or <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// The name may be supplied with or without the <c>Attribute</c> suffix.
	/// </remarks>
	public CodeQueryResult<AttributeSyntax>? GetAttribute(SyntaxNode node, string name)
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The attribute name cannot be null or whitespace.", nameof(name));

		foreach (var attribute in node.DescendantNodes().OfType<AttributeSyntax>())
		{
			if (MatchesAttributeName(attribute, name))
				return new(this, attribute);
		}

		return null;
	}

	static bool MatchesAttributeName(AttributeSyntax attribute, string name)
	{
		var rendered = attribute.Name.ToString();
		if (string.Equals(rendered, name, StringComparison.Ordinal))
			return true;

		// The attribute name may be supplied with or without the "Attribute" suffix, and may be fully qualified.
		return name.EndsWith("Attribute", StringComparison.Ordinal)
			? string.Equals(rendered, name, StringComparison.Ordinal)
			: string.Equals(rendered, name + "Attribute", StringComparison.Ordinal)
				|| string.Equals(rendered, $"global::{name}", StringComparison.Ordinal)
				|| string.Equals(rendered, $"global::{name}Attribute", StringComparison.Ordinal);
	}
}
