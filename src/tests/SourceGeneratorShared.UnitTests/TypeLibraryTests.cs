namespace Purview.SourceGeneratorFramework;

public sealed class TypeLibraryTests
{
	// ---------------------------------------------------------------------------------------------
	// Open generic definitions
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task GenericTypes_RenderAsOpenDefinitions()
	{
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Generic.List.RenderFullName)
			.IsEqualTo("global::System.Collections.Generic.List<>");
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Generic.IReadOnlyList.RenderFullName)
			.IsEqualTo("global::System.Collections.Generic.IReadOnlyList<>");
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Generic.Dictionary.RenderFullName)
			.IsEqualTo("global::System.Collections.Generic.Dictionary<,>");
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Immutable.ImmutableArray.RenderFullName)
			.IsEqualTo("global::System.Collections.Immutable.ImmutableArray<>");
	}

	[Test]
	public async Task GenericTypes_AreOpenDefinitions()
	{
		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.List.IsGenericTypeDefinition).IsTrue();
		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.List.GenericArity).IsEqualTo(1);
		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.Dictionary.GenericArity).IsEqualTo(2);
	}

	// ---------------------------------------------------------------------------------------------
	// Constructed rendering
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task GenericTypes_MakeGeneric_RenderConstructedName()
	{
		var listOfString = PurviewTypeLibrary.System.Collections.Generic.List.MakeGeneric(
			TypeIdentity.Create<string>()
		);
		var dictionary = PurviewTypeLibrary.System.Collections.Generic.Dictionary.MakeGeneric(
			TypeIdentity.Create<string>(),
			TypeIdentity.Create<int>()
		);

		await Assert.That(listOfString.RenderFullName).IsEqualTo("global::System.Collections.Generic.List<string>");
		await Assert
			.That(dictionary.RenderFullName)
			.IsEqualTo("global::System.Collections.Generic.Dictionary<string, int>");
	}

	// ---------------------------------------------------------------------------------------------
	// Symbol matching
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task GenericTypes_MatchConstructedSymbols()
	{
		var list = TestCompilation.FieldType("public List<string> Value = null!;");
		var dictionary = TestCompilation.FieldType("public Dictionary<string, int> Value = null!;");
		var readOnlyList = TestCompilation.FieldType("public IReadOnlyList<string> Value = null!;");
		var immutableArray = TestCompilation.FieldType(
			"public System.Collections.Immutable.ImmutableArray<int> Value = null!;"
		);

		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.List.Matches(list)).IsTrue();
		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.Dictionary.Matches(dictionary)).IsTrue();
		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.IReadOnlyList.Matches(readOnlyList)).IsTrue();
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Immutable.ImmutableArray.Matches(immutableArray))
			.IsTrue();
	}

	[Test]
	public async Task GenericTypes_DoNotMatchDifferentArity()
	{
		var dictionary = TestCompilation.FieldType("public Dictionary<string, int> Value = null!;");

		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.List.Matches(dictionary)).IsFalse();
	}

	[Test]
	public async Task ImmutableBuilders_RenderAndMatch()
	{
		var builder = TestCompilation.FieldType(
			"public System.Collections.Immutable.ImmutableArray<int>.Builder Value = null!;"
		);

		await Assert
			.That(PurviewTypeLibrary.System.Collections.Immutable.ImmutableArrayBuilder.RenderFullName)
			.IsEqualTo("global::System.Collections.Immutable.ImmutableArray<>.Builder");
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Immutable.ImmutableArrayBuilder.Matches(builder))
			.IsTrue();
	}

	[Test]
	public async Task ReadOnlySet_DeclaredByName_Matches()
	{
		var symbol = TestCompilation.FieldType("public IReadOnlySet<string> Value = null!;");

		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.IReadOnlySet.IsGenericTypeDefinition).IsTrue();
		await Assert.That(PurviewTypeLibrary.System.Collections.Generic.IReadOnlySet.Matches(symbol)).IsTrue();
	}

	// ---------------------------------------------------------------------------------------------
	// Func arity family
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Func_IsLowestArityOpenDefinition()
	{
		await Assert.That(PurviewTypeLibrary.System.Func.GenericArity).IsEqualTo(1);
		await Assert.That(PurviewTypeLibrary.System.Func.RenderFullName).IsEqualTo("global::System.Func<>");
	}

	[Test]
	public async Task Func_WithArity_MatchesHighArityConstruction()
	{
		var compilation = TestCompilation.Create();
		var func = compilation.GetTypeByMetadataName("System.Func`17")!;
		var value = PurviewTypeLibrary.System.Func.WithArity(17);

		await Assert.That(value.GenericArity).IsEqualTo(17);
		await Assert.That(value.Matches(func)).IsTrue();
	}

	// ---------------------------------------------------------------------------------------------
	// Namespace constants
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task NamespaceConstants_MatchTheirHierarchy()
	{
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Generic.Namespace)
			.IsEqualTo("System.Collections.Generic");
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Concurrent.Namespace)
			.IsEqualTo("System.Collections.Concurrent");
		await Assert
			.That(PurviewTypeLibrary.System.Collections.ObjectModel.Namespace)
			.IsEqualTo("System.Collections.ObjectModel");
		await Assert
			.That(PurviewTypeLibrary.System.Collections.Immutable.Namespace)
			.IsEqualTo("System.Collections.Immutable");
	}

	// ---------------------------------------------------------------------------------------------
	// Null literal
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task SystemNull_RendersAsBareKeyword()
	{
		await Assert.That(PurviewTypeLibrary.System.Null.RenderFullName).IsEqualTo("null");
		await Assert.That(PurviewTypeLibrary.System.Null).IsEqualTo(TypeIdentity.Null);
	}

	[Test]
	public async Task NullReference_RendersAndIsIdentified()
	{
		var reference = TypeReference.Null;

		await Assert.That(reference.IsNullLiteral).IsTrue();
		await Assert.That(reference.RenderFullName).IsEqualTo("null");
		await Assert.That(reference.ToString()).IsEqualTo("null");
	}

	[Test]
	public async Task EmptyReference_IsNotNullLiteral()
	{
		await Assert.That(TypeReference.Empty.IsNullLiteral).IsFalse();
		await Assert.That(TypeReference.Dynamic.IsNullLiteral).IsFalse();
	}
}
