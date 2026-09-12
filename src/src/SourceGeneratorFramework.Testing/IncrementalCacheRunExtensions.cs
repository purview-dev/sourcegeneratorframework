using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Extensions for reading and asserting the tracked incremental steps of a cache test run.
/// </summary>
public static class IncrementalCacheRunExtensions
{
	/// <summary>
	/// Flattens the tracked steps of a run into a map of tracking name to step reasons, proving whether each
	/// pipeline stage was recomputed (<see cref="IncrementalStepRunReason.Modified"/>) or reused
	/// (<see cref="IncrementalStepRunReason.Cached"/> / <see cref="IncrementalStepRunReason.Unchanged"/>).
	/// </summary>
	/// <param name="run">The run whose tracked steps should be flattened.</param>
	/// <returns>A map of tracking name to the reasons of each output in that stage.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
	public static ImmutableDictionary<string, ImmutableArray<IncrementalStepRunReason>> GetStepReasons(
		this IncrementalCacheRun run
	)
	{
		if (run is null)
			throw new ArgumentNullException(nameof(run));

		var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<IncrementalStepRunReason>>();
		foreach (var pair in run.Steps)
		{
			builder[pair.Key] = [.. pair.Value.SelectMany(step => step.Outputs.Select(static output => output.Reason))];
		}

		return builder.ToImmutable();
	}
}
