using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Rewrites a <c>CodeWriter</c> XML block-writing call that was embedded in a string into its static
/// inline-string equivalent, for example <c>writer.XmlCode(value)</c> becomes
/// <c>XmlCommentWriter.XmlInlineCode(value)</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CodeWriterToStringCodeFixProvider))]
public sealed class CodeWriterToStringCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "UseXmlInlineCode";

	public override ImmutableArray<string> FixableDiagnosticIds => ["PSGFR29"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var node = root.FindNode(diagnostic.Location.SourceSpan);
			if (node is not InvocationExpressionSyntax invocation)
				continue;

			if (!TryGetReplacement(invocation, out var replacement))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Use the XmlCommentWriter inline-code helper instead",
					_ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement))),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static bool TryGetReplacement(InvocationExpressionSyntax invocation, out InvocationExpressionSyntax replacement)
	{
		replacement = invocation;

		if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: not null } member)
			return false;

		var inlineMethod = member.Name.Identifier.Text switch
		{
			"XmlCode" => "XmlInlineCode",
			"XmlCodeBlock" => "XmlInlineCodeBlock",
			_ => null,
		};
		if (inlineMethod is null)
			return false;

		var newMemberAccess = SyntaxFactory
			.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				SyntaxFactory.IdentifierName("XmlCommentWriter"),
				SyntaxFactory.IdentifierName(inlineMethod)
			)
			.WithTriviaFrom(member);

		replacement = SyntaxFactory
			.InvocationExpression(newMemberAccess, invocation.ArgumentList)
			.WithTriviaFrom(invocation);

		return true;
	}
}
