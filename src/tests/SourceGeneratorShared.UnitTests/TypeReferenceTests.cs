using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

public sealed class TypeReferenceTests
{
	// ---------------------------------------------------------------------------------------------
	// Rendering
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task RenderFullName_RendersComposedSuffixesInSourceOrder()
	{
		TypeIdentity int32 = new(SpecialType.System_Int32);

		await Assert.That(int32.AsTypeReference().RenderFullName).IsEqualTo("int");
		await Assert.That(int32.MakeArray().RenderFullName).IsEqualTo("int[]");
		await Assert.That(int32.MakeArray(2).RenderFullName).IsEqualTo("int[,]");
		await Assert.That(int32.MakeNullable().RenderFullName).IsEqualTo("int?");
		await Assert.That(int32.MakeNullable().MakeArray().RenderFullName).IsEqualTo("int?[]");
		await Assert.That(int32.MakeArray().Nullable().RenderFullName).IsEqualTo("int[]?");
		await Assert.That(int32.MakePointer().MakeArray().RenderFullName).IsEqualTo("int*[]");

		// A run of array declarators reads outermost-first: a rank-1 array of rank-2 arrays.
		await Assert.That(int32.MakeArray(2).MakeArray(1).RenderFullName).IsEqualTo("int[][,]");
	}

	[Test]
	public async Task RenderFullName_GivenTypeParameterOrDynamic_RendersCore()
	{
		await Assert.That(TypeReference.ForTypeParameter("TKey").RenderFullName).IsEqualTo("TKey");
		await Assert.That(TypeReference.ForTypeParameter("TKey").MakeArray().RenderFullName).IsEqualTo("TKey[]");
		await Assert.That(TypeReference.Dynamic.RenderFullName).IsEqualTo("dynamic");
	}

	// ---------------------------------------------------------------------------------------------
	// Nullable-aware rendering
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task RenderFullNameForNullable_GivenDisabledContext_StripsReferenceAnnotationsOnly()
	{
		var @string = TypeIdentity.Create<string>();
		var @int = TypeIdentity.Create<int>();

		await Assert.That(@string.MakeNullable().RenderFullNameForNullable(false)).IsEqualTo("string");
		await Assert.That(@string.MakeNullable().RenderFullNameForNullable(true)).IsEqualTo("string?");
		await Assert.That(@int.MakeNullable().RenderFullNameForNullable(false)).IsEqualTo("int?");
		await Assert.That(@int.MakeNullable().RenderFullNameForNullable(true)).IsEqualTo("int?");
	}

	[Test]
	public async Task RenderFullNameForNullable_GivenArrayAnnotations_StripsThem()
	{
		var @string = TypeIdentity.Create<string>();

		await Assert.That(@string.MakeNullable().MakeArray().RenderFullNameForNullable(false)).IsEqualTo("string[]");
		await Assert
			.That(@string.MakeNullable().MakeArray().Nullable().RenderFullNameForNullable(false))
			.IsEqualTo("string[]");
		await Assert.That(@string.MakeArray().Nullable().RenderFullNameForNullable(false)).IsEqualTo("string[]");
	}

	[Test]
	public async Task RenderFullNameForNullable_GivenValueTypeArray_KeepsNullableElement()
	{
		var @int = TypeIdentity.Create<int>();

		await Assert.That(@int.MakeNullable().MakeArray().RenderFullNameForNullable(false)).IsEqualTo("int?[]");
	}

	[Test]
	public async Task RenderFullNameForNullable_GivenUnknownType_KeepsAnnotation()
	{
		var unknown = new TypeIdentity("MyClass", "Sample").MakeNullable();

		await Assert.That(unknown.RenderFullNameForNullable(false)).IsEqualTo("global::Sample.MyClass?");
	}

