using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class AvoidNormalizeWhitespaceAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<AvoidNormalizeWhitespaceAnalyzer>
{
	[Test]
	public async Task NormalizeWhitespace_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using Microsoft.CodeAnalysis;
			using Microsoft.CodeAnalysis.CSharp;
			using Microsoft.CodeAnalysis.CSharp.Syntax;

			public class Generator
			{
				public void M()
				{
					MemberDeclarationSyntax declaration = SyntaxFactory.ClassDeclaration("C");
					var text = declaration.NormalizeWhitespace().ToFullString();
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(AvoidNormalizeWhitespaceAnalyzer.Rule.Id);
	}

	[Test]
	public async Task NoNormalizeWhitespace_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			public class C
			{
				public void M()
				{
					var value = "hello";
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}
}
