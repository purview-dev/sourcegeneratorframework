using Purview.SourceGeneratorFramework.Generators.Helpers;

namespace Purview.SourceGeneratorFramework.Generators;

/// <summary>
/// Proves the <see cref="TypeLibraryGenerator"/> pipeline caches correctly stage-by-stage, mirroring the
/// AttributeDataModelGeneratorCacheTests.
/// </summary>
public class TypeLibraryGeneratorCacheTests : TUnitSourceGeneratorTestBase<TypeLibraryGenerator, TypeLibraryTestOptions>
{
	const string Source = """
		using Purview.SourceGeneratorFramework;
		using Purview.SourceGeneratorFramework.Generators;

		namespace Test;

		[GenerateTypeLibrary]
		static partial class TypeLibraryModel
		{
			[TypeRef("Test")]
			static readonly TypeIdentity MyAttribute = default!;
		}
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
		await Assert.That(result.Runs[1]).StepIsCached("GetTypeLibraryTargets");
		await Assert.That(result.Runs[1]).StepIsCached("GetFrameworkTypeLibraryTree");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationConfiguration");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationContext_EmptyCapabilities");
	}

	[Test]
	public async Task ConfigChange_MarksConfigurationStagesModified_TargetStageStaysCached(
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
							SourceGeneratorBuildProperties.BuildProperty + PropertyLibrary.DisableTypeLibraryGenerator,
							"true"
						),
					]
				),
			],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("GetGenerationConfiguration");
		await Assert.That(result.Runs[1]).StepIsCached("GetTypeLibraryTargets");
		await Assert.That(result.Runs[1]).StepIsCached("GetFrameworkTypeLibraryTree");
	}

	[Test]
	public async Task SourceChange_MarksTargetStageModified_ConfigurationStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		const string changedSource = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary]
			static partial class TypeLibraryModel
			{
				[TypeRef("Test")]
				static readonly TypeIdentity OtherAttribute = default!;
			}
			""";

		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([changedSource])],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[1]).StepIsModified("GetTypeLibraryTargets");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationConfiguration");
	}
}
