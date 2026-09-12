#pragma warning disable CS1591
using System.Collections.Immutable;
using System.ComponentModel;

namespace Microsoft.CodeAnalysis;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class IncrementalValueProviderExtensions
{
	/// <summary>
	/// Combines a state provider with another value and immediately projects the pair into a
	/// named result, avoiding deeply nested tuple structures as a pipeline expands.
	/// </summary>
	public static IncrementalValueProvider<TResult> CombineWith<TState, TValue, TResult>(
		this IncrementalValueProvider<TState> stateProvider,
		IncrementalValueProvider<TValue> valueProvider,
		Func<TState, TValue, CancellationToken, TResult> selector,
		string? trackingName = null
	)
	{
		if (selector is null)
			throw new ArgumentNullException(nameof(selector));

		var result = stateProvider
			.Combine(valueProvider)
			.Select((pair, cancellationToken) => selector(pair.Left, pair.Right, cancellationToken));

		return string.IsNullOrWhiteSpace(trackingName) ? result : result.WithTrackingName(trackingName!);
	}

	/// <summary>
	/// Combines every item from a values provider with a single value and immediately projects
	/// each pair, preserving independent per-item incrementality.
	/// </summary>
	public static IncrementalValuesProvider<TResult> CombineWith<TState, TValue, TResult>(
		this IncrementalValuesProvider<TState> stateProvider,
		IncrementalValueProvider<TValue> valueProvider,
		Func<TState, TValue, CancellationToken, TResult> selector,
		string? trackingName = null
	)
	{
		if (selector is null)
			throw new ArgumentNullException(nameof(selector));

		var result = stateProvider
			.Combine(valueProvider)
			.Select((pair, cancellationToken) => selector(pair.Left, pair.Right, cancellationToken));

		return string.IsNullOrWhiteSpace(trackingName) ? result : result.WithTrackingName(trackingName!);
	}

	/// <summary>
	/// Combines every item from a values provider with a single state value and immediately
	/// projects each pair, preserving independent per-item incrementality.
	/// </summary>
	public static IncrementalValuesProvider<TResult> CombineWith<TState, TValue, TResult>(
		this IncrementalValueProvider<TState> stateProvider,
		IncrementalValuesProvider<TValue> valuesProvider,
		Func<TState, TValue, CancellationToken, TResult> selector,
		string? trackingName = null
	)
	{
		return selector is null
			? throw new ArgumentNullException(nameof(selector))
			: valuesProvider.CombineWith(
				stateProvider,
				(value, state, cancellationToken) => selector(state, value, cancellationToken),
				trackingName
			);
	}

	/// <summary>
	/// Collects a values provider and immediately projects its immutable array together with an
	/// existing state, making additional pipeline inputs straightforward to add.
	/// </summary>
	public static IncrementalValueProvider<TResult> CollectWith<TState, TValue, TResult>(
		this IncrementalValueProvider<TState> stateProvider,
		IncrementalValuesProvider<TValue> valuesProvider,
		Func<TState, ImmutableArray<TValue>, CancellationToken, TResult> selector,
		string? trackingName = null
	)
	{
		return selector is null
			? throw new ArgumentNullException(nameof(selector))
			: stateProvider.CombineWith(valuesProvider.Collect(), selector, trackingName);
	}

	/// <summary>
	/// Collects all items from a values provider, combines the resulting immutable array with a
	/// single value, and projects both into one aggregate result.
	/// </summary>
	public static IncrementalValueProvider<TResult> CollectWith<TState, TValue, TResult>(
		this IncrementalValuesProvider<TState> stateProvider,
		IncrementalValueProvider<TValue> valueProvider,
		Func<ImmutableArray<TState>, TValue, CancellationToken, TResult> selector,
		string? trackingName = null
	)
	{
		return selector is null
			? throw new ArgumentNullException(nameof(selector))
			: stateProvider.Collect().CombineWith(valueProvider, selector, trackingName);
	}

	/// <summary>
	/// Collects all items from two values providers and projects both immutable arrays into one
	/// aggregate result. This is an aggregate operation rather than a Cartesian product.
	/// </summary>
	public static IncrementalValueProvider<TResult> CollectWith<TLeft, TRight, TResult>(
		this IncrementalValuesProvider<TLeft> leftProvider,
		IncrementalValuesProvider<TRight> rightProvider,
		Func<ImmutableArray<TLeft>, ImmutableArray<TRight>, CancellationToken, TResult> selector,
		string? trackingName = null
	)
	{
		return selector is null
			? throw new ArgumentNullException(nameof(selector))
			: leftProvider.Collect().CombineWith(rightProvider.Collect(), selector, trackingName);
	}

	/// <summary>
	/// Combines a values provider with a single value provider, returning a values provider of tuples.
	/// </summary>
	public static IncrementalValuesProvider<(TOutput Output, TContext Context)> CombineWithContext<TOutput, TContext>(
		this IncrementalValuesProvider<TOutput> valuesProvider,
		IncrementalValueProvider<TContext> contextProvider
	)
		where TOutput : notnull
	{
		return valuesProvider.CombineWith(
			contextProvider,
			static (output, generationContext, _) => (output, generationContext)
		);
	}
}
