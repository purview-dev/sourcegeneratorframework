using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Resolves the effective accessibility of a member from its syntax, applying the C# defaults when no
/// accessibility modifier is written. Used by the query and assertion layers so that <c>void M()</c> inside a
/// class is correctly reported as <see cref="Accessibility.Private"/>, an unmodified nested type as
/// <see cref="Accessibility.Private"/>, an unmodified top-level type as <see cref="Accessibility.Internal"/>,
/// and interface and enum members as <see cref="Accessibility.Public"/>.
/// </summary>
public static class AccessibilityFacts
{
	/// <summary>
	/// Gets the effective accessibility of a member declaration, resolving C# defaults when no accessibility
	/// modifier is present.
	/// </summary>
	public static Accessibility GetEffectiveAccessibility(MemberDeclarationSyntax member)
	{
		if (member is null)
			throw new ArgumentNullException(nameof(member));

		var declared = GetDeclaredAccessibility(member.Modifiers);
		if (declared != Accessibility.NotApplicable)
			return declared;

		// Enum members are always public.
		if (member is EnumMemberDeclarationSyntax)
			return Accessibility.Public;

		// Interface members (including nested types) default to public.
		if (member.Parent is InterfaceDeclarationSyntax)
			return Accessibility.Public;

		// Nested types default to private; top-level types default to internal.
		if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
			return member.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax
				? Accessibility.Internal
				: Accessibility.Private;

		// Members of a class or struct default to private.
		return Accessibility.Private;
	}

	/// <summary>
	/// Gets the effective accessibility of a property accessor, inheriting the containing property's
	/// accessibility when the accessor carries no modifier of its own. This makes <c>{ get; set; }</c> on a
	/// public property report both accessors as <see cref="Accessibility.Public"/>, while
	/// <c>{ get; private set; }</c> reports the setter as <see cref="Accessibility.Private"/>.
	/// </summary>
	public static Accessibility GetEffectiveAccessibility(
		AccessorDeclarationSyntax accessor,
		BasePropertyDeclarationSyntax property
	)
	{
		if (accessor is null)
			throw new ArgumentNullException(nameof(accessor));
		if (property is null)
			throw new ArgumentNullException(nameof(property));

		var declared = GetDeclaredAccessibility(accessor.Modifiers);
		if (declared != Accessibility.NotApplicable)
			return declared;

		// If the accessor has no modifier, it inherits the property's accessibility.
		return GetEffectiveAccessibility(property);
	}

	static Accessibility GetDeclaredAccessibility(SyntaxTokenList modifiers)
	{
		var hasPublic = false;
		var hasPrivate = false;
		var hasProtected = false;
		var hasInternal = false;

		foreach (var modifier in modifiers)
		{
			var kind = modifier.Kind();
			if (kind == SyntaxKind.PublicKeyword)
				hasPublic = true;
			else if (kind == SyntaxKind.PrivateKeyword)
				hasPrivate = true;
			else if (kind == SyntaxKind.ProtectedKeyword)
				hasProtected = true;
			else if (kind == SyntaxKind.InternalKeyword)
				hasInternal = true;
		}

		if (hasPublic)
			return Accessibility.Public;

		// `private protected` and `protected internal` are each a single combined token pair.
		if (hasPrivate && hasProtected)
			return Accessibility.ProtectedAndInternal;
		if (hasProtected && hasInternal)
			return Accessibility.ProtectedOrInternal;

		if (hasPrivate)
			return Accessibility.Private;
		if (hasProtected)
			return Accessibility.Protected;
		if (hasInternal)
			return Accessibility.Internal;

		// No accessibility modifier was found.
		return Accessibility.NotApplicable;
	}
}
