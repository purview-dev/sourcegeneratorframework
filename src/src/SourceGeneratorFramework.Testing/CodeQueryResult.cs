using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// The result of a <see cref="CodeQuery"/> lookup: the matched syntax node and the query that found it.
/// </summary>
/// <remarks>
/// <para>
/// Implicitly converts to both the underlying <typeparamref name="T"/> node and to a <see cref="CodeQuery"/>
/// scoped to that node, so a result can be used anywhere a raw node is expected and chained member queries
/// (see <c>MemberQueryExtensions</c> and <c>CodeQuerySignatureExtensions</c>) can run without re-passing the
/// originating query.
/// </para>
/// <para>
/// The type is invariant in <typeparamref name="T"/> to allow the implicit conversion operators, so extension
/// methods target <c>CodeQueryResult&lt;T&gt;</c> with a type constraint on the node kind (for example
/// <c>where T : TypeDeclarationSyntax</c>).
/// </para>
/// </remarks>
/// <typeparam name="T">The type of syntax node that was matched.</typeparam>
public sealed class CodeQueryResult<T>
	where T : SyntaxNode
{
	internal CodeQueryResult(CodeQuery query, T node)
	{
		if (query is null)
			throw new ArgumentNullException(nameof(query));
		if (node is null)
			throw new ArgumentNullException(nameof(node));

		Query = query.In(node);
		Node = node;
	}

	/// <summary>
	/// Gets the matched syntax node.
	/// </summary>
	public T Node { get; }

	/// <summary>
	/// Gets a query scoped to <see cref="Node"/>, sharing the originating trees and compilation.
	/// </summary>
	public CodeQuery Query { get; }

	/// <summary>
	/// Implicitly converts a result to its underlying syntax node, or <see langword="null"/> if the result is
	/// <see langword="null"/>.
	/// </summary>
	public static implicit operator T(CodeQueryResult<T> result) => result?.Node ?? null!;

	/// <summary>
	/// Implicitly converts a result to a query scoped to its node, or <see langword="null"/> if the result is
	/// <see langword="null"/>.
	/// </summary>
	public static implicit operator CodeQuery(CodeQueryResult<T> result) => result?.Query ?? null!;
}
