using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Provides diagnostic descriptors shared between the source generator and the analyzer for the
/// attribute-data model feature.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"MicrosoftCodeAnalysisReleaseTracking",
	"RS2008:Enable analyzer release tracking",
	Justification = "The descriptors are shared between the source generator and the analyzer project; release tracking is maintained in the consuming analyzer project."
)]
public static class AttributeDataModelDiagnosticRules
{
	/// <summary>
	/// Diagnostic raised when the target attribute type cannot be resolved.
	/// </summary>
	public static readonly DiagnosticDescriptor TargetAttributeNotResolved = new(
		"ADM0001",
		"Target attribute type cannot be resolved",
		"Target attribute type for '{0}' cannot be resolved",
		"Target",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when a property type is not supported for attribute extraction.
	/// </summary>
	public static readonly DiagnosticDescriptor PropertyTypeNotSupported = new(
		"ADM0002",
		"Property type is not supported for attribute extraction",
		"Property '{0}' type '{1}' is not supported for attribute extraction",
		"Property",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when a specified constructor index/name does not exist on the target attribute.
	/// </summary>
	public static readonly DiagnosticDescriptor ConstructorMemberNotFound = new(
		"ADM0003",
		"Specified constructor index/name does not exist on the target attribute",
		"Constructor argument '{0}' does not exist on target attribute '{1}'",
		"Source",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when a nested model type is not annotated with GenerateAttributeDataModel.
	/// </summary>
	public static readonly DiagnosticDescriptor NestedModelNotGenerated = new(
		"ADM0004",
		"Nested model type is not annotated with GenerateAttributeDataModel",
		"Nested model type '{0}' is not annotated with GenerateAttributeDataModel",
		"NestedModel",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when a default value cannot be emitted for the property type.
	/// </summary>
	public static readonly DiagnosticDescriptor DefaultValueNotSupported = new(
		"ADM0005",
		"Default value cannot be emitted for the property type",
		"Default value '{0}' cannot be emitted for property type '{1}'",
		"DefaultValue",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when a non-nullable reference type property requires a default value.
	/// </summary>
	public static readonly DiagnosticDescriptor NonNullableReferenceTypeRequiresDefault = new(
		"ADM0006",
		"Non-nullable reference type property requires a default value",
		"Non-nullable reference type property '{0}' requires an explicit or inferred default value",
		"DefaultValue",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when auto-discovery requires a target attribute type.
	/// </summary>
	public static readonly DiagnosticDescriptor AutoDiscoverRequiresType = new(
		"ADM0007",
		"Auto-discovery requires a target attribute type",
		"Auto-discovery requires a target attribute type; use the Type constructor overload instead of the string overload",
		"AutoDiscovery",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when a type argument property type must be TypeIdentity.
	/// </summary>
	public static readonly DiagnosticDescriptor TypeArgumentPropertyTypeInvalid = new(
		"ADM0008",
		"Type argument property type must be TypeIdentity",
		"Type argument property '{0}' type '{1}' must be Purview.SourceGeneratorFramework.TypeIdentity",
		"TypeArgument",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when an IsEnum property must be a string type.
	/// </summary>
	public static readonly DiagnosticDescriptor IsEnumRequiresStringType = new(
		"ADM0009",
		"IsEnum property must be a string type",
		"Property '{0}' is marked with IsEnum but its type '{1}' is not a string; IsEnum requires a string or string? property type",
		"Property",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Diagnostic raised when an attribute-data model property is declared with a non-cacheable
	/// <see cref="ISymbol"/> or <see cref="Type"/> type.
	/// </summary>
	public static readonly DiagnosticDescriptor SymbolPropertyNotCacheable = new(
		"ADM0010",
		"Attribute data model property type is not cacheable",
		"Property '{0}' type '{1}' is not cacheable for attribute data extraction. Use Purview.SourceGeneratorFramework.TypeIdentity or a string/string? type to capture type identity in a cacheable form.",
		"Property",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);
}
