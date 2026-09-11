using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class AvoidSymbolsInPipelineModelAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<AvoidSymbolsInPipelineModelAnalyzer>
{
	[Test]
	public async Task ModelRetainingSymbol_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;

			public class MyGenerator : IIncrementalGenerator
			{
				public void Initialize(IncrementalGeneratorInitializationContext context)
				{
					var pipeline = GetPipeline(context);
				}

				IncrementalValuesProvider<MyModel> GetPipeline(IncrementalGeneratorInitializationContext context) =>
					context.SyntaxProvider.ForAttributeWithMetadataName(
						"System.ObsoleteAttribute",
						static (node, _) => true,
						static (ctx, _) => new MyModel(ctx.TargetSymbol)
					);
			}

			public sealed record MyModel(ISymbol Symbol);
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(AvoidSymbolsInPipelineModelAnalyzer.Rule.Id);
	}

	[Test]
	public async Task ModelWithValueMembers_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;

			public class MyGenerator : IIncrementalGenerator
			{
				public void Initialize(IncrementalGeneratorInitializationContext context)
				{
					var pipeline = GetPipeline(context);
				}

				IncrementalValuesProvider<MyModel> GetPipeline(IncrementalGeneratorInitializationContext context) =>
					context.SyntaxProvider.ForAttributeWithMetadataName(
						"System.ObsoleteAttribute",
						static (node, _) => true,
						static (ctx, _) => new MyModel(ctx.TargetSymbol.Name)
					);
			}

			public sealed record MyModel(string Name);
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task NonPipelineTypeRetainingSymbol_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;

			public sealed record NotAModel(ISymbol Symbol);
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}
}