	[Test]
	public async Task RenderFullNameForNullable_ThreadsThroughGenericArguments()
	{
		var list = new TypeIdentity(typeof(List<>)).MakeGeneric(TypeIdentity.Create<string>().MakeNullable());
		var reference = list.AsTypeReference();

		await Assert
			.That(reference.RenderFullNameForNullable(true))
			.IsEqualTo("global::System.Collections.Generic.List<string?>");
		await Assert
			.That(reference.RenderFullNameForNullable(false))
			.IsEqualTo("global::System.Collections.Generic.List<string>");
	}

	// ---------------------------------------------------------------------------------------------
	// Conditional nullable composition
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Nullable_GivenDisabledSettings_DoesNotAppendAnnotation()
	{
		GenerationSettings settings = new("TestGenerator", "1.0.0") { IsNullableContextEnabled = false };
		var @string = TypeIdentity.Create<string>();

		await Assert.That(@string.MakeNullable(settings).RenderFullName).IsEqualTo("string");
		await Assert.That(@string.MakeNullable(settings).IsNullable).IsFalse();
	}

	[Test]
	public async Task Nullable_GivenEnabledOrUnknownSettings_AppendsAnnotation()
	{
		GenerationSettings enabled = new("TestGenerator", "1.0.0") { IsNullableContextEnabled = true };
		GenerationSettings unknown = new("TestGenerator", "1.0.0");

		await Assert.That(TypeIdentity.Create<string>().MakeNullable(enabled).RenderFullName).IsEqualTo("string?");
		await Assert.That(TypeIdentity.Create<string>().MakeNullable(unknown).RenderFullName).IsEqualTo("string?");
	}

	[Test]
	public async Task Nullable_GivenWriterContext_BehavesLikeSettings()
	{
		CodeWriter disabled = new(
			new GenerationSettings("TestGenerator", "1.0.0") { IsNullableContextEnabled = false }
		);
		CodeWriter enabled = new(new GenerationSettings("TestGenerator", "1.0.0") { IsNullableContextEnabled = true });
		var @string = TypeIdentity.Create<string>();

		await Assert.That(@string.MakeNullable(disabled).RenderFullName).IsEqualTo("string");
		await Assert.That(@string.MakeNullable(enabled).RenderFullName).IsEqualTo("string?");
	}

	[Test]
	public async Task Nullable_GivenAlwaysModeAndDisabledContext_AppendsAnnotation()
	{
		GenerationSettings settings = new("TestGenerator", "1.0.0")
		{
			NullableDirectiveMode = NullableDirectiveMode.Always,
			IsNullableContextEnabled = false,
		};
		CodeWriter writer = new(settings);
		var @string = TypeIdentity.Create<string>();

		await Assert.That(@string.MakeNullable(settings).RenderFullName).IsEqualTo("string?");
		await Assert.That(@string.MakeNullable(writer).RenderFullName).IsEqualTo("string?");
	}

	[Test]
	public async Task Nullable_GivenDisableModeAndEnabledContext_DoesNotAppendAnnotation()
	{
		GenerationSettings settings = new("TestGenerator", "1.0.0")
		{
			NullableDirectiveMode = NullableDirectiveMode.Disable,
			IsNullableContextEnabled = true,
		};
		CodeWriter writer = new(settings);
		var @string = TypeIdentity.Create<string>();

		await Assert.That(@string.MakeNullable(settings).RenderFullName).IsEqualTo("string");
		await Assert.That(@string.MakeNullable(writer).RenderFullName).IsEqualTo("string");
	}

	[Test]
	public async Task Nullable_GivenNullSettingsOrWriter_Throws()
	{
		var @string = TypeIdentity.Create<string>();

		await Assert.That(() => @string.MakeNullable((GenerationSettings)null!)).Throws<ArgumentNullException>();
		await Assert.That(() => @string.MakeNullable((CodeWriter)null!)).Throws<ArgumentNullException>();
	}

