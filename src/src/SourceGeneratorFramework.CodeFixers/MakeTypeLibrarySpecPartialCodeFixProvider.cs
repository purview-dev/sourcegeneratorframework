using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Adds the <c>partial</c> modifier to a <c>[GenerateTypeLibrary]</c> spec so the generated
/// <c>TypeRefMarkers</c> partial can be merged into it (fixes <c>TLB0011</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeTypeLibrarySpecPartialCodeFixProvider))]
public sealed class MakeTypeLibrarySpecPartialCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "MakePartial";

	public override ImmutableArray<string> FixableDiagnosticIds => ["TLB0011"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var node = root.FindNode(diagnostic.Location.SourceSpan);
			if (node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } typeDeclaration)
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Make partial",
					_ => MakePartialAsync(context.Document, typeDeclaration, context.CancellationToken),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static async Task<Document> MakePartialAsync(
		Document document,
		TypeDeclarationSyntax typeDeclaration,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		if (typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
			return document;

		var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword);
		var modifiers = typeDeclaration.Modifiers;

		// Insert after the static modifier so the declaration reads "static partial class", matching
		// the DSL's convention; otherwise insert at the front.
		var staticIndex = -1;
		for (var i = 0; i < modifiers.Count; i++)
		{
			if (modifiers[i].IsKind(SyntaxKind.StaticKeyword))
			{
				staticIndex = i;
				break;
			}
		}

		var updatedModifiers =
			staticIndex >= 0 ? modifiers.Insert(staticIndex + 1, partialToken) : modifiers.Insert(0, partialToken);

		var updated = typeDeclaration.WithModifiers(updatedModifiers);

		return document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, updated));
	}
}
