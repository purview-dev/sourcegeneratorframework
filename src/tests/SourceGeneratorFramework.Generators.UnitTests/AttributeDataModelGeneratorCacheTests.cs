using Purview.SourceGeneratorFramework.Generators.Helpers;

namespace Purview.SourceGeneratorFramework.Generators;

/// <summary>
/// Proves the <see cref="AttributeDataModelGenerator"/> pipeline caches correctly stage-by-stage, mirroring the
/// example generator's ServiceRegistrationCacheTests.
/// </summary>
public class AttributeDataModelGeneratorCacheTests
	: TUnitSourceGeneratorTestBase<AttributeDataModelGenerator, AttributeDataModelTestOptions>
{
	const string Source = """
		using Purview.SourceGeneratorFramework.Generators;

		namespace Test;

		[Generate(typeof(System.Attribute))]
		public readonly partial record struct AttributeData(bool Enabled);
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
		await Assert.That(result.Runs[1]).StepIsCached("GetAttributeDataTargets");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationConfiguration");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationContext_EmptyCapabilities");
	}

	[Test]
	public async Task ConfigChange_MarksConfigurationStagesModified_AttributeStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		var result = await GenerateIncrementalAsync(
			[
				new IncrementalRunInput([Source]),
				new IncrementalRunInput(
					[Source],
					[
						(
							SourceGeneratorBuildProperties.BuildProperty
								+ PropertyLibrary.DisableAttributeDataSourceGenerator,
							"true"
						),
					]
				),
			],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("GetGenerationConfiguration");
		await Assert.That(result.Runs[1]).StepIsCached("GetAttributeDataTargets");
	}

	[Test]
	public async Task SourceChange_MarksAttributeStageModified_ConfigurationStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		const string changedSource = """
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[Generate(typeof(System.Attribute))]
			public readonly partial record struct OtherAttributeData(bool Enabled, string? Name);
			""";

		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([changedSource])],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("GetAttributeDataTargets");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationConfiguration");
	}
}
