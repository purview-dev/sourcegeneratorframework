using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;

namespace Purview.SourceGeneratorFramework.Benchmarks;

[MemoryDiagnoser]
public class EquatableArrayBenchmarks
{
	[Params(10, 100, 1000)]
	public int Count { get; set; }

	EquatableArray<int> _equatableArray1;
	EquatableArray<int> _equatableArray2;
	ImmutableArray<int> _immutableArray1;
	ImmutableArray<int> _immutableArray2;

	[GlobalSetup]
	public void Setup()
	{
		var values1 = Enumerable.Range(0, Count).ToArray();
		var values2 = Enumerable.Range(0, Count).ToArray();

		_equatableArray1 = EquatableArray<int>.Create(values1);
		_equatableArray2 = EquatableArray<int>.Create(values2);
		_immutableArray1 = [.. values1];
		_immutableArray2 = [.. values2];
	}

	[Benchmark(Baseline = true)]
	public bool EquatableArrayEquals() => _equatableArray1.Equals(_equatableArray2);

	[Benchmark]
	public int EquatableArrayGetHashCode() => _equatableArray1.GetHashCode();

	[Benchmark]
	public bool ImmutableArrayReferenceEquals() => _immutableArray1.Equals(_immutableArray2);

	[Benchmark]
	public bool ImmutableArraySequenceEqual() => _immutableArray1.SequenceEqual(_immutableArray2);
}
