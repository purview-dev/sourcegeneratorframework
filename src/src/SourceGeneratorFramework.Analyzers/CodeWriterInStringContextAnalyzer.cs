using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags a <c>CodeWriter</c> value used in a string interpolation hole or string concatenation, where the
/// compiler implicitly calls <see cref="CodeWriter.ToString"/>. That dumps the writer's possibly-incomplete
/// buffer (and throws when a scope is still open), instead of emitting an inline fragment.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CodeWriterInStringContextAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR29";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Do not embed a CodeWriter in a string",
		"Embedding a CodeWriter in a string implicitly calls its ToString(), which dumps the writer's possibly incomplete output. Use XmlCommentWriter.XmlInlineCode(...) for inline XML tags, or call ToString() explicitly once generation is complete.",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A CodeWriter value must not be interpolated or concatenated into a string because that implicitly invokes ToString() on a possibly-incomplete writer."
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(AnalyzeInterpolatedString, SyntaxKind.InterpolatedStringExpression);
		context.RegisterSyntaxNodeAction(AnalyzeAddExpression, SyntaxKind.AddExpression);
	}

	static void AnalyzeInterpolatedString(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not InterpolatedStringExpressionSyntax interpolated)
			return;

		foreach (var content in interpolated.Contents)
		{
			if (content is not InterpolationSyntax interpolation)
				continue;

			CheckCodeWriterExpression(context, interpolation.Expression);
		}
	}

	static void AnalyzeAddExpression(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not BinaryExpressionSyntax binary)
			return;

		CheckCodeWriterExpression(context, binary.Left);
		CheckCodeWriterExpression(context, binary.Right);
	}

	static void CheckCodeWriterExpression(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
	{
		if (expression is null)
			return;

		var type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
		if (type is null || type.TypeKind == TypeKind.Error)
			return;

		if (type.ToDisplayString() != "Purview.SourceGeneratorFramework.CodeWriter")
			return;

		context.ReportDiagnostic(Diagnostic.Create(Rule, expression.GetLocation()));
	}
}
