using Purview.SourceGeneratorFramework.Analyzers;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.CodeFixers;

public sealed class ReorganizeExtensionClassCodeFixProviderTests
	: TUnitCodeFixTestBase<ExtensionClassOrganizationAnalyzer, ReorganizeExtensionClassCodeFixProvider>
{
	static CodeFixTestOptions Options =>
		new() { EquivalenceKey = ReorganizeExtensionClassCodeFixProvider.RenameEquivalenceKey };

	static CodeFixTestOptions SplitOptions =>
		new() { EquivalenceKey = ReorganizeExtensionClassCodeFixProvider.SplitEquivalenceKey };

	[Test]
	public async Task MisnamedClass_IsRenamed(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Microsoft.CodeAnalysis;

			public static class TypedConstants
			{
				extension(TypedConstant constant)
				{
					public string? ToString() => constant.ToString();
				}
			}
			""";

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.NameDiagnosticId);
		await Assert.That(result.FixedSource).Contains("class TypedConstantExtensions");
		await Assert.That(result.FixedSource).DoesNotContain("class TypedConstants");
	}

	[Test]
	public async Task AcronymClassName_IsRenamedToCanonicalForm(CancellationToken cancellationToken)
	{
		const string source = """
			public struct SQL { }

			public static class SQLExtensions
			{
				extension(SQL value)
				{
					public string Format() => value.ToString();
				}
			}
			""";

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.NameDiagnosticId);
		await Assert.That(result.FixedSource).Contains("class SqlExtensions");
		await Assert.That(result.FixedSource).DoesNotContain("class SQLExtensions");
	}

	[Test]
	public async Task MixedCaseApi_IsRenamedToAPIExtensions(CancellationToken cancellationToken)
	{
		const string source = """
			public struct API { }

			public static class ApiExtensions
			{
				extension(API value)
				{
					public string Format() => value.ToString();
				}
			}
			""";

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.NameDiagnosticId);
		await Assert.That(result.FixedSource).Contains("class APIExtensions");
		await Assert.That(result.FixedSource).DoesNotContain("class ApiExtensions");
	}

	[Test]
	public async Task MultipleReceivers_RemovesMovedBlockFromOriginal(CancellationToken cancellationToken)
	{
		// The class is correctly named and placed for its primary receiver (TypedConstant), so the only
		// diagnostic is the split, and the split code action is the one registered.
		const string source = """
			namespace Microsoft.CodeAnalysis;

			public static class TypedConstantExtensions
			{
				extension(TypedConstant constant)
				{
					public string? ToString() => constant.ToString();
				}

				extension(ISymbol symbol)
				{
					public string? Name() => symbol.Name;
				}
			}
			""";

		var result = await ApplyCodeFixAsync(source, SplitOptions, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.SplitDiagnosticId);
		// The original document keeps the primary receiver's block.
		await Assert.That(result.FixedSource).Contains("extension(TypedConstant constant)");
		// The moved block was extracted into a new document.
		await Assert.That(result.FixedSource).DoesNotContain("extension(ISymbol symbol)");
		await Assert.That(result.FixedSource).DoesNotContain("public string? Name()");

		var splitDocument = result
			.ChangedSolution!.Projects.SelectMany(static project => project.Documents)
			.FirstOrDefault(document => document.Name == "ISymbolExtensions.cs");
		await Assert.That(splitDocument).IsNotNull();

		if (splitDocument is not null)
		{
			var splitSource = (await splitDocument.GetTextAsync(cancellationToken)).ToString();
			await Assert.That(splitSource).Contains("class ISymbolExtensions");
			await Assert.That(splitSource).Contains("extension(ISymbol symbol)");
			await Assert.That(splitSource).Contains("namespace Microsoft.CodeAnalysis");
		}
	}
}
