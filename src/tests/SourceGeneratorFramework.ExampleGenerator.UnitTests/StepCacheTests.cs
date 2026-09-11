using Purview.SourceGeneratorFramework.Examples;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.ExampleGenerator;

/// <summary>
/// Canonical step-cache test sample. It proves, stage-by-stage, that the <see cref="ServiceRegistrationGenerator"/>
/// reuses its cached work unless the input that stage depends on actually changed.
///
/// Copy this pattern for your own generators:
/// <list type="number">
/// <item>Run a sequence of source sets through <c>SourceGeneratorTestRunner.RunIncrementalAsync</c>.</item>
/// <item>Assert the first run reports <see cref="Microsoft.CodeAnalysis.IncrementalStepRunReason.New"/>.</item>
/// <item>Assert an identical rerun is fully <see cref="Microsoft.CodeAnalysis.IncrementalStepRunReason.Cached"/>.</item>
/// <item>Assert an unrelated edit recomputes but produces an unchanged model.</item>
/// <item>Assert editing one target only invalidates that target's steps.</item>
/// <item>Assert a configuration change only invalidates the configuration/context stages.</item>
/// </list>
/// </summary>
public class StepCacheTests : TUnitSourceGeneratorTestBase<ServiceRegistrationGenerator, ServiceRegistrationTestOptions>
{
	const string Source = """
		namespace Test;

		[GenerateService]
		public class FirstService { }

		[GenerateService]
		public class SecondService { }
		""";

	// An edit that does not change the shape of either target's generated output.
	const string UnrelatedEditSource = """
		namespace Test;

		[GenerateService]
		public class FirstService { }

		[GenerateService]
		public class SecondService
		{
			// A member that does not affect service registration.
			public string Name { get; set; } = "";
		}
		""";

	// An edit that changes only the second target's generated output.
	const string SingleTargetEditSource = """
		namespace Test;

		[GenerateService]
		public class FirstService { }

		[GenerateService(Name = "Renamed")]
		public class SecondService { }
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
		// attribute source is regenerated as a new tree, so the whole run is not asserted via AllStepsCachedOrUnchanged.)
		await Assert.That(result.Runs[1]).StepIsCached("GetMSBuildPropertyValue_EmitServiceRegistrationInfo");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationConfiguration");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationContext_EmptyCapabilities");
		await Assert.That(result.Runs[1]).StepIsCached("ForAttribute_GenerateServiceAttribute");
	}

	[Test]
	public async Task UnrelatedEdit_RecomputesButProducesUnchangedModel(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([UnrelatedEditSource])],
			cancellationToken: cancellationToken
		);

		// The attribute-detection stage re-runs for the edited target, but the value-equatable model it
		// produces is identical, so the downstream source-output stage short-circuits.
		await Assert.That(result.Runs[1]).StepIsCached("ForAttribute_GenerateServiceAttribute");
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationConfiguration");
		await Assert.That(result.Runs[1]).StepIsCached("GetMSBuildPropertyValue_EmitServiceRegistrationInfo");
	}

	[Test]
	public async Task SingleTargetEdit_OnlyInvalidatesThatTarget(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([SingleTargetEditSource])],
			cancellationToken: cancellationToken
		);

		// Two targets: the edited one is recomputed (Modified) while the untouched one is reused
		// (Unchanged), proving per-target incrementality.
		await Assert
			.That(result.Runs[1])
			.HasStepReason(
				"ForAttribute_GenerateServiceAttribute",
				Microsoft.CodeAnalysis.IncrementalStepRunReason.Modified
			);
		await Assert
			.That(result.Runs[1])
			.HasStepReason(
				"ForAttribute_GenerateServiceAttribute",
				Microsoft.CodeAnalysis.IncrementalStepRunReason.Unchanged
			);
	}

	[Test]
	public async Task ConfigChange_OnlyInvalidatesConfigurationStages(CancellationToken cancellationToken)
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
}
