using System.ComponentModel;
using Microsoft.CodeAnalysis;
using TUnit.Assertions.Attributes;

namespace Purview.SourceGeneratorFramework.Testing.TUnit.Assertions;

/// <summary>
/// Contains assertion methods for <see cref="IncrementalCacheRun"/> and <see cref="IncrementalCacheResult"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class IncrementalCacheAssertions
{
	/// <summary>
	/// Asserts that a step with the given tracking name produced the expected run reason in this run.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[GenerateAssertion(ExpectationMessage = "step '{stage}' should have run reason {reason}")]
	public static bool HasStepReason(this IncrementalCacheRun run, string stage, IncrementalStepRunReason reason)
	{
		if (run is null)
			throw new ArgumentNullException(nameof(run));
		if (string.IsNullOrWhiteSpace(stage))
			throw new ArgumentException("Tracking name cannot be null or whitespace.", nameof(stage));

		var reasons = run.GetStepReasons();
		return reasons.TryGetValue(stage, out var stepReasons) && stepReasons.Contains(reason);
	}

	/// <summary>
	/// Asserts that every output of the step with the given tracking name was reused from the cache
	/// (<see cref="IncrementalStepRunReason.Cached"/> or <see cref="IncrementalStepRunReason.Unchanged"/>).
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[GenerateAssertion(ExpectationMessage = "step '{stage}' should have been cached or unchanged")]
	public static bool StepIsCached(this IncrementalCacheRun run, string stage)
	{
		if (run is null)
			throw new ArgumentNullException(nameof(run));
		if (string.IsNullOrWhiteSpace(stage))
			throw new ArgumentException("Tracking name cannot be null or whitespace.", nameof(stage));

		var reasons = run.GetStepReasons();
		return reasons.TryGetValue(stage, out var stepReasons)
			&& stepReasons.All(static reason =>
				reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged
			);
	}

	/// <summary>
	/// Asserts that the step with the given tracking name was recomputed
	/// (<see cref="IncrementalStepRunReason.Modified"/>).
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[GenerateAssertion(ExpectationMessage = "step '{stage}' should have been modified")]
	public static bool StepIsModified(this IncrementalCacheRun run, string stage)
	{
		if (run is null)
			throw new ArgumentNullException(nameof(run));
		if (string.IsNullOrWhiteSpace(stage))
			throw new ArgumentException("Tracking name cannot be null or whitespace.", nameof(stage));

		var reasons = run.GetStepReasons();
		return reasons.TryGetValue(stage, out var stepReasons)
			&& stepReasons.Contains(IncrementalStepRunReason.Modified);
	}

	/// <summary>
	/// Asserts that every tracked step of this run is <see cref="IncrementalStepRunReason.New"/> (a first run).
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[GenerateAssertion(ExpectationMessage = "all steps should be new")]
	public static bool AllStepsNew(this IncrementalCacheRun run)
	{
		if (run is null)
			throw new ArgumentNullException(nameof(run));

		var reasons = run.GetStepReasons();
		return reasons.Count > 0
			&& reasons.Values.All(static steps => steps.All(static r => r == IncrementalStepRunReason.New));
	}

	/// <summary>
	/// Asserts that every tracked step of this run was reused from the cache
	/// (<see cref="IncrementalStepRunReason.Cached"/> or <see cref="IncrementalStepRunReason.Unchanged"/>).
	/// Note that generators which emit marker attributes via <c>RegisterPostInitializationOutput</c> can have
	/// Roslyn-internal steps report <see cref="IncrementalStepRunReason.Modified"/> on an identical rerun
	/// because the post-initialization source is regenerated as a new tree; prefer asserting the generator's
	/// own stages with <see cref="StepIsCached"/> in that case.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[GenerateAssertion(ExpectationMessage = "all steps should be cached or unchanged")]
	public static bool AllStepsCachedOrUnchanged(this IncrementalCacheRun run)
	{
		if (run is null)
			throw new ArgumentNullException(nameof(run));

		var reasons = run.GetStepReasons();
		return reasons.Count > 0
			&& reasons.Values.All(static steps =>
				steps.All(static r => r is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged)
			);
	}
}
