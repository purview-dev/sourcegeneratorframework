using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Testing;

public sealed partial class CodeQuery
{
	/// <summary>
	/// Determines whether a method or constructor declaration matches the given parameter types.
	/// </summary>
	/// <remarks>
	/// Parameter types are resolved through the query's <see cref="Compilation"/> and matched with the
	/// framework's <c>MatchesTypeReference</c> semantics: nullable <i>value</i> types are significant
	/// (<c>int?</c> does not match <c>int</c>) while nullable <i>reference</i> annotations are metadata.
	/// </remarks>
	public bool HasParameters(BaseMethodDeclarationSyntax method, params TypeReference[] expected)
	{
		if (method is null)
			throw new ArgumentNullException(nameof(method));
		if (expected is null)
			throw new ArgumentNullException(nameof(expected));

		var parameters = method.ParameterList.Parameters;
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

	/// <summary>
	/// Determines whether a method declaration's return type matches the given reference.
	/// </summary>
	public bool HasReturnType(string methodName, TypeReference returnType)
	{
		if (string.IsNullOrWhiteSpace(methodName))
			throw new ArgumentException("The method name cannot be null or whitespace.", nameof(methodName));
		if (returnType is null)
			throw new ArgumentNullException(nameof(returnType));

		if (!TryGetMethod(methodName, out var method))
			return false;

		// If the method has no return type (e.g., it's a constructor), it cannot match any reference.
		return method!.ReturnType is { } returnTypeSyntax && Matches(returnTypeSyntax, returnType);
	}

	/// <summary>
	/// Determines whether a type syntax resolves to the given reference, using the query's compilation.
	/// </summary>
	public bool Matches(TypeSyntax typeSyntax, TypeReference reference)
	{
		if (typeSyntax is null)
			throw new ArgumentNullException(nameof(typeSyntax));
		if (reference is null)
			throw new ArgumentNullException(nameof(reference));

		var compilation = Compilation;
		if (compilation is null || !compilation.ContainsSyntaxTree(typeSyntax.SyntaxTree))
		{
			throw new InvalidOperationException(
				"The query's compilation does not contain the syntax tree being matched. Construct the query from the compilation's own trees (for example via the Generated/Output/FixedCode adapters) or use the syntactic Get/Has overloads."
			);
		}

		// Delegate to the reference's MatchesTypeReference method, which handles symbol resolution and comparison.
		return reference.MatchesTypeReference(typeSyntax, compilation.GetSemanticModel(typeSyntax.SyntaxTree));
	}
}

/// <summary>
/// Signature inspection helpers for members obtained from a <see cref="CodeQuery"/>. These support chaining
/// from a <see cref="CodeQueryResult{T}"/>, for example <c>query.GetClass("C").HasMethod("M", intType)</c>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class CodeQuerySignatureExtensions
{
	/// <summary>
	/// Creates a nested query scoped to this node, for example <c>node.Query(parent)</c> when a raw syntax
	/// node is at hand. For <see cref="CodeQueryResult{T}"/> results, use the <c>Query</c> property or the
	/// implicit conversion to a scoped <see cref="CodeQuery"/> instead.
	/// </summary>
	public static CodeQuery Query(this SyntaxNode node, CodeQuery parent)
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));
		if (parent is null)
			throw new ArgumentNullException(nameof(parent));

		// Delegate to the parent query's In method, which creates a new query scoped to the given node.
		return parent.In(node);
	}

	/// <summary>
	/// Determines whether the method's or constructor's parameters match the given types, resolved through the
	/// query's compilation.
	/// </summary>
	public static bool HasParameters<T>(this CodeQueryResult<T> method, params TypeReference[] expected)
		where T : BaseMethodDeclarationSyntax
	{
		if (method is null)
			throw new ArgumentNullException(nameof(method));

		// Delegate to the query's HasParameters method, which handles the parameter count and type matching.
		return method.Query.HasParameters(method.Node, expected);
	}

	/// <summary>
	/// Determines whether the method's return type matches the given reference, resolved through the query's
	/// compilation.
	/// </summary>
	public static bool HasReturnType<T>(this CodeQueryResult<T> method, TypeReference returnType)
		where T : BaseMethodDeclarationSyntax
	{
		if (method is null)
			throw new ArgumentNullException(nameof(method));
		if (returnType is null)
			throw new ArgumentNullException(nameof(returnType));

		// If the method has no return type (e.g., it's a constructor), it cannot match any reference.
		return method.Node is MethodDeclarationSyntax { ReturnType: { } returnTypeSyntax }
			&& method.Query.Matches(returnTypeSyntax, returnType);
	}

	/// <summary>
	/// Determines whether the property's or indexer's type matches the given reference, resolved through the
	/// query's compilation.
	/// </summary>
	public static bool HasType<T>(this CodeQueryResult<T> node, TypeReference type)
		where T : BasePropertyDeclarationSyntax
	{
		if (node is null)
			throw new ArgumentNullException(nameof(node));
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// The property type is always non-nullable in C# syntax, so we can directly match it with the expected type reference.
		return node.Query.Matches(node.Node.Type, type);
	}
}
