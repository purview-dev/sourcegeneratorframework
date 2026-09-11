using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Benchmarks;

/// <summary>
/// Compares re-resolving a symbol from its syntax node with <c>SemanticModel.GetDeclaredSymbol</c> against
/// reading the symbol that <c>ForAttributeWithMetadataName</c> has already resolved
/// (<c>GeneratorAttributeSyntaxContext.TargetSymbol</c>).
/// </summary>
[MemoryDiagnoser]
public class ForAttributeTransformBenchmarks
{
	Compilation _compilation;
	SemanticModel _semanticModel;
	ClassDeclarationSyntax _targetNode;
	ISymbol _preResolvedSymbol;

	[GlobalSetup]
	public void Setup()
	{
		const string source = """
			using System;

			namespace Benchmarks
			{
				[Obsolete]
				public class Target { }
			}
			""";

		var tree = CSharpSyntaxTree.ParseText(source);
		_compilation = CSharpCompilation.Create(
			"benchmarks",
			new[] { tree },
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]
		);
		_semanticModel = _compilation.GetSemanticModel(tree);
		_targetNode = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
		_preResolvedSymbol = _semanticModel.GetDeclaredSymbol(_targetNode);
	}

	[Benchmark(Baseline = true)]
	public ISymbol GetDeclaredSymbolFromNode() => _semanticModel.GetDeclaredSymbol(_targetNode);

	[Benchmark]
	public ISymbol PreResolvedTargetSymbol() => _preResolvedSymbol;
}