	// ---------------------------------------------------------------------------------------------
	// Similarity (nullable reference annotations are metadata)
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Similar_IgnoresNullableReferenceAnnotations()
	{
		var @string = TypeIdentity.Create<string>();

		await Assert.That(@string.MakeNullable().Similar(@string.AsTypeReference())).IsTrue();
		await Assert.That(@string.AsTypeReference().Similar(@string.MakeNullable())).IsTrue();
		await Assert.That(TypeModifier.NullableReference).IsEqualTo(TypeModifier.Nullable);
	}

	[Test]
	public async Task Similar_KeepsNullableValueTypesSignificant()
	{
		var @int = TypeIdentity.Create<int>();

		await Assert.That(@int.MakeNullable().Similar(@int.AsTypeReference())).IsFalse();
		await Assert.That(@int.MakeNullable()).IsNotEqualTo(@int.AsTypeReference());
	}

	[Test]
	public async Task Equality_RemainsStructural_GivenReferenceAnnotationDifference()
	{
		var @string = TypeIdentity.Create<string>();

		await Assert.That(@string.MakeNullable()).IsNotEqualTo(@string.AsTypeReference());
		await Assert.That(@string.MakeNullable() == @string.AsTypeReference()).IsFalse();
	}

	[Test]
	public async Task Equality_MixedTypeIdentityAndTypeReference_ComparesStructurally()
	{
		var @string = TypeIdentity.Create<string>();
		var plain = @string.AsTypeReference();
		var nullable = @string.AsTypeReference().Nullable();

		await Assert.That(@string == plain).IsTrue();
		await Assert.That(plain == @string).IsTrue();
		await Assert.That(@string != nullable).IsTrue();
		await Assert.That(nullable != @string).IsTrue();
		await Assert.That(@string == nullable).IsFalse();
		await Assert.That(nullable == @string).IsFalse();
	}

	[Test]
	public async Task Equality_MixedTypeIdentityAndTypeReference_NullableValueTypeIsSignificant()
	{
		var @int = TypeIdentity.Create<int>();
		var nullableInt = @int.AsTypeReference().Nullable();

		await Assert.That(@int == nullableInt).IsFalse();
		await Assert.That(nullableInt == @int).IsFalse();
		await Assert.That(@int != nullableInt).IsTrue();
	}

	[Test]
	public async Task Similar_GivenNestedGeneric_DiffersOnlyByReferenceAnnotation_ReturnsTrue()
	{
		// IEnumerable<KeyValuePair<string, object>> versus
		// IEnumerable<KeyValuePair<string, object?>> — the annotation is metadata, so the two are similar.
		var withObject = TypeIdentity.Create<IEnumerable<KeyValuePair<string, object>>>().AsTypeReference();
		var withNullableObject = new TypeIdentity(typeof(IEnumerable<>))
			.MakeGeneric(
				new TypeReference(
					new TypeIdentity("KeyValuePair", "System.Collections.Generic").MakeGeneric(
						TypeIdentity.Create<string>().AsTypeReference(),
						TypeIdentity.Create<object>().AsTypeReference().Nullable()
					)
				)
			)
			.AsTypeReference();

		await Assert.That(withNullableObject.Similar(withObject)).IsTrue();
		await Assert.That(withObject.Similar(withNullableObject)).IsTrue();
		await Assert.That(withNullableObject).IsNotEqualTo(withObject);
	}

	[Test]
	public async Task Similar_GivenSymbolSource_ComparesEqualToHandBuiltReference()
	{
		var symbol = TestCompilation.FieldType(
			"public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> Value = null!;"
		);
		var handBuilt = new TypeIdentity(typeof(IEnumerable<>))
			.MakeGeneric(
				new TypeReference(
					new TypeIdentity("KeyValuePair", "System.Collections.Generic").MakeGeneric(
						TypeIdentity.Create<string>().AsTypeReference(),
						TypeIdentity.Create<object>().AsTypeReference().Nullable()
					)
				)
			)
			.AsTypeReference();

		await Assert.That(handBuilt.Similar(symbol)).IsTrue();
		await Assert.That(handBuilt.Matches(symbol)).IsTrue();
		await Assert.That(TypeReference.Create(symbol).Similar(handBuilt)).IsTrue();
	}

