using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Adds the <c>static</c> modifier to a non-capturing lambda passed to an incremental generator pipeline
/// method, eliminating per-item closure allocations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferStaticLambdaCodeFixProvider))]
public sealed class PreferStaticLambdaCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "MakeLambdaStatic";

	public override ImmutableArray<string> FixableDiagnosticIds => ["PSGFR30"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			// The lambda and its wrapping ArgumentSyntax share the same span, so the innermost node is
			// required to resolve to the lambda itself.
			var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
			if (node is not AnonymousFunctionExpressionSyntax lambda)
				continue;

			if (IsStaticLambda(lambda))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Mark lambda as static",
					_ => MakeStaticAsync(context.Document, root, lambda),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static bool IsStaticLambda(AnonymousFunctionExpressionSyntax lambda) =>
		lambda switch
		{
			SimpleLambdaExpressionSyntax simple => simple.Modifiers.Any(SyntaxKind.StaticKeyword),
			ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Modifiers.Any(SyntaxKind.StaticKeyword),
			AnonymousMethodExpressionSyntax anonymous => anonymous.Modifiers.Any(SyntaxKind.StaticKeyword),
			_ => false,
		};

	static Task<Document> MakeStaticAsync(Document document, SyntaxNode root, AnonymousFunctionExpressionSyntax lambda)
	{
		// The static keyword takes the lambda's original leading trivia; the lambda's own leading trivia is
		// cleared so the output stays on a single line (e.g. 'static (ctx, _) => ...').
		var staticToken = SyntaxFactory
			.Token(SyntaxKind.StaticKeyword)
			.WithLeadingTrivia(lambda.GetLeadingTrivia())
			.WithTrailingTrivia(SyntaxFactory.Space);

		var cleared = lambda.WithLeadingTrivia(SyntaxFactory.TriviaList());
		var updated = cleared switch
		{
			SimpleLambdaExpressionSyntax simple => simple.WithModifiers(simple.Modifiers.Insert(0, staticToken)),
			ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.WithModifiers(
				parenthesized.Modifiers.Insert(0, staticToken)
			),
			AnonymousMethodExpressionSyntax anonymous => anonymous.WithModifiers(
				anonymous.Modifiers.Insert(0, staticToken)
			),
			_ => lambda,
		};

		if (ReferenceEquals(updated, lambda))
			return Task.FromResult(document);

		var newRoot = root.ReplaceNode(lambda, updated);
		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}
}
