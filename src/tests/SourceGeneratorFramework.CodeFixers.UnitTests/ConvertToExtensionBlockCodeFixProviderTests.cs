using Purview.SourceGeneratorFramework.Analyzers;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.CodeFixers;

public sealed class ConvertToExtensionBlockCodeFixProviderTests
	: TUnitCodeFixTestBase<PreferExtensionKeywordAnalyzer, ConvertToExtensionBlockCodeFixProvider>
{
	static CodeFixTestOptions Options =>
		new() { EquivalenceKey = ConvertToExtensionBlockCodeFixProvider.EquivalenceKey };

	[Test]
	public async Task SingleReceiver_ConvertsToExtensionBlock(CancellationToken cancellationToken)
	{
		const string source = """
			public static class StringExtensions
			{
				public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);
			}
			""";

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(PreferExtensionKeywordAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("extension(string value)");
		await Assert.That(result.FixedSource).Contains("public bool IsBlank() => string.IsNullOrWhiteSpace(value);");
	}

	[Test]
	public async Task MixedReceivers_ProducesMultipleExtensionBlocks(CancellationToken cancellationToken)
	{
		const string source = """
			public static class MixedExtensions
			{
				public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);
				public static bool IsPositive(this int value) => value > 0;
			}
			""";

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(PreferExtensionKeywordAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("extension(string value)");
		await Assert.That(result.FixedSource).Contains("extension(int value)");
		await Assert.That(result.FixedSource).DoesNotContain("this string");
		await Assert.That(result.FixedSource).DoesNotContain("this int");
	}

	[Test]
	public async Task NonExtensionMembers_ArePreserved(CancellationToken cancellationToken)
	{
		const string source = """
			public static class StringExtensions
			{
				public static string Separator => ",";

				public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);
			}
			""";

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result.FixedSource).Contains("public static string Separator => \",\";");
		await Assert.That(result.FixedSource).Contains("extension(string value)");
	}
}
