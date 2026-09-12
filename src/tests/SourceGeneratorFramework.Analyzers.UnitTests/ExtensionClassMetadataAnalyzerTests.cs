using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class ExtensionClassMetadataAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<ExtensionClassMetadataAnalyzer>
{
	[Test]
	public async Task ExtensionClassWithoutMetadata_ReportsDiagnostic(CancellationToken cancellationToken)
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

		await Assert.That(result).HasDiagnostic(ExtensionClassMetadataAnalyzer.Rule.Id);
	}

	[Test]
	public async Task ExtensionClassWithEditorBrowsableOnly_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using System.ComponentModel;

			[EditorBrowsable(EditorBrowsableState.Never)]
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
	public async Task ExtensionClassWithPragmaOnly_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			#pragma warning disable CS1591
			public static class StringExtensions
			{
				extension(string value)
				{
					public bool IsBlank() => string.IsNullOrWhiteSpace(value);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassMetadataAnalyzer.Rule.Id);
	}

	[Test]
	public async Task ExtensionClassWithFullMetadata_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			using System.ComponentModel;

			[EditorBrowsable(EditorBrowsableState.Never)]
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
	public async Task NonExtensionClass_DoesNotReportDiagnostic(CancellationToken cancellationToken)
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
}
