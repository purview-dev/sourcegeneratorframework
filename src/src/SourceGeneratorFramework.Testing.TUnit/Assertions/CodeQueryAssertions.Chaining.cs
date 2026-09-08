using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TUnit.Assertions.Core;

namespace Purview.SourceGeneratorFramework.Testing.TUnit.Assertions;

/// <summary>
/// Structured node-inspection assertions that chain from a <see cref="CodeQueryResult{T}"/> and keep the
/// matched node flowing through the chain, so further assertions can be appended with <c>.And</c>. For
/// example:
/// <code>
/// await Assert.That(query)
///     .HasGeneratedClass("Service")
///     .And.HasNestedType("Builder")
///     .And.WithAccessibility(Accessibility.Private)
///     .And.HasMethodOfType("Build", []);
/// </code>
/// </summary>
/// <remarks>
/// These assertions are implemented as custom <see cref="Assertion{TValue}"/> classes rather than
/// <c>[GenerateAssertion]</c> methods because TUnit's chain type is the assertion's <i>receiver</i>. A
/// <c>[GenerateAssertion]</c> method returning <c>AssertionResult&lt;TOut&gt;</c> still chains on its receiver
/// type, so it cannot change the node being asserted. The custom classes use <c>context.Map</c> to move the
/// chain onto the node that was found, which is what enables <c>HasGeneratedClass(...).And.HasPropertyOfType(...)</c>.
/// </remarks>
public static partial class CodeQueryAssertions
{
	// ---------------------------------------------------------------------------------------------
	// Helpers
	// ---------------------------------------------------------------------------------------------

	static void AppendExpression<T>(IAssertionSource<T> source, string expression) =>
		source.Context.ExpressionBuilder.Append(expression);

