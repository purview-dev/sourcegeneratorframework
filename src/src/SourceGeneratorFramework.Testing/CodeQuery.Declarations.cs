using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Testing;

public sealed partial class CodeQuery
{
	// ---------------------------------------------------------------------------------------------
	// Methods
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets a method declaration by name, optionally matching its parameter types.
	/// </summary>
	/// <exception cref="SyntaxNotFoundException">No method matched.</exception>
	public CodeQueryResult<MethodDeclarationSyntax> GetMethod(string name, params TypeReference[]? parameters) =>
		TryGetMethod(name, out var method, parameters)
			? new(this, method!)
			: throw new SyntaxNotFoundException(
				$"No method named '{name}' was found in the {ScopeDescription()}{(parameters is { Length: > 0 } ? " with the specified parameters" : "")}."
			);

	/// <summary>
	/// Determines whether a method declaration with the given name, optionally matching parameter types, exists.
	/// </summary>
	public bool HasMethod(string name, params TypeReference[]? parameters) => TryGetMethod(name, out _, parameters);

	/// <summary>
	/// Attempts to get a method declaration by name, optionally matching its parameter types.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public bool TryGetMethod(string name, out MethodDeclarationSyntax? method, params TypeReference[]? parameters)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The method name cannot be null or whitespace.", nameof(name));

