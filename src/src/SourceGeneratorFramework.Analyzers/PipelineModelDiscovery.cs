using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Shared discovery of incremental source generator pipeline model types: the value-equatable types that
/// flow through <c>GeneratorResult&lt;T&gt;</c>, <c>IncrementalValueProvider&lt;T&gt;</c> and
/// <c>IncrementalValuesProvider&lt;T&gt;</c>.
/// </summary>
static class PipelineModelDiscovery
{
	public static ImmutableArray<INamedTypeSymbol> ResolvePipelineTypes(Compilation compilation)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		AddIfNotNull(builder, compilation.GetTypeByMetadataName("Purview.SourceGeneratorFramework.GeneratorResult`1"));
		AddIfNotNull(builder, compilation.GetTypeByMetadataName("Microsoft.CodeAnalysis.IncrementalValuesProvider`1"));
		AddIfNotNull(builder, compilation.GetTypeByMetadataName("Microsoft.CodeAnalysis.IncrementalValueProvider`1"));
		return builder.ToImmutable();
	}

	public static ImmutableArray<INamedTypeSymbol> ResolveCollectionTypes(Compilation compilation)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		AddIfNotNull(builder, compilation.GetTypeByMetadataName("Purview.SourceGeneratorFramework.EquatableArray`1"));
		AddIfNotNull(builder, compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1"));
		return builder.ToImmutable();
	}

	public static void AddIfNotNull(ImmutableArray<INamedTypeSymbol>.Builder builder, INamedTypeSymbol? type)
	{
		if (type is not null)
			builder.Add(type);
	}

	public static HashSet<INamedTypeSymbol> CollectPipelineModelTypes(
		Compilation compilation,
		ImmutableArray<INamedTypeSymbol> pipelineTypes,
		ImmutableArray<INamedTypeSymbol> collectionTypes
	)
	{
		HashSet<INamedTypeSymbol> directModels = new(SymbolEqualityComparer.Default);

		foreach (var typeSymbol in GetAllTypes(compilation.GlobalNamespace))
		{
			foreach (var member in typeSymbol.GetMembers())
			{
				var memberType = GetMemberType(member);
				if (memberType is null)
					continue;

				CollectPipelineTypeArguments(memberType, pipelineTypes, directModels);
			}
		}

		HashSet<INamedTypeSymbol> expanded = new(SymbolEqualityComparer.Default);
		Queue<INamedTypeSymbol> worklist = new(directModels);

		while (worklist.Count > 0)
		{
			var modelType = worklist.Dequeue();
			if (!ShouldExpand(modelType) || !expanded.Add(modelType))
				continue;

			foreach (var member in modelType.GetMembers())
			{
				var memberType = GetMemberType(member);
				if (memberType is not INamedTypeSymbol namedMemberType)
					continue;

				if (namedMemberType.IsGenericType)
				{
					var originalDefinition = namedMemberType.OriginalDefinition;
					if (collectionTypes.Any(c => SymbolEqualityComparer.Default.Equals(originalDefinition, c)))
					{
						foreach (var typeArgument in namedMemberType.TypeArguments)
						{
							if (typeArgument is INamedTypeSymbol namedTypeArgument && ShouldExpand(namedTypeArgument))
								worklist.Enqueue(namedTypeArgument);
						}

						continue;
					}
				}

				if (ShouldExpand(namedMemberType))
					worklist.Enqueue(namedMemberType);
			}
		}

		return expanded;
	}

	public static bool ShouldExpand(INamedTypeSymbol typeSymbol) =>
		typeSymbol.Locations.Any(static loc => loc.IsInSource);

	public static void CollectPipelineTypeArguments(
		ITypeSymbol typeSymbol,
		ImmutableArray<INamedTypeSymbol> pipelineTypes,
		HashSet<INamedTypeSymbol> modelTypes
	)
	{
		if (typeSymbol is not INamedTypeSymbol namedType)
			return;

		if (namedType.IsGenericType)
		{
			var originalDefinition = namedType.OriginalDefinition;
			foreach (var pipelineType in pipelineTypes)
			{
				if (SymbolEqualityComparer.Default.Equals(originalDefinition, pipelineType))
				{
					foreach (var typeArgument in namedType.TypeArguments)
					{
						if (
							typeArgument is INamedTypeSymbol namedTypeArgument
							&& namedTypeArgument.Locations.Any(static loc => loc.IsInSource)
						)
							modelTypes.Add(namedTypeArgument);
					}
				}
			}

			foreach (var typeArgument in namedType.TypeArguments)
			{
				CollectPipelineTypeArguments(typeArgument, pipelineTypes, modelTypes);
			}
		}
	}

	public static ITypeSymbol? GetMemberType(ISymbol member)
	{
		return member switch
		{
			IFieldSymbol field => field.Type,
			IPropertySymbol property => property.Type,
			IParameterSymbol parameter => parameter.Type,
			IMethodSymbol method when method.AssociatedSymbol is null => method.ReturnType,
			IEventSymbol eventSymbol => eventSymbol.Type,
			_ => null,
		};
	}

	public static Location? GetMemberLocation(ISymbol member) =>
		member.Locations.FirstOrDefault(static loc => loc.IsInSource);

	public static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
	{
		foreach (var member in namespaceSymbol.GetMembers())
		{
			if (member is INamespaceSymbol nestedNamespace)
			{
				foreach (var type in GetAllTypes(nestedNamespace))
					yield return type;
			}
			else if (member is INamedTypeSymbol type)
			{
				yield return type;
				foreach (var nested in GetAllTypes(type))
					yield return nested;
			}
		}
	}

	public static IEnumerable<INamedTypeSymbol> GetAllTypes(INamedTypeSymbol typeSymbol)
	{
		foreach (var nested in typeSymbol.GetTypeMembers())
		{
			yield return nested;
			foreach (var deeper in GetAllTypes(nested))
				yield return deeper;
		}
	}
}
