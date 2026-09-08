using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Shared syntax helpers for the Roslyn component setup code fix providers.
/// </summary>
static class RoslynComponentFixHelpers
{
	public static AttributeSyntax CreateAttribute(string name, string? argumentExpression = null)
	{
		if (argumentExpression is null)
			return SyntaxFactory.Attribute(SyntaxFactory.ParseName(name));

		// The attribute has an argument, so create an attribute argument list with a single argument.
		return SyntaxFactory.Attribute(
			SyntaxFactory.ParseName(name),
			SyntaxFactory.AttributeArgumentList(
				SyntaxFactory.SingletonSeparatedList(
					SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression(argumentExpression))
				)
			)
		);
	}

	/// <summary>
	/// Adds <paramref name="attribute"/> to the front of <paramref name="typeDeclaration"/>'s
	/// attribute lists and inserts any missing <paramref name="requiredNamespaces"/> imports.
	/// </summary>
	public static async Task<Document> AddAttributeAsync(
		Document document,
		TypeDeclarationSyntax typeDeclaration,
		AttributeSyntax attribute,
		ImmutableArray<string> requiredNamespaces,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
		var updatedType = typeDeclaration.WithAttributeLists(typeDeclaration.AttributeLists.Insert(0, attributeList));
		var updatedRoot = root.ReplaceNode(typeDeclaration, updatedType);

		if (requiredNamespaces.IsDefaultOrEmpty)
			return document.WithSyntaxRoot(updatedRoot);

		// Add any missing using directives for the required namespaces.
		return document.WithSyntaxRoot(AddMissingUsings(updatedRoot, requiredNamespaces) ?? updatedRoot);
	}

	static SyntaxNode? AddMissingUsings(SyntaxNode root, ImmutableArray<string> requiredNamespaces)
	{
		var container = FindUsingContainer(root);
		if (container is null)
			return null;

		var existing = container switch
		{
			CompilationUnitSyntax compilationUnit => compilationUnit.Usings,
			BaseNamespaceDeclarationSyntax @namespace => @namespace.Usings,
			_ => default,
		};

		var missing = requiredNamespaces
			.Where(namespaceName => !existing.Any(usingDirective => usingDirective.Name?.ToString() == namespaceName))
			.Select(namespaceName =>
				SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName)).NormalizeWhitespace()
			)
			.ToList();

		if (missing.Count == 0)
			return root;

		// Add the missing using directives to the appropriate container.
		return container switch
		{
			CompilationUnitSyntax compilationUnit => root.ReplaceNode(
				compilationUnit,
				compilationUnit.WithUsings(compilationUnit.Usings.AddRange(missing))
			),
			BaseNamespaceDeclarationSyntax @namespace => root.ReplaceNode(
				@namespace,
				@namespace.WithUsings(@namespace.Usings.AddRange(missing))
			),
			_ => root,
		};
	}

	static SyntaxNode? FindUsingContainer(SyntaxNode root)
	{
		if (root is CompilationUnitSyntax compilationUnit)
		{
			if (compilationUnit.Usings.Any())
				return compilationUnit;

			if (compilationUnit.Members.FirstOrDefault() is FileScopedNamespaceDeclarationSyntax fileScoped)
				return fileScoped;

			// If there are no using directives and no file-scoped namespace, return the compilation unit itself to add the usings at the top.
			return compilationUnit;
		}

		return null;
	}

	/// <summary>
	/// Replaces the type's existing accessibility modifier with <c>public</c>, or inserts one when
	/// the type has none (types default to internal).
	/// </summary>
	public static TypeDeclarationSyntax MakePublic(TypeDeclarationSyntax typeDeclaration)
	{
		var modifiers = typeDeclaration.Modifiers;
		var accessibilityIndex = -1;

		for (var i = 0; i < modifiers.Count; i++)
		{
			if (IsAccessibilityModifier(modifiers[i]))
			{
				accessibilityIndex = i;
				break;
			}
		}

		var publicToken = SyntaxFactory.Token(SyntaxKind.PublicKeyword);

		if (accessibilityIndex >= 0)
		{
			var updated = modifiers.Replace(modifiers[accessibilityIndex], publicToken);
			return typeDeclaration.WithModifiers(updated);
		}

		var inserted = modifiers.Insert(0, publicToken);
		return typeDeclaration.WithModifiers(inserted);
	}

	static bool IsAccessibilityModifier(SyntaxToken token) =>
		token.Kind()
			is SyntaxKind.PublicKeyword
				or SyntaxKind.InternalKeyword
				or SyntaxKind.PrivateKeyword
				or SyntaxKind.ProtectedKeyword
				or SyntaxKind.FileKeyword;
}
