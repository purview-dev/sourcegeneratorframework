using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Reports attribute-data model validation diagnostics (ADM0001-ADM0009) on the record structs annotated
/// with <c>Purview.SourceGeneratorFramework.Generators.GenerateAttribute</c>. These rules were moved out of the
/// source generator so they run as standard IDE/build analyzers instead of generator-reported diagnostics.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AttributeDataModelValidationAnalyzer : DiagnosticAnalyzer
{
	public static DiagnosticDescriptor TargetAttributeNotResolved =>
		AttributeDataModelDiagnosticRules.TargetAttributeNotResolved;

	public static DiagnosticDescriptor PropertyTypeNotSupported =>
		AttributeDataModelDiagnosticRules.PropertyTypeNotSupported;

	public static DiagnosticDescriptor ConstructorMemberNotFound =>
		AttributeDataModelDiagnosticRules.ConstructorMemberNotFound;

	public static DiagnosticDescriptor NestedModelNotGenerated =>
		AttributeDataModelDiagnosticRules.NestedModelNotGenerated;

	public static DiagnosticDescriptor DefaultValueNotSupported =>
		AttributeDataModelDiagnosticRules.DefaultValueNotSupported;

	public static DiagnosticDescriptor NonNullableReferenceTypeRequiresDefault =>
		AttributeDataModelDiagnosticRules.NonNullableReferenceTypeRequiresDefault;

	public static DiagnosticDescriptor AutoDiscoverRequiresType =>
		AttributeDataModelDiagnosticRules.AutoDiscoverRequiresType;

	public static DiagnosticDescriptor TypeArgumentPropertyTypeInvalid =>
		AttributeDataModelDiagnosticRules.TypeArgumentPropertyTypeInvalid;

	public static DiagnosticDescriptor IsEnumRequiresStringType =>
		AttributeDataModelDiagnosticRules.IsEnumRequiresStringType;

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[
			TargetAttributeNotResolved,
			PropertyTypeNotSupported,
			ConstructorMemberNotFound,
			NestedModelNotGenerated,
			DefaultValueNotSupported,
			NonNullableReferenceTypeRequiresDefault,
			AutoDiscoverRequiresType,
			TypeArgumentPropertyTypeInvalid,
			IsEnumRequiresStringType,
		];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(context =>
		{
			var generateAttributeType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.Generators.GenerateAttribute"
			);
			var propertyAttributeType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.Generators.PropertyAttribute"
			);
			var argumentAttributeType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.Generators.ArgumentAttribute"
			);
			var nestedModelAttributeType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.Generators.NestedModelAttribute"
			);
			var excludeAttributeType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.Generators.ExcludeAttribute"
			);
			var typeArgumentAttributeType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.Generators.GenericTypeArgumentAttribute"
			);
			var typeIdentityType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.TypeIdentity"
			);
			var typedConstantType = context.Compilation.GetTypeByMetadataName("Microsoft.CodeAnalysis.TypedConstant");

			context.RegisterSymbolAction(
				context =>
					AnalyzeNamedType(
						context,
						generateAttributeType,
						propertyAttributeType,
						argumentAttributeType,
						nestedModelAttributeType,
						excludeAttributeType,
						typeArgumentAttributeType,
						typeIdentityType,
						typedConstantType
					),
				SymbolKind.NamedType
			);
		});
	}

	static void AnalyzeNamedType(
		SymbolAnalysisContext context,
		INamedTypeSymbol? generateAttributeType,
		INamedTypeSymbol? propertyAttributeType,
		INamedTypeSymbol? argumentAttributeType,
		INamedTypeSymbol? nestedModelAttributeType,
		INamedTypeSymbol? excludeAttributeType,
		INamedTypeSymbol? typeArgumentAttributeType,
		INamedTypeSymbol? typeIdentityType,
		INamedTypeSymbol? typedConstantType
	)
	{
		if (context.Symbol is not INamedTypeSymbol typeSymbol)
			return;

		if (typeSymbol.TypeKind is not TypeKind.Struct and not TypeKind.Class)
			return;

		if (generateAttributeType is null)
			return;

		var generateAttribute = GetAttribute(typeSymbol, generateAttributeType);
		if (generateAttribute is null)
			return;

		var typeLocation = typeSymbol.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;

		var (targetAttributeType, _) = AnalyzeTargetAttribute(context, generateAttribute, typeLocation, typeSymbol);

		var targetConstructorParameters = targetAttributeType is INamedTypeSymbol namedTargetType
			? namedTargetType.InstanceConstructors.SelectMany(static ctor => ctor.Parameters).ToImmutableArray()
			: [];

		foreach (var constructor in typeSymbol.InstanceConstructors)
		{
			foreach (var parameter in constructor.Parameters)
			{
				if (!parameter.Locations.Any(static location => location.IsInSource))
					continue;

				AnalyzeParameter(
					context,
					parameter,
					generateAttributeType,
					targetAttributeType,
					targetConstructorParameters,
					propertyAttributeType,
					argumentAttributeType,
					nestedModelAttributeType,
					excludeAttributeType,
					typeArgumentAttributeType,
					typeIdentityType,
					typedConstantType
				);
			}
		}
	}

	static (ITypeSymbol? TargetAttributeType, bool HasTargetType) AnalyzeTargetAttribute(
		SymbolAnalysisContext context,
		AttributeData generateAttribute,
		Location typeLocation,
		INamedTypeSymbol typeSymbol
	)
	{
		if (generateAttribute.ConstructorArguments.Length == 0)
		{
			context.ReportDiagnostic(Diagnostic.Create(TargetAttributeNotResolved, typeLocation, typeSymbol.Name));
			return (null, false);
		}

		var firstArgument = generateAttribute.ConstructorArguments[0].Value;
		if (firstArgument is ITypeSymbol typeArgument)
			return (typeArgument, true);

		if (firstArgument is not string)
			context.ReportDiagnostic(Diagnostic.Create(TargetAttributeNotResolved, typeLocation, typeSymbol.Name));

		if (GetBoolNamedArgument(generateAttribute, "AutoDiscover"))
			context.ReportDiagnostic(Diagnostic.Create(AutoDiscoverRequiresType, typeLocation, typeSymbol.Name));

		return (null, false);
	}

	static void AnalyzeParameter(
		SymbolAnalysisContext context,
		IParameterSymbol parameter,
		INamedTypeSymbol generateAttributeType,
		ITypeSymbol? targetAttributeType,
		ImmutableArray<IParameterSymbol> targetConstructorParameters,
		INamedTypeSymbol? propertyAttributeType,
		INamedTypeSymbol? argumentAttributeType,
		INamedTypeSymbol? nestedModelAttributeType,
		INamedTypeSymbol? excludeAttributeType,
		INamedTypeSymbol? typeArgumentAttributeType,
		INamedTypeSymbol? typeIdentityType,
		INamedTypeSymbol? typedConstantType
	)
	{
		var parameterLocation = parameter.Locations.First(static location => location.IsInSource);

		// ADM0002 - unsupported property type (reported before exclusion handling, matching the generator).
		if (parameter.Type.TypeKind is TypeKind.Array or TypeKind.Pointer or TypeKind.FunctionPointer)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					PropertyTypeNotSupported,
					parameterLocation,
					parameter.Name,
					parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
				)
			);
			return;
		}

		var nestedModelAttribute = GetAttribute(parameter, nestedModelAttributeType);
		var typeArgumentAttribute = GetAttribute(parameter, typeArgumentAttributeType);
		var excludeAttribute = GetAttribute(parameter, excludeAttributeType);
		var argumentAttribute = GetAttribute(parameter, argumentAttributeType);
		var propertyAttribute = GetAttribute(parameter, propertyAttributeType);

		var isExcluded = excludeAttribute is not null;
		var isNestedModel = nestedModelAttribute is not null;
		var isTypeArgument = typeArgumentAttribute is not null;
		var hasExclusive = isExcluded || isNestedModel || isTypeArgument;

		// ADM0003 - the specified constructor member does not exist on the target attribute.
		if (argumentAttribute is not null && targetAttributeType is not null && !hasExclusive)
		{
			ValidateConstructorMember(
				context,
				argumentAttribute,
				parameterLocation,
				targetConstructorParameters,
				targetAttributeType
			);
		}

		if (isExcluded)
			return;

		if (isNestedModel && !IsGeneratedAttributeModel(parameter.Type, generateAttributeType))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					NestedModelNotGenerated,
					parameterLocation,
					parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
				)
			);
		}

		if (isTypeArgument && !IsTypeIdentityType(parameter.Type, typeIdentityType))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					TypeArgumentPropertyTypeInvalid,
					parameterLocation,
					parameter.Name,
					parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
				)
			);
		}

		if (HasIsEnum(propertyAttribute, argumentAttribute) && parameter.Type.SpecialType != SpecialType.System_String)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					IsEnumRequiresStringType,
					parameterLocation,
					parameter.Name,
					parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
				)
			);
		}

		var defaultValue = GetEffectiveDefaultValue(
			nestedModelAttribute,
			typeArgumentAttribute,
			argumentAttribute,
			propertyAttribute,
			hasExclusive
		);

		if (defaultValue is not null && !IsDefaultValueEmittable(defaultValue, parameter.Type, typedConstantType))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DefaultValueNotSupported,
					parameterLocation,
					Convert.ToString(defaultValue, CultureInfo.InvariantCulture) ?? "null",
					parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
				)
			);
		}

		// ADM0006 - a non-nullable reference type property must supply a default value.
		if (
			parameter.Type.IsReferenceType
			&& parameter.Type.NullableAnnotation == NullableAnnotation.NotAnnotated
			&& defaultValue is null
		)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(NonNullableReferenceTypeRequiresDefault, parameterLocation, parameter.Name)
			);
		}
	}

	static void ValidateConstructorMember(
		SymbolAnalysisContext context,
		AttributeData argumentAttribute,
		Location parameterLocation,
		ImmutableArray<IParameterSymbol> targetConstructorParameters,
		ITypeSymbol targetAttributeType
	)
	{
		var name = GetCtorName(argumentAttribute);
		var index = GetCtorIndex(argumentAttribute);

		if (name is not null)
		{
			if (!targetConstructorParameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				context.ReportDiagnostic(
					Diagnostic.Create(
						ConstructorMemberNotFound,
						parameterLocation,
						name,
						targetAttributeType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
					)
				);
			}

			return;
		}

		if (index >= 0 && !targetConstructorParameters.Any(p => p.Ordinal == index))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					ConstructorMemberNotFound,
					parameterLocation,
					index.ToString(CultureInfo.InvariantCulture),
					targetAttributeType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
				)
			);
		}
	}

	static string? GetCtorName(AttributeData attributeData)
	{
		if (attributeData.ConstructorArguments.Length > 0 && attributeData.ConstructorArguments[0].Value is string name)
			return name;

		// If the name is not specified in the constructor arguments, check for a named argument "Name".
		return GetStringNamedArgument(attributeData, "Name");
	}

	static int GetCtorIndex(AttributeData attributeData)
	{
		if (attributeData.ConstructorArguments.Length > 0 && attributeData.ConstructorArguments[0].Value is int index)
			return index;

		// If the index is not specified in the constructor arguments, check for a named argument "Index".
		return GetIntNamedArgument(attributeData, "Index", -1);
	}

	static object? GetEffectiveDefaultValue(
		AttributeData? nestedModelAttribute,
		AttributeData? typeArgumentAttribute,
		AttributeData? argumentAttribute,
		AttributeData? propertyAttribute,
		bool hasExclusive
	)
	{
		object? defaultValue = null;
		if (nestedModelAttribute is not null)
			defaultValue = GetObjectNamedArgument(nestedModelAttribute, "DefaultValue");
		else if (typeArgumentAttribute is not null)
			defaultValue = GetObjectNamedArgument(typeArgumentAttribute, "DefaultValue");

		if (argumentAttribute is not null && !hasExclusive)
		{
			var argumentDefault = GetObjectNamedArgument(
				argumentAttribute,
				"DefaultValue",
				argumentAttribute.ConstructorArguments.Length > 1
					? argumentAttribute.ConstructorArguments[1].Value
					: null
			);
			if (argumentDefault is not null)
				defaultValue = argumentDefault;
		}

		if (propertyAttribute is not null && !hasExclusive)
		{
			var propertyDefault = GetObjectNamedArgument(
				propertyAttribute,
				"DefaultValue",
				propertyAttribute.ConstructorArguments.Length > 0
					? propertyAttribute.ConstructorArguments[0].Value
					: null
			);
			if (propertyDefault is not null)
				defaultValue = propertyDefault;
		}

		return defaultValue;
	}

	static bool HasIsEnum(AttributeData? propertyAttribute, AttributeData? argumentAttribute) =>
		(propertyAttribute is not null && GetBoolNamedArgument(propertyAttribute, "IsEnum"))
		|| (argumentAttribute is not null && GetBoolNamedArgument(argumentAttribute, "IsEnum"));

	static bool IsDefaultValueEmittable(object? value, ITypeSymbol parameterType, INamedTypeSymbol? typedConstantType)
	{
		if (value is null)
			return true;

		if (value is string)
			return typedConstantType is null
				|| !SymbolEqualityComparer.Default.Equals(parameterType.OriginalDefinition, typedConstantType);

		if (value is bool)
			return true;

		if (value is ITypeSymbol)
			return true;

		if (parameterType.TypeKind == TypeKind.Enum)
			return true;

		// Check if the parameter type implements IFormattable for numeric types.
		return value is IFormattable;
	}

	static bool IsTypeIdentityType(ITypeSymbol typeSymbol, INamedTypeSymbol? typeIdentityType)
	{
		if (typeIdentityType is null)
			return false;

		var candidate = typeSymbol;
		if (
			candidate is INamedTypeSymbol nullableType
			&& nullableType.IsValueType
			&& nullableType.ContainingNamespace?.ToDisplayString() == "System"
			&& nullableType.Name == "Nullable"
			&& nullableType.TypeArguments.Length == 1
		)
		{
			candidate = nullableType.TypeArguments[0];
		}

		if (candidate is not INamedTypeSymbol namedType)
			return false;

		var namespaceName = namedType.ContainingNamespace.IsGlobalNamespace
			? null
			: namedType.ContainingNamespace.ToDisplayString();

		return namespaceName == typeIdentityType.ContainingNamespace.ToDisplayString()
			&& namedType.Name == typeIdentityType.Name;
	}

	static bool IsGeneratedAttributeModel(ITypeSymbol typeSymbol, INamedTypeSymbol generateAttributeType)
	{
		if (typeSymbol is not INamedTypeSymbol namedType || namedType.TypeKind != TypeKind.Struct)
			return false;

		// Check if the type has the GenerateAttribute applied.
		return GetAttribute(namedType, generateAttributeType) is not null;
	}

	static AttributeData? GetAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
	{
		if (attributeType is null)
			return null;

		foreach (var attribute in symbol.GetAttributes())
		{
			if (
				attribute.AttributeClass is not null
				&& SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)
			)
				return attribute;
		}

		return null;
	}

	static string? GetStringNamedArgument(AttributeData attributeData, string name)
	{
		foreach (var argument in attributeData.NamedArguments)
		{
			if (argument.Key == name && argument.Value.Value is string value)
				return value;
		}

		return null;
	}

	static int GetIntNamedArgument(AttributeData attributeData, string name, int defaultValue)
	{
		foreach (var argument in attributeData.NamedArguments)
		{
			if (argument.Key == name && argument.Value.Value is int value)
				return value;
		}

		return defaultValue;
	}

	static bool GetBoolNamedArgument(AttributeData attributeData, string name)
	{
		foreach (var argument in attributeData.NamedArguments)
		{
			if (argument.Key == name && argument.Value.Value is bool value)
				return value;
		}

		return false;
	}

	static object? GetObjectNamedArgument(AttributeData attributeData, string name, object? defaultValue = null)
	{
		foreach (var argument in attributeData.NamedArguments)
		{
			if (argument.Key == name)
				return argument.Value.Value;
		}

		return defaultValue;
	}
}
