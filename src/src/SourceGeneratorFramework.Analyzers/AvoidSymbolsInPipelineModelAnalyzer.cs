using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags incremental generator pipeline model members that retain Roslyn objects (<c>ISymbol</c>,
/// <c>SyntaxNode</c>, <c>Location</c>, <c>AttributeData</c>, <c>SemanticModel</c>, <c>Compilation</c>, ...)
/// or collections thereof. Such objects are never equatable between runs, so they break incremental
/// caching and can root old compilations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidSymbolsInPipelineModelAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR33";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Pipeline model retains a Roslyn object",
		"Pipeline model member '{0}' uses '{1}', which is never equatable between runs and can root old compilations. Extract the information you need into strings or value types so incremental caching works.",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Pipeline models must not retain ISymbol, SyntaxNode, Location, or other Roslyn objects; extract the information needed into an equatable representation (strings usually work well).",
		customTags: WellKnownDiagnosticTags.CompilationEnd
	);

	static readonly string[] NonEquatableRoslynTypes =
	[
		"Microsoft.CodeAnalysis.ISymbol",
		"Microsoft.CodeAnalysis.SyntaxNode",
		"Microsoft.CodeAnalysis.SyntaxToken",
		"Microsoft.CodeAnalysis.SyntaxTrivia",
		"Microsoft.CodeAnalysis.Location",
		"Microsoft.CodeAnalysis.AttributeData",
		"Microsoft.CodeAnalysis.SemanticModel",
		"Microsoft.CodeAnalysis.Compilation",
		"Microsoft.CodeAnalysis.SyntaxTree",
		"Microsoft.CodeAnalysis.SyntaxReference",
		"Microsoft.CodeAnalysis.Text.SourceText",
		"Microsoft.CodeAnalysis.GeneratorAttributeSyntaxContext",
	];

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

			if (pipelineTypes.IsEmpty)
				return;

			var modelTypes = PipelineModelDiscovery.CollectPipelineModelTypes(
				compilation,
				pipelineTypes,
				collectionTypes
			);

			foreach (var modelType in modelTypes)
			{
				AnalyzeModelType(context, modelType);
			}
		});
	}

	static void AnalyzeModelType(CompilationAnalysisContext context, INamedTypeSymbol modelType)
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

			var retainedType = FindRetainedRoslynType(memberType);
			if (retainedType is null)
				continue;

			var location = PipelineModelDiscovery.GetMemberLocation(member);
			if (location is null)
				continue;

			context.ReportDiagnostic(
				Diagnostic.Create(
					Rule,
					location,
					member.Name,
					retainedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
				)
			);
		}
	}

	/// <summary>
	/// Returns the first non-equatable Roslyn type reachable from <paramref name="typeSymbol"/>, unwrapping
	/// arrays and generic collections.
	/// </summary>
	static INamedTypeSymbol? FindRetainedRoslynType(ITypeSymbol typeSymbol) =>
		typeSymbol switch
		{
			INamedTypeSymbol named => IsNonEquatableRoslynType(named) ? named : FindRetainedElementType(named),
			IArrayTypeSymbol array => FindRetainedRoslynType(array.ElementType),
			_ => null,
		};

	static INamedTypeSymbol? FindRetainedElementType(INamedTypeSymbol named)
	{
		if (!named.IsGenericType)
			return null;

		foreach (var typeArgument in named.TypeArguments)
		{
			if (FindRetainedRoslynType(typeArgument) is { } retained)
				return retained;
		}

		return null;
	}

	static bool IsNonEquatableRoslynType(INamedTypeSymbol typeSymbol)
	{
		var displayName = typeSymbol.ToDisplayString();
		foreach (var nonEquatableType in NonEquatableRoslynTypes)
		{
			if (displayName == nonEquatableType)
				return true;
		}

		return false;
	}
}
