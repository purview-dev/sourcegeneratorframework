using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace Purview.SourceGeneratorFramework.Testing.TUnit.Assertions;

/// <summary>
/// TUnit assertion extensions that query the code produced by a test run and return the matched syntax node,
/// wrapped as a <see cref="CodeQueryResult{T}"/> so member queries can chain without re-passing the query.
/// </summary>
/// <remarks>
/// Every assertion operates directly on a <see cref="CodeQuery"/>, so the query can come from a test result
/// (for example <c>result.Generated()</c> for a source-generator run) or any nested/derived query. Convenience
/// overloads accept the test result types directly and query the relevant code.
/// </remarks>
public static partial class CodeQueryAssertions
{
	// ---------------------------------------------------------------------------------------------
	// Generated code (source generators)
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the generated code contains a method with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasGeneratedMethod(
		this DriverRunResult result,
		string methodName
	) => result is null ? NullResult<MethodDeclarationSyntax>() : HasGeneratedMethod(result.Generated(), methodName);

	/// <summary>
	/// Asserts that the generated code contains a method with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasGeneratedMethod(
		this CodeQuery query,
		string methodName
	) => GetMethod(query, methodName, null, "generated code");

	/// <summary>
	/// Asserts that the generated code contains a method with the given name and parameter types, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasGeneratedMethod(
		this DriverRunResult result,
		string methodName,
		TypeReference[] parameters
	) =>
		result is null
			? NullResult<MethodDeclarationSyntax>()
			: HasGeneratedMethod(result.Generated(), methodName, parameters);

	/// <summary>
	/// Asserts that the generated code contains a method with the given name and parameter types, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasGeneratedMethod(
		this CodeQuery query,
		string methodName,
		TypeReference[] parameters
	) => GetMethod(query, methodName, parameters, "generated code");

	/// <summary>
	/// Asserts that the generated code contains a method with the given name and return type, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasGeneratedMethodReturnType(
		this DriverRunResult result,
		string methodName,
		TypeReference returnType
	) =>
		result is null
			? NullResult<MethodDeclarationSyntax>()
			: HasGeneratedMethodReturnType(result.Generated(), methodName, returnType);

	/// <summary>
	/// Asserts that the generated code contains a method with the given name and return type, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasGeneratedMethodReturnType(
		this CodeQuery query,
		string methodName,
		TypeReference returnType
	)
	{
		if (query is null)
			return (AssertionResult<CodeQueryResult<MethodDeclarationSyntax>>)
				AssertionResult.Failed("expected CodeQuery is null");
		if (string.IsNullOrWhiteSpace(methodName))
			return (AssertionResult<CodeQueryResult<MethodDeclarationSyntax>>)
				AssertionResult.Failed("method name cannot be null or whitespace");

		if (query.TryGetMethod(methodName, out var method) && query.HasReturnType(methodName, returnType))
			return AssertionResult<CodeQueryResult<MethodDeclarationSyntax>>.Passed(
				new CodeQueryResult<MethodDeclarationSyntax>(query, method!)
			);

		// If the method exists but has a different return type, we could provide more detail in the failure message.
		return (AssertionResult<CodeQueryResult<MethodDeclarationSyntax>>)
			AssertionResult.Failed(
				$"generated code did not contain a method named '{methodName}' with the expected return type"
			);
	}

	/// <summary>
	/// Asserts that the generated code contains a property with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<PropertyDeclarationSyntax>> HasGeneratedProperty(
		this DriverRunResult result,
		string propertyName
	) =>
		result is null
			? NullResult<PropertyDeclarationSyntax>()
			: HasGeneratedProperty(result.Generated(), propertyName);

	/// <summary>
	/// Asserts that the generated code contains a property with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<PropertyDeclarationSyntax>> HasGeneratedProperty(
		this CodeQuery query,
		string propertyName
	)
	{
		if (query is null)
			return (AssertionResult<CodeQueryResult<PropertyDeclarationSyntax>>)
				AssertionResult.Failed("expected CodeQuery is null");
		if (string.IsNullOrWhiteSpace(propertyName))
			return (AssertionResult<CodeQueryResult<PropertyDeclarationSyntax>>)
				AssertionResult.Failed("property name cannot be null or whitespace");

		if (query.TryGetProperty(propertyName, out var declaration))
			return AssertionResult<CodeQueryResult<PropertyDeclarationSyntax>>.Passed(
				new CodeQueryResult<PropertyDeclarationSyntax>(query, declaration!)
			);

		// If the property exists but has a different type, we could provide more detail in the failure message.
		return (AssertionResult<CodeQueryResult<PropertyDeclarationSyntax>>)
			AssertionResult.Failed($"generated code did not contain a property named '{propertyName}'");
	}

	/// <summary>
	/// Asserts that the generated code contains a field with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<FieldDeclarationSyntax>> HasGeneratedField(
		this DriverRunResult result,
		string fieldName
	) => result is null ? NullResult<FieldDeclarationSyntax>() : HasGeneratedField(result.Generated(), fieldName);

	/// <summary>
	/// Asserts that the generated code contains a field with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<FieldDeclarationSyntax>> HasGeneratedField(
		this CodeQuery query,
		string fieldName
	)
	{
		if (query is null)
			return (AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>)
				AssertionResult.Failed("expected CodeQuery is null");
		if (string.IsNullOrWhiteSpace(fieldName))
			return (AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>)
				AssertionResult.Failed("field name cannot be null or whitespace");

		if (query.TryGetField(fieldName, out var declaration))
			return AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>.Passed(
				new CodeQueryResult<FieldDeclarationSyntax>(query, declaration!)
			);

		// If the field exists but has a different type, we could provide more detail in the failure message.
		return (AssertionResult<CodeQueryResult<FieldDeclarationSyntax>>)
			AssertionResult.Failed($"generated code did not contain a field named '{fieldName}'");
	}

	/// <summary>
	/// Asserts that the generated code contains a syntax tree with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<SyntaxTree> HasGeneratedSyntaxTree(this DriverRunResult result, string treeName) =>
		result is null
			? (AssertionResult<SyntaxTree>)AssertionResult.Failed("expected DriverRunResult is null")
			: HasGeneratedSyntaxTree(result.Generated(), treeName);

	/// <summary>
	/// Asserts that the generated code contains a syntax tree with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<SyntaxTree> HasGeneratedSyntaxTree(this CodeQuery query, string treeName)
	{
		if (query is null)
			return (AssertionResult<SyntaxTree>)AssertionResult.Failed("expected CodeQuery is null");
		if (string.IsNullOrWhiteSpace(treeName))
			return (AssertionResult<SyntaxTree>)AssertionResult.Failed("tree name cannot be null or whitespace");

		if (query.TryGetSyntaxTree(treeName, out var tree))
			return AssertionResult<SyntaxTree>.Passed(tree!);

		// If the syntax tree exists but has a different name, we could provide more detail in the failure message.
		return (AssertionResult<SyntaxTree>)
			AssertionResult.Failed($"generated code did not contain a syntax tree named '{treeName}'");
	}

	// ---------------------------------------------------------------------------------------------
	// Fixed code (code fixes and refactorings)
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the fixed code contains a method with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasFixedMethod(
		this CodeFixTestResult result,
		string methodName
	) => result is null ? NullResult<MethodDeclarationSyntax>() : HasFixedMethod(result.FixedCode(), methodName);

	/// <summary>
	/// Asserts that the fixed code contains a method with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasFixedMethod(
		this CodeQuery query,
		string methodName
	) => GetMethod(query, methodName, null, "fixed code");

	/// <summary>
	/// Asserts that the fixed code contains a method with the given name and parameter types, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasFixedMethod(
		this CodeFixTestResult result,
		string methodName,
		TypeReference[] parameters
	) =>
		result is null
			? NullResult<MethodDeclarationSyntax>()
			: HasFixedMethod(result.FixedCode(), methodName, parameters);

	/// <summary>
	/// Asserts that the fixed code contains a method with the given name and parameter types, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasFixedMethod(
		this CodeQuery query,
		string methodName,
		TypeReference[] parameters
	) => GetMethod(query, methodName, parameters, "fixed code");

	/// <summary>
	/// Asserts that the fixed code contains a method with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasFixedMethod(
		this CodeFixFixAllResult result,
		string methodName
	) => result is null ? NullResult<MethodDeclarationSyntax>() : HasFixedMethod(result.FixedCode(), methodName);

	/// <summary>
	/// Asserts that the fixed code contains a method with the given name, returning it.
	/// </summary>
	[GenerateAssertion]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> HasFixedMethod(
		this RefactorTestResult result,
		string methodName
	) => result is null ? NullResult<MethodDeclarationSyntax>() : HasFixedMethod(result.FixedCode(), methodName);

	// ---------------------------------------------------------------------------------------------
	// Shared
	// ---------------------------------------------------------------------------------------------

	static AssertionResult<CodeQueryResult<T>> NullResult<T>()
		where T : SyntaxNode =>
		(AssertionResult<CodeQueryResult<T>>)AssertionResult.Failed($"expected {nameof(DriverRunResult)} is null");

	static AssertionResult<CodeQueryResult<MethodDeclarationSyntax>> GetMethod(
		CodeQuery? query,
		string methodName,
		TypeReference[]? parameters,
		string scope
	)
	{
		if (query is null)
			return (AssertionResult<CodeQueryResult<MethodDeclarationSyntax>>)
				AssertionResult.Failed("expected test result is null");
		if (string.IsNullOrWhiteSpace(methodName))
			return (AssertionResult<CodeQueryResult<MethodDeclarationSyntax>>)
				AssertionResult.Failed("method name cannot be null or whitespace");

		if (query.TryGetMethod(methodName, out var method, parameters))
			return AssertionResult<CodeQueryResult<MethodDeclarationSyntax>>.Passed(
				new CodeQueryResult<MethodDeclarationSyntax>(query, method!)
			);

		// If the method exists but has different parameters, we could provide more detail in the failure message.
		return (AssertionResult<CodeQueryResult<MethodDeclarationSyntax>>)
			AssertionResult.Failed($"{scope} did not contain a method named '{methodName}'");
	}
}
