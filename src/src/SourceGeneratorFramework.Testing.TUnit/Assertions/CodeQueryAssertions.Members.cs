using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace Purview.SourceGeneratorFramework.Testing.TUnit.Assertions;

public static partial class CodeQueryAssertions
{
	// ---------------------------------------------------------------------------------------------
	// Scoped member assertions
	//
	// These chain from a scoped <see cref="CodeQueryResult{T}"/> (for example the result of
	// <c>HasGeneratedClass</c>) and return the matched member. They are named <c>OfType</c>/<c>Named</c>
	// so they do not collide with the bool <c>Has*</c> predicates in <c>MemberQueryExtensions</c>.
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the type declares a field with the given name and type, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<FieldDeclarationSyntax>> HasFieldOfType<T>(
		this CodeQueryResult<T> type,
		string name,
		TypeReference fieldType
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			return (AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>)
				AssertionResult.Failed("expected CodeQueryResult is null");
		if (string.IsNullOrWhiteSpace(name))
			return (AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>)
				AssertionResult.Failed("field name cannot be null or whitespace");
		if (fieldType is null)
			return (AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>)
				AssertionResult.Failed("field type cannot be null");

		foreach (var candidate in type.Node.Members.OfType<FieldDeclarationSyntax>())
		{
			if (!candidate.Declaration.Variables.Any(variable => variable.Identifier.ValueText == name))
				continue;

			if (type.Query.Matches(candidate.Declaration.Type, fieldType))
				return AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>.Passed(
					new CodeQueryResult<FieldDeclarationSyntax>(type.Query, candidate)
				);
		}

		return (AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>)
			AssertionResult.Failed(
				$"type '{type.Node.Identifier.ValueText}' did not contain a field named '{name}' of type '{fieldType}'"
			);
	}

	/// <summary>
	/// Asserts that the type declares a constructor with the given parameter types, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<ConstructorDeclarationSyntax>> HasConstructorOfType<T>(
		this CodeQueryResult<T> type,
		TypeReference[] parameters
	)
		where T : TypeDeclarationSyntax
	{
		if (type is null)
			return (AssertionResult<CodeQueryResult<ConstructorDeclarationSyntax>>)
				AssertionResult.Failed("expected CodeQueryResult is null");

		if (type.TryGetConstructor(out var constructor, parameters))
			return AssertionResult<CodeQueryResult<ConstructorDeclarationSyntax>>.Passed(
				new CodeQueryResult<ConstructorDeclarationSyntax>(type.Query, constructor!)
			);

		// If we get here, the constructor was not found or did not match the parameter types.
		return (AssertionResult<CodeQueryResult<ConstructorDeclarationSyntax>>)
			AssertionResult.Failed($"type '{type.Node.Identifier.ValueText}' did not contain a matching constructor");
	}

	/// <summary>
	/// Asserts that the node has an attribute with the given name, returning it.
	/// </summary>
	/// <remarks>
	/// The name may be supplied with or without the <c>Attribute</c> suffix.
	/// </remarks>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<AttributeSyntax>> HasAttributeOfType<T>(
		this CodeQueryResult<T> node,
		string name
	)
		where T : SyntaxNode
	{
		if (node is null)
			return (AssertionResult<CodeQueryResult<AttributeSyntax>>)
				AssertionResult.Failed("expected CodeQueryResult is null");
		if (string.IsNullOrWhiteSpace(name))
			return (AssertionResult<CodeQueryResult<AttributeSyntax>>)
				AssertionResult.Failed("attribute name cannot be null or whitespace");

		if (node.GetAttribute(name) is { } attribute)
			return AssertionResult<CodeQueryResult<AttributeSyntax>>.Passed(attribute);

		// If we get here, the attribute was not found.
		return (AssertionResult<CodeQueryResult<AttributeSyntax>>)
			AssertionResult.Failed($"node did not contain an attribute named '{name}'");
	}
}
