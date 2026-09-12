using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Rewrites <c>ctx.SemanticModel.GetDeclaredSymbol(ctx.TargetNode, ct)</c> inside a
/// <c>ForAttributeWithMetadataName</c> transform to the already-resolved <c>ctx.TargetSymbol</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferTargetSymbolCodeFixProvider))]
public sealed class PreferTargetSymbolCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "UseTargetSymbol";

	public override ImmutableArray<string> FixableDiagnosticIds => ["PSGFR31"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation)
				continue;

			if (!TryResolveContextIdentifier(invocation, out var contextIdentifier))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Use TargetSymbol",
					_ => UseTargetSymbolAsync(context.Document, root, invocation, contextIdentifier),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static Task<Document> UseTargetSymbolAsync(
		Document document,
		SyntaxNode root,
		InvocationExpressionSyntax invocation,
		IdentifierNameSyntax contextIdentifier
	)
	{
		var replacement = SyntaxFactory
			.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				contextIdentifier.WithoutTrivia(),
				SyntaxFactory.IdentifierName("TargetSymbol")
			)
			.WithTriviaFrom(invocation);

		var newRoot = root.ReplaceNode(invocation, replacement);
		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}

	static bool TryResolveContextIdentifier(
		InvocationExpressionSyntax invocation,
		out IdentifierNameSyntax contextIdentifier
	)
	{
		contextIdentifier = null!;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		if (memberAccess.Name.Identifier.Text != "GetDeclaredSymbol")
			return false;

		SyntaxNode? targetNodeExpression = null;
		if (
			memberAccess.Expression is MemberAccessExpressionSyntax receiver
			&& receiver.Name.Identifier.Text == "SemanticModel"
		)
		{
			targetNodeExpression = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
		}

		if (targetNodeExpression is not MemberAccessExpressionSyntax targetNodeAccess)
			return false;

		if (targetNodeAccess.Expression is not IdentifierNameSyntax identifier)
			return false;

		contextIdentifier = identifier;
		return true;
	}
}
