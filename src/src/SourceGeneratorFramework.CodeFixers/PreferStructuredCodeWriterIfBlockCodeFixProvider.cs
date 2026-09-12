using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Fixes a block- or scope-opening <c>CodeWriter</c> method whose header writes an <c>if</c>,
/// <c>else if</c>, or <c>else</c> statement by rewriting it to the structured <c>IfBlock</c>,
/// <c>ElseIf</c>, or <c>Else</c> API.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferStructuredCodeWriterIfBlockCodeFixProvider))]
public sealed class PreferStructuredCodeWriterIfBlockCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "UseStructuredConditionalBlock";

	public override ImmutableArray<string> FixableDiagnosticIds => ["PSGFR23"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var semanticModel = await context
			.Document.GetSemanticModelAsync(context.CancellationToken)
			.ConfigureAwait(false);
		if (semanticModel is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var node = root.FindNode(diagnostic.Location.SourceSpan);
			if (node is not InvocationExpressionSyntax invocation)
				continue;

			if (
				invocation.Expression
				is not MemberAccessExpressionSyntax { Expression: ExpressionSyntax receiver } member
			)
				continue;

			if (!TryGetReplacement(invocation, semanticModel, context.CancellationToken, out var replacement))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Use the structured conditional block API",
					_ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement))),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static bool TryGetReplacement(
		InvocationExpressionSyntax invocation,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out InvocationExpressionSyntax replacement
	)
	{
		replacement = invocation;

		if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: ExpressionSyntax receiver } member)
			return false;

		var arguments = invocation.ArgumentList.Arguments;
		if (arguments.Count == 0)
			return false;

		var methodName = member.Name.Identifier.Text;
		var isScopeForm = methodName.EndsWith("Scope", StringComparison.Ordinal);
		var isDelimited = methodName is "OpenDelimitedBlockScope" or "OpenDelimitedBlock" or "DelimitedBlock";

		if (
			!TryGetLiteralText(arguments[0].Expression, semanticModel, cancellationToken, out var header)
			|| !TryClassifyHeader(header, isScopeForm, out var structuredApi, out var condition)
		)
			return false;

		// The delimited forms must use brace delimiters; only then is the header an if-style block.
		if (
			isDelimited
			&& (
				arguments.Count < 3
				|| !TryGetLiteralText(arguments[1].Expression, semanticModel, cancellationToken, out var openingToken)
				|| !TryGetLiteralText(arguments[2].Expression, semanticModel, cancellationToken, out var closingToken)
				|| openingToken != "{"
				|| closingToken != "}"
			)
		)
			return false;

		ArgumentListSyntax newArgumentList;
		if (isScopeForm)
		{
			newArgumentList = condition is null
				? SyntaxFactory.ArgumentList()
				: SyntaxFactory.ArgumentList(
					SyntaxFactory.SingletonSeparatedList(WithCondition(arguments[0], condition))
				);
		}
		else
		{
			// Action forms drop the header (and any delimiter arguments), keeping only the body callback.
			var bodyIndex = isDelimited ? 3 : 1;
			if (bodyIndex >= arguments.Count)
				return false;

			newArgumentList = condition is null
				? SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arguments[bodyIndex]))
				: SyntaxFactory.ArgumentList(
					CreateSeparatedList([WithCondition(arguments[0], condition), arguments[bodyIndex]])
				);
		}

		newArgumentList = newArgumentList.WithTriviaFrom(invocation.ArgumentList);

		var newMemberAccess = SyntaxFactory
			.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				receiver,
				SyntaxFactory.IdentifierName(structuredApi)
			)
			.WithTriviaFrom(member);

		replacement = SyntaxFactory.InvocationExpression(newMemberAccess, newArgumentList).WithTriviaFrom(invocation);

		return true;
	}

	static ArgumentSyntax WithCondition(ArgumentSyntax original, string condition) =>
		original.WithExpression(
			SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(condition))
		);

	static SeparatedSyntaxList<ArgumentSyntax> CreateSeparatedList(List<ArgumentSyntax> arguments)
	{
		if (arguments.Count == 0)
			return SyntaxFactory.SeparatedList<ArgumentSyntax>();

		List<SyntaxNodeOrToken> nodesAndTokens = new((arguments.Count * 2) - 1);
		for (var index = 0; index < arguments.Count; index++)
		{
			if (index > 0)
				nodesAndTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
			nodesAndTokens.Add(arguments[index]);
		}

		return SyntaxFactory.SeparatedList<ArgumentSyntax>(nodesAndTokens);
	}

	static bool TryGetLiteralText(
		ExpressionSyntax expression,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out string text
	)
	{
		switch (expression)
		{
			case LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal:
				text = literal.Token.ValueText;
				return true;

			case InterpolatedStringExpressionSyntax interpolated:
#pragma warning disable format
			{
				System.Text.StringBuilder builder = new();
				foreach (var content in interpolated.Contents)
				{
					if (content is InterpolatedStringTextSyntax textPart)
						builder.Append(textPart.TextToken.ValueText);
				}

				text = builder.ToString();
				return true;
			}
#pragma warning restore format

			default:
				break;
		}

		var constant = semanticModel.GetConstantValue(expression, cancellationToken);
		if (constant.HasValue && constant.Value is string value)
		{
			text = value;
			return true;
		}

		text = string.Empty;
		return false;
	}

	static bool TryClassifyHeader(string header, bool isScopeForm, out string structuredApi, out string? condition)
	{
		structuredApi = string.Empty;
		condition = null;

		var trimmed = header.Trim();
		if (trimmed.StartsWith("else if (", StringComparison.Ordinal))
		{
			if (!TryExtractCondition(trimmed, out condition))
				return false;
			structuredApi = isScopeForm ? "ElseIfScope" : "ElseIf";
			return true;
		}

		if (trimmed == "else")
		{
			structuredApi = isScopeForm ? "ElseScope" : "Else";
			return true;
		}

		if (trimmed.StartsWith("if (", StringComparison.Ordinal))
		{
			if (!TryExtractCondition(trimmed, out condition))
				return false;
			structuredApi = isScopeForm ? "IfBlockScope" : "IfBlock";
			return true;
		}

		return false;
	}

	static bool TryExtractCondition(string header, out string condition)
	{
		condition = string.Empty;

		var trimmed = header;
		if (trimmed.EndsWith(")", StringComparison.Ordinal))
			trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
		else if (trimmed.EndsWith(");", StringComparison.Ordinal))
			trimmed = trimmed.Substring(0, trimmed.Length - 2).TrimEnd();

		var openParen = trimmed.IndexOf('(');
		if (openParen < 0)
			return false;

		var inner = trimmed.Substring(openParen + 1).Trim();
		if (inner.Length == 0)
			return false;

		condition = inner;
		return true;
	}
}
