using Purview.SourceGeneratorFramework.Analyzers;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.CodeFixers;

public sealed class PreferTargetSymbolCodeFixProviderTests
	: TUnitCodeFixTestBase<PreferTargetSymbolAnalyzer, PreferTargetSymbolCodeFixProvider>
{
	static CodeFixTestOptions Options => new() { EquivalenceKey = PreferTargetSymbolCodeFixProvider.EquivalenceKey };

	[Test]
	public async Task GetDeclaredSymbol_UsesTargetSymbol(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;
			using Microsoft.CodeAnalysis.CSharp.Syntax;

			public class MyGenerator : IIncrementalGenerator
			{
				public void Initialize(IncrementalGeneratorInitializationContext context)
				{
					context.SyntaxProvider.ForAttributeWithMetadataName(
						"System.ObsoleteAttribute",
						static (node, _) => node is ClassDeclarationSyntax,
						static (ctx, ct) => ctx.SemanticModel.GetDeclaredSymbol(ctx.TargetNode, ct)
					);
				}
			}
			""";

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(PreferTargetSymbolAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("static (ctx, ct) => ctx.TargetSymbol");
		await Assert.That(result.FixedSource).DoesNotContain("GetDeclaredSymbol");
	}
}
