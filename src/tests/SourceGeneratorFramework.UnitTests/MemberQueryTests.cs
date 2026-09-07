namespace Purview.SourceGeneratorFramework;

public class MemberQueryTests
{
	const string Source = """
		namespace Test;

		public class ComplexType { }

		public sealed class Sample
		{
			public Sample() { }
			public Sample(int id) { }

			public int Count { get; set; }
			public string Name { get; set; } = "";

			public string this[int index] => "value";

			public void DoWork(int value, int? optional, ComplexType complex) { }
			public int Compute(int left, int right) => left + right;
			public string Format(string format, object? value) => "";
		}
		""";

	static CodeQuery CreateQuery()
	{
		var (compilation, _) = TestCompilation.CreateWithRoot(Source);

		return new([.. compilation.SyntaxTrees], compilation);
	}

	static readonly TypeReference IntType = TypeReference.Create<int>();
	static readonly TypeReference StringType = TypeReference.Create<string>();
	static readonly TypeReference NullableIntType = TypeReference.Create<int>().Nullable();
	static readonly TypeReference ComplexType = new(new TypeIdentity("ComplexType", "Test"));

	[Test]
	public async Task Class_HasMethod_MatchesParameterTypes()
	{
		var query = CreateQuery();
		var cls = query.GetClass("Sample");

		await Assert.That(cls.HasMethod("DoWork", IntType, NullableIntType, ComplexType)).IsTrue();
		await Assert.That(cls.HasMethod("DoWork", IntType, IntType)).IsFalse();
		await Assert.That(cls.HasMethod("Missing")).IsFalse();
		await Assert.That(cls.GetMethod("Compute").HasParameters(IntType, IntType)).IsTrue();
	}

	[Test]
	public async Task Class_HasMethodReturnType_Matches()
	{
		var query = CreateQuery();
		var cls = query.GetClass("Sample");

		await Assert.That(cls.HasMethodReturnType("Compute", IntType)).IsTrue();
		await Assert.That(cls.HasMethodReturnType("Compute", StringType)).IsFalse();
		await Assert.That(cls.GetMethod("Compute").HasReturnType(IntType)).IsTrue();
	}

	[Test]
	public async Task Class_HasProperty_MatchesType()
	{
		var query = CreateQuery();
		var cls = query.GetClass("Sample");

		await Assert.That(cls.HasProperty("Count")).IsTrue();
		await Assert.That(cls.HasProperty("Count", IntType)).IsTrue();
		await Assert.That(cls.HasProperty("Count", StringType)).IsFalse();
		await Assert.That(cls.GetProperty("Name").HasType(StringType)).IsTrue();
	}

	[Test]
	public async Task Class_HasIndexer_MatchesTypeAndIndexParameters()
	{
		var query = CreateQuery();
		var cls = query.GetClass("Sample");

		await Assert.That(cls.HasIndexer()).IsTrue();
		await Assert.That(cls.HasIndexer(StringType)).IsTrue();
		await Assert.That(cls.HasIndexer(StringType, IntType)).IsTrue();
		await Assert.That(cls.HasIndexer(IntType, IntType)).IsFalse();
		await Assert.That(cls.GetIndexer().HasType(StringType)).IsTrue();
	}

	[Test]
	public async Task Class_HasConstructor_MatchesParameterTypes()
	{
		var query = CreateQuery();
		var cls = query.GetClass("Sample");

		await Assert.That(cls.HasConstructor()).IsTrue();
		await Assert.That(cls.HasConstructor(IntType)).IsTrue();
		await Assert.That(cls.HasConstructor(StringType)).IsFalse();
		await Assert.That(cls.GetConstructor(IntType).Node.ParameterList.Parameters.Count).IsEqualTo(1);
	}

	[Test]
	public async Task GetClass_GivenNamespace_FiltersByNamespace()
	{
		var query = CreateQuery();

		await Assert.That(query.HasClass("Sample", "Test")).IsTrue();
		await Assert.That(query.HasClass("Sample", "Other")).IsFalse();
		await Assert.That(query.HasClass("Sample")).IsTrue();
		await Assert
			.That(() => query.GetClass("Sample", "Other"))
			.Throws<SyntaxNotFoundException>()
			.WithMessageContaining("Other", StringComparison.Ordinal);
	}
}
