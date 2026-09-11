using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags non-static lambdas passed to incremental generator pipeline methods (such as the transform or
/// predicate of <c>ForAttributeWithMetadataName</c>, or <c>Select</c>/<c>Where</c>/<c>Combine</c>) that do
/// not capture anything. Such lambdas are guaranteed allocation-free on the per-item hot path when declared
/// <c>static</c>; the compiler otherwise caches them, but only as an optimization that is easy to break.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferStaticLambdaInPipelineAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR30";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Prefer static lambdas in incremental generator pipelines",
		"Lambda passed to '{0}' can be marked static; it does not capture any state, so a static lambda avoids per-item closure allocations on the incremental generator hot path",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Per-item lambdas in incremental source generator pipelines should be declared static so the compiler never allocates a closure for them."
	);

	static readonly HashSet<string> PipelineMethodNames =
	[
		"Select",
		"Where",
		"Combine",
		"Collect",
		"ForAttributeWithMetadataName",
		"CombineWith",
		"CollectWith",
		"CombineWithContext",
		"RegisterSourceOutput",
	];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(
			AnalyzeLambda,
			SyntaxKind.SimpleLambdaExpression,
			SyntaxKind.ParenthesizedLambdaExpression,
			SyntaxKind.AnonymousMethodExpression
		);
	}

	static void AnalyzeLambda(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not AnonymousFunctionExpressionSyntax lambda)
			return;

		if (IsStaticLambda(lambda))
			return;

		if (!TryGetPipelineMethod(context, lambda, out var methodName))
			return;

		if (!CouldBeStatic(context, lambda))
			return;

		context.ReportDiagnostic(Diagnostic.Create(Rule, lambda.GetLocation(), methodName));
	}

	static bool IsStaticLambda(AnonymousFunctionExpressionSyntax lambda) =>
		lambda switch
		{
			SimpleLambdaExpressionSyntax simple => simple.Modifiers.Any(SyntaxKind.StaticKeyword),
			ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Modifiers.Any(SyntaxKind.StaticKeyword),
			AnonymousMethodExpressionSyntax anonymous => anonymous.Modifiers.Any(SyntaxKind.StaticKeyword),
			_ => false,
		};

	static bool TryGetPipelineMethod(
		SyntaxNodeAnalysisContext context,
		AnonymousFunctionExpressionSyntax lambda,
		out string methodName
	)
	{
		methodName = string.Empty;

		if (lambda.Parent is not ArgumentSyntax)
			return false;

		if (lambda.Parent?.Parent?.Parent is not InvocationExpressionSyntax invocation)
			return false;

		var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
		if (symbolInfo.Symbol is not IMethodSymbol invokedMethod)
			return false;

		if (!PipelineMethodNames.Contains(invokedMethod.Name))
			return false;

		// Incremental pipeline methods either come from Roslyn (namespace Microsoft.CodeAnalysis) or from
		// the framework's IncrementalPipeline helper. This excludes LINQ (System.Linq) and other
		// similarly-named methods.
		var containingType = invokedMethod.ContainingType;
		if (containingType is null)
			return false;

		var containingNamespace = containingType.ContainingNamespace?.ToDisplayString();
		if (containingNamespace != "Microsoft.CodeAnalysis" && containingType.Name != "IncrementalPipeline")
			return false;

		methodName = invokedMethod.Name;
		return true;
	}

	/// <summary>
	/// Determines whether the lambda captures no state and therefore could legally be marked <c>static</c>.
	/// </summary>
	static bool CouldBeStatic(SyntaxNodeAnalysisContext context, AnonymousFunctionExpressionSyntax lambda)
	{
		try
		{
			// The data-flow analysis does not report 'this'/'base' as captured, so check explicitly.
			if (lambda.DescendantNodes().Any(static node => node is ThisExpressionSyntax or BaseExpressionSyntax))
				return false;

			// Only the roots of member-access chains matter: a member name such as 'TargetSymbol' in
			// 'ctx.TargetSymbol' is never captured, its receiver is.
			foreach (var identifier in lambda.DescendantNodes().OfType<IdentifierNameSyntax>())
			{
				if (identifier.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == identifier)
					continue;

				var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
				if (symbol is null)
					continue;

				if (IsCapturedOutsideLambda(symbol, lambda))
					return false;
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	static bool IsCapturedOutsideLambda(ISymbol symbol, AnonymousFunctionExpressionSyntax lambda)
	{
		// Types and static/const members are never captured.
		if (symbol is INamedTypeSymbol or ITypeParameterSymbol)
			return false;
		if (
			symbol
			is IFieldSymbol { IsConst: true }
				or IFieldSymbol { IsStatic: true }
				or IPropertySymbol { IsStatic: true }
				or IMethodSymbol { IsStatic: true }
		)
			return false;

		// Symbols declared within the lambda (parameters and locals) are not captured.
		foreach (var location in symbol.Locations)
		{
			if (
				location.IsInSource
				&& location.SourceTree == lambda.SyntaxTree
				&& lambda.FullSpan.Contains(location.SourceSpan)
			)
				return false;
		}

		// A standalone identifier resolving to an instance field/property/method, or to a local/parameter
		// declared outside the lambda, requires capture.
		return symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol or IMethodSymbol;
	}
}
