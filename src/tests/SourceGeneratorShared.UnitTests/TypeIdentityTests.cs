using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

public sealed class TypeIdentityTests
{
	// ---------------------------------------------------------------------------------------------
	// Keyword / special types
	// ---------------------------------------------------------------------------------------------

	[Test]
	[Arguments("System.Int32")]
	[Arguments("System.String")]
	[Arguments("System.Boolean")]
	[Arguments("System.Object")]
	public async Task Matches_GivenKeywordType_ReturnsTrue(string metadataName)
	{
		var compilation = TestCompilation.Create();
		var symbol = compilation.GetTypeByMetadataName(metadataName)!;
		var value = new TypeIdentity(symbol);

		await Assert.That(value.Matches(symbol)).IsTrue();
		await Assert.That(value.SpecialType).IsNotEqualTo(SpecialType.None);
	}

	[Test]
	public async Task Matches_GivenKeywordTypeDeclaredByName_ReturnsTrue()
	{
		var compilation = TestCompilation.Create();
		var symbol = compilation.GetTypeByMetadataName("System.Int32")!;

		// Constructed without keyword knowledge, so SpecialType is None on this side.
		var value = new TypeIdentity("Int32", "System");

		await Assert.That(value.SpecialType).IsEqualTo(SpecialType.None);
		await Assert.That(value.Matches(symbol)).IsTrue();
	}

	// ---------------------------------------------------------------------------------------------
	// Regression: non-keyword special types must not be rejected
	// ---------------------------------------------------------------------------------------------

	[Test]
	[Arguments("System.DateTime")]
	[Arguments("System.IDisposable")]
	[Arguments("System.Array")]
	[Arguments("System.Enum")]
	[Arguments("System.ValueType")]
	[Arguments("System.Delegate")]
	[Arguments("System.Collections.IEnumerable")]
	public async Task Matches_GivenNonKeywordSpecialType_ReturnsTrue(string metadataName)
	{
		var compilation = TestCompilation.Create();
		var symbol = compilation.GetTypeByMetadataName(metadataName)!;

		// Guards the premise: Roslyn stamps SpecialType well beyond the C# keyword types.
		await Assert.That(symbol.SpecialType).IsNotEqualTo(SpecialType.None);

		var fromSymbol = new TypeIdentity(symbol);
		var fromName = new TypeIdentity(symbol.Name, symbol.ContainingNamespace.ToDisplayString());

		await Assert.That(fromSymbol.Matches(symbol)).IsTrue();
		await Assert.That(fromName.Matches(symbol)).IsTrue();
	}

	[Test]
	public async Task Matches_GivenOpenGenericSpecialTypeDefinition_ReturnsTrue()
	{
		var compilation = TestCompilation.Create();
		var definition = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1")!;
		var constructed = definition.Construct(compilation.GetSpecialType(SpecialType.System_Int32));

		var value = new TypeIdentity("IEnumerable", "System.Collections.Generic") with { GenericArity = 1 };

		await Assert.That(definition.SpecialType).IsNotEqualTo(SpecialType.None);
		await Assert.That(value.Matches(definition)).IsTrue();
		await Assert.That(value.Matches(constructed)).IsTrue();
	}

	// ---------------------------------------------------------------------------------------------
	// Regression: composed symbols must not throw
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Matches_GivenArrayPointerOrDynamicSymbol_ReturnsFalseWithoutThrowing()
	{
		var compilation = TestCompilation.Create();
		var int32 = compilation.GetSpecialType(SpecialType.System_Int32);
		var value = TypeIdentity.Create<int>();

		await Assert.That(value.Matches(compilation.CreateArrayTypeSymbol(int32))).IsFalse();
		await Assert.That(value.Matches(compilation.CreatePointerTypeSymbol(int32))).IsFalse();
		await Assert.That(value.Matches(compilation.DynamicType)).IsFalse();
	}

