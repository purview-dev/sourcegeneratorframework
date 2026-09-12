using Purview.SourceGeneratorFramework.Analyzers;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.CodeFixers;

public sealed class PreferStaticLambdaCodeFixProviderTests
	: TUnitCodeFixTestBase<PreferStaticLambdaInPipelineAnalyzer, PreferStaticLambdaCodeFixProvider>
{
	static CodeFixTestOptions Options => new() { EquivalenceKey = PreferStaticLambdaCodeFixProvider.EquivalenceKey };

	[Test]
	public async Task NonStaticLambda_AddsStaticModifier(CancellationToken cancellationToken)
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

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(PreferStaticLambdaInPipelineAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("static (ctx, _) => ctx.TargetSymbol.Name");
	}
}
