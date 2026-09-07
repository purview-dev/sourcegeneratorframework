using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Classifies named types as Roslyn components (source generators, diagnostic analyzers, or code
/// fix providers) so the setup diagnostics can share one set of rules.
/// </summary>
static class RoslynComponentDiscovery
{
	public static bool IsCodeFixProvider(INamedTypeSymbol type, INamedTypeSymbol? codeFixProviderType)
	{
		if (codeFixProviderType is null)
			return false;

		for (var current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, codeFixProviderType))
				return true;
		}

		return false;
	}

	public static bool IsDiagnosticAnalyzer(INamedTypeSymbol type, INamedTypeSymbol? diagnosticAnalyzerType)
	{
		if (diagnosticAnalyzerType is null)
			return false;

		for (var current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, diagnosticAnalyzerType))
				return true;
		}

		return false;
	}

	public static bool IsSourceGenerator(
		INamedTypeSymbol type,
		INamedTypeSymbol? incrementalGeneratorType,
		INamedTypeSymbol? legacyGeneratorType
	)
	{
		if (incrementalGeneratorType is null && legacyGeneratorType is null)
			return false;

		foreach (var implemented in type.AllInterfaces)
		{
			if (
				SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, incrementalGeneratorType)
				|| SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, legacyGeneratorType)
			)
				return true;
		}

		return false;
	}

	public static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol? attributeType) =>
		attributeType is null
			? false
			: type.GetAttributes()
				.Any(a =>
					a.AttributeClass is not null
					&& SymbolEqualityComparer.Default.Equals(a.AttributeClass.OriginalDefinition, attributeType)
				);

	/// <summary>
	/// True when the type must be instantiated by the Roslyn compiler host: it derives from a
	/// component base type, implements a generator interface, or carries a component attribute.
	/// </summary>
	public static bool IsRoslynComponent(
		INamedTypeSymbol type,
		INamedTypeSymbol? codeFixProviderType,
		INamedTypeSymbol? diagnosticAnalyzerType,
		INamedTypeSymbol? incrementalGeneratorType,
		INamedTypeSymbol? legacyGeneratorType,
		INamedTypeSymbol? exportCodeFixProviderAttributeType,
		INamedTypeSymbol? diagnosticAnalyzerAttributeType,
		INamedTypeSymbol? generatorAttributeType
	)
	{
		if (IsCodeFixProvider(type, codeFixProviderType))
			return true;

		if (IsDiagnosticAnalyzer(type, diagnosticAnalyzerType))
			return true;

		if (IsSourceGenerator(type, incrementalGeneratorType, legacyGeneratorType))
			return true;

		// Some Roslyn components are not derived from a base type or interface, but are instead marked with an attribute.
		return HasAttribute(type, exportCodeFixProviderAttributeType)
			|| HasAttribute(type, diagnosticAnalyzerAttributeType)
			|| HasAttribute(type, generatorAttributeType);
	}

	public static bool IsEffectivelyPublic(INamedTypeSymbol type)
	{
		if (type.DeclaredAccessibility != Accessibility.Public)
			return false;

		if (type.ContainingType is not null)
			return IsEffectivelyPublic(type.ContainingType);

		// The type is public and not nested, so it is effectively public.
		return true;
	}

	public static string DescribeKind(
		INamedTypeSymbol type,
		INamedTypeSymbol? codeFixProviderType,
		INamedTypeSymbol? diagnosticAnalyzerType,
		INamedTypeSymbol? incrementalGeneratorType,
		INamedTypeSymbol? legacyGeneratorType,
		INamedTypeSymbol? exportCodeFixProviderAttributeType,
		INamedTypeSymbol? generatorAttributeType
	)
	{
		if (IsCodeFixProvider(type, codeFixProviderType) || HasAttribute(type, exportCodeFixProviderAttributeType))
			return "code fix provider";
		if (IsDiagnosticAnalyzer(type, diagnosticAnalyzerType))
			return "diagnostic analyzer";
		if (
			IsSourceGenerator(type, incrementalGeneratorType, legacyGeneratorType)
			|| HasAttribute(type, generatorAttributeType)
		)
		{
			return "source generator";
		}

		// The type is not a known Roslyn component, but it is still a public type in a generator assembly, so it is effectively a Roslyn component.
		return "Roslyn component";
	}
}
