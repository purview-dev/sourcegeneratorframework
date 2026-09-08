using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Classifies text emitted through the raw <c>CodeWriter</c> text methods so analyzers can suggest a
/// structured declaration or statement API.
/// </summary>
static class CodeWriterLiteralClassifier
{
	static readonly string[] DeclarationStarts =
	[
		"public ",
		"internal ",
		"private ",
		"protected ",
		"file ",
		"static ",
		"sealed ",
		"abstract ",
		"partial ",
		"readonly ",
		"ref ",
		"required ",
		"const ",
		"class ",
		"struct ",
		"interface ",
		"enum ",
		"record ",
		"delegate ",
		"namespace ",
		"global using ",
		"using ",
	];

	/// <summary>
	/// Attempts to resolve the text that would be emitted by a raw text-emission call, handling plain,
	/// interpolated, and raw string literals as well as constant expressions such as <c>const</c>
	/// references and string concatenation.
	/// </summary>
	/// <param name="expression">The first argument of the text-emission call.</param>
	/// <param name="semanticModel">The semantic model of the containing compilation.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="text">The resolved text, or <see langword="null"/> when it could not be resolved.</param>
	/// <returns><see langword="true"/> when the emitted text was resolved.</returns>
	public static bool TryGetLiteralText(
		ExpressionSyntax expression,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out string? text
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
				var builder = new StringBuilder();
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

		text = null;
		return false;
	}

	/// <summary>
	/// Determines whether the trimmed value starts with a C# declaration keyword.
	/// </summary>
	public static bool StartsWithDeclaration(string value)
	{
		foreach (var prefix in DeclarationStarts)
		{
			if (!value.StartsWith(prefix, StringComparison.Ordinal))
				continue;

			// "using (var x = ...)" is a using statement inside a body, not a directive; don't flag it.
			if (prefix == "using " && value.StartsWith("using (", StringComparison.Ordinal))
				return false;

			// "namespace global::" is a valid namespace declaration, but "global::" is not a declaration keyword.
			return true;
		}

		return false;
	}

	/// <summary>
	/// Determines whether the trimmed value is a <c>#if</c>, <c>#else</c>, or <c>#endif</c> preprocessor
	/// directive that can be expressed with <c>HashDefines</c>/<c>HashElse</c>.
	/// </summary>
	public static bool IsHashDefine(string value)
	{
		var trimmed = value.TrimStart();
		return trimmed.StartsWith("#if ", StringComparison.Ordinal)
			|| trimmed.StartsWith("#else", StringComparison.Ordinal)
			|| trimmed.StartsWith("#endif", StringComparison.Ordinal);
	}

	/// <summary>
	/// Determines whether the trimmed value is a <c>#pragma warning disable</c> or
	/// <c>#pragma warning restore</c> directive that can be expressed with <c>PragmaDisable</c> or
	/// <c>OpenPragmasScope</c>.
	/// </summary>
	public static bool IsPragmaWarningDirective(string value)
	{
		var trimmed = value.TrimStart();
		return trimmed.StartsWith("#pragma warning disable", StringComparison.Ordinal)
			|| trimmed.StartsWith("#pragma warning restore", StringComparison.Ordinal);
	}

	/// <summary>
	/// Classifies a single-line emitted statement and returns the structured <c>CodeWriter</c> API that
	/// can express it, or <see langword="null"/> when no structured equivalent is recognized.
	/// </summary>
	public static string? ClassifyStatement(string value)
	{
		var trimmed = value.TrimStart();
		if (trimmed.Length == 0 || trimmed.Contains("\n") || trimmed.Contains("\r"))
			return null;

		if (trimmed.StartsWith("//", StringComparison.Ordinal))
			return "Comment";

		if (ClassifyUsingDirective(trimmed) is { } usingApi)
			return usingApi;

		if (StartsWithDeclaration(trimmed))
			return null;

		// The structured CodeWriter API does not yet support preprocessor directives other than #if/#else/#endif and
		return ClassifyExecutable(trimmed);
	}