	// ---------------------------------------------------------------------------------------------
	// Finding a class
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the generated code contains a class with the given name, moving the chain onto it.
	/// </summary>
	public static HasGeneratedClassAssertion HasGeneratedClass(
		this IAssertionSource<CodeQuery> source,
		string className,
		[CallerArgumentExpression(nameof(className))] string? classNameExpression = null
	)
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));

		AppendExpression(source, ".HasGeneratedClass(" + classNameExpression + ")");

		return new HasGeneratedClassAssertion(source.Context.Map(query => FindClass(query, className, null, null)));
	}

	/// <summary>
	/// Asserts that the generated code contains a class with the given name and generic arity, moving the
	/// chain onto it.
	/// </summary>
	public static HasGeneratedClassAssertion HasGeneratedClass(
		this IAssertionSource<CodeQuery> source,
		string className,
		int arity,
		[CallerArgumentExpression(nameof(className))] string? classNameExpression = null
	)
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));

		AppendExpression(
			source,
			".HasGeneratedClass(" + classNameExpression + ", " + arity.ToString(CultureInfo.InvariantCulture) + ")"
		);

		return new HasGeneratedClassAssertion(source.Context.Map(query => FindClass(query, className, arity, null)));
	}

	/// <summary>
	/// Asserts that the generated code contains a class with the given type identity, moving the chain onto it.
	/// </summary>
	public static HasGeneratedClassAssertion HasGeneratedClass(
		this IAssertionSource<CodeQuery> source,
		TypeReference type,
		[CallerArgumentExpression(nameof(type))] string? typeExpression = null
	)
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		AppendExpression(source, ".HasGeneratedClass(" + typeExpression + ")");

		return new HasGeneratedClassAssertion(
			source.Context.Map(query =>
				FindClass(query, type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace)
			)
		);
	}

	/// <summary>
	/// Asserts that the generated code contains a class with the given name, moving the chain onto it.
	/// </summary>
	public static HasGeneratedClassAssertion HasGeneratedClass(
		this IAssertionSource<DriverRunResult> source,
		string className,
		[CallerArgumentExpression(nameof(className))] string? classNameExpression = null
	)
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));

		AppendExpression(source, ".HasGeneratedClass(" + classNameExpression + ")");

		return new HasGeneratedClassAssertion(
			source.Context.Map(result =>
				result is null
					? throw new InvalidOperationException("the test result was null")
					: FindClass(result.Generated(), className, null, null)
			)
		);
	}

	/// <summary>
	/// Asserts that the generated code contains a class with the given name and generic arity, moving the
	/// chain onto it.
	/// </summary>
	public static HasGeneratedClassAssertion HasGeneratedClass(
		this IAssertionSource<DriverRunResult> source,
		string className,
		int arity,
		[CallerArgumentExpression(nameof(className))] string? classNameExpression = null
	)
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));

		AppendExpression(
			source,
			".HasGeneratedClass(" + classNameExpression + ", " + arity.ToString(CultureInfo.InvariantCulture) + ")"
		);

		return new HasGeneratedClassAssertion(
			source.Context.Map(result =>
				result is null
					? throw new InvalidOperationException("the test result was null")
					: FindClass(result.Generated(), className, arity, null)
			)
		);
	}

	/// <summary>
	/// Asserts that the generated code contains a class with the given type identity, moving the chain onto it.
	/// </summary>
	public static HasGeneratedClassAssertion HasGeneratedClass(
		this IAssertionSource<DriverRunResult> source,
		TypeReference type,
		[CallerArgumentExpression(nameof(type))] string? typeExpression = null
	)
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		AppendExpression(source, ".HasGeneratedClass(" + typeExpression + ")");

		return new HasGeneratedClassAssertion(
			source.Context.Map(result =>
				result is null
					? throw new InvalidOperationException("the test result was null")
					: FindClass(
						result.Generated(),
						type.Identity.Name,
						type.Identity.GenericArity,
						type.Identity.Namespace
					)
			)
		);
	}

	static CodeQueryResult<ClassDeclarationSyntax> FindClass(
		CodeQuery? query,
		string name,
		int? arity,
		string? @namespace
	)
	{
		if (query is null)
			throw new InvalidOperationException("the query was null");

		var found = arity is null
			? query.TryGetClass(name, out var declaration, @namespace)
			: query.TryGetClass(name, arity.Value, out declaration, @namespace);

		return found
			? new(query, declaration!)
			: throw new InvalidOperationException($"generated code did not contain a class named '{name}'");
	}

	// ---------------------------------------------------------------------------------------------
	// Finding properties and methods
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the type declares a property with the given name and type, moving the chain onto it.
	/// </summary>
	public static HasPropertyOfTypeAssertion HasPropertyOfType<T>(
		this IAssertionSource<CodeQueryResult<T>> source,
		string name,
		TypeReference propertyType,
		[CallerArgumentExpression(nameof(name))] string? nameExpression = null,
		[CallerArgumentExpression(nameof(propertyType))] string? propertyTypeExpression = null
	)
		where T : TypeDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The property name cannot be null or whitespace.", nameof(name));
		if (propertyType is null)
			throw new ArgumentNullException(nameof(propertyType));

		AppendExpression(source, ".HasPropertyOfType(" + nameExpression + ", " + propertyTypeExpression + ")");

		return new HasPropertyOfTypeAssertion(
			source.Context.Map(type =>
			{
				if (type is null)
					throw new InvalidOperationException("the type being queried was null");

				if (type.TryGetProperty(name, out var property, propertyType))
					return new CodeQueryResult<PropertyDeclarationSyntax>(type.Query, property!);

				// If the property was not found, throw an exception with a detailed message.
				throw new InvalidOperationException(
					$"type '{type.Node.Identifier.ValueText}' did not contain a property named '{name}' of type '{propertyType}'"
				);
			})
		);
	}

	/// <summary>
	/// Asserts that the type declares a method with the given name and parameter types, moving the chain onto it.
	/// </summary>
	public static HasMethodOfTypeAssertion HasMethodOfType<T>(
		this IAssertionSource<CodeQueryResult<T>> source,
		string name,
		TypeReference[] parameters,
		[CallerArgumentExpression(nameof(name))] string? nameExpression = null,
		[CallerArgumentExpression(nameof(parameters))] string? parametersExpression = null
	)
		where T : TypeDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The method name cannot be null or whitespace.", nameof(name));

		AppendExpression(source, ".HasMethodOfType(" + nameExpression + ", " + parametersExpression + ")");

		return new HasMethodOfTypeAssertion(
			source.Context.Map(type =>
			{
				if (type is null)
					throw new InvalidOperationException("the type being queried was null");

				if (type.TryGetMethod(name, out var method, parameters))
					return new CodeQueryResult<MethodDeclarationSyntax>(type.Query, method!);

				// If the method was not found, throw an exception with a detailed message.
				throw new InvalidOperationException(
					$"type '{type.Node.Identifier.ValueText}' did not contain a method named '{name}'"
				);
			})
		);
	}

	// ---------------------------------------------------------------------------------------------
	// Finding nested types
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the type declares a nested type (class, struct, interface or record) with the given name,
	/// moving the chain onto it.
	/// </summary>
	public static HasNestedTypeAssertion HasNestedType<T>(
		this IAssertionSource<CodeQueryResult<T>> source,
		string name,
		[CallerArgumentExpression(nameof(name))] string? nameExpression = null
	)
		where T : TypeDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The nested type name cannot be null or whitespace.", nameof(name));

		AppendExpression(source, ".HasNestedType(" + nameExpression + ")");

		return new HasNestedTypeAssertion(
			source.Context.Map(type =>
			{
				if (type is null)
					throw new InvalidOperationException("the type being queried was null");

				if (type.TryGetNestedType(name, out var nested))
					return new CodeQueryResult<TypeDeclarationSyntax>(type.Query, nested!);

				// If the nested type was not found, throw an exception with a detailed message.
				throw new InvalidOperationException(
					$"type '{type.Node.Identifier.ValueText}' did not contain a nested type named '{name}'"
				);
			})
		);
	}

	// ---------------------------------------------------------------------------------------------
	// Accessibility
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the member's effective accessibility matches the given value, keeping the member on the
	/// chain. When the member has no accessibility modifier, the C# default is applied.
	/// </summary>
	public static WithAccessibilityAssertion<T> WithAccessibility<T>(
		this IAssertionSource<CodeQueryResult<T>> source,
		Accessibility accessibility,
		[CallerArgumentExpression(nameof(accessibility))] string? accessibilityExpression = null
	)
		where T : MemberDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));

		AppendExpression(source, ".WithAccessibility(" + accessibilityExpression + ")");

		return new WithAccessibilityAssertion<T>(source.Context, accessibility);
	}

	/// <summary>
	/// Asserts that the property has a getter with the given effective accessibility, keeping the property on
	/// the chain.
	/// </summary>
	public static WithAccessorAccessibilityAssertion WithGetterAccessibility(
		this IAssertionSource<CodeQueryResult<PropertyDeclarationSyntax>> source,
		Accessibility accessibility,
		[CallerArgumentExpression(nameof(accessibility))] string? accessibilityExpression = null
	)
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));

		AppendExpression(source, ".WithGetterAccessibility(" + accessibilityExpression + ")");

		// The WithAccessorAccessibilityAssertion class takes a boolean parameter isGetter to indicate whether the assertion is for a getter or a setter. In this case, we pass true to indicate that we are asserting the accessibility of the getter.
		return new WithAccessorAccessibilityAssertion(source.Context, accessibility, isGetter: true);
	}

	/// <summary>
	/// Asserts that the property has a setter with the given effective accessibility, keeping the property on
	/// the chain.
	/// </summary>
	public static WithAccessorAccessibilityAssertion WithSetterAccessibility(
		this IAssertionSource<CodeQueryResult<PropertyDeclarationSyntax>> source,
		Accessibility accessibility,
		[CallerArgumentExpression(nameof(accessibility))] string? accessibilityExpression = null
	)
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));

		AppendExpression(source, ".WithSetterAccessibility(" + accessibilityExpression + ")");

		return new WithAccessorAccessibilityAssertion(source.Context, accessibility, isGetter: false);
	}

	// ---------------------------------------------------------------------------------------------
	// Base types
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the type declares the given base type in its base list, keeping the type on the chain.
	/// </summary>
	public static WithBaseTypeAssertion<T> WithBaseType<T>(
		this IAssertionSource<CodeQueryResult<T>> source,
		TypeReference baseType,
		[CallerArgumentExpression(nameof(baseType))] string? baseTypeExpression = null
	)
		where T : TypeDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (baseType is null)
			throw new ArgumentNullException(nameof(baseType));

		AppendExpression(source, ".WithBaseType(" + baseTypeExpression + ")");

		return new WithBaseTypeAssertion<T>(source.Context, baseType);
	}

	// ---------------------------------------------------------------------------------------------
	// Generic type parameters
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the type declares a type parameter with the given name, keeping the type on the chain.
	/// </summary>
	public static WithGenericTypeParameterAssertion<T> WithGenericTypeParameter<T>(
		this IAssertionSource<CodeQueryResult<T>> source,
		string typeParameter,
		[CallerArgumentExpression(nameof(typeParameter))] string? typeParameterExpression = null
	)
		where T : TypeDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (string.IsNullOrWhiteSpace(typeParameter))
			throw new ArgumentException("The type parameter name cannot be null or whitespace.", nameof(typeParameter));

		AppendExpression(source, ".WithGenericTypeParameter(" + typeParameterExpression + ")");

		return new WithGenericTypeParameterAssertion<T>(source.Context, typeParameter);
	}

	/// <summary>
	/// Asserts that the type declares type parameters with all of the given names, keeping the type on the
	/// chain.
	/// </summary>
	public static WithGenericTypeParametersAssertion<T> WithGenericTypeParameters<T>(
		this IAssertionSource<CodeQueryResult<T>> source,
		params string[] typeParameters
	)
		where T : TypeDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (typeParameters is null || typeParameters.Length == 0)
			throw new ArgumentException("The type parameter names cannot be null or empty.", nameof(typeParameters));

		AppendExpression(source, ".WithGenericTypeParameters(" + string.Join(", ", typeParameters) + ")");

		return new WithGenericTypeParametersAssertion<T>(source.Context, typeParameters);
	}

	// ---------------------------------------------------------------------------------------------
	// Namespaces
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Asserts that the type is declared within the specified namespace, keeping the type on the chain.
	/// </summary>
	public static IsInNamespaceAssertion<T> IsInNamespace<T>(
		this IAssertionSource<CodeQueryResult<T>> source,
		string namespaceName,
		[CallerArgumentExpression(nameof(namespaceName))] string? namespaceNameExpression = null
	)
		where T : TypeDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));
		if (string.IsNullOrWhiteSpace(namespaceName))
			throw new ArgumentException("The namespace cannot be null or whitespace.", nameof(namespaceName));

		AppendExpression(source, ".IsInNamespace(" + namespaceNameExpression + ")");

		return new IsInNamespaceAssertion<T>(source.Context, namespaceName);
	}

	/// <summary>
	/// Asserts that the type is declared in the global namespace (no enclosing namespace declaration),
	/// keeping the type on the chain.
	/// </summary>
	public static IsInGlobalNamespaceAssertion<T> IsInGlobalNamespace<T>(
		this IAssertionSource<CodeQueryResult<T>> source
	)
		where T : TypeDeclarationSyntax
	{
		if (source is null)
			throw new ArgumentNullException(nameof(source));

		AppendExpression(source, ".IsInGlobalNamespace()");

		return new IsInGlobalNamespaceAssertion<T>(source.Context);
	}
}

