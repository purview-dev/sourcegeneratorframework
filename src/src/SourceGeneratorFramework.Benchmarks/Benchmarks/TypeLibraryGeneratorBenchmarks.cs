using BenchmarkDotNet.Attributes;
using Purview.SourceGeneratorFramework.Generators;
using Purview.SourceGeneratorFramework.Testing;

namespace Purview.SourceGeneratorFramework.Benchmarks;

/// <summary>
/// Measures the <see cref="TypeLibraryGenerator"/> end-to-end. The generator walks the framework
/// <c>PurviewTypeLibrary</c> shape once per compilation (cached by the <c>GetFrameworkTypeLibraryTree</c>
/// stage) and merges the per-spec <c>[TypeRef]</c> members into it.
/// </summary>
[MemoryDiagnoser]
public class TypeLibraryGeneratorBenchmarks
{
	readonly SourceGeneratorTestRunner<TypeLibraryGenerator> _runner = new();
	string _source;

	[Params(1, 5, 20)]
	public int SpecCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		var builder = new System.Text.StringBuilder();
		builder.AppendLine("using Purview.SourceGeneratorFramework;");
		builder.AppendLine("using Purview.SourceGeneratorFramework.Generators;");
		builder.AppendLine("namespace Benchmarks;");

		for (var i = 0; i < SpecCount; i++)
		{
			builder.AppendLine(
				System.Globalization.CultureInfo.InvariantCulture,
				$"[GenerateTypeLibrary(ClassName = \"TypeLibrary{i}\")]"
			);
			builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"static partial class Spec{i}");
			builder.AppendLine("{");
			builder.AppendLine("    [TypeRef(\"System\")]");
			builder.AppendLine(
				System.Globalization.CultureInfo.InvariantCulture,
				$"    static readonly TypeIdentity MyAttribute{i} = default!;"
			);
			builder.AppendLine("}");
		}

		_source = builder.ToString();
	}

	[Benchmark]
	public async Task RunAsync()
	{
		var options = new SourceGeneratorTestOptions
		{
			CompileToAssembly = false,
			EnableLogging = false,
			ValidateCodeWriterScopes = false,
		};

		await _runner.RunAsync(_source, options);
	}
}
