using Microsoft.CodeAnalysis.CSharp;
using Purview.SourceGeneratorFramework.Helpers;

namespace Purview.SourceGeneratorFramework;

public class IncrementalPipelineLanguageVersionTests
{
	[Test]
	public async Task TryParseLanguageVersion_NumericForms_MapToCSharpValues()
	{
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("14")).IsEqualTo(LanguageVersion.CSharp14);
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("14.0")).IsEqualTo(LanguageVersion.CSharp14);
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("13")).IsEqualTo(LanguageVersion.CSharp13);
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("12.0")).IsEqualTo(LanguageVersion.CSharp12);
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("9")).IsEqualTo(LanguageVersion.CSharp9);
	}

	[Test]
	public async Task TryParseLanguageVersion_Keywords_MapToLatestAndPreview()
	{
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("latest")).IsEqualTo(LanguageVersion.LatestMajor);
		await Assert
			.That(IncrementalPipeline.TryParseLanguageVersion("latestMajor"))
			.IsEqualTo(LanguageVersion.LatestMajor);
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("preview")).IsEqualTo(LanguageVersion.Preview);
	}

	[Test]
	public async Task TryParseLanguageVersion_NamedValues_Parse()
	{
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("CSharp12")).IsEqualTo(LanguageVersion.CSharp12);
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("csharp10")).IsEqualTo(LanguageVersion.CSharp10);
	}

	[Test]
	public async Task TryParseLanguageVersion_UnknownAndEmpty_ReturnNull()
	{
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("not-a-version")).IsNull();
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("")).IsNull();
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion("  ")).IsNull();
		await Assert.That(IncrementalPipeline.TryParseLanguageVersion(null)).IsNull();
	}
}
