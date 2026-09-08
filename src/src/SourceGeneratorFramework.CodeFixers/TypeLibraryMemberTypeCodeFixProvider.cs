using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Changes a type library member's type to a value object the DSL understands: a marker must be
/// <c>TypeIdentity</c> and a composed value member <c>TypeReference</c> (fixes <c>TLB0002</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TypeLibraryMemberTypeCodeFixProvider))]
public sealed class TypeLibraryMemberTypeCodeFixProvider : CodeFixProvider
{
	internal const string TypeIdentityEquivalenceKey = "TypeIdentity";
	internal const string TypeReferenceEquivalenceKey = "TypeReference";

	public override ImmutableArray<string> FixableDiagnosticIds => ["TLB0002"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var node = root.FindNode(diagnostic.Location.SourceSpan);
			if (!TryGetTypeSyntax(node, out var typeSyntax))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Use TypeIdentity",
					_ =>
						ReplaceTypeAsync(
							context.Document,
							root,
							typeSyntax,
							"global::Purview.SourceGeneratorFramework.TypeIdentity"
						),
					TypeIdentityEquivalenceKey
				),
				diagnostic
			);

			context.RegisterCodeFix(
				CodeAction.Create(
					"Use TypeReference",
					_ =>
						ReplaceTypeAsync(
							context.Document,
							root,
							typeSyntax,
							"global::Purview.SourceGeneratorFramework.TypeReference"
						),
					TypeReferenceEquivalenceKey
				),
				diagnostic
			);
		}
	}

	static bool TryGetTypeSyntax(SyntaxNode node, out TypeSyntax typeSyntax)
	{
		typeSyntax = null!;

		TypeSyntax? candidate = null;
		if (node is TypeSyntax typeSyntaxNode)
		{
			candidate = typeSyntaxNode;
		}
		else if (node.FirstAncestorOrSelf<VariableDeclarationSyntax>() is { Type: { } declarationType })
		{
			candidate = declarationType;
		}

		if (candidate is null)
			return false;

		typeSyntax = candidate;
		return true;
	}

	static Task<Document> ReplaceTypeAsync(
		Document document,
		SyntaxNode root,
		TypeSyntax typeSyntax,
		string newTypeName
	)
	{
		var newType = SyntaxFactory
			.ParseTypeName(newTypeName)
			.WithLeadingTrivia(typeSyntax.GetLeadingTrivia())
			.WithTrailingTrivia(typeSyntax.GetTrailingTrivia());

		var newRoot = root.ReplaceNode(typeSyntax, newType);
		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}
}
