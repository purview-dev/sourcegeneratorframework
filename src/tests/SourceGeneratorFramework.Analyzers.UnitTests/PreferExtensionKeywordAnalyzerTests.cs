using Microsoft.CodeAnalysis.CSharp;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class PreferExtensionKeywordAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<PreferExtensionKeywordAnalyzer>
{
	[Test]
	public async Task ClassicExtensionMethod_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			public static class StringExtensions
			{
				public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(PreferExtensionKeywordAnalyzer.Rule.Id);
	}

	[Test]
	public async Task ExtensionBlock_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			public static class StringExtensions
			{
				extension(string value)
				{
					public bool IsBlank() => string.IsNullOrWhiteSpace(value);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task StaticMethodWithoutThis_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			public static class Helpers
			{
				public static string Format(string value) => value.ToUpperInvariant();
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task LanguageVersionBelow14_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			public static class StringExtensions
			{
				public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);
			}
			""";

		AnalyzerTestOptions options = new() { LanguageVersion = LanguageVersion.CSharp13 };
		var result = await AnalyzeAsync(source, options, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}
}
