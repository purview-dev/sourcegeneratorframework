using Purview.SourceGeneratorFramework.Examples;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;
using Purview.SourceGeneratorFramework.Testing.TUnit.Assertions;

namespace Purview.SourceGeneratorFramework.ExampleGenerator;

public class TypeLibrarySampleGeneratorTests
	: TUnitSourceGeneratorTestBase<TypeLibrarySampleGenerator, TypeLibrarySampleTestOptions>
{
	[Test]
	public async Task GenerateSample_ExposesGeneratedTypeLibraryIdentities(CancellationToken cancellationToken)
	{
		const string source = "public sealed class UnrelatedType { }";

		var result = await GenerateAsync(source, cancellationToken);

		await Assert.That(result).HasGeneratedClass("TypeLibrarySample");

		var classText = (
			await result.Generated().GetSyntaxTree("TypeLibrarySample.g.cs").GetTextAsync(cancellationToken)
		).ToString();
		await Assert
			.That(classText)
			.Contains("const string SampleAttributeName = \"GenerateTypeLibrarySampleAttribute\"");
		await Assert.That(classText).Contains("const string DebugName = \"Debug\"");
		await Assert.That(classText).Contains("const string ILoggerName = \"ILogger\"");
		await Assert.That(classText).Contains("const string LogLevelName = \"LogLevel\"");
		await Assert.That(classText).Contains("const string EventIdName = \"EventId\"");
		await Assert.That(classText).Contains("const string LoggerMessageName = \"LoggerMessage\"");
		await Assert.That(classText).Contains("const string SampleItemsType = \"IEnumerable\"");
		await Assert.That(classText).Contains("const string InheritedStringName = \"String\"");
		await Assert.That(classText).Contains("const int LoggingTypesCount = 1");
	}
}
