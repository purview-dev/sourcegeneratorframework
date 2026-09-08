using Purview.SourceGeneratorFramework.Analyzers;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.CodeFixers;

public sealed class CodeWriterToStringCodeFixProviderTests
	: TUnitCodeFixTestBase<CodeWriterInStringContextAnalyzer, CodeWriterToStringCodeFixProvider>
{
	static CodeFixTestOptions Options =>
		new()
		{
			EquivalenceKey = CodeWriterToStringCodeFixProvider.EquivalenceKey,
			AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings), typeof(XmlCommentWriter)],
		};

	[Test]
	public async Task XmlCodeInInterpolation_UsesXmlInlineCode(CancellationToken cancellationToken)
	{
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

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(CodeWriterInStringContextAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("XmlCommentWriter.XmlInlineCode(\"value\")");
	}

	[Test]
	public async Task XmlCodeInConcatenation_UsesXmlInlineCode(CancellationToken cancellationToken)
	{
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

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(CodeWriterInStringContextAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("XmlCommentWriter.XmlInlineCode(\"value\")");
	}

	[Test]
	public async Task XmlCodeBlockInInterpolation_UsesXmlInlineCodeBlock(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					var text = $"code: {writer.XmlCodeBlock("var value = 1;")}";
				}
			}
			""";

		var result = await ApplyCodeFixAsync(source, Options, cancellationToken);

		await Assert.That(result).HasDiagnostic(CodeWriterInStringContextAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("XmlCommentWriter.XmlInlineCodeBlock(\"var value = 1;\")");
	}
}
