using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Fixes the accessibility of a type library member so it matches the DSL: plain
/// <c>TypeIdentity</c> markers must be <c>private</c>, while <c>TypeReference</c> value members (and
/// initialized <c>TypeIdentity</c> members) must be <c>internal</c> (fixes <c>TLB0008</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TypeLibraryMemberAccessibilityCodeFixProvider))]
public sealed class TypeLibraryMemberAccessibilityCodeFixProvider : CodeFixProvider
{
	internal const string MakePrivateEquivalenceKey = "MakePrivate";
	internal const string MakeInternalEquivalenceKey = "MakeInternal";

	public override ImmutableArray<string> FixableDiagnosticIds => ["TLB0008"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var node = root.FindNode(diagnostic.Location.SourceSpan);
			if (node.FirstAncestorOrSelf<FieldDeclarationSyntax>() is not { } fieldDeclaration)
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Make private",
					_ =>
						SetAccessibilityAsync(
							context.Document,
							fieldDeclaration,
							SyntaxKind.PrivateKeyword,
							context.CancellationToken
						),
					MakePrivateEquivalenceKey
				),
				diagnostic
			);

			context.RegisterCodeFix(
				CodeAction.Create(
					"Make internal",
					_ =>
						SetAccessibilityAsync(
							context.Document,
							fieldDeclaration,
							SyntaxKind.InternalKeyword,
							context.CancellationToken
						),
					MakeInternalEquivalenceKey
				),
				diagnostic
			);
		}
	}

	static async Task<Document> SetAccessibilityAsync(
		Document document,
		FieldDeclarationSyntax fieldDeclaration,
		SyntaxKind accessibilityKind,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var updated = WithAccessibility(fieldDeclaration, accessibilityKind);

		return document.WithSyntaxRoot(root.ReplaceNode(fieldDeclaration, updated));
	}

	static FieldDeclarationSyntax WithAccessibility(
		FieldDeclarationSyntax fieldDeclaration,
		SyntaxKind accessibilityKind
	)
	{
		var modifiers = fieldDeclaration.Modifiers;
		var accessibilityIndex = -1;

		for (var i = 0; i < modifiers.Count; i++)
		{
			if (IsAccessibilityModifier(modifiers[i]))
			{
				accessibilityIndex = i;
				break;
			}
		}

		var accessibilityToken = SyntaxFactory.Token(accessibilityKind);

		if (accessibilityIndex >= 0)
		{
			var updated = modifiers.Replace(modifiers[accessibilityIndex], accessibilityToken);
			return fieldDeclaration.WithModifiers(updated);
		}

		var inserted = modifiers.Insert(0, accessibilityToken);
		return fieldDeclaration.WithModifiers(inserted);
	}

	static bool IsAccessibilityModifier(SyntaxToken token) =>
		token.Kind()
			is SyntaxKind.PublicKeyword
				or SyntaxKind.InternalKeyword
				or SyntaxKind.PrivateKeyword
				or SyntaxKind.ProtectedKeyword
				or SyntaxKind.FileKeyword;
}
