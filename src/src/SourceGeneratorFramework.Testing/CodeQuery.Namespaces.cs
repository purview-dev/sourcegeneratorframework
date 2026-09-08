using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework.Testing;

public sealed partial class CodeQuery
{
	// ---------------------------------------------------------------------------------------------
	// Namespaces
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines whether the given node is declared within the specified namespace, using an exact
	/// ordinal comparison. File-scoped and block-scoped namespace declarations are both handled.
	/// </summary>
	/// <param name="node">The node whose declared namespace is checked.</param>
	/// <param name="namespace">The expected dotted namespace.</param>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
	public bool IsInNamespace(SyntaxNode node, string @namespace)
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));
		if (string.IsNullOrWhiteSpace(@namespace))
			throw new ArgumentException("The namespace cannot be null or whitespace.", nameof(@namespace));

		// The global namespace is represented by a null value, so we can just check for that.
		return string.Equals(TypeSyntaxFacts.GetDeclaredNamespace(node), @namespace, StringComparison.Ordinal);
	}

	/// <summary>
	/// Determines whether the given node is declared in the global namespace (no enclosing namespace
	/// declaration).
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
	public bool IsInGlobalNamespace(SyntaxNode node)
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		// The global namespace is represented by a null value, so we can just check for that.
		return TypeSyntaxFacts.GetDeclaredNamespace(node) is null;
	}

	/// <summary>
	/// Gets the dotted namespace the given node is declared within, or <see langword="null"/> for the global
	/// namespace.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
	public string? GetDeclaredNamespace(SyntaxNode node)
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		// The returned namespace is a dotted string, e.g. "MyCompany.MyProduct.MyFeature". File-scoped and
		return TypeSyntaxFacts.GetDeclaredNamespace(node);
	}
}
