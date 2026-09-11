using Purview.SourceGeneratorFramework.TestGenerators;
using StepReason = Microsoft.CodeAnalysis.IncrementalStepRunReason;

namespace Purview.SourceGeneratorFramework;

public class IncrementalPipelineCacheTests
{
	const string AttributedSource = """
		[TestAttribute]
		public partial class MyClass { }
		""";

	const string ChangedAttributedSource = """
		[TestAttribute]
		public partial class AnotherClass { }
		""";

	const string TestAttributeSource = """
		[System.AttributeUsage(System.AttributeTargets.Class)]
		public sealed class TestAttribute : System.Attribute { }
		""";

	static SourceGeneratorTestOptions CreateOptions() =>
		new SourceGeneratorTestOptions()
			.WithAdditionalSources(TestAttributeSource)
			.WithExcludeGeneratedSourceHintNames("TestAttribute");

	[Test]
	public async Task FirstRun_AllStagesAreNew(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<TestGenerator> runner = new();

		var result = await runner.RunIncrementalAsync(
			[new IncrementalRunInput([AttributedSource])],
			CreateOptions(),
			cancellationToken
		);

		await Assert.That(result.Runs[0]).AllStepsNew();
	}

	[Test]
	public async Task IdenticalRerun_AllStagesCached(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<TestGenerator> runner = new();

		var result = await runner.RunIncrementalAsync([AttributedSource], CreateOptions(), cancellationToken);

		await Assert.That(result.Runs[1]).AllStepsCachedOrUnchanged();
		await Assert.That(result.Runs[0]).HasStepReason("ForAttribute_TestAttribute", StepReason.New);
	}

	[Test]
	public async Task SourceChange_MarksAttributeStageModified_PropertyStagesStayCached(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<TestGenerator> runner = new();

		var result = await runner.RunIncrementalAsync(
			[new IncrementalRunInput([AttributedSource]), new IncrementalRunInput([ChangedAttributedSource])],
			CreateOptions(),
			cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("ForAttribute_TestAttribute");
		await Assert.That(result.Runs[1]).StepIsCached("GetMSBuildPropertyValue_DisableTestGenerator");
	}

	[Test]
	public async Task PropertyChange_MarksPropertyStageModified_AttributeStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<TestGenerator> runner = new();

		var result = await runner.RunIncrementalAsync(
			[
				new IncrementalRunInput([AttributedSource]),
				new IncrementalRunInput([AttributedSource], [("build_property.DisableTestGenerator", "true")]),
			],
			CreateOptions(),
			cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("GetMSBuildPropertyValue_DisableTestGenerator");
		await Assert.That(result.Runs[1]).StepIsCached("ForAttribute_TestAttribute");
	}

	[Test]
	public async Task ConfigChange_MarksConfigurationStagesModified_AttributeStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<DiagnosticTestGenerator> runner = new();

		var result = await runner.RunIncrementalAsync(
			[
				new IncrementalRunInput([AttributedSource]),
				new IncrementalRunInput(
					[AttributedSource],
					[(SourceGeneratorBuildProperties.ValidateCodeWriterScopes, "false")]
				),
			],
			CreateOptions(),
			cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("GetGenerationConfiguration");
		await Assert.That(result.Runs[1]).StepIsModified("GetGenerationContext_EmptyCapabilities");
		await Assert.That(result.Runs[1]).StepIsCached("ForAttribute_TestAttribute");
	}
}
