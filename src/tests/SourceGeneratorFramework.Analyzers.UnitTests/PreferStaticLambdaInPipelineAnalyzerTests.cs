using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class PreferStaticLambdaInPipelineAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<PreferStaticLambdaInPipelineAnalyzer>
{
	[Test]
	public async Task NonStaticNonCapturingTransform_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;

			public class MyGenerator : IIncrementalGenerator
			{
				public void Initialize(IncrementalGeneratorInitializationContext context)
				{
					context.SyntaxProvider.ForAttributeWithMetadataName(
						"System.ObsoleteAttribute",
						static (node, _) => true,
						(ctx, _) => ctx.TargetSymbol.Name
					);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(PreferStaticLambdaInPipelineAnalyzer.Rule.Id);
	}

	[Test]
	public async Task StaticTransform_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;

			public class MyGenerator : IIncrementalGenerator
			{
				public void Initialize(IncrementalGeneratorInitializationContext context)
				{
					context.SyntaxProvider.ForAttributeWithMetadataName(
						"System.ObsoleteAttribute",
						static (node, _) => true,
						static (ctx, _) => ctx.TargetSymbol.Name
					);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task CapturingLambda_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;

			public class MyGenerator : IIncrementalGenerator
			{
				string _prefix;

				public void Initialize(IncrementalGeneratorInitializationContext context)
				{
					context.SyntaxProvider.ForAttributeWithMetadataName(
						"System.ObsoleteAttribute",
						static (node, _) => true,
						(ctx, _) => _prefix + ctx.TargetSymbol.Name
					);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task NonPipelineSelect_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using System.Collections.Generic;
			using System.Linq;

			public class C
			{
				public void M()
				{
					var items = new List<string>();
					var result = items.Select(x => x.Length);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}
}
