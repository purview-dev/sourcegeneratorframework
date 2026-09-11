using Purview.SourceGeneratorFramework.Examples;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.ExampleGenerator;

/// <summary>
/// Proves the <see cref="ServiceRegistrationGenerator"/> pipeline caches correctly stage-by-stage. Other
/// generator projects should mirror this pattern using <c>SourceGeneratorTestRunner.RunIncrementalAsync</c> or
/// <c>GenerateIncrementalAsync</c>.
/// </summary>
public class ServiceRegistrationCacheTests
	: TUnitSourceGeneratorTestBase<ServiceRegistrationGenerator, ServiceRegistrationTestOptions>
{
	const string Source = """
		namespace Test;

		[GenerateService]
		public class MyService { }
		""";

	[Test]
	public async Task FirstRun_AllStagesAreNew(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source])],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[0]).AllStepsNew();
	}

	[Test]
	public async Task IdenticalRerun_AllStagesCached(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync([Source], cancellationToken: cancellationToken);

		// The generator's own pipeline stages must all be cached or unchanged. (Roslyn's internal
		// ForAttributeWithMetadataName steps can report Modified on rerun because the post-initialization
		// attribute source is regenerated as a new tree.)
		await Assert.That(result.Runs[1]).StepIsCached("GetMSBuildPropertyValue_EmitServiceRegistrationInfo");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationConfiguration");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationContext_EmptyCapabilities");
		await Assert.That(result.Runs[1]).StepIsCached("ForAttribute_GenerateServiceAttribute");
	}

	[Test]
	public async Task PropertyChange_MarksPropertyStageModified_AttributeStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		var result = await GenerateIncrementalAsync(
			[
				new IncrementalRunInput([Source]),
				new IncrementalRunInput([Source], [(PropertyLibrary.EmitServiceRegistrationInfo, "true")]),
			],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("GetMSBuildPropertyValue_EmitServiceRegistrationInfo");
		await Assert.That(result.Runs[1]).StepIsCached("ForAttribute_GenerateServiceAttribute");
	}

	[Test]
	public async Task SourceChange_MarksAttributeStageModified_PropertyStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		const string changedSource = """
			namespace Test;

			[GenerateService(ServiceLifetime.Transient, Name = "Other")]
			public class OtherService { }
			""";

		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([changedSource])],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("ForAttribute_GenerateServiceAttribute");
		await Assert.That(result.Runs[1]).StepIsCached("GetMSBuildPropertyValue_EmitServiceRegistrationInfo");
	}
}
