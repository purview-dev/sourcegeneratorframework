using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class ExtensionClassOrganizationAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<ExtensionClassOrganizationAnalyzer>
{
	[Test]
	public async Task MisnamedClass_ReportsNameDiagnostic(CancellationToken cancellationToken)
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

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.NameDiagnosticId);
	}

	[Test]
	public async Task WrongNamespace_ReportsPlacementDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Purview.SourceGeneratorFramework;

			public static class TypedConstantExtensions
			{
				extension(TypedConstant constant)
				{
					public string? ToString() => constant.ToString();
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.PlacementDiagnosticId);
	}

	[Test]
	public async Task MultipleReceivers_ReportsSplitDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Microsoft.CodeAnalysis;

			public static class MixedExtensions
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

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.SplitDiagnosticId);
	}

	[Test]
	public async Task WellFormedExtensionClass_DoesNotReportDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Microsoft.CodeAnalysis;

			public static class TypedConstantExtensions
			{
				extension(TypedConstant constant)
				{
					public string? ToString() => constant.ToString();
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task NonExtensionClass_DoesNotReportDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Test;

			public static class Helpers
			{
				public static string Format(string value) => value.ToUpperInvariant();
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task AcronymClassName_IsReportedWithCanonicalName(CancellationToken cancellationToken)
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

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.NameDiagnosticId);
	}

	[Test]
	public async Task CanonicalAcronymClassName_DoesNotReportNameDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			public struct SQL { }

			public static class SqlExtensions
			{
				extension(SQL value)
				{
					public string Format() => value.ToString();
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).DoesNotHaveDiagnostic(ExtensionClassOrganizationAnalyzer.NameDiagnosticId);
	}

	[Test]
	public async Task CanonicalFrameworkType_IsNotRenamed(CancellationToken cancellationToken)
	{
		const string source = """
			namespace System;

			public static class GuidExtensions
			{
				extension(Guid value)
				{
					public string Format() => value.ToString();
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task APIStaysUppercase_CanonicalClassIsNotFlagged(CancellationToken cancellationToken)
	{
		const string source = """
			public struct API { }

			public static class APIExtensions
			{
				extension(API value)
				{
					public string Format() => value.ToString();
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task MixedCaseApi_IsReportedAsAPIExtensions(CancellationToken cancellationToken)
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

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ExtensionClassOrganizationAnalyzer.NameDiagnosticId);
	}

	[Test]
	public async Task AIStaysUppercase_CanonicalClassIsNotFlagged(CancellationToken cancellationToken)
	{
		const string source = """
			public struct AI { }

			public static class AIExtensions
			{
				extension(AI value)
				{
					public string Format() => value.ToString();
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}
}