/// <summary>
/// Base class for node-chain assertions. The chain value is the matched
/// <see cref="CodeQueryResult{T}"/>, and awaiting the assertion yields that same non-null result.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CodeQueryResultAssertion{T}"/> class.
/// </remarks>
public abstract class CodeQueryResultAssertion<T>(AssertionContext<CodeQueryResult<T>> context)
	: Assertion<CodeQueryResult<T>>(context)
	where T : SyntaxNode
{
	/// <summary>
	/// Awaits the assertion and returns the matched non-null <see cref="CodeQueryResult{T}"/>.
	/// </summary>
	public new TaskAwaiter<CodeQueryResult<T>> GetAwaiter() => ExecuteAndReturnResultAsync().GetAwaiter();

	async Task<CodeQueryResult<T>> ExecuteAndReturnResultAsync()
	{
		var value = await AssertAsync();
		return value!;
	}
}

/// <summary>
/// Asserts that the generated code contains a class, and moves the chain onto it.
/// </summary>
public sealed class HasGeneratedClassAssertion : CodeQueryResultAssertion<ClassDeclarationSyntax>
{
	internal HasGeneratedClassAssertion(AssertionContext<CodeQueryResult<ClassDeclarationSyntax>> context)
		: base(context) { }

	protected override Task<AssertionResult> CheckAsync(
		EvaluationMetadata<CodeQueryResult<ClassDeclarationSyntax>> metadata
	) =>
		Task.FromResult(
			metadata.Exception is null ? AssertionResult.Passed : AssertionResult.Failed(metadata.Exception.Message)
		);

