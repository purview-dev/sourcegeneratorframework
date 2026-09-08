using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class CodeWriterInStringContextAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<CodeWriterInStringContextAnalyzer>
{
	static AnalyzerTestOptions Options(params Type[] additionalAssemblyTypes) =>
		new() { AdditionalAssemblyTypes = [.. additionalAssemblyTypes] };

	[Test]
	public async Task XmlCodeCallInInterpolation_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					var text = $"the {writer.XmlCode("value")} namespace.";
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			Options([typeof(CodeWriter), typeof(GenerationSettings)]),
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(CodeWriterInStringContextAnalyzer.Rule.Id);
	}

	[Test]
	public async Task BareWriterInInterpolation_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					var text = $"source: {writer}";
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			Options([typeof(CodeWriter), typeof(GenerationSettings)]),
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(CodeWriterInStringContextAnalyzer.Rule.Id);
	}

	[Test]
	public async Task XmlCodeCallInConcatenation_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					var text = "the " + writer.XmlCode("value") + " namespace.";
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			Options([typeof(CodeWriter), typeof(GenerationSettings)]),
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(CodeWriterInStringContextAnalyzer.Rule.Id);
	}

	[Test]
	public async Task ExplicitToStringInInterpolation_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					var text = $"source: {writer.ToString()}";
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			Options([typeof(CodeWriter), typeof(GenerationSettings)]),
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task StaticInlineCodeInInterpolation_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					var text = $"the {XmlCommentWriter.XmlInlineCode("value")} namespace.";
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			Options([typeof(CodeWriter), typeof(GenerationSettings), typeof(XmlCommentWriter)]),
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task XmlSummaryAsStatement_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.XmlSummary("Represents the value.");
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			Options([typeof(CodeWriter), typeof(GenerationSettings)]),
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
	}
}
