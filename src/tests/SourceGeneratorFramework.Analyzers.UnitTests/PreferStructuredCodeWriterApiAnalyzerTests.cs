using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class PreferStructuredCodeWriterAPIAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<PreferStructuredCodeWriterAPIAnalyzer>
{
	[Test]
	public async Task Line_WithClassDeclaration_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Line("public class C { }");
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterAPIAnalyzer.Rule.Id);
	}

	[Test]
	public async Task Line_WithStatement_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Line("return value;");
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task Line_WithUsingStatement_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Line("using (var stream = Open())");
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task MethodCall_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.MethodCall("Run", "value");
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task Line_WithInterpolatedClassDeclaration_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit(string name)
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Line($"public class {name} {{ }}");
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterAPIAnalyzer.Rule.Id);
	}

	[Test]
	public async Task Line_WithRawStringClassDeclaration_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """"
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Line("""public class C { }""");
				}
			}
			"""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterAPIAnalyzer.Rule.Id);
	}

	[Test]
	public async Task Line_WithConstClassDeclaration_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				const string Header = "public class C { }";

				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Line(Header);
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterAPIAnalyzer.Rule.Id);
	}

	[Test]
	public async Task Line_WithConcatenatedClassDeclaration_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Line("public " + "class C { }");
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterAPIAnalyzer.Rule.Id);
	}

	[Test]
	public async Task Line_WithInterpolatedStatement_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit(string value)
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Line($"return {value};");
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new AnalyzerTestOptions { AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)] },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
	}
}