	protected override string GetExpectation() => "generated code to contain the class";
}

/// <summary>
/// Asserts that the type declares a property, and moves the chain onto it.
/// </summary>
public sealed class HasPropertyOfTypeAssertion : CodeQueryResultAssertion<PropertyDeclarationSyntax>
{
	internal HasPropertyOfTypeAssertion(AssertionContext<CodeQueryResult<PropertyDeclarationSyntax>> context)
		: base(context) { }

	protected override Task<AssertionResult> CheckAsync(
		EvaluationMetadata<CodeQueryResult<PropertyDeclarationSyntax>> metadata
	) =>
		Task.FromResult(
			metadata.Exception is null ? AssertionResult.Passed : AssertionResult.Failed(metadata.Exception.Message)
		);

	protected override string GetExpectation() => "type to declare the property";
}

/// <summary>
/// Asserts that the type declares a method, and moves the chain onto it.
/// </summary>
public sealed class HasMethodOfTypeAssertion : CodeQueryResultAssertion<MethodDeclarationSyntax>
{
	internal HasMethodOfTypeAssertion(AssertionContext<CodeQueryResult<MethodDeclarationSyntax>> context)
		: base(context) { }

	protected override Task<AssertionResult> CheckAsync(
		EvaluationMetadata<CodeQueryResult<MethodDeclarationSyntax>> metadata
	) =>
		Task.FromResult(
			metadata.Exception is null ? AssertionResult.Passed : AssertionResult.Failed(metadata.Exception.Message)
		);