	[Test]
	public async Task Matches_GivenErrorType_ReturnsFalse()
	{
		var symbol = TestCompilation.FieldType("public Missing.Thing Value;");

		await Assert.That(symbol.TypeKind).IsEqualTo(TypeKind.Error);
		await Assert.That(new TypeIdentity("Thing", "Missing").Matches(symbol)).IsFalse();
		await Assert.That(TypeIdentity.TryCreate(symbol, out _)).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Nested types
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Matches_GivenNestedType_DoesNotMatchTopLevelTypeOfSameName()
	{
		var compilation = TestCompilation.Create(
			"""
			namespace Sample
			{
				public class Outer { public class Inner { } }
				public class Inner { }
			}
			"""
		);

		var nested = compilation.GetTypeByMetadataName("Sample.Outer+Inner")!;
		var topLevel = compilation.GetTypeByMetadataName("Sample.Inner")!;

		var nestedValue = new TypeIdentity(nested);
		var topLevelValue = new TypeIdentity(topLevel);

		await Assert.That(nestedValue.Matches(nested)).IsTrue();
		await Assert.That(nestedValue.Matches(topLevel)).IsFalse();
		await Assert.That(topLevelValue.Matches(nested)).IsFalse();
		await Assert.That(topLevelValue.Matches(topLevel)).IsTrue();
	}

	[Test]
	public async Task MetadataFullName_GivenNestedType_UsesPlusSeparator()
	{
		var value = new TypeIdentity("Outer", "Sample").Nested("Inner");

		await Assert.That(value.IsNested).IsTrue();
		await Assert.That(value.MetadataFullName).IsEqualTo("Sample.Outer+Inner");
		await Assert.That(value.RenderFullName).IsEqualTo("global::Sample.Outer.Inner");
	}

	[Test]
	public async Task Nested_RoundTripsThroughSymbol()
	{
		var compilation = TestCompilation.Create(
			"namespace Sample { public class Outer { public class Middle { public class Inner { } } } }"
		);

		var symbol = compilation.GetTypeByMetadataName("Sample.Outer+Middle+Inner")!;
		var value = new TypeIdentity("Outer", "Sample").Nested("Middle").Nested("Inner");

		await Assert.That(value.Matches(symbol)).IsTrue();
		await Assert.That(value).IsEqualTo(new TypeIdentity(symbol));
	}

	[Test]
	public async Task Matches_GivenNestedTypeInsideGenericContainer_ComparesContainerArity()
	{
		var compilation = TestCompilation.Create(
			"namespace Sample { public class Outer<T> { public class Inner { } } }"
		);

		var symbol = compilation.GetTypeByMetadataName("Sample.Outer`1+Inner")!;

		var correct = (new TypeIdentity("Outer", "Sample") with { GenericArity = 1 }).Nested("Inner");
		var wrongArity = new TypeIdentity("Outer", "Sample").Nested("Inner");

		await Assert.That(correct.Matches(symbol)).IsTrue();
		await Assert.That(wrongArity.Matches(symbol)).IsFalse();
		await Assert.That(correct.MetadataFullName).IsEqualTo("Sample.Outer`1+Inner");
	}

	// ---------------------------------------------------------------------------------------------
	// Generic shape
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Matches_GivenOpenDefinition_MatchesEveryConstruction()
	{
		var compilation = TestCompilation.Create();
		var list = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!;
		var value = new TypeIdentity("List", "System.Collections.Generic") with { GenericArity = 1 };

		await Assert.That(value.IsGenericTypeDefinition).IsTrue();
		await Assert.That(value.Matches(list)).IsTrue();
		await Assert
			.That(value.Matches(list.Construct(compilation.GetSpecialType(SpecialType.System_String))))
			.IsTrue();
	}

	[Test]
	public async Task Matches_GivenConstructedGeneric_RequiresMatchingArguments()
	{
		var compilation = TestCompilation.Create();
		var list = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!;

		var value = new TypeIdentity("List", "System.Collections.Generic").MakeGeneric(
			new TypeIdentity(SpecialType.System_Int32)
		);

		await Assert.That(value.Matches(list.Construct(compilation.GetSpecialType(SpecialType.System_Int32)))).IsTrue();
		await Assert
			.That(value.Matches(list.Construct(compilation.GetSpecialType(SpecialType.System_String))))
			.IsFalse();
		await Assert.That(value.Matches(list)).IsFalse();
	}

	[Test]
	public async Task Matches_GivenArityMismatch_ReturnsFalse()
	{
		var compilation = TestCompilation.Create();
		var dictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2")!;
		var value = new TypeIdentity("Dictionary", "System.Collections.Generic") with { GenericArity = 1 };

		await Assert.That(value.Matches(dictionary)).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Composed type arguments no longer widen
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task TypeArguments_GivenArrayArgument_ArePreserved()
	{
		var symbol = TestCompilation.FieldType("public List<int[]> Value = null!;");
		var value = new TypeIdentity(symbol);

		await Assert.That(value.IsGenericTypeDefinition).IsFalse();
		await Assert.That(value.TypeArguments.Length).IsEqualTo(1);
		await Assert.That(value.TypeArguments[0].IsArray).IsTrue();
		await Assert.That(value.RenderFullName).IsEqualTo("global::System.Collections.Generic.List<int[]>");

		await Assert.That(value.Matches(symbol)).IsTrue();
		await Assert.That(value.Matches(TestCompilation.FieldType("public List<int> Value = null!;"))).IsFalse();
		await Assert.That(value.Matches(TestCompilation.FieldType("public List<string[]> Value = null!;"))).IsFalse();
		await Assert.That(value.Matches(TestCompilation.FieldType("public List<int[,]> Value = null!;"))).IsFalse();
	}

	[Test]
	public async Task TypeArguments_GivenTypeParameterArgument_ArePreserved()
	{
		var symbol = TestCompilation.FieldType("public List<T> Value = null!;");
		var value = new TypeIdentity(symbol);

		await Assert.That(value.IsGenericTypeDefinition).IsFalse();
		await Assert.That(value.TypeArguments[0].Kind).IsEqualTo(TypeReferenceKind.TypeParameter);
		await Assert.That(value.TypeArguments[0].TypeParameterName).IsEqualTo("T");
		await Assert.That(value.RenderFullName).IsEqualTo("global::System.Collections.Generic.List<T>");

		await Assert.That(value.Matches(symbol)).IsTrue();
		await Assert.That(value.Matches(TestCompilation.FieldType("public List<int> Value = null!;"))).IsFalse();
	}

	[Test]
	public async Task TypeArguments_GivenNullableValueTypeArgument_ArePreserved()
	{
		var symbol = TestCompilation.FieldType("public List<int?> Value = null!;");
		var value = new TypeIdentity(symbol);

		await Assert.That(value.TypeArguments[0].IsNullable).IsTrue();
		await Assert.That(value.RenderFullName).IsEqualTo("global::System.Collections.Generic.List<int?>");
		await Assert.That(value.Matches(symbol)).IsTrue();
		await Assert.That(value.Matches(TestCompilation.FieldType("public List<int> Value = null!;"))).IsFalse();
	}

	[Test]
	public async Task TypeArguments_GivenNestedGenericArgument_ArePreserved()
	{
		var symbol = TestCompilation.FieldType("public Dictionary<string, List<int>> Value = null!;");
		var value = new TypeIdentity(symbol);

		await Assert.That(value.TypeArguments.Length).IsEqualTo(2);
		await Assert.That(value.Matches(symbol)).IsTrue();
		await Assert
			.That(value.Matches(TestCompilation.FieldType("public Dictionary<string, List<string>> Value = null!;")))
			.IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Namespace comparison
	// ---------------------------------------------------------------------------------------------

	[Test]
	[Arguments("System.Collections.Generic", true)]
	[Arguments("Collections.Generic", false)]
	[Arguments("System.Collections", false)]
	[Arguments("System.Collections.Generic.Extra", false)]
	[Arguments(null, false)]
	public async Task Matches_ComparesFullNamespace(string? @namespace, bool expected)
	{
		var compilation = TestCompilation.Create();
		var list = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!;
		var value = new TypeIdentity("List", @namespace) with { GenericArity = 1 };

		await Assert.That(value.Matches(list)).IsEqualTo(expected);
	}

	[Test]
	public async Task Matches_GivenGlobalNamespaceType_ReturnsTrue()
	{
		var compilation = TestCompilation.Create("public class Rootless { }");
		var symbol = compilation.GetTypeByMetadataName("Rootless")!;
		var value = new TypeIdentity("Rootless", null);

		await Assert.That(value.IsGlobalNamespace).IsTrue();
		await Assert.That(value.Matches(symbol)).IsTrue();
		await Assert.That(value.RenderFullName).IsEqualTo("Rootless");
	}

	// ---------------------------------------------------------------------------------------------
	// ISymbol matching
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Matches_GivenMemberSymbols_ResolvesTheirType()
	{
		var compilation = TestCompilation.Create(
			"""
			using System;

			namespace Sample;

			public class Holder
			{
				public Guid Field;
				public Guid Property { get; set; }
				public Guid Method(Guid parameter) => default;
				public event EventHandler? Event;
			}
			"""
		);

		var holder = compilation.GetTypeByMetadataName("Sample.Holder")!;
		var guid = new TypeIdentity("Guid", "System");

		await Assert.That(guid.Matches(holder.GetMembers("Field").Single())).IsTrue();
		await Assert.That(guid.Matches(holder.GetMembers("Property").Single())).IsTrue();
		await Assert.That(guid.Matches(holder.GetMembers("Method").Single())).IsTrue();

		var method = (IMethodSymbol)holder.GetMembers("Method").Single();
		await Assert.That(guid.Matches(method.Parameters[0])).IsTrue();

		var eventHandler = new TypeIdentity("EventHandler", "System");
		await Assert.That(eventHandler.Matches(holder.GetMembers("Event").OfType<IEventSymbol>().Single())).IsTrue();

		await Assert.That(guid.Matches(holder.GetMembers("Event").OfType<IEventSymbol>().Single())).IsFalse();
		await Assert.That(guid.Matches((ISymbol?)null)).IsFalse();
	}

	[Test]
	public async Task Matches_GivenNamespaceSymbol_ReturnsFalse()
	{
		var compilation = TestCompilation.Create();
		var @namespace = compilation.GlobalNamespace.GetNamespaceMembers().First();

		await Assert.That(new TypeIdentity("System", null).Matches(@namespace)).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Reflection parity
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Create_GivenRuntimeType_MatchesEquivalentSymbol()
	{
		var compilation = TestCompilation.Create();

		await Assert
			.That(TypeIdentity.Create<DateTime>().Matches(compilation.GetTypeByMetadataName("System.DateTime")))
			.IsTrue();
		await Assert
			.That(TypeIdentity.Create<Guid>().Matches(compilation.GetTypeByMetadataName("System.Guid")))
			.IsTrue();

		var listOfInt = compilation
			.GetTypeByMetadataName("System.Collections.Generic.List`1")!
			.Construct(compilation.GetSpecialType(SpecialType.System_Int32));

		await Assert.That(TypeIdentity.Create<List<int>>().Matches(listOfInt)).IsTrue();
		await Assert.That(TypeIdentity.Create<List<string>>().Matches(listOfInt)).IsFalse();
	}

	[Test]
	public async Task Create_GivenRuntimeTypeWithArrayArgument_DoesNotWiden()
	{
		var value = TypeIdentity.Create<List<int[]>>();

		await Assert.That(value.TypeArguments.Length).IsEqualTo(1);
		await Assert.That(value.TypeArguments[0].IsArray).IsTrue();
		await Assert.That(value.Matches(TestCompilation.FieldType("public List<int[]> Value = null!;"))).IsTrue();
		await Assert.That(value.Matches(TestCompilation.FieldType("public List<int> Value = null!;"))).IsFalse();
	}

	[Test]
	public async Task TryCreate_GivenUnrepresentableRuntimeType_ReturnsFalse()
	{
		await Assert.That(TypeIdentity.TryCreate(typeof(int[]), out _)).IsFalse();
		await Assert.That(TypeIdentity.TryCreate(typeof(int).MakeByRefType(), out _)).IsFalse();
		await Assert.That(TypeIdentity.TryCreate(typeof(List<>).GetGenericArguments()[0], out _)).IsFalse();
		await Assert.That(TypeIdentity.TryCreate((Type?)null, out _)).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Equality contract
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Equality_IsStructuralAndHashConsistent()
	{
		var left = new TypeIdentity("Outer", "Sample")
			.Nested("Inner")
			.MakeGeneric(new TypeIdentity(SpecialType.System_String));
		var right = new TypeIdentity("Outer", "Sample")
			.Nested("Inner")
			.MakeGeneric(new TypeIdentity(SpecialType.System_String));
		var different = new TypeIdentity("Outer", "Sample")
			.Nested("Inner")
			.MakeGeneric(new TypeIdentity(SpecialType.System_Int32));

		await Assert.That(left).IsEqualTo(right);
		await Assert.That(left.GetHashCode()).IsEqualTo(right.GetHashCode());
		await Assert.That(left).IsNotEqualTo(different);
	}

	[Test]
	public async Task Equality_DistinguishesComposedTypeArguments()
	{
		var listOfInt = new TypeIdentity("List", "System.Collections.Generic").MakeGeneric(
			new TypeIdentity(SpecialType.System_Int32)
		);

		var listOfIntArray = new TypeIdentity("List", "System.Collections.Generic").MakeGeneric(
			new TypeIdentity(SpecialType.System_Int32).MakeArray()
		);

		await Assert.That(listOfInt).IsNotEqualTo(listOfIntArray);
		await Assert.That(listOfIntArray.RenderFullName).IsEqualTo("global::System.Collections.Generic.List<int[]>");
	}

	[Test]
	public async Task MakeGeneric_GivenWrongArgumentCount_Throws()
	{
		var dictionary = new TypeIdentity("Dictionary", "System.Collections.Generic") with { GenericArity = 2 };

		await Assert
			.That(void () => _ = dictionary.MakeGeneric(new TypeIdentity(SpecialType.System_String)))
			.Throws<ArgumentException>();
	}

	// ---------------------------------------------------------------------------------------------
	// Arity
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Constructor_GivenArity_SetsGenericArity()
	{
		var value = new TypeIdentity("Func", "System", 1);

		await Assert.That(value.GenericArity).IsEqualTo(1);
		await Assert.That(value.IsGenericTypeDefinition).IsTrue();
		await Assert.That(value.RenderFullName).IsEqualTo("global::System.Func<>");
		await Assert.That(value.MetadataName).IsEqualTo("Func`1");
	}

	[Test]
	public async Task Constructor_GivenDefaultArity_PreservesNonGenericBehavior()
	{
		var value = new TypeIdentity("List", "System.Collections.Generic");

		await Assert.That(value.GenericArity).IsEqualTo(0);
		await Assert.That(value.IsGenericTypeDefinition).IsFalse();
		await Assert.That(value.RenderFullName).IsEqualTo("global::System.Collections.Generic.List");
		await Assert.That(value.MetadataName).IsEqualTo("List");
	}

	[Test]
	public async Task Constructor_GivenNegativeArity_Throws()
	{
		await Assert.That(() => new TypeIdentity("Func", "System", -1)).Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task WithArity_CreatesOpenDefinitionMatchingEveryConstruction()
	{
		var compilation = TestCompilation.Create();
		var func = compilation.GetTypeByMetadataName("System.Func`17")!;
		var value = new TypeIdentity("Func", "System", 1).WithArity(17);

		await Assert.That(value.GenericArity).IsEqualTo(17);
		await Assert.That(value.IsGenericTypeDefinition).IsTrue();
		await Assert.That(value.Matches(func)).IsTrue();
		await Assert.That(value.RenderFullName).IsEqualTo("global::System.Func<" + new string(',', 16) + ">");
		await Assert.That(value.MetadataName).IsEqualTo("Func`17");
	}

	[Test]
	public async Task WithArity_GivenNegativeArity_Throws()
	{
		await Assert
			.That(() => new TypeIdentity("Func", "System", 1).WithArity(-1))
			.Throws<ArgumentOutOfRangeException>();
	}

	// ---------------------------------------------------------------------------------------------
	// Null literal
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Null_RendersAsBareKeyword()
	{
		string converted = TypeIdentity.Null;

		await Assert.That(TypeIdentity.Null.RenderFullName).IsEqualTo("null");
		await Assert.That(TypeIdentity.Null.RenderTypeName).IsEqualTo("null");
		await Assert.That(TypeIdentity.Null.MetadataName).IsEqualTo("null");
		await Assert.That(converted).IsEqualTo("null");
	}

	[Test]
	public async Task Null_IsDistinctFromEmpty()
	{
		await Assert.That(TypeIdentity.Null).IsNotEqualTo(TypeIdentity.Empty);
		await Assert.That(TypeIdentity.Null.IsGenericTypeDefinition).IsFalse();
		await Assert.That(TypeIdentity.Null.IsGlobalNamespace).IsTrue();
	}

	[Test]
	public async Task Null_DoesNotMatchAnySymbol()
	{
		ITypeSymbol? noType = null;
		var symbol = TestCompilation.FieldType("public string Value = null!;");

		await Assert.That(TypeIdentity.Null.Matches(symbol)).IsFalse();
		await Assert.That(TypeIdentity.Null.Matches(noType)).IsFalse();
	}

	[Test]
	public async Task Equals_ConstructedGenericWithOpenGenericArgument_IsEqual()
	{
		var resourceKitBase = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2).MakeGeneric(
			new TypeIdentity("HostKitBase", "Purview.Aspire.ResourceKit", arity: 1),
			new TypeIdentity("RedisResourceKit", "Testing")
		);
		var other = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2).MakeGeneric(
			new TypeIdentity("HostKitBase", "Purview.Aspire.ResourceKit", arity: 1),
			new TypeIdentity("RedisResourceKit", "Testing")
		);

		await Assert.That(resourceKitBase.Equals(other)).IsTrue();
	}

	[Test]
	public async Task GetHashCode_ConstructedGenericWithOpenGenericArgument_DoesNotOverflow()
	{
		var resourceKitBase = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2).MakeGeneric(
			new TypeIdentity("HostKitBase", "Purview.Aspire.ResourceKit", arity: 1),
			new TypeIdentity("RedisResourceKit", "Testing")
		);

		await Assert.That(resourceKitBase.GetHashCode()).IsNotEqualTo(0);
	}

	[Test]
	public async Task ImplicitConversion_ConstructedGeneric_DoesNotOverflow()
	{
		var resourceKitBase = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2).MakeGeneric(
			new TypeIdentity("HostKitBase", "Purview.Aspire.ResourceKit", arity: 1).MakeGeneric(
				new TypeIdentity("TestingHostKit", "Testing.HostKitNamespace")
			),
			new TypeIdentity("RedisResourceKit", "Testing")
		);

		TypeIdentity? nullable = resourceKitBase;
		TypeReference? reference = nullable;
		await Assert.That(reference is not null).IsTrue();
		await Assert.That(reference!.Identity.Equals(resourceKitBase)).IsTrue();
	}

	[Test]
	public async Task EqualityWithNullAndEmpty_ConstructedGeneric_DoesNotOverflow()
	{
		var resourceKitBase = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2).MakeGeneric(
			new TypeIdentity("HostKitBase", "Purview.Aspire.ResourceKit", arity: 1),
			new TypeIdentity("RedisResourceKit", "Testing")
		);

		await Assert.That(resourceKitBase == TypeIdentity.Null).IsFalse();
		await Assert.That(resourceKitBase == TypeIdentity.Empty).IsFalse();
	}
}
