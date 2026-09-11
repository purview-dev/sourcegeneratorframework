# Step-Cache Tests for Incremental Source Generators

Snapshot-testing generated source is not enough. A generator can produce perfectly correct code while
defeating almost all incremental caching: on every edit the driver re-runs every stage and regenerates
every output. The way to prove a generator caches correctly is to track the incremental pipeline steps and
assert which stages were recomputed (`Modified`/`New`) and which were reused (`Cached`/`Unchanged`) between
runs.

This page documents the framework's step-cache testing support:

- the `RunIncrementalAsync` runner and its tracked-step model;
- the tracking names every pipeline stage receives;
- the assertion API for stage-level reasons;
- the canonical sample to copy for your own generators.

The reference implementation is
`src/src/SourceGeneratorFramework.ExampleGenerator/`, and the canonical sample is
`src/tests/SourceGeneratorFramework.ExampleGenerator.UnitTests/StepCacheTests.cs`.

---

## How the runner works

`SourceGeneratorTestRunner<Generator>.RunIncrementalAsync(inputs, options)` runs one shared
`GeneratorDriver` over a sequence of `IncrementalRunInput`s (each with its own sources and optional
analyzer-config overrides):

```csharp
var result = await new SourceGeneratorTestRunner<ServiceRegistrationGenerator>().RunIncrementalAsync(
	[
		new IncrementalRunInput([firstSources]),
		new IncrementalRunInput([changedSources]),
	],
	options,
	cancellationToken
);
```

The driver is created with `GeneratorDriverOptions(IncrementalGeneratorOutputKind.None,
trackIncrementalGeneratorSteps: true)`, so each run's `TrackedSteps` are captured. Because the same driver
is reused and the same source set reuses the same compilation, the second run's step reasons tell you
exactly what the driver decided to recompute.

`GenerateIncrementalAsync` on `TUnitSourceGeneratorTestBase` wraps the same runner for test-base users.

## Step reasons

Each pipeline stage (tracked by its tracking name) has outputs, each carrying an
`IncrementalStepRunReason`:

| Reason       | Meaning                                                                 |
|--------------|-------------------------------------------------------------------------|
| `New`        | The step ran for the first time.                                        |
| `Modified`   | The step ran and produced a different output than the previous run.     |
| `Unchanged`  | The step ran but produced an output equal to the previous run.          |
| `Cached`     | The step did not run; its previous output was reused unchanged.         |

## Tracking names

The framework's pipeline helpers assign a tracking name to every stage. Cache tests reference these names:

| Helper                                            | Tracking name                            |
|---------------------------------------------------|------------------------------------------|
| `IncrementalPipeline.PropertyValueProvider`       | `GetMSBuildPropertyValue_{property}`     |
| `IncrementalPipeline.GenerationContextValueProvider` | `GetGenerationContext_{Capabilities}`  |
| `IncrementalPipeline` configuration provider      | `GetGenerationConfiguration`             |
| `IncrementalPipeline.ForAttributeWithMetadataName` | `ForAttribute_{AttributeName}`          |
| `RegisterSourceOutput` extension                  | `RegisterSourceOutput_{OutputType}`      |

Bundled generators add their own names, for example `GetAttributeDataTargets`,
`GetTypeLibraryTargets`, and `GetFrameworkTypeLibraryTree` (the cached framework
`PurviewTypeLibrary` shape). `WithTrackingName` can rename any stage.

## Assertion API

`IncrementalCacheRunExtensions.GetStepReasons()` flattens a run's tracked steps into
`ImmutableDictionary<string, ImmutableArray<IncrementalStepRunReason>>` keyed by tracking name.

TUnit assertion extensions on `IncrementalCacheRun` make the assertions fluent:

```csharp
await Assert.That(result.Runs[0]).AllStepsNew();
await Assert.That(result.Runs[1]).AllStepsCachedOrUnchanged();
await Assert.That(result.Runs[1]).StepIsModified("ForAttribute_GenerateServiceAttribute");
await Assert.That(result.Runs[1]).StepIsCached("GetGenerationConfiguration");
await Assert.That(result.Runs[1]).HasStepReason("ForAttribute_GenerateServiceAttribute", IncrementalStepRunReason.Unchanged);
```

- `AllStepsNew` — every tracked stage is `New` (a first run).
- `AllStepsCachedOrUnchanged` — every tracked stage was reused. Prefer asserting the generator's own
  stages with `StepIsCached` when the generator emits marker attributes via
  `RegisterPostInitializationOutput`: Roslyn's internal `ForAttributeWithMetadataName` steps can report
  `Modified` on an identical rerun because the post-initialization source is regenerated as a new tree.
- `StepIsModified(stage)` — the stage was recomputed and produced different output.
- `StepIsCached(stage)` — every output of the stage was `Cached` or `Unchanged`.
- `HasStepReason(stage, reason)` — the stage contains at least one output with the given reason.

## Golden test matrix

At minimum test:

1. **First run is all `New`** — proves every stage runs once.
2. **Identical rerun is cached** — proves value-equatable models short-circuit the pipeline.
3. **Unrelated source edit recomputes but stays unchanged** — proves model equality prevents regeneration.
4. **Editing one target invalidates only that target** — proves per-target incrementality
   (`ForAttributeWithMetadataName`).
5. **Changing one MSBuild/analyzer-config property invalidates only dependent stages** — proves
   configuration stages are independent of target stages.
6. **Deleting/renaming a target changes its output** — proves outputs are removed and hint names follow
   the model.

## Canonical sample

`src/tests/SourceGeneratorFramework.ExampleGenerator.UnitTests/StepCacheTests.cs` exercises the full matrix
against the reference generator:

```csharp
public class StepCacheTests : TUnitSourceGeneratorTestBase<ServiceRegistrationGenerator, ServiceRegistrationTestOptions>
{
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
	public async Task SingleTargetEdit_OnlyInvalidatesThatTarget(CancellationToken cancellationToken)
	{
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([Source]), new IncrementalRunInput([SingleTargetEditSource])],
			cancellationToken: cancellationToken
		);

		await Assert.That(result.Runs[1])
			.HasStepReason("ForAttribute_GenerateServiceAttribute", IncrementalStepRunReason.Modified);
		await Assert.That(result.Runs[1])
			.HasStepReason("ForAttribute_GenerateServiceAttribute", IncrementalStepRunReason.Unchanged);
	}
}
```

Mirror this pattern in every generator project; see the `*CacheTests` classes in
`SourceGeneratorShared.UnitTests`, `SourceGeneratorFramework.Generators.UnitTests`, and
`SourceGeneratorFramework.ExampleGenerator.UnitTests` for per-generator variants.