	protected override string GetExpectation() => "type to declare the method";
}

/// <summary>
/// Asserts that the type declares a nested type, and moves the chain onto it.
/// </summary>
public sealed class HasNestedTypeAssertion : CodeQueryResultAssertion<TypeDeclarationSyntax>
{
	internal HasNestedTypeAssertion(AssertionContext<CodeQueryResult<TypeDeclarationSyntax>> context)
		: base(context) { }

	protected override Task<AssertionResult> CheckAsync(
		EvaluationMetadata<CodeQueryResult<TypeDeclarationSyntax>> metadata
	) =>
		Task.FromResult(
			metadata.Exception is null ? AssertionResult.Passed : AssertionResult.Failed(metadata.Exception.Message)
		);

	protected override string GetExpectation() => "type to declare the nested type";
}

/// <summary>
/// Asserts that the member's effective accessibility matches the expected value, keeping the member on the
/// chain.
/// </summary>
public sealed class WithAccessibilityAssertion<T> : CodeQueryResultAssertion<T>
	where T : MemberDeclarationSyntax
{
	readonly Accessibility _accessibility;

	internal WithAccessibilityAssertion(AssertionContext<CodeQueryResult<T>> context, Accessibility accessibility)
		: base(context) => _accessibility = accessibility;

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<CodeQueryResult<T>> metadata)
	{
		if (metadata.Exception is not null)
			return Task.FromResult(AssertionResult.Failed(metadata.Exception.Message));

		var member = metadata.Value;
		return Task.FromResult(
			member is not null && member.HasAccessibility(_accessibility)
				? AssertionResult.Passed
				: AssertionResult.Failed($"node did not have the expected accessibility '{_accessibility}'")
		);
	}

	protected override string GetExpectation() => $"to have accessibility '{_accessibility}'";
}

