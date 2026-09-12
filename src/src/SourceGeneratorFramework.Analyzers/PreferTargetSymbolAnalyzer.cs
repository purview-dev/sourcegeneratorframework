using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags <c>SemanticModel.GetDeclaredSymbol(TargetNode)</c> calls inside a
/// <c>ForAttributeWithMetadataName</c> transform. Roslyn already resolves <c>TargetSymbol</c> for the
/// <c>GeneratorAttributeSyntaxContext</c>, so calling <c>GetDeclaredSymbol</c> re-runs the same symbol
/// resolution on every pipeline rerun.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferTargetSymbolAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR31";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Prefer TargetSymbol over GetDeclaredSymbol(TargetNode)",
		"ForAttributeWithMetadataName already resolves the target symbol; use '{0}.TargetSymbol' instead of re-resolving it with GetDeclaredSymbol",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "GeneratorAttributeSyntaxContext.TargetSymbol is resolved by ForAttributeWithMetadataName; re-calling SemanticModel.GetDeclaredSymbol(ctx.TargetNode) performs redundant symbol resolution on every pipeline rerun."
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

		if (memberAccess.Name.Identifier.Text != "GetDeclaredSymbol")
			return;

		if (!TryResolveTargetNodeExpression(invocation, out var contextIdentifier))
			return;

		if (!IsGeneratorAttributeSyntaxContextParameter(context, contextIdentifier, context.CancellationToken))
			return;

		context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), contextIdentifier.Identifier.Text));
	}

	/// <summary>
	/// Resolves the <c>ctx.TargetNode</c> argument (in either the instance-style
	/// <c>ctx.SemanticModel.GetDeclaredSymbol(ctx.TargetNode, ct)</c> form or the extension-method form) and
	/// the <c>ctx</c> identifier it is expressed against.
	/// </summary>
	static bool TryResolveTargetNodeExpression(
		InvocationExpressionSyntax invocation,
		out IdentifierNameSyntax contextIdentifier
	)
	{
		contextIdentifier = null!;

		SyntaxNode? targetNodeExpression = null;
		if (invocation.Expression is MemberAccessExpressionSyntax methodAccess)
		{
			// Instance-style: GetDeclaredSymbol(ctx.TargetNode, ct) where the receiver is ctx.SemanticModel.
			if (
				methodAccess.Expression is MemberAccessExpressionSyntax receiver
				&& receiver.Name.Identifier.Text == "SemanticModel"
			)
			{
				targetNodeExpression = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
			}
		}

		if (
			targetNodeExpression is not MemberAccessExpressionSyntax targetNodeAccess
			|| targetNodeAccess.Name.Identifier.Text != "TargetNode"
		)
			return false;

		if (targetNodeAccess.Expression is not IdentifierNameSyntax identifier)
			return false;

		contextIdentifier = identifier;
		return true;
	}

	static bool IsGeneratorAttributeSyntaxContextParameter(
		SyntaxNodeAnalysisContext context,
		IdentifierNameSyntax identifier,
		CancellationToken cancellationToken
	)
	{
		if (identifier.Parent is not MemberAccessExpressionSyntax)
			return false;

		if (identifier.Ancestors().OfType<SimpleLambdaExpressionSyntax>().FirstOrDefault() is { } simple)
			return IsContextParameter(context, simple.Parameter, cancellationToken);

		if (identifier.Ancestors().OfType<ParenthesizedLambdaExpressionSyntax>().FirstOrDefault() is { } parenthesized)
		{
			var parameter = parenthesized.ParameterList.Parameters.FirstOrDefault(p =>
				p.Identifier.ValueText == identifier.Identifier.Text
			);
			return parameter is not null && IsContextParameter(context, parameter, cancellationToken);
		}

		return false;
	}

	static bool IsContextParameter(
		SyntaxNodeAnalysisContext context,
		ParameterSyntax? parameter,
		CancellationToken cancellationToken
	)
	{
		if (parameter is null)
			return false;

		var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter, cancellationToken);
		if (parameterSymbol is not IParameterSymbol typeParameter)
			return false;

		// Check that the parameter is of type GeneratorAttributeSyntaxContext.
		return typeParameter.Type.ToDisplayString() == "Microsoft.CodeAnalysis.GeneratorAttributeSyntaxContext";
	}
}
