using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags classic static extension methods (<c>public static T Method(this Receiver receiver, ...)</c>) in
/// favor of C# 14 <c>extension(Receiver receiver)</c> blocks. The rule only fires when the compilation's
/// language version supports extension blocks, so the accompanying code fix always produces compilable code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferExtensionKeywordAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR34";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Prefer extension blocks over classic extension methods",
		"Method '{0}' is a classic extension method; prefer a C# 14 extension(Receiver) block",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Use C# 14 extension(Receiver) blocks instead of classic static methods with a 'this' receiver parameter for a more coherent extension shape."
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
	}

	static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not MethodDeclarationSyntax method)
			return;

		if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
			return;

		if (method.ParameterList.Parameters.FirstOrDefault() is not { } firstParameter)
			return;

		if (!firstParameter.Modifiers.Any(SyntaxKind.ThisKeyword))
			return;

		if (method.Ancestors().OfType<ExtensionBlockDeclarationSyntax>().Any())
			return;

		if (!ExtensionClassDiscovery.SupportsExtensionKeyword(context.Compilation))
			return;

		context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.ValueText));
	}
}