/// <summary>
/// Asserts that the property's getter or setter has the expected effective accessibility, keeping the
/// property on the chain.
/// </summary>
public sealed class WithAccessorAccessibilityAssertion : CodeQueryResultAssertion<PropertyDeclarationSyntax>
{
	readonly Accessibility _accessibility;
	readonly bool _isGetter;

	internal WithAccessorAccessibilityAssertion(
		AssertionContext<CodeQueryResult<PropertyDeclarationSyntax>> context,
		Accessibility accessibility,
		bool isGetter
	)
		: base(context)
	{
		_accessibility = accessibility;
		_isGetter = isGetter;
	}

	string AccessorName => _isGetter ? "getter" : "setter";

	protected override Task<AssertionResult> CheckAsync(
		EvaluationMetadata<CodeQueryResult<PropertyDeclarationSyntax>> metadata
	)
	{
		if (metadata.Exception is not null)
			return Task.FromResult(AssertionResult.Failed(metadata.Exception.Message));

		var property = metadata.Value;
		if (property is null)
			return Task.FromResult(AssertionResult.Failed("property was not found"));

		var matches = _isGetter
			? property.HasGetterAccessibility(_accessibility)
			: property.HasSetterAccessibility(_accessibility);

		return Task.FromResult(
			matches
				? AssertionResult.Passed
				: AssertionResult.Failed(
					$"property '{property.Node.Identifier.ValueText}' did not have a {AccessorName} with accessibility '{_accessibility}'"
				)
		);
	}

	protected override string GetExpectation() => $"to have {AccessorName} accessibility '{_accessibility}'";
}

/// <summary>
/// Asserts that the type declares the given base type, keeping the type on the chain.
/// </summary>
public sealed class WithBaseTypeAssertion<T> : CodeQueryResultAssertion<T>
	where T : TypeDeclarationSyntax
{
	readonly TypeReference _baseType;

	internal WithBaseTypeAssertion(AssertionContext<CodeQueryResult<T>> context, TypeReference baseType)
		: base(context) => _baseType = baseType;

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<CodeQueryResult<T>> metadata)
	{
		if (metadata.Exception is not null)
			return Task.FromResult(AssertionResult.Failed(metadata.Exception.Message));

		var type = metadata.Value;
		return Task.FromResult(
			type is not null && type.HasBaseType(_baseType)
				? AssertionResult.Passed
				: AssertionResult.Failed(
					$"type '{type?.Node.Identifier.ValueText}' did not have base type '{_baseType}'"
				)
		);
	}

	protected override string GetExpectation() => $"to have base type '{_baseType}'";
}

