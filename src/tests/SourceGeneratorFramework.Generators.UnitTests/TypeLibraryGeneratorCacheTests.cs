using System.Collections.Immutable;
using Purview.SourceGeneratorFramework.Generators.Helpers;
using StepReason = Microsoft.CodeAnalysis.IncrementalStepRunReason;

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

	static ImmutableDictionary<string, ImmutableArray<StepReason>> StepReasons(IncrementalCacheRun run)
	{
		var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<StepReason>>();
		foreach (var pair in run.Steps)
		{
			builder[pair.Key] = [.. pair.Value.SelectMany(step => step.Outputs.Select(static output => output.Reason))];
		}

		return builder.ToImmutable();
	}

	[Test]
	public async Task FirstRun_AllStagesAreNew(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source])],
			cancellationToken: cancellationToken
		);

		var reasons = StepReasons(result.Runs[0]);
		await Assert.That(reasons).IsNotEmpty();
		await Assert.That(reasons.Values.SelectMany(static r => r).All(static r => r == StepReason.New)).IsTrue();
	}

	[Test]
	public async Task IdenticalRerun_AllStagesCached(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync([Source], cancellationToken: cancellationToken);

		var second = StepReasons(result.Runs[1]);
		await Assert.That(second).IsNotEmpty();

		// The generator's own pipeline stages must all be cached or unchanged. (Roslyn's internal
		// ForAttributeWithMetadataName steps can report Modified on rerun because the post-initialization
		// attribute source is regenerated as a new tree.)
		string[] frameworkStages =
		[
			"GetTypeLibraryTargets",
			"GetGenerationConfiguration",
			"GetGenerationContext_EmptyCapabilities",
		];

		await Assert
			.That(
				frameworkStages.All(stage =>
					second.TryGetValue(stage, out var reasons)
					&& reasons.All(static r => r is StepReason.Cached or StepReason.Unchanged)
				)
			)
			.IsTrue();
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

		var second = StepReasons(result.Runs[1]);

		await Assert.That(second["GetGenerationConfiguration"]).Contains(StepReason.Modified);
		await Assert
			.That(second["GetTypeLibraryTargets"].All(static r => r is StepReason.Cached or StepReason.Unchanged))
			.IsTrue();
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

		var second = StepReasons(result.Runs[1]);

		await Assert.That(second["GetTypeLibraryTargets"]).Contains(StepReason.Modified);
		await Assert.That(second["GetGenerationConfiguration"].All(static r => r == StepReason.Cached)).IsTrue();
	}
}
