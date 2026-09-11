using System.Collections;
using System.Collections.Immutable;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// An immutable, equatable wrapper around <see cref="ImmutableArray{T}"/> that is safe to use in incremental source generator pipelines.
/// </summary>
/// <typeparam name="T">The type of elements in the array. Must implement <see cref="IEquatable{T}"/>.</typeparam>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
	where T : IEquatable<T>
{
	public static readonly EquatableArray<T> Empty = new([]);

	readonly ImmutableArray<T> _array;
	readonly int _hashCode;

	public EquatableArray(ImmutableArray<T> array)
	{
		_array = array.IsDefault ? [] : array;
		// The hash is computed once at construction because EquatableArray values are compared repeatedly
		// while the incremental driver decides whether a pipeline stage's output changed. Caching it avoids
		// re-walking the elements on every GetHashCode call in that hot path.
		_hashCode = ComputeHash(_array);
	}

	public int Count => _array.IsDefault ? 0 : _array.Length;

	public bool IsEmpty => _array.IsDefaultOrEmpty;

	public T this[int index] => _array[index];

	public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? [] : _array;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static EquatableArray<T> Create(params T[] items)
	{
		if (items is null || items.Length == 0)
			return Empty;

		// All valid...
		return new(ImmutableArray.Create(items));
	}

	public bool Equals(EquatableArray<T> other) => AsImmutableArray().SequenceEqual(other.AsImmutableArray());

	public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

	public override int GetHashCode()
	{
		// A default(EquatableArray<T>) has no cached hash (0) and is equal to Empty, so it must fall back to
		// computing the hash over the normalized array. A real array whose computed hash is 0 recomputes on
		// each call; that is a rare edge and remains correct.
		return _hashCode != 0 ? _hashCode : ComputeHash(AsImmutableArray());
	}

	static int ComputeHash(ImmutableArray<T> array)
	{
		unchecked
		{
			var hash = 17;
			// Enumerate only the normalized array: a default ImmutableArray<T> cannot be enumerated and
			// would throw a NullReferenceException.
			var normalized = array.IsDefault ? [] : array;
			foreach (var item in normalized)
				hash = (hash * 31) + (item?.GetHashCode() ?? 0);

			return hash;
		}
	}

	public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public static implicit operator EquatableArray<T>(ImmutableArray<T> array) => new(array);

	public static implicit operator EquatableArray<T>(ImmutableArray<T>.Builder builder) =>
		builder is null || builder.Count == 0 ? Empty : new(builder.ToImmutable());

	public static implicit operator ImmutableArray<T>(EquatableArray<T> array) => array.AsImmutableArray();

	public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

	public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
