using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags extension classes that are missing the <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>
/// attribute, so they do not pollute IntelliSense.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionClassMetadataAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR38";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Extension class is missing EditorBrowsable",
		"Extension class '{0}' is missing [EditorBrowsable(EditorBrowsableState.Never)]",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Extension classes should be hidden from IntelliSense with [EditorBrowsable(EditorBrowsableState.Never)]."
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

		if (!HasEditorBrowsable(type))
			context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
	}

	static bool HasEditorBrowsable(INamedTypeSymbol type) =>
		type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == EditorBrowsableAttribute);
}
