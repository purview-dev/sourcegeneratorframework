using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags <c>NormalizeWhitespace()</c> calls in source generator code. Re-formatting a syntax tree is
/// expensive and is not incremental-friendly; generated source should be produced by an indented text
/// writer such as <c>CodeWriter</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidNormalizeWhitespaceAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR32";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Avoid NormalizeWhitespace when generating source",
		"NormalizeWhitespace is expensive and not incremental-friendly; build generated source with an indented text writer such as CodeWriter instead of constructing and re-formatting a syntax tree",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "The Roslyn incremental generators cookbook recommends producing source with an indented text writer rather than SyntaxNodes; calling NormalizeWhitespace is often quite expensive."
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
		var invocation = (InvocationExpressionSyntax)context.Node;
		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return;

		if (memberAccess.Name.Identifier.Text != "NormalizeWhitespace")
			return;

		context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
	}
}
