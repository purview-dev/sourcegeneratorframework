using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags extension classes that are not organized coherently: misnamed, placed in a namespace/folder that
/// does not match the extended type, or extending more than one receiver type. A well-formed extension class
/// is named <c>{Receiver}Extensions</c>, lives under an <c>Extensions</c> folder mirroring the receiver's
/// namespace, and extends exactly one type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionClassOrganizationAnalyzer : DiagnosticAnalyzer
{
	public const string NameDiagnosticId = "PSGFR35";
	public const string PlacementDiagnosticId = "PSGFR36";
	public const string SplitDiagnosticId = "PSGFR37";

	static readonly DiagnosticDescriptor NameRule = new(
		NameDiagnosticId,
		"Extension class name does not match the extended type",
		"Extension class '{0}' should be named '{1}' to match the type it extends",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Name extension classes '{Receiver}Extensions' after the type they extend."
	);

	static readonly DiagnosticDescriptor PlacementRule = new(
		PlacementDiagnosticId,
		"Extension class is not placed in the extended type's namespace",
		"Extension class '{0}' should be placed in namespace '{1}' under an Extensions folder matching the extended type",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Place extension classes under an Extensions folder whose path and namespace mirror the extended type's namespace."
	);

	static readonly DiagnosticDescriptor SplitRule = new(
		SplitDiagnosticId,
		"Extension class extends multiple receiver types",
		"Extension class '{0}' extends multiple types ({1}); split it into one class per receiver type",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Keep one extension class per receiver type for a coherent, discoverable extension shape."
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [NameRule, PlacementRule, SplitRule];

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

		var receivers = ExtensionClassDiscovery.ResolveReceivers(type, context.Compilation);
		if (receivers.Length == 0)
			return;

		var location = type.Locations.FirstOrDefault(static location => location.IsInSource);
		if (location is null)
			return;

		var primary = receivers[0];
		var expectedName = ExtensionClassDiscovery.ExpectedClassName(primary);
		if (type.Name != expectedName)
		{
			context.ReportDiagnostic(Diagnostic.Create(NameRule, location, type.Name, expectedName));
		}

		var expectedNamespace = ExtensionClassDiscovery.ExpectedNamespace(primary);
		var actualNamespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns
			? ns.ToDisplayString()
			: string.Empty;
		if (!string.Equals(actualNamespace, expectedNamespace, StringComparison.Ordinal))
		{
			context.ReportDiagnostic(Diagnostic.Create(PlacementRule, location, type.Name, expectedNamespace));
		}

		if (receivers.Length > 1)
		{
			var receiverNames = string.Join(", ", receivers.Select(static receiver => receiver.Name));
			context.ReportDiagnostic(Diagnostic.Create(SplitRule, location, type.Name, receiverNames));
		}
	}
}
