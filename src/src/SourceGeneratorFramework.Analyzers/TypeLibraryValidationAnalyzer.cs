using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Reports type-library validation diagnostics (TLB0001-TLB0006, TLB0008-TLB0009) on the static classes
/// annotated with <c>Purview.SourceGeneratorFramework.Generators.GenerateTypeLibraryAttribute</c>. These
/// rules were moved out of the source generator so they run as standard IDE/build analyzers instead of
/// generator-reported diagnostics.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeLibraryValidationAnalyzer : DiagnosticAnalyzer
{
	public static DiagnosticDescriptor SpecNotStaticClass => TypeLibraryDiagnosticRules.SpecNotStaticClass;

	public static DiagnosticDescriptor MemberTypeInvalid => TypeLibraryDiagnosticRules.MemberTypeInvalid;

	public static DiagnosticDescriptor MemberTypeNotResolved => TypeLibraryDiagnosticRules.MemberTypeNotResolved;

	public static DiagnosticDescriptor DuplicateMember => TypeLibraryDiagnosticRules.DuplicateMember;

	public static DiagnosticDescriptor InvalidClassName => TypeLibraryDiagnosticRules.InvalidClassName;

	public static DiagnosticDescriptor InvalidNamespace => TypeLibraryDiagnosticRules.InvalidNamespace;

	public static DiagnosticDescriptor MemberAccessibilityInvalid =>
		TypeLibraryDiagnosticRules.MemberAccessibilityInvalid;

	public static DiagnosticDescriptor ReferenceMemberMissingInitializer =>
		TypeLibraryDiagnosticRules.ReferenceMemberMissingInitializer;

	public static DiagnosticDescriptor MarkerMissingDefaultInitializer =>
		TypeLibraryDiagnosticRules.MarkerMissingDefaultInitializer;

	public static DiagnosticDescriptor SpecMustBePartial => TypeLibraryDiagnosticRules.SpecMustBePartial;

	public static DiagnosticDescriptor SpecClassNameClashesWithGeneratedClass =>
		TypeLibraryDiagnosticRules.SpecClassNameClashesWithGeneratedClass;

	public static DiagnosticDescriptor SpecClassNameCollidesAcrossNamespaces =>
		TypeLibraryDiagnosticRules.SpecClassNameCollidesAcrossNamespaces;

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[
			SpecNotStaticClass,
			MemberTypeInvalid,
			MemberTypeNotResolved,
			DuplicateMember,
			InvalidClassName,
			InvalidNamespace,
			MemberAccessibilityInvalid,
			ReferenceMemberMissingInitializer,
			MarkerMissingDefaultInitializer,
			SpecMustBePartial,
			SpecClassNameClashesWithGeneratedClass,
			SpecClassNameCollidesAcrossNamespaces,
		];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(context =>
		{
			var generateTypeLibraryAttributeType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.Generators.GenerateTypeLibraryAttribute"
			);
			var typeRefAttributeType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.Generators.TypeRefAttribute"
			);
			var typeIdentityType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.TypeIdentity"
			);
			var typeReferenceType = context.Compilation.GetTypeByMetadataName(
				"Purview.SourceGeneratorFramework.TypeReference"
			);

			context.RegisterSymbolAction(
				context =>
					AnalyzeNamedType(
						context,
						generateTypeLibraryAttributeType,
						typeRefAttributeType,
						typeIdentityType,
						typeReferenceType
					),
				SymbolKind.NamedType
			);
		});
	}

	static void AnalyzeNamedType(
		SymbolAnalysisContext context,
		INamedTypeSymbol? generateTypeLibraryAttributeType,
		INamedTypeSymbol? typeRefAttributeType,
		INamedTypeSymbol? typeIdentityType,
		INamedTypeSymbol? typeReferenceType
	)
	{
		if (context.Symbol is not INamedTypeSymbol typeSymbol)
			return;

		if (typeSymbol.TypeKind != TypeKind.Class)
			return;

		if (generateTypeLibraryAttributeType is null)
			return;

		var generateAttribute = GetAttribute(typeSymbol, generateTypeLibraryAttributeType);
		if (generateAttribute is null)
			return;

		var typeLocation = typeSymbol.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;

		if (!typeSymbol.IsStatic)
			context.ReportDiagnostic(Diagnostic.Create(SpecNotStaticClass, typeLocation));

		if (!IsPartial(typeSymbol, context.CancellationToken))
			context.ReportDiagnostic(Diagnostic.Create(SpecMustBePartial, typeLocation, typeSymbol.Name));

		var className = GetNamedArgument(generateAttribute, "ClassName", (string?)null);
		var generatedClassName = className ?? "TypeLibrary";
		if (string.Equals(typeSymbol.Name, generatedClassName, StringComparison.Ordinal))
		{
			var generatedNamespace = GetNamedArgument(generateAttribute, "Namespace", (string?)null);
			var specNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
				? null
				: typeSymbol.ContainingNamespace.ToDisplayString();
			var sameNamespace = string.Equals(
				generatedNamespace ?? string.Empty,
				specNamespace ?? string.Empty,
				StringComparison.Ordinal
			);

			var descriptor = sameNamespace
				? SpecClassNameClashesWithGeneratedClass
				: SpecClassNameCollidesAcrossNamespaces;
			context.ReportDiagnostic(Diagnostic.Create(descriptor, typeLocation, typeSymbol.Name, generatedClassName));
		}
		if (className is not null && !SyntaxFacts.IsValidIdentifier(className))
			context.ReportDiagnostic(Diagnostic.Create(InvalidClassName, typeLocation, className));

		var outputNamespace = GetNamedArgument(generateAttribute, "Namespace", (string?)null);
		if (outputNamespace is not null && !IsValidNamespace(outputNamespace))
			context.ReportDiagnostic(Diagnostic.Create(InvalidNamespace, typeLocation, outputNamespace));

		Dictionary<string, HashSet<string>> memberNamesByPath = new(StringComparer.Ordinal);

		foreach (var field in typeSymbol.GetMembers().OfType<IFieldSymbol>())
		{
			AnalyzeMember(
				context,
				field,
				typeRefAttributeType,
				typeIdentityType,
				typeReferenceType,
				memberNamesByPath,
				typeLocation
			);
		}
	}

	static void AnalyzeMember(
		SymbolAnalysisContext context,
		IFieldSymbol field,
		INamedTypeSymbol? typeRefAttributeType,
		INamedTypeSymbol? typeIdentityType,
		INamedTypeSymbol? typeReferenceType,
		Dictionary<string, HashSet<string>> memberNamesByPath,
		Location typeLocation
	)
	{
		var typeRef = GetAttribute(field, typeRefAttributeType);
		if (typeRef is null)
			return;

		var memberLocation = field.Locations.FirstOrDefault(static location => location.IsInSource) ?? typeLocation;

		var isTypeIdentity =
			typeIdentityType is not null && SymbolEqualityComparer.Default.Equals(field.Type, typeIdentityType);
		var isTypeReference =
			typeReferenceType is not null && SymbolEqualityComparer.Default.Equals(field.Type, typeReferenceType);

		if (!isTypeIdentity && !isTypeReference)
		{
			context.ReportDiagnostic(Diagnostic.Create(MemberTypeInvalid, memberLocation, field.Name, field.Type));
			return;
		}

		string? placementNamespace;
		var hasRealInitializer = HasRealInitializer(field, context.CancellationToken);

		if (isTypeReference || (isTypeIdentity && hasRealInitializer))
		{
			// Value members (TypeReference or an initialised TypeIdentity) require a value and internal accessibility.
			if (!hasRealInitializer)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(ReferenceMemberMissingInitializer, memberLocation, field.Name)
				);
				return;
			}

			if (field.DeclaredAccessibility != Accessibility.Internal)
			{
				context.ReportDiagnostic(Diagnostic.Create(MemberAccessibilityInvalid, memberLocation, field.Name));
				return;
			}

			placementNamespace = GetPlacementNamespace(typeRef);
		}
		else
		{
			// Plain TypeIdentity markers require private accessibility and a resolvable type/namespace.
			if (field.DeclaredAccessibility != Accessibility.Private)
			{
				context.ReportDiagnostic(Diagnostic.Create(MemberAccessibilityInvalid, memberLocation, field.Name));
				return;
			}

			if (HasNoInitializer(field, context.CancellationToken))
				context.ReportDiagnostic(
					Diagnostic.Create(MarkerMissingDefaultInitializer, memberLocation, field.Name)
				);

			if (!TryResolveTypeRef(typeRef, field.Name, out _, out placementNamespace))
			{
				context.ReportDiagnostic(Diagnostic.Create(MemberTypeNotResolved, memberLocation, field.Name));
				return;
			}
		}

		var pathKey = placementNamespace ?? string.Empty;
		if (!memberNamesByPath.TryGetValue(pathKey, out var names))
		{
			names = new(StringComparer.Ordinal);
			memberNamesByPath[pathKey] = names;
		}

		if (!names.Add(field.Name))
			context.ReportDiagnostic(Diagnostic.Create(DuplicateMember, memberLocation, field.Name));
	}

	/// <summary>
	/// Resolves the placement namespace for a value member from the <c>[TypeRef]</c> namespace argument
	/// or named property.
	/// </summary>
	static string? GetPlacementNamespace(AttributeData typeRef) =>
		GetNamedArgument(typeRef, "Namespace", (string?)null)
		?? GetConstructorArgument(typeRef, 0, (string?)null)
		?? GetConstructorArgument(typeRef, 1, (string?)null);

	static bool HasRealInitializer(IFieldSymbol field, CancellationToken cancellationToken)
	{
		foreach (var reference in field.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator)
				return !IsDefaultExpression(declarator.Initializer?.Value);
		}

		return false;
	}

	/// <summary>
	/// Determines whether the field declares no initializer at all, rather than an explicit
	/// <c>= default</c> marker.
	/// </summary>
	static bool HasNoInitializer(IFieldSymbol field, CancellationToken cancellationToken)
	{
		foreach (var reference in field.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator)
				return declarator.Initializer is null;
		}

		return false;
	}

	/// <summary>
	/// Determines whether every declaration of the spec type carries the <c>partial</c> modifier, so
	/// the generated <c>TypeRefMarkers</c> partial can be merged into it.
	/// </summary>
	static bool IsPartial(INamedTypeSymbol symbol, CancellationToken cancellationToken)
	{
		var hasDeclarations = false;
		foreach (var reference in symbol.DeclaringSyntaxReferences)
		{
			hasDeclarations = true;
			if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax declaration)
				return false;

			if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
				return false;
		}

		return hasDeclarations;
	}

	static bool IsDefaultExpression(ExpressionSyntax? expression) =>
		expression switch
		{
			null => true,
			LiteralExpressionSyntax { RawKind: (int)SyntaxKind.DefaultLiteralExpression } => true,
			PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppression =>
				IsDefaultExpression(suppression.Operand),
			DefaultExpressionSyntax => true,
			_ => false,
		};

	static bool TryResolveTypeRef(
		AttributeData typeRef,
		string fieldName,
		out string? typeName,
		out string? memberNamespace
	)
	{
		typeName = null;
		memberNamespace = null;

		var namedNamespace = GetNamedArgument(typeRef, "Namespace", (string?)null);
		var namedArity = GetNamedArgument(typeRef, "Arity", -1);

		// The namespace-only overload declares a single string parameter; when used, the type name
		// defaults to the member (field) name.
		var isNamespaceOnlyForm =
			typeRef.AttributeConstructor is { Parameters.Length: > 0 }
			&& typeRef.AttributeConstructor.Parameters[0].Type.SpecialType == SpecialType.System_String;

		if (isNamespaceOnlyForm)
		{
			typeName = fieldName;
			memberNamespace = namedNamespace ?? GetConstructorArgument(typeRef, 0, (string?)null);
			if (string.IsNullOrWhiteSpace(memberNamespace))
				return false;

			var arity = namedArity != -1 ? namedArity : GetConstructorArgument(typeRef, 1, 0);
			return arity >= 0;
		}

		if (typeRef.ConstructorArguments.Length == 0)
			return false;

#pragma warning disable format
		switch (typeRef.ConstructorArguments[0].Value)
		{
			case ITypeSymbol typeSymbol:
			{
				if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
					return false;

				typeName = typeSymbol.Name;
				memberNamespace =
					namedNamespace
					?? GetConstructorArgument(typeRef, 1, (string?)null)
					?? typeSymbol.ContainingNamespace.ToDisplayString();

				break;
			}
			case string typeNameString:
			{
				typeName = typeNameString;
				memberNamespace = namedNamespace ?? GetConstructorArgument(typeRef, 1, (string?)null);

				break;
			}
			default:
				return false;
		}
#pragma warning restore format

		if (string.IsNullOrWhiteSpace(memberNamespace))
			return false;

		var explicitArity = namedArity != -1 ? namedArity : GetConstructorArgument(typeRef, 2, -1);
		return explicitArity is >= 0 or -1;
	}

	static bool IsValidNamespace(string @namespace)
	{
		if (string.IsNullOrWhiteSpace(@namespace))
			return false;

		// A valid namespace is a series of valid identifiers separated by dots. Empty segments are not allowed.
		return @namespace.Split('.').All(segment => segment.Length > 0 && SyntaxFacts.IsValidIdentifier(segment));
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

	static T? GetConstructorArgument<T>(AttributeData attributeData, int index, T? defaultValue)
	{
		if (index < 0 || index >= attributeData.ConstructorArguments.Length)
			return defaultValue;

		var value = attributeData.ConstructorArguments[index].Value;
		if (value is T typedValue)
			return typedValue;
		if (value is null)
			return defaultValue;

		try
		{
			return (T?)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
		}
		catch
		{
			return defaultValue;
		}
	}

	static T? GetNamedArgument<T>(AttributeData attributeData, string name, T? defaultValue)
	{
		foreach (var arg in attributeData.NamedArguments)
		{
			if (arg.Key != name)
				continue;

			var value = arg.Value.Value;
			if (value is T typedValue)
				return typedValue;

			if (value is null)
				return defaultValue;

			try
			{
				return (T?)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
			}
			catch
			{
				return defaultValue;
			}
		}
		return defaultValue;
	}
}