		var expected = parameters ?? [];
		return TryFind(
			name,
			static candidate => candidate.Identifier.ValueText,
			expected.Length == 0 ? null : candidate => HasParameters(candidate, expected),
			out method
		);
	}

	// ---------------------------------------------------------------------------------------------
	// Type declarations
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets a type declaration (class, struct, interface, record, enum or delegate) by name.
	/// </summary>
	public CodeQueryResult<MemberDeclarationSyntax> GetTypeDeclaration(string name, string? @namespace = null) =>
		GetTypeDeclaration(name, null, @namespace);

	/// <summary>
	/// Gets a type declaration (class, struct, interface, record, enum or delegate) by name and generic arity.
	/// </summary>
	public CodeQueryResult<MemberDeclarationSyntax> GetTypeDeclaration(
		string name,
		int arity,
		string? @namespace = null
	) => GetTypeDeclaration(name, (int?)arity, @namespace);

	CodeQueryResult<MemberDeclarationSyntax> GetTypeDeclaration(string name, int? arity, string? @namespace) =>
		Get<MemberDeclarationSyntax>(node =>
			IsTypeDeclarationMatch(node, name, arity) && NamespaceMatches(node, @namespace)
		);

	/// <summary>
	/// Gets a type declaration (class, struct, interface, record, enum or delegate) by type identity.
	/// </summary>
	public CodeQueryResult<MemberDeclarationSyntax> GetTypeDeclaration(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return GetTypeDeclaration(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Determines whether a type declaration with the given name exists.
	/// </summary>
	public bool HasTypeDeclaration(string name, string? @namespace = null) =>
		HasTypeDeclaration(name, null, @namespace);

	/// <summary>
	/// Determines whether a type declaration with the given name and generic arity exists.
	/// </summary>
	public bool HasTypeDeclaration(string name, int arity, string? @namespace = null) =>
		HasTypeDeclaration(name, (int?)arity, @namespace);

	bool HasTypeDeclaration(string name, int? arity, string? @namespace) =>
		Has<MemberDeclarationSyntax>(node =>
			IsTypeDeclarationMatch(node, name, arity) && NamespaceMatches(node, @namespace)
		);

	/// <summary>
	/// Determines whether a type declaration with the given type identity exists.
	/// </summary>
	public bool HasTypeDeclaration(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return HasTypeDeclaration(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Gets a class declaration by name, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<ClassDeclarationSyntax> GetClass(string name, string? @namespace = null) =>
		FindByName<ClassDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Gets a class declaration by name and generic arity, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<ClassDeclarationSyntax> GetClass(string name, int arity, string? @namespace = null) =>
		FindByName<ClassDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Gets a class declaration by type identity, within the namespace of its identity.
	/// </summary>
	public CodeQueryResult<ClassDeclarationSyntax> GetClass(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		//
		return GetClass(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Determines whether a class declaration with the given name exists, optionally within a namespace.
	/// </summary>
	public bool HasClass(string name, string? @namespace = null) => HasByName<ClassDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Determines whether a class declaration with the given name and generic arity exists, optionally within
	/// a namespace.
	/// </summary>
	public bool HasClass(string name, int arity, string? @namespace = null) =>
		HasByName<ClassDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Determines whether a class declaration with the given type identity exists.
	/// </summary>
	public bool HasClass(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return HasClass(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Attempts to get a class declaration by name, optionally within a namespace.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public bool TryGetClass(string name, out ClassDeclarationSyntax? declaration, string? @namespace = null) =>
		TryFindByName(name, out declaration, @namespace);

	/// <summary>
	/// Attempts to get a class declaration by name and generic arity, optionally within a namespace.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public bool TryGetClass(
		string name,
		int arity,
		out ClassDeclarationSyntax? declaration,
		string? @namespace = null
	) => TryFindByName(name, out declaration, @namespace, null, arity);

	/// <summary>
	/// Attempts to get a class declaration by type identity.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1021:Avoid out parameters")]
	public bool TryGetClass(TypeReference type, out ClassDeclarationSyntax? declaration)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return TryGetClass(type.Identity.Name, type.Identity.GenericArity, out declaration, type.Identity.Namespace);
	}

	/// <summary>
	/// Gets a struct declaration by name, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<StructDeclarationSyntax> GetStruct(string name, string? @namespace = null) =>
		FindByName<StructDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Gets a struct declaration by name and generic arity, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<StructDeclarationSyntax> GetStruct(string name, int arity, string? @namespace = null) =>
		FindByName<StructDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Gets a struct declaration by type identity, within the namespace of its identity.
	/// </summary>
	public CodeQueryResult<StructDeclarationSyntax> GetStruct(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return GetStruct(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Determines whether a struct declaration with the given name exists, optionally within a namespace.
	/// </summary>
	public bool HasStruct(string name, string? @namespace = null) =>
		HasByName<StructDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Determines whether a struct declaration with the given name and generic arity exists, optionally within
	/// a namespace.
	/// </summary>
	public bool HasStruct(string name, int arity, string? @namespace = null) =>
		HasByName<StructDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Determines whether a struct declaration with the given type identity exists.
	/// </summary>
	public bool HasStruct(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return HasStruct(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Gets an interface declaration by name, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<InterfaceDeclarationSyntax> GetInterface(string name, string? @namespace = null) =>
		FindByName<InterfaceDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Gets an interface declaration by name and generic arity, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<InterfaceDeclarationSyntax> GetInterface(
		string name,
		int arity,
		string? @namespace = null
	) => FindByName<InterfaceDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Gets an interface declaration by type identity, within the namespace of its identity.
	/// </summary>
	public CodeQueryResult<InterfaceDeclarationSyntax> GetInterface(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return GetInterface(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Determines whether an interface declaration with the given name exists, optionally within a namespace.
	/// </summary>
	public bool HasInterface(string name, string? @namespace = null) =>
		HasByName<InterfaceDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Determines whether an interface declaration with the given name and generic arity exists, optionally
	/// within a namespace.
	/// </summary>
	public bool HasInterface(string name, int arity, string? @namespace = null) =>
		HasByName<InterfaceDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Determines whether an interface declaration with the given type identity exists.
	/// </summary>
	public bool HasInterface(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return HasInterface(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Gets an enum declaration by name, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<EnumDeclarationSyntax> GetEnum(string name, string? @namespace = null) =>
		FindByName<EnumDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Gets an enum declaration by name and generic arity, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<EnumDeclarationSyntax> GetEnum(string name, int arity, string? @namespace = null) =>
		FindByName<EnumDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Gets an enum declaration by type identity, within the namespace of its identity.
	/// </summary>
	public CodeQueryResult<EnumDeclarationSyntax> GetEnum(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return GetEnum(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Determines whether an enum declaration with the given name exists, optionally within a namespace.
	/// </summary>
	public bool HasEnum(string name, string? @namespace = null) => HasByName<EnumDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Determines whether an enum declaration with the given name and generic arity exists, optionally within
	/// a namespace.
	/// </summary>
	public bool HasEnum(string name, int arity, string? @namespace = null) =>
		HasByName<EnumDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Determines whether an enum declaration with the given type identity exists.
	/// </summary>
	public bool HasEnum(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		//
		return HasEnum(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Gets a delegate declaration by name, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<DelegateDeclarationSyntax> GetDelegate(string name, string? @namespace = null) =>
		FindByName<DelegateDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Gets a delegate declaration by name and generic arity, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<DelegateDeclarationSyntax> GetDelegate(string name, int arity, string? @namespace = null) =>
		FindByName<DelegateDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Gets a delegate declaration by type identity, within the namespace of its identity.
	/// </summary>
	public CodeQueryResult<DelegateDeclarationSyntax> GetDelegate(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return GetDelegate(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Determines whether a delegate declaration with the given name exists, optionally within a namespace.
	/// </summary>
	public bool HasDelegate(string name, string? @namespace = null) =>
		HasByName<DelegateDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Determines whether a delegate declaration with the given name and generic arity exists, optionally
	/// within a namespace.
	/// </summary>
	public bool HasDelegate(string name, int arity, string? @namespace = null) =>
		HasByName<DelegateDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Determines whether a delegate declaration with the given type identity exists.
	/// </summary>
	public bool HasDelegate(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return HasDelegate(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Gets a record declaration by name, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<RecordDeclarationSyntax> GetRecord(string name, string? @namespace = null) =>
		FindByName<RecordDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Gets a record declaration by name and generic arity, optionally within a namespace.
	/// </summary>
	public CodeQueryResult<RecordDeclarationSyntax> GetRecord(string name, int arity, string? @namespace = null) =>
		FindByName<RecordDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Gets a record declaration by type identity, within the namespace of its identity.
	/// </summary>
	public CodeQueryResult<RecordDeclarationSyntax> GetRecord(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return GetRecord(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	/// <summary>
	/// Determines whether a record declaration with the given name exists, optionally within a namespace.
	/// </summary>
	public bool HasRecord(string name, string? @namespace = null) =>
		HasByName<RecordDeclarationSyntax>(name, @namespace);

	/// <summary>
	/// Determines whether a record declaration with the given name and generic arity exists, optionally within
	/// a namespace.
	/// </summary>
	public bool HasRecord(string name, int arity, string? @namespace = null) =>
		HasByName<RecordDeclarationSyntax>(name, @namespace, null, arity);

	/// <summary>
	/// Determines whether a record declaration with the given type identity exists.
	/// </summary>
	public bool HasRecord(TypeReference type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// Use the namespace and arity from the type identity to find the declaration.
		return HasRecord(type.Identity.Name, type.Identity.GenericArity, type.Identity.Namespace);
	}

	// ---------------------------------------------------------------------------------------------
	// Members
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets a property declaration by name.
	/// </summary>
	public CodeQueryResult<PropertyDeclarationSyntax> GetProperty(string name) =>
		TryGetProperty(name, out var property)
			? new(this, property!)
			: throw new SyntaxNotFoundException($"No property named '{name}' was found in the {ScopeDescription()}.");

	/// <summary>
	/// Determines whether a property declaration with the given name exists.
	/// </summary>
	public bool HasProperty(string name) => TryGetProperty(name, out _);

	/// <summary>
	/// Attempts to get a property declaration by name.
	/// </summary>
	public bool TryGetProperty(string name, out PropertyDeclarationSyntax? property) =>
		TryFindByName(name, out property);

	/// <summary>
	/// Gets a field declaration by name.
	/// </summary>
	/// <remarks>
	/// Finds a <see cref="VariableDeclaratorSyntax"/> by identifier and returns its declaring field.
	/// </remarks>
	public CodeQueryResult<FieldDeclarationSyntax> GetField(string name) =>
		TryGetField(name, out var field)
			? new(this, field!)
			: throw new SyntaxNotFoundException($"No field named '{name}' was found in the {ScopeDescription()}.");

	/// <summary>
	/// Determines whether a field declaration with the given name exists.
	/// </summary>
	public bool HasField(string name) => TryGetField(name, out _);

	/// <summary>
	/// Attempts to get a field declaration by name.
	/// </summary>
	public bool TryGetField(string name, out FieldDeclarationSyntax? field)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The field name cannot be null or whitespace.", nameof(name));

		foreach (var tree in Trees)
		{
			foreach (var declarator in RootOf(tree).DescendantNodes().OfType<VariableDeclaratorSyntax>())
			{
				if (declarator.Identifier.ValueText != name)
					continue;

				if (declarator.Parent is not VariableDeclarationSyntax { Parent: FieldDeclarationSyntax candidate })
					continue;

				field = candidate;
				return true;
			}
		}

		field = null;
		return false;
	}

	/// <summary>
	/// Gets a constructor declaration by the name of its containing type.
	/// </summary>
	public CodeQueryResult<ConstructorDeclarationSyntax> GetConstructor(string containingTypeName) =>
		FindByName<ConstructorDeclarationSyntax>(containingTypeName);

	/// <summary>
	/// Determines whether a constructor declaration for the given containing type exists.
	/// </summary>
	public bool HasConstructor(string containingTypeName) =>
		HasByName<ConstructorDeclarationSyntax>(containingTypeName);

	/// <summary>
	/// Gets a namespace declaration (block or file-scoped) by its dotted name.
	/// </summary>
	public CodeQueryResult<BaseNamespaceDeclarationSyntax> GetNamespace(string name) =>
		FindByName<BaseNamespaceDeclarationSyntax>(
			name,
			null,
			namespaceDeclaration => namespaceDeclaration.Name.ToString()
		);

	/// <summary>
	/// Determines whether a namespace declaration with the given dotted name exists.
	/// </summary>
	public bool HasNamespace(string name) =>
		HasByName<BaseNamespaceDeclarationSyntax>(
			name,
			null,
			namespaceDeclaration => namespaceDeclaration.Name.ToString()
		);

	// ---------------------------------------------------------------------------------------------
	// Shared
	// ---------------------------------------------------------------------------------------------

	static bool IsTypeDeclarationMatch(MemberDeclarationSyntax node, string name) =>
		node switch
		{
			ClassDeclarationSyntax @class => @class.Identifier.ValueText == name,
			StructDeclarationSyntax @struct => @struct.Identifier.ValueText == name,
			InterfaceDeclarationSyntax @interface => @interface.Identifier.ValueText == name,
			EnumDeclarationSyntax @enum => @enum.Identifier.ValueText == name,
			DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText == name,
			RecordDeclarationSyntax record => record.Identifier.ValueText == name,
			_ => false,
		};

	static bool IsTypeDeclarationMatch(MemberDeclarationSyntax node, string name, int? arity) =>
		IsTypeDeclarationMatch(node, name) && DeclaredArityMatches(node, arity);

	CodeQueryResult<T> FindByName<T>(
		string name,
		string? @namespace = null,
		Func<T, string>? getName = null,
		int? arity = null
	)
		where T : SyntaxNode =>
		TryFindByName(name, out var node, @namespace, getName, arity)
			? new(this, node!)
			: throw new SyntaxNotFoundException(
				$"No {typeof(T).Name} named '{name}' was found in the {ScopeDescription()}{(string.IsNullOrEmpty(@namespace) ? "" : $" within namespace '{@namespace}'")}."
			);

	bool HasByName<T>(string name, string? @namespace = null, Func<T, string>? getName = null, int? arity = null)
		where T : SyntaxNode => TryFindByName(name, out _, @namespace, getName, arity);

	bool TryFindByName<T>(
		string name,
		out T? node,
		string? @namespace = null,
		Func<T, string>? getName = null,
		int? arity = null
	)
		where T : SyntaxNode
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The name cannot be null or whitespace.", nameof(name));

		// If a namespace is provided, we need to check that the node's declared namespace matches.
		return TryFind(
			name,
			getName is null ? static candidate => GetIdentifier(candidate) : getName,
			BuildFilter<T>(@namespace, arity),
			out node
		);
	}

	static Func<T, bool>? BuildFilter<T>(string? @namespace, int? arity)
		where T : SyntaxNode
	{
		Func<T, bool>? namespaceFilter = null;
		if (!string.IsNullOrEmpty(@namespace))
			namespaceFilter = candidate => NamespaceMatches(candidate, @namespace);

		Func<T, bool>? arityFilter = null;
		if (arity is not null)
			arityFilter = candidate => DeclaredArityMatches(candidate, arity);

		if (namespaceFilter is not null && arityFilter is not null)
			return candidate => namespaceFilter(candidate) && arityFilter(candidate);

		// If only one filter is provided, return it; otherwise, return null (no filter).
		return namespaceFilter ?? arityFilter;
	}

	static bool DeclaredArityMatches(SyntaxNode node, int? arity) =>
		arity is null
		|| (TypeSyntaxFacts.TryGetDeclarationName(node, out _, out var declaredArity) && declaredArity == arity);

	static bool NamespaceMatches(SyntaxNode node, string? @namespace) =>
		string.IsNullOrEmpty(@namespace)
		|| string.Equals(TypeSyntaxFacts.GetDeclaredNamespace(node), @namespace, StringComparison.Ordinal);

	bool TryFind<T>(string name, Func<T, string> getName, Func<T, bool>? additional, out T? node)
		where T : SyntaxNode
	{
		foreach (var tree in Trees)
		{
			foreach (var candidate in RootOf(tree).DescendantNodes().OfType<T>())
			{
				if (getName(candidate) != name)
					continue;

				if (additional is not null && !additional(candidate))
					continue;

				node = candidate;
				return true;
			}
		}

		node = null;
		return false;
	}

	static string GetIdentifier<T>(T node)
		where T : SyntaxNode =>
		node switch
		{
			MethodDeclarationSyntax method => method.Identifier.ValueText,
			ClassDeclarationSyntax @class => @class.Identifier.ValueText,
			StructDeclarationSyntax @struct => @struct.Identifier.ValueText,
			InterfaceDeclarationSyntax @interface => @interface.Identifier.ValueText,
			EnumDeclarationSyntax @enum => @enum.Identifier.ValueText,
			DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
			RecordDeclarationSyntax record => record.Identifier.ValueText,
			PropertyDeclarationSyntax property => property.Identifier.ValueText,
			ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
			_ => string.Empty,
		};
}
