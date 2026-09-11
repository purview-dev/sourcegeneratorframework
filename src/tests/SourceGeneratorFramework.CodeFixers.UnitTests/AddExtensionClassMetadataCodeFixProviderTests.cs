using Purview.SourceGeneratorFramework.Analyzers;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.CodeFixers;

public sealed class AddExtensionClassMetadataCodeFixProviderTests
	: TUnitCodeFixTestBase<ExtensionClassMetadataAnalyzer, AddExtensionClassMetadataCodeFixProvider>
{
	static CodeFixTestOptions Options =>
		new() { EquivalenceKey = AddExtensionClassMetadataCodeFixProvider.EquivalenceKey };

	[Test]
	public async Task MissingMetadata_AddsAttributeUsingAndPragma(CancellationToken cancellationToken)
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

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassMetadataAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("[EditorBrowsable(EditorBrowsableState.Never)]");
		await Assert.That(result.FixedSource).Contains("using System.ComponentModel;");
		await Assert.That(result.FixedSource).Contains("#pragma warning disable CS1591");
	}

	[Test]
	public async Task PartialMetadata_OnlyAddsMissingParts(CancellationToken cancellationToken)
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

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassMetadataAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("#pragma warning disable CS1591");
		await Assert.That(result.FixedSource.Split("#pragma warning disable CS1591").Length).IsEqualTo(2);
		await Assert
			.That(result.FixedSource.Split("[EditorBrowsable(EditorBrowsableState.Never)]").Length)
			.IsEqualTo(2);
	}
}