	// ---------------------------------------------------------------------------------------------
	// Round-tripping from symbols
	// ---------------------------------------------------------------------------------------------

	[Test]
	[Arguments("public int[] Value = null!;", "int[]")]
	[Arguments("public int[,] Value = null!;", "int[,]")]
	[Arguments("public int[][,] Value = null!;", "int[][,]")]
	[Arguments("public int? Value;", "int?")]
	[Arguments("public byte* Value;", "byte*")]
	[Arguments("public byte** Value;", "byte**")]
	[Arguments("public byte*[] Value = null!;", "byte*[]")]
	[Arguments("public T Value = default!;", "T")]
	[Arguments("public T[] Value = null!;", "T[]")]
	[Arguments("public dynamic Value = null!;", "dynamic")]
	public async Task TryCreate_RoundTripsSymbolToRenderedSource(string fieldDeclaration, string expected)
	{
		var symbol = TestCompilation.FieldType(fieldDeclaration);

		await Assert.That(TypeReference.TryCreate(symbol, out var reference)).IsTrue();
		await Assert.That(reference.RenderFullName).IsEqualTo(expected);
		await Assert.That(reference.Matches(symbol)).IsTrue();
	}

	[Test]
	public async Task TryCreate_GivenNullableReferenceType_RecordsAnnotation()
	{
		var symbol = TestCompilation.FieldType("public string? Value;");

		await Assert.That(TypeReference.TryCreate(symbol, out var reference)).IsTrue();
		await Assert.That(reference.IsNullable).IsTrue();
		await Assert.That(reference.RenderFullName).IsEqualTo("string?");
	}

	[Test]
	public async Task TryCreate_GivenNullableArrayOfNullableElements_PreservesOrdering()
	{
		var symbol = TestCompilation.FieldType("public string?[]? Value;");

		await Assert.That(TypeReference.TryCreate(symbol, out var reference)).IsTrue();
		await Assert.That(reference.RenderFullName).IsEqualTo("string?[]?");
	}

	[Test]
	public async Task TryCreate_GivenErrorType_ReturnsFalse()
	{
		var symbol = TestCompilation.FieldType("public Missing.Thing Value;");

		await Assert.That(TypeReference.TryCreate(symbol, out _)).IsFalse();
	}

	[Test]
	[Arguments(typeof(int[]), "int[]")]
	[Arguments(typeof(int[,]), "int[,]")]
	[Arguments(typeof(int?), "int?")]
	public async Task TryCreate_GivenRuntimeType_RoundTrips(Type type, string expected)
	{
		await Assert.That(TypeReference.TryCreate(type, out var reference)).IsTrue();
		await Assert.That(reference.RenderFullName).IsEqualTo(expected);
	}

	// ---------------------------------------------------------------------------------------------
	// Matching
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Matches_GivenArrayRankMismatch_ReturnsFalse()
	{
		var reference = new TypeIdentity(SpecialType.System_Int32).MakeArray();

		await Assert.That(reference.Matches(TestCompilation.FieldType("public int[] Value = null!;"))).IsTrue();
		await Assert.That(reference.Matches(TestCompilation.FieldType("public int[,] Value = null!;"))).IsFalse();
		await Assert.That(reference.Matches(TestCompilation.FieldType("public int Value;"))).IsFalse();
	}

	[Test]
	public async Task Matches_GivenNullableValueType_IsEnforced()
	{
		var nullable = new TypeIdentity(SpecialType.System_Int32).MakeNullable();

		await Assert.That(nullable.Matches(TestCompilation.FieldType("public int? Value;"))).IsTrue();
		await Assert.That(nullable.Matches(TestCompilation.FieldType("public int Value;"))).IsFalse();
	}

