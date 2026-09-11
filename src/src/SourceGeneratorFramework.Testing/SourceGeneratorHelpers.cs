using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Purview.SourceGeneratorFramework.Testing;

static class SourceGeneratorHelpers
{
	public static readonly string[] TrustedAssemblies = ResolveTrustedAssemblies();

	public static ImmutableArray<MetadataReference> ResolveTrustedReferences { get; } =
		CreateMetadataReferences(TrustedAssemblies);

	static readonly string? GeneratorAssemblyPath = typeof(TypeIdentity).Assembly.Location;
	static readonly ImmutableArray<MetadataReference> CachedFilteredReferences = FilterGeneratorAssembly(
		ResolveTrustedReferences,
		GeneratorAssemblyPath
	);

	public static CSharpCompilation CreateCompilation(
		IEnumerable<SyntaxTree> syntaxTrees,
		ImmutableArray<MetadataReference> references,
		SourceGeneratorTestOptions options
	)
	{
		return CSharpCompilation.Create(
			options.CompilationAssemblyName,
			syntaxTrees,
			references,
			new CSharpCompilationOptions(options.OutputKind).WithNullableContextOptions(options.NullableContextOptions)
		);
	}

	public static ImmutableArray<MetadataReference> ResolveReferences(
		SourceGeneratorTestOptions options,
		Assembly generatorAssembly
	)
	{
		if (generatorAssembly is null)
			throw new ArgumentNullException(nameof(generatorAssembly));

		if (options.AdditionalAssemblyTypes.IsDefaultOrEmpty && options.AdditionalReferences.IsDefaultOrEmpty)
			return CachedFilteredReferences;

		var builder = ImmutableArray.CreateBuilder<MetadataReference>(
			CachedFilteredReferences.Length
				+ options.AdditionalAssemblyTypes.Length
				+ options.AdditionalReferences.Length
		);

		builder.AddRange(CachedFilteredReferences);

		foreach (var type in options.AdditionalAssemblyTypes)
			builder.Add(MetadataReference.CreateFromFile(type.Assembly.Location));

		builder.AddRange(options.AdditionalReferences);

		var references = builder.ToImmutable();
		options.PreprocessReferences?.Invoke(references);
		return references;
	}

	public static string PrepareSource(string source, SourceGeneratorTestOptions options)
	{
		if (!options.IncludeDefaultNamespaces)
			return source;

		var namespaces = options.DefaultNamespaces.AddRange(options.AdditionalNamespaces);
		if (namespaces.IsDefaultOrEmpty)
			return source;

		StringBuilder builder = new(source.Length + (namespaces.Length * 20));
		foreach (var ns in namespaces)
		{
			builder.Append("using ").Append(ns).AppendLine(";");
		}
		builder.AppendLine();

		return builder.Append(source).ToString();
	}

	static string[] ResolveTrustedAssemblies()
	{
		var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(trusted))
			return trusted.Split([Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries);

		// .NET Framework does not populate TRUSTED_PLATFORM_ASSEMBLIES; fall back to the loaded
		// framework assemblies so test compilations still have their core references.
		return
		[
			.. AppDomain
				.CurrentDomain.GetAssemblies()
				.Where(static assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
				.Select(static assembly => assembly.Location)
				.Distinct(StringComparer.OrdinalIgnoreCase),
		];
	}

	static ImmutableArray<MetadataReference> CreateMetadataReferences(string[] paths)
	{
		if (paths.Length == 0)
			return [];

		var builder = ImmutableArray.CreateBuilder<MetadataReference>(paths.Length);
		foreach (var path in paths)
			builder.Add(MetadataReference.CreateFromFile(path));

		return builder.ToImmutable();
	}

	static ImmutableArray<MetadataReference> FilterGeneratorAssembly(
		ImmutableArray<MetadataReference> references,
		string? generatorAssemblyPath
	)
	{
		if (generatorAssemblyPath is null)
			return references;

		var builder = ImmutableArray.CreateBuilder<MetadataReference>(references.Length);
		foreach (var reference in references)
		{
			if (!string.Equals(reference.Display, generatorAssemblyPath, StringComparison.OrdinalIgnoreCase))
				builder.Add(reference);
		}

		return builder.ToImmutable();
	}
}