/// <summary>
/// Asserts that the type declares a type parameter with the given name, keeping the type on the chain.
/// </summary>
public sealed class WithGenericTypeParameterAssertion<T> : CodeQueryResultAssertion<T>
	where T : TypeDeclarationSyntax
{
	readonly string _typeParameter;

	internal WithGenericTypeParameterAssertion(AssertionContext<CodeQueryResult<T>> context, string typeParameter)
		: base(context) => _typeParameter = typeParameter;

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<CodeQueryResult<T>> metadata)
	{
		if (metadata.Exception is not null)
			return Task.FromResult(AssertionResult.Failed(metadata.Exception.Message));

		var type = metadata.Value;
		return Task.FromResult(
			type is not null && type.HasGenericTypeParameter(_typeParameter)
				? AssertionResult.Passed
				: AssertionResult.Failed(
					$"type '{type?.Node.Identifier.ValueText}' did not declare type parameter '{_typeParameter}'"
				)
		);
	}

	protected override string GetExpectation() => $"to declare type parameter '{_typeParameter}'";
}

/// <summary>
/// Asserts that the type declares type parameters with all of the given names, keeping the type on the chain.
/// </summary>
public sealed class WithGenericTypeParametersAssertion<T> : CodeQueryResultAssertion<T>
	where T : TypeDeclarationSyntax
{
	readonly string[] _typeParameters;

	internal WithGenericTypeParametersAssertion(AssertionContext<CodeQueryResult<T>> context, string[] typeParameters)
		: base(context) => _typeParameters = typeParameters;

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<CodeQueryResult<T>> metadata)
	{
		if (metadata.Exception is not null)
			return Task.FromResult(AssertionResult.Failed(metadata.Exception.Message));

		var type = metadata.Value;
		return Task.FromResult(
			type is not null && type.HasGenericTypeParameters(_typeParameters)
				? AssertionResult.Passed
				: AssertionResult.Failed(
					$"type '{type?.Node.Identifier.ValueText}' did not declare all type parameters '{string.Join(", ", _typeParameters)}'"
				)
		);
	}

	protected override string GetExpectation() => $"to declare type parameters '{string.Join(", ", _typeParameters)}'";
}

/// <summary>
/// Asserts that the type is declared within the specified namespace, keeping the type on the chain.
/// </summary>
public sealed class IsInNamespaceAssertion<T> : CodeQueryResultAssertion<T>
	where T : TypeDeclarationSyntax
{
	readonly string _namespaceName;

	internal IsInNamespaceAssertion(AssertionContext<CodeQueryResult<T>> context, string namespaceName)
		: base(context) => _namespaceName = namespaceName;

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<CodeQueryResult<T>> metadata)
	{
		if (metadata.Exception is not null)
			return Task.FromResult(AssertionResult.Failed(metadata.Exception.Message));

		var type = metadata.Value;
		return Task.FromResult(
			type is not null && type.Query.IsInNamespace(type.Node, _namespaceName)
				? AssertionResult.Passed
				: AssertionResult.Failed(
					$"type '{type?.Node.Identifier.ValueText}' was not in namespace '{_namespaceName}'"
				)
		);
	}

	protected override string GetExpectation() => $"to be in namespace '{_namespaceName}'";
}

/// <summary>
/// Asserts that the type is declared in the global namespace, keeping the type on the chain.
/// </summary>
public sealed class IsInGlobalNamespaceAssertion<T> : CodeQueryResultAssertion<T>
	where T : TypeDeclarationSyntax
{
	internal IsInGlobalNamespaceAssertion(AssertionContext<CodeQueryResult<T>> context)
		: base(context) { }

	protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<CodeQueryResult<T>> metadata)
	{
		if (metadata.Exception is not null)
			return Task.FromResult(AssertionResult.Failed(metadata.Exception.Message));

		var type = metadata.Value;
		return Task.FromResult(
			type is not null && type.Query.IsInGlobalNamespace(type.Node)
				? AssertionResult.Passed
				: AssertionResult.Failed($"type '{type?.Node.Identifier.ValueText}' was not in the global namespace")
		);
	}

	protected override string GetExpectation() => "to be in the global namespace";
}
