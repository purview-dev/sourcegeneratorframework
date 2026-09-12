using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags raw <c>CodeWriter</c> text emission that starts with a C# declaration keyword, suggesting a
/// structured declaration API such as <c>Class</c> or <c>Property</c> instead. Plain,
/// interpolated, and raw string literals as well as constant expressions are inspected.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferStructuredCodeWriterAPIAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR18";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Prefer a structured CodeWriter declaration API",
		"'{0}' with a declaration should use a structured API such as Class, Method, Property, or Field",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Emitting declaration syntax through raw text bypasses the structured, deterministic declaration APIs on CodeWriter."
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
	}

	static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
	{
		if (
			context.Node
			is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation
		)
			return;

		var name = member.Name.Identifier.Text;
		if (name is not ("Write" or "Line" or "Append" or "AppendLine" or "MultiLine"))
			return;

		if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not ExpressionSyntax expression)
			return;

		if (
			context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
			is not IMethodSymbol method
		)
			return;

		if (method.ContainingType?.ToDisplayString() != "Purview.SourceGeneratorFramework.CodeWriter")
			return;

		if (
			!CodeWriterLiteralClassifier.TryGetLiteralText(
				expression,
				context.SemanticModel,
				context.CancellationToken,
				out var value
			)
		)
			return;

		if (value is null)
			return;

		var trimmed = value.TrimStart();
		if (!CodeWriterLiteralClassifier.StartsWithDeclaration(trimmed))
			return;

		context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), name));
	}
}
