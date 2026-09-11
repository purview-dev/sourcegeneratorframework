using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class PreferTargetSymbolAnalyzerTests : TUnitDiagnosticAnalyzerTestBase<PreferTargetSymbolAnalyzer>
{
	[Test]
	public async Task GetDeclaredSymbolOnTargetNode_ReportsDiagnostic(CancellationToken cancellationToken)
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

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(PreferTargetSymbolAnalyzer.Rule.Id);
	}

	[Test]
	public async Task GetDeclaredSymbolOnOtherNode_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;

			public class C
			{
				public void M()
				{
					var model = default(SemanticModel);
					var node = default(Microsoft.CodeAnalysis.SyntaxNode);
					var symbol = model.GetDeclaredSymbol(node);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task TargetSymbol_DoesNotReportDiagnostic(CancellationToken cancellationToken)
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
						static (ctx, _) => ctx.TargetSymbol
					);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}
}
