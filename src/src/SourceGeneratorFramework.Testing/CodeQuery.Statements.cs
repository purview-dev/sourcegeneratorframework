using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Testing;

public sealed partial class CodeQuery
{
	// ---------------------------------------------------------------------------------------------
	// Statements
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets the first <c>foreach</c> statement, optionally matching its iterator text.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No statement matched.</exception>
	public CodeQueryResult<ForEachStatementSyntax> GetForeach(string? iterator = null) =>
		Get<ForEachStatementSyntax>(statement => iterator is null || statement.Expression.ToString() == iterator);

	/// <summary>
	/// Determines whether a <c>foreach</c> statement exists, optionally matching its iterator text.
	/// </summary>
	public bool HasForeach(string? iterator = null) =>
		Has<ForEachStatementSyntax>(statement => iterator is null || statement.Expression.ToString() == iterator);

	/// <summary>
	/// Gets the first <c>for</c> statement.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No statement matched.</exception>
	public CodeQueryResult<ForStatementSyntax> GetFor() => Get<ForStatementSyntax>();

	/// <summary>
	/// Determines whether a <c>for</c> statement exists.
	/// </summary>
	public bool HasFor() => Has<ForStatementSyntax>();

	/// <summary>
	/// Gets the first <c>while</c> statement, optionally matching its condition.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No statement matched.</exception>
	public CodeQueryResult<WhileStatementSyntax> GetWhile(string? condition = null) =>
		Get<WhileStatementSyntax>(statement => condition is null || statement.Condition.ToString() == condition);

	/// <summary>
	/// Determines whether a <c>while</c> statement exists, optionally matching its condition.
	/// </summary>
	public bool HasWhile(string? condition = null) =>
		Has<WhileStatementSyntax>(statement => condition is null || statement.Condition.ToString() == condition);

	/// <summary>
	/// Gets the first <c>if</c> statement.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No statement matched.</exception>
	public CodeQueryResult<IfStatementSyntax> GetIf() => Get<IfStatementSyntax>();

	/// <summary>
	/// Determines whether an <c>if</c> statement exists.
	/// </summary>
	public bool HasIf() => Has<IfStatementSyntax>();

	/// <summary>
	/// Gets the first <c>try</c> statement.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No statement matched.</exception>
	public CodeQueryResult<TryStatementSyntax> GetTry() => Get<TryStatementSyntax>();

	/// <summary>
	/// Determines whether a <c>try</c> statement exists.
	/// </summary>
	public bool HasTry() => Has<TryStatementSyntax>();

	/// <summary>
	/// Gets the first invocation of a method with the given simple name.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No invocation matched.</exception>
	public CodeQueryResult<InvocationExpressionSyntax> GetInvocation(string methodName) =>
		TryGetInvocation(methodName, out var invocation)
			? new(this, invocation!)
			: throw new SyntaxNotFoundException(
				$"No invocation of '{methodName}' was found in the {ScopeDescription()}."
			);

	/// <summary>
	/// Determines whether an invocation of a method with the given simple name exists.
	/// </summary>
	public bool HasInvocation(string methodName) => TryGetInvocation(methodName, out _);

	/// <summary>
	/// Attempts to get the first invocation of a method with the given simple name.
	/// </summary>
	public bool TryGetInvocation(string methodName, out InvocationExpressionSyntax? invocation)
	{
		if (string.IsNullOrWhiteSpace(methodName))
			throw new ArgumentException("The method name cannot be null or whitespace.", nameof(methodName));

		foreach (var tree in Trees)
		{
			foreach (var candidate in RootOf(tree).DescendantNodes().OfType<InvocationExpressionSyntax>())
			{
				if (GetInvokedMethodName(candidate) == methodName)
				{
					invocation = candidate;
					return true;
				}
			}
		}

		invocation = null;
		return false;
	}

	/// <summary>
	/// Determines whether an object-creation expression of the given type exists.
	/// </summary>
	public bool HasObjectCreation(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		foreach (var tree in Trees)
		{
			foreach (var candidate in RootOf(tree).DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
			{
				if (Matches(candidate.Type, type))
					return true;
			}
		}

		return false;
	}

	static string? GetInvokedMethodName(InvocationExpressionSyntax invocation) =>
		invocation.Expression switch
		{
			IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
			GenericNameSyntax generic => generic.Identifier.ValueText,
			MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
			MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
			_ => null,
		};
}