	[Test]
	public async Task Matches_GivenNullableReferenceType_IgnoresAnnotation()
	{
		var nullable = new TypeIdentity(SpecialType.System_String).MakeNullable();

		// Annotation is metadata, not identity: both spellings resolve to System.String.
		await Assert.That(nullable.Matches(TestCompilation.FieldType("public string? Value;"))).IsTrue();
		await Assert.That(nullable.Matches(TestCompilation.FieldType("public string Value = null!;"))).IsTrue();
	}

	[Test]
	public async Task Matches_GivenTypeParameter_ComparesByName()
	{
		var symbol = TestCompilation.FieldType("public T Value = default!;");

		await Assert.That(TypeReference.ForTypeParameter("T").Matches(symbol)).IsTrue();
		await Assert.That(TypeReference.ForTypeParameter("TOther").Matches(symbol)).IsFalse();
		await Assert.That(TypeReference.Dynamic.Matches(symbol)).IsFalse();
	}

	[Test]
	public async Task Matches_GivenDynamic_ReturnsTrue()
	{
		var symbol = TestCompilation.FieldType("public dynamic Value = null!;");

		await Assert.That(TypeReference.Dynamic.Matches(symbol)).IsTrue();
		await Assert.That(new TypeIdentity(SpecialType.System_Object).AsTypeReference().Matches(symbol)).IsFalse();
	}

	[Test]
	public async Task Matches_GivenMemberSymbol_ResolvesItsType()
	{
		var compilation = TestCompilation.Create(
			"""
			namespace Sample;

			public class Holder
			{
				public int[] Field = null!;
				public int Other;
			}
			"""
		);

		var holder = compilation.GetTypeByMetadataName("Sample.Holder")!;
		var reference = new TypeIdentity(SpecialType.System_Int32).MakeArray();

		await Assert.That(reference.Matches(holder.GetMembers("Field").Single())).IsTrue();
		await Assert.That(reference.Matches(holder.GetMembers("Other").Single())).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Equality
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Equality_DistinguishesModifierOrder()
	{
		TypeIdentity int32 = new(SpecialType.System_Int32);

		await Assert.That(int32.MakeNullable().MakeArray()).IsNotEqualTo(int32.MakeArray().Nullable());
		await Assert.That(int32.MakeArray()).IsEqualTo(int32.MakeArray());
		await Assert.That(int32.MakeArray().GetHashCode()).IsEqualTo(int32.MakeArray().GetHashCode());
		await Assert.That(int32.MakeArray(1)).IsNotEqualTo(int32.MakeArray(2));
	}

	[Test]
	public async Task Equals_GivenPlainNamedType_MatchesUnderlyingValueObject()
	{
		TypeIdentity int32 = new(SpecialType.System_Int32);

		await Assert.That(int32.AsTypeReference().Equals(int32)).IsTrue();
		await Assert.That(int32.MakeArray().Equals(int32)).IsFalse();
		await Assert.That(int32.AsTypeReference().IsPlainNamedType).IsTrue();
	}

	[Test]
	public async Task ImplicitConversion_FromNamedType_ProducesPlainReference()
	{
		TypeReference reference = new TypeIdentity("Order", "Sample");

		await Assert.That(reference.Kind).IsEqualTo(TypeReferenceKind.Named);
		await Assert.That(reference.IsPlainNamedType).IsTrue();
		await Assert.That(reference.RenderFullName).IsEqualTo("global::Sample.Order");
	}

	[Test]
	public async Task Append_GivenEmptyReference_Throws()
	{
		await Assert.That(void () => _ = TypeReference.Empty.MakeArray()).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task MakeArray_GivenInvalidRank_Throws()
	{
		TypeIdentity int32 = new(SpecialType.System_Int32);

		await Assert.That(void () => _ = int32.MakeArray(0)).Throws<ArgumentOutOfRangeException>();
	}
}
