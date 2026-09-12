using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AttributeDataModelSymbolPropertyCodeFixProvider))]
public sealed class AttributeDataModelSymbolPropertyCodeFixProvider : CodeFixProvider
{
	internal const string TypeIdentityEquivalenceKey = "TypeIdentity";
	internal const string StringEquivalenceKey = "string";

	public override ImmutableArray<string> FixableDiagnosticIds =>
		[AttributeDataModelDiagnosticRules.SymbolPropertyNotCacheable.Id];

	public override FixAllProvider GetFixAllProvider() => new AttributeDataModelFixAllProvider();

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var node = root.FindNode(diagnostic.Location.SourceSpan);
			if (!TryGetTypeSyntax(node, out var typeSyntax, out var isNullable))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Use TypeIdentity",
					_ =>
						ReplaceTypeAsync(
							context.Document,
							root,
							typeSyntax,
							isNullable,
							"global::Purview.SourceGeneratorFramework.TypeIdentity"
						),
					TypeIdentityEquivalenceKey
				),
				diagnostic
			);

			context.RegisterCodeFix(
				CodeAction.Create(
					"Use string",
					_ => ReplaceTypeAsync(context.Document, root, typeSyntax, isNullable, "string"),
					StringEquivalenceKey
				),
				diagnostic
			);
		}
	}

	internal static bool TryGetTypeSyntax(SyntaxNode node, out TypeSyntax typeSyntax, out bool isNullable)
	{
		isNullable = false;
		typeSyntax = null!;

		TypeSyntax? candidate = null;
		if (node is TypeSyntax typeSyntaxNode)
		{
			candidate = typeSyntaxNode;
		}
		else if (node is ParameterSyntax parameter)
		{
			candidate = parameter.Type;
		}
		else if (node.AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault() is { } ancestorParameter)
		{
			candidate = ancestorParameter.Type;
		}
		else if (node is PropertyDeclarationSyntax property)
		{
			candidate = property.Type;
		}
		else if (node.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().FirstOrDefault() is { } ancestorProperty)
		{
			candidate = ancestorProperty.Type;
		}
		else if (node is VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type: { } declarationType } })
		{
			candidate = declarationType;
		}
		else if (
			node.AncestorsAndSelf().OfType<VariableDeclarationSyntax>().FirstOrDefault() is
			{ Type: { } ancestorDeclarationType }
		)
		{
			candidate = ancestorDeclarationType;
		}

		if (candidate is null)
			return false;

		typeSyntax = candidate;
		isNullable = candidate is NullableTypeSyntax;
		return true;
	}

	internal static Task<Document> ReplaceTypeAsync(
		Document document,
		SyntaxNode root,
		TypeSyntax typeSyntax,
		bool isNullable,
		string newTypeName
	)
	{
		var newType = SyntaxFactory
			.ParseTypeName(newTypeName + (isNullable ? "?" : ""))
			.WithTriviaFrom(typeSyntax)
			.WithLeadingTrivia(typeSyntax.GetLeadingTrivia())
			.WithTrailingTrivia(typeSyntax.GetTrailingTrivia());

		var newRoot = root.ReplaceNode(typeSyntax, newType);
		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}

	internal sealed class AttributeDataModelFixAllProvider : DocumentBasedFixAllProvider
	{
		protected override async Task<Document?> FixAllAsync(
			FixAllContext fixAllContext,
			Document document,
			ImmutableArray<Diagnostic> diagnostics
		)
		{
			if (fixAllContext is null)
				throw new ArgumentNullException(nameof(fixAllContext));
			if (document is null)
				throw new ArgumentNullException(nameof(document));

			var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
			if (root is null)
				return null;

			var isTypeIdentity = fixAllContext.CodeActionEquivalenceKey == TypeIdentityEquivalenceKey;
			var newTypeName = isTypeIdentity ? "global::Purview.SourceGeneratorFramework.TypeIdentity" : "string";

			List<(SyntaxNode oldNode, SyntaxNode newNode)> replacements = [];
			foreach (var diagnostic in diagnostics)
			{
				var node = root.FindNode(diagnostic.Location.SourceSpan);
				if (!TryGetTypeSyntax(node, out var typeSyntax, out var isNullable))
					continue;

				var newType = SyntaxFactory
					.ParseTypeName(newTypeName + (isNullable ? "?" : ""))
					.WithTriviaFrom(typeSyntax)
					.WithLeadingTrivia(typeSyntax.GetLeadingTrivia())
					.WithTrailingTrivia(typeSyntax.GetTrailingTrivia());

				replacements.Add((typeSyntax, newType));
			}

			if (replacements.Count == 0)
				return document;

			var newRoot = root.ReplaceNodes(
				replacements.Select(static r => r.oldNode),
				(original, _) => replacements.First(r => r.oldNode == original).newNode
			);

			return document.WithSyntaxRoot(newRoot);
		}
	}
}
