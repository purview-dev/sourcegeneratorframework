using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PipelineModelReferenceEqualityCollectionAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR15";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Pipeline model collection lacks sequence equality",
		"Pipeline model member '{0}' uses '{1}', which does not provide sequence equality for incremental caching. Use EquatableArray<T> or an equivalent value-equatable collection instead.",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Incremental source generator pipeline models should use value-equatable collections for members so that value equality compares contents rather than references.",
		customTags: WellKnownDiagnosticTags.CompilationEnd
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationAction(static context =>
		{
			var compilation = context.Compilation;
			var pipelineTypes = PipelineModelDiscovery.ResolvePipelineTypes(compilation);
			var collectionTypes = PipelineModelDiscovery.ResolveCollectionTypes(compilation);
			var referenceEqualityCollectionTypes = ResolveReferenceEqualityCollectionTypes(compilation);

			if (pipelineTypes.IsEmpty || referenceEqualityCollectionTypes.IsEmpty)
				return;

			var modelTypes = PipelineModelDiscovery.CollectPipelineModelTypes(
				compilation,
				pipelineTypes,
				collectionTypes
			);

			foreach (var modelType in modelTypes)
			{
				AnalyzeModelType(context, modelType, referenceEqualityCollectionTypes);
			}
		});
	}

	static ImmutableArray<INamedTypeSymbol> ResolveReferenceEqualityCollectionTypes(Compilation compilation)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		PipelineModelDiscovery.AddIfNotNull(
			builder,
			compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1")
		);
		PipelineModelDiscovery.AddIfNotNull(
			builder,
			compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")
		);
		return builder.ToImmutable();
	}

	static void AnalyzeModelType(
		CompilationAnalysisContext context,
		INamedTypeSymbol modelType,
		ImmutableArray<INamedTypeSymbol> referenceEqualityCollectionTypes
	)
	{
		foreach (var member in modelType.GetMembers())
		{
			if (member.IsImplicitlyDeclared)
				continue;

			var memberType = member switch
			{
				IFieldSymbol field => field.Type,
				IPropertySymbol property => property.Type,
				_ => null,
			};

			if (memberType is null)
				continue;

			if (
				memberType is IArrayTypeSymbol
				|| IsReferenceEqualityCollectionType(memberType, referenceEqualityCollectionTypes)
			)
			{
				ReportDiagnostic(context, member, memberType);
			}
		}
	}

	static bool IsReferenceEqualityCollectionType(
		ITypeSymbol typeSymbol,
		ImmutableArray<INamedTypeSymbol> referenceEqualityCollectionTypes
	)
	{
		if (typeSymbol is not INamedTypeSymbol namedType || !namedType.IsGenericType)
			return false;

		var originalDefinition = namedType.OriginalDefinition;
		foreach (var collectionType in referenceEqualityCollectionTypes)
		{
			if (SymbolEqualityComparer.Default.Equals(originalDefinition, collectionType))
				return true;
		}

		return false;
	}

	static void ReportDiagnostic(CompilationAnalysisContext context, ISymbol member, ITypeSymbol memberType)
	{
		var location = PipelineModelDiscovery.GetMemberLocation(member);
		if (location is null)
			return;

		context.ReportDiagnostic(
			Diagnostic.Create(
				Rule,
				location,
				member.Name,
				memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
			)
		);
	}
}
