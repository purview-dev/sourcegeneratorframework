using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags extension classes that are missing the <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>
/// attribute or a file-level <c>#pragma warning disable CS1591</c> suppression, so they do not pollute
/// IntelliSense or produce pointless XML-documentation warnings.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionClassMetadataAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR38";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Extension class is missing EditorBrowsable or CS1591 suppression",
		"Extension class '{0}' is missing {1}; add [EditorBrowsable(EditorBrowsableState.Never)] and a file-level #pragma warning disable CS1591",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Extension classes should be hidden from IntelliSense with [EditorBrowsable(EditorBrowsableState.Never)] and suppress the pointless CS1591 XML-documentation warning."
	);

	const string EditorBrowsableAttribute = "System.ComponentModel.EditorBrowsableAttribute";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
	}

	static void AnalyzeNamedType(SymbolAnalysisContext context)
	{
		if (context.Symbol is not INamedTypeSymbol type || !ExtensionClassDiscovery.IsExtensionClass(type))
			return;

		var location = type.Locations.FirstOrDefault(static location => location.IsInSource);
		if (location is null)
			return;

		List<string> missing = [];
		if (!HasEditorBrowsable(type))
			missing.Add("[EditorBrowsable(EditorBrowsableState.Never)]");
		if (!HasCs1591Suppression(type, context.CancellationToken))
			missing.Add("#pragma warning disable CS1591");

		if (missing.Count == 0)
			return;

		context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name, string.Join(" and ", missing)));
	}

	static bool HasEditorBrowsable(INamedTypeSymbol type) =>
		type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == EditorBrowsableAttribute);

	static bool HasCs1591Suppression(INamedTypeSymbol type, CancellationToken cancellationToken)
	{
		var reference = type.DeclaringSyntaxReferences.FirstOrDefault();
		if (reference is null)
			return false;

		var root = reference.GetSyntax(cancellationToken).SyntaxTree.GetRoot(cancellationToken);

		return root.DescendantTrivia(descendIntoTrivia: true)
			.Select(static trivia => trivia.GetStructure())
			.OfType<PragmaWarningDirectiveTriviaSyntax>()
			.Any(static pragma =>
				pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)
				&& pragma.ErrorCodes.Any(static code => code.ToString().Contains("CS1591", StringComparison.Ordinal))
			);
	}
}