	static string? ClassifyUsingDirective(string trimmed)
	{
		if (
			!trimmed.StartsWith("using ", StringComparison.Ordinal)
			&& !trimmed.StartsWith("global using ", StringComparison.Ordinal)
		)
			return null;

		// "using (var x = ...)" is a using statement inside a body, not a directive; don't flag it.
		if (trimmed.StartsWith("using (", StringComparison.Ordinal) || !trimmed.EndsWith(";", StringComparison.Ordinal))
			return null;

		// "using alias = ..." is a using-alias directive, while "using ..." is a using-directive.
		return trimmed.Contains(" = ") ? "UsingAlias" : "Using";
	}

	static string? ClassifyExecutable(string trimmed)
	{
		if (
			(trimmed == "return;" || trimmed.StartsWith("return ", StringComparison.Ordinal))
			&& trimmed.EndsWith(";", StringComparison.Ordinal)
		)
			return "Return";

		if (trimmed.StartsWith("throw ", StringComparison.Ordinal) && trimmed.EndsWith(";", StringComparison.Ordinal))
			return "Throw";

		if (trimmed.StartsWith("await ", StringComparison.Ordinal) && trimmed.EndsWith(";", StringComparison.Ordinal))
			return HasReceiver(trimmed) ? "AwaitedMethodCallOn" : "AwaitedMethodCall";

		if (trimmed.StartsWith("if (", StringComparison.Ordinal))
			return "IfBlock";

		if (trimmed.StartsWith("else if (", StringComparison.Ordinal))
			return "ElseIf";

		if (trimmed == "else")
			return "Else";

		if (trimmed.StartsWith("foreach (", StringComparison.Ordinal))
			return "Foreach";

		if (trimmed.StartsWith("while (", StringComparison.Ordinal))
			return "While";

		if (trimmed.StartsWith("for (", StringComparison.Ordinal))
			return "For";

		if (!trimmed.EndsWith(";", StringComparison.Ordinal))
			return null;

		if (trimmed.Contains(" = "))
			return "Assignment";

		if (trimmed.Contains("(") && trimmed.EndsWith(");", StringComparison.Ordinal))
			return HasReceiver(trimmed) ? "MethodCallOn" : "MethodCall";

		// The structured CodeWriter API does not yet support preprocessor directives other than #if/#else/#endif and
		return null;
	}

	static bool HasReceiver(string trimmed)
	{
		var openParen = trimmed.IndexOf('(');
		return openParen > 0 && trimmed.LastIndexOf('.', openParen) >= 0;
	}

	/// <summary>
	/// Classifies the header of a block- or scope-opening <c>CodeWriter</c> method and returns the
	/// structured <c>if</c>/<c>else if</c>/<c>else</c> API that can express it, or
	/// <see langword="null"/> when the header is not a conditional block.
	/// </summary>
	/// <param name="header">The header text resolved from the first argument of the block method.</param>
	/// <param name="isScopeForm">Whether the block method is the scope-returning form, which selects the
	/// <c>Scope</c>-suffixed suggestion.</param>
	/// <returns>
	/// The structured API name, or <see langword="null"/> when the header does not describe a
	/// conditional block.
	/// </returns>
	public static string? ClassifyBlockHeader(string? header, bool isScopeForm)
	{
		var trimmed = header?.Trim();
		if (trimmed is null || trimmed.Length == 0)
			return null;

		if (trimmed.EndsWith(";", StringComparison.Ordinal) || trimmed.EndsWith(")", StringComparison.Ordinal))
			trimmed = trimmed.TrimEnd(';', ')').Trim();

		if (trimmed.StartsWith("else if (", StringComparison.Ordinal))
			return isScopeForm ? "ElseIfScope" : "ElseIf";

		if (trimmed == "else")
			return isScopeForm ? "ElseScope" : "Else";

		if (trimmed.StartsWith("if (", StringComparison.Ordinal))
			return isScopeForm ? "IfBlockScope" : "IfBlock";

		// The structured CodeWriter API does not yet support preprocessor directives other than #if/#else/#endif and
		return null;
	}
}
