using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Helpers;

public class TypeHelpersTests
{
	static async Task<INamedTypeSymbol> GetTypeSymbolAsync(
		string source,
		string typeName,
		CancellationToken cancellationToken
	)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			[
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
			],
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);
		var model = compilation.GetSemanticModel(syntaxTree);
		var root = await syntaxTree.GetRootAsync(cancellationToken);
		var typeDeclaration = root.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.First(t => t.Identifier.ValueText == typeName);
		return model.GetDeclaredSymbol(typeDeclaration, cancellationToken)!;
	}

	static INamedTypeSymbol GetTypeReferenceSymbol(string typeName, CancellationToken cancellationToken)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(
			$"internal sealed class TypeReferenceHolder {{ public {typeName} Value {{ get; }} = default!; }}",
			cancellationToken: cancellationToken
		);
		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			[
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
			],
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);
		var model = compilation.GetSemanticModel(syntaxTree);
		var root = syntaxTree.GetRoot(cancellationToken);
		var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();

		return (INamedTypeSymbol)model.GetTypeInfo(property.Type, cancellationToken).Type!;
	}

	static (ITypeSymbol Symbol, TypeDeclarationSyntax Syntax) GetTypeDescriptor(
		string source,
		string typeName,
		CancellationToken cancellationToken
	)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);
		var root = syntaxTree.GetRoot(cancellationToken);
		var declaration = root.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.Single(type => type.Identifier.ValueText == typeName);
		var symbol = compilation.GetSemanticModel(syntaxTree).GetDeclaredSymbol(declaration, cancellationToken)!;
		return new(symbol, declaration);
	}

	static string GeneratedAttributes(bool includeCoverageExclusion = true) =>
		(includeCoverageExclusion ? "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\n" : "")
		+ "[global::System.Runtime.CompilerServices.CompilerGenerated]\n"
		+ "[global::System.CodeDom.Compiler.GeneratedCode(\"TestGenerator\", \"1.0.0\")]\n";

	[Test]
	public async Task IsAttribute_TypeNameEndsWithAttribute_ReturnsTrue() =>
		await Assert.That(TypeHelpers.IsAttribute("MyAttribute")).IsTrue();

	[Test]
	public async Task IsDerivedFromExpectedBase_GenericArgumentImplementsExpectedContract_ReturnsTrue(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			"namespace Testing { interface IResource { } class DefaultAspireResource : IResource { } class ResourceKitBase<T> where T : IResource { } class HostKit : ResourceKitBase<DefaultAspireResource> { } }";
		var (Symbol, Syntax) = GetTypeDescriptor(source, "HostKit", cancellationToken);
		var expectedBase = new TypeIdentity("ResourceKitBase", "Testing").MakeGeneric(
			new TypeIdentity("IResource", "Testing")
		);

		// Act
		var symbolResult = TypeHelpers.IsDerivedFromExpectedBase(Symbol, expectedBase);
		var syntaxResult = TypeHelpers.IsDerivedFromExpectedBase(Syntax, expectedBase);

		// Assert
		await Assert.That(symbolResult).IsTrue();
		await Assert.That(syntaxResult).IsTrue();
	}

	[Test]
	public async Task IsDerivedFromExpectedBase_NameOnlyGenericBase_ReturnsTrueForConstructedBase(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			"namespace Testing { interface IResource { } class DefaultAspireResource : IResource { } class ResourceKitBase<T> where T : IResource { } class HostKit : ResourceKitBase<DefaultAspireResource> { } }";
		var (Symbol, Syntax) = GetTypeDescriptor(source, "HostKit", cancellationToken);
		TypeIdentity expectedBase = new("ResourceKitBase", "Testing");

		// Act
		var symbolResult = TypeHelpers.IsDerivedFromExpectedBase(Symbol, expectedBase);
		var syntaxResult = TypeHelpers.IsDerivedFromExpectedBase(Syntax, expectedBase);

		// Assert
		await Assert.That(symbolResult).IsTrue();
		await Assert.That(syntaxResult).IsTrue();
	}

	[Test]
	public async Task IsAttribute_TypeNameWithoutSuffix_ReturnsFalse() =>
		await Assert.That(TypeHelpers.IsAttribute("MyClass")).IsFalse();

	[Test]
	public async Task IsAttribute_ConstructedGenericAttribute_ReturnsTrue() =>
		await Assert.That(TypeHelpers.IsAttribute("global::Example.MarkerAttribute<string>")).IsTrue();

	[Test]
	public async Task GetTypeName_AttributeType_TrimsSuffix() =>
		await Assert.That(TypeHelpers.GetTypeName("MyAttribute")).IsEqualTo("My");

	[Test]
	public async Task GetTypeName_NonAttributeType_ReturnsOriginal() =>
		await Assert.That(TypeHelpers.GetTypeName("MyClass")).IsEqualTo("MyClass");

	[Test]
	public async Task GetTypeName_ConstructedGenericAttribute_PreservesTypeArgumentsAndTrimsSuffix() =>
		await Assert
			.That(TypeHelpers.GetTypeName("global::Example.MarkerAttribute<string>"))
			.IsEqualTo("global::Example.Marker<string>");

	[Test]
	public async Task TypeValueObject_RenderAttributeName_ConstructedGenericAttribute_PreservesTypeArguments()
	{
		var attribute = new TypeIdentity("MarkerAttribute", "Example").MakeGeneric(
			new TypeIdentity(SpecialType.System_String)
		);

		await Assert.That(attribute.IsAttribute).IsTrue();
		await Assert.That(attribute.RenderAttributeName).IsEqualTo("global::Example.Marker<string>");
	}

	[Test]
	public async Task IsValidIdentifier_ValidIdentifier_ReturnsTrue()
	{
		await Assert.That(TypeHelpers.IsValidIdentifier("validName")).IsTrue();
		await Assert.That(TypeHelpers.IsValidIdentifier("_validName")).IsTrue();
	}

	[Test]
	[Arguments("123invalid")]
	[Arguments("")]
	[Arguments(null)]
	public async Task IsValidIdentifier_InvalidIdentifier_ReturnsFalse(string? name)
	{
		await Assert.That(TypeHelpers.IsValidIdentifier(name)).IsFalse();
	}

	[Test]
	public async Task IsPartial_PartialClass_ReturnsTrue(CancellationToken cancellationToken)
	{
		var source = "public partial class MyClass { }";
		var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
		var declaration = (await tree.GetRootAsync(cancellationToken))
			.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.First();

		await Assert.That(TypeHelpers.IsPartial(declaration)).IsTrue();
	}

	[Test]
	public async Task IsPartial_NonPartialClass_ReturnsFalse(CancellationToken cancellationToken)
	{
		var source = "public class MyClass { }";
		var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
		var declaration = (await tree.GetRootAsync(cancellationToken))
			.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.First();

		await Assert.That(TypeHelpers.IsPartial(declaration)).IsFalse();
	}

	[Test]
	public async Task HasNonEmptyConstructors_WithParameterConstructor_ReturnsTrue(CancellationToken cancellationToken)
	{
		var source = """
			public class MyClass
			{
				public MyClass(int value) { }
			}
			""";
		var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
		var declaration = (await tree.GetRootAsync(cancellationToken))
			.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.First();

		await Assert.That(TypeHelpers.HasNonEmptyConstructors(declaration, "MyClass")).IsTrue();
	}

	[Test]
	public async Task HasNonEmptyConstructors_WithEmptyConstructor_ReturnsFalse(CancellationToken cancellationToken)
	{
		var source = """
			public class MyClass
			{
				public MyClass() { }
			}
			""";
		var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
		var declaration = (await tree.GetRootAsync(cancellationToken))
			.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.First();

		await Assert.That(TypeHelpers.HasNonEmptyConstructors(declaration, "MyClass")).IsFalse();
	}

	[Test]
	public async Task HasAttribute_AttributedClass_ReturnsTrue(CancellationToken cancellationToken)
	{
		var source = """
			using System;

			[Serializable]
			public class MyClass { }
			""";
		var symbol = await GetTypeSymbolAsync(source, "MyClass", cancellationToken);

		await Assert.That(TypeHelpers.HasAttribute(symbol, "System.SerializableAttribute")).IsTrue();
	}

	[Test]
	public async Task HasAttribute_MissingAttribute_ReturnsFalse(CancellationToken cancellationToken)
	{
		var source = "public class MyClass { }";
		var symbol = await GetTypeSymbolAsync(source, "MyClass", cancellationToken);

		await Assert.That(TypeHelpers.HasAttribute(symbol, "System.SerializableAttribute")).IsFalse();
	}

	[Test]
	public async Task InheritsFrom_DerivedClass_ReturnsTrue(CancellationToken cancellationToken)
	{
		var source = """
			public class Base { }
			public class Derived : Base { }
			""";
		var symbol = await GetTypeSymbolAsync(source, "Derived", cancellationToken);

		await Assert.That(TypeHelpers.InheritsFrom(symbol, "Base")).IsTrue();
	}

	[Test]
	public async Task InheritsFrom_UnrelatedClass_ReturnsFalse(CancellationToken cancellationToken)
	{
		var source = """
			public class Base { }
			public class Other { }
			""";
		var symbol = await GetTypeSymbolAsync(source, "Other", cancellationToken);

		await Assert.That(TypeHelpers.InheritsFrom(symbol, "Base")).IsFalse();
	}

	[Test]
	public async Task Implements_IEnumerableT_ReturnsTrue(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			public class MyCollection : IEnumerable<int>
			{
				public IEnumerator<int> GetEnumerator() => null;
				System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
			}
			""";
		var symbol = await GetTypeSymbolAsync(source, "MyCollection", cancellationToken);

		await Assert.That(TypeHelpers.Implements(symbol, "System.Collections.Generic.IEnumerable`1")).IsTrue();
	}

	[Test]
	public async Task ToFullyQualifiedDisplayString_ReturnsGlobalQualifiedName(CancellationToken cancellationToken)
	{
		var source = "namespace Test { public class MyClass { } }";
		var symbol = await GetTypeSymbolAsync(source, "MyClass", cancellationToken);

		await Assert.That(TypeHelpers.ToFullyQualifiedDisplayString(symbol)).IsEqualTo("global::Test.MyClass");
	}

	[Test]
	public async Task IsCollectionLike_List_ReturnsTrue(CancellationToken cancellationToken)
	{
		var source = "using System.Collections.Generic; public class MyClass { public List<int> Items; }";
		var symbol = await GetTypeSymbolAsync(source, "MyClass", cancellationToken);
		var fieldSymbol = symbol.GetMembers("Items").OfType<IFieldSymbol>().First();

		await Assert.That(TypeHelpers.IsCollectionLike(fieldSymbol.Type)).IsTrue();
	}

	[Test]
	public async Task IsArray_Array_ReturnsTrue(CancellationToken cancellationToken)
	{
		var source = "using System; public class MyClass { public int[] Items; }";
		var symbol = await GetTypeSymbolAsync(source, "MyClass", cancellationToken);
		var fieldSymbol = symbol.GetMembers("Items").OfType<IFieldSymbol>().First();

		await Assert.That(TypeHelpers.IsArray(fieldSymbol.Type)).IsTrue();
	}

	[Test]
	public async Task TryGetElementType_List_ReturnsIntElement(CancellationToken cancellationToken)
	{
		var source = "using System.Collections.Generic; public class MyClass { public List<int> Items; }";
		var symbol = await GetTypeSymbolAsync(source, "MyClass", cancellationToken);
		var fieldSymbol = symbol.GetMembers("Items").OfType<IFieldSymbol>().First();

		var result = TypeHelpers.TryGetElementType(fieldSymbol.Type, out var elementType);

		await Assert.That(result).IsTrue();
		await Assert.That(elementType).IsNotNull();
		await Assert.That(elementType!.SpecialType).IsEqualTo(SpecialType.System_Int32);
	}

	[Test]
	public async Task DeriveName_WithSuffix_RemovesSuffix()
	{
		await Assert.That(TypeHelpers.DeriveName("MyService", "Service")).IsEqualTo("My");
	}

	[Test]
	public async Task DeriveName_WithoutSuffix_ReturnsOriginal()
	{
		await Assert.That(TypeHelpers.DeriveName("MyClass", "Service")).IsEqualTo("MyClass");
	}

	[Test]
	[Arguments(Accessibility.Public, "public")]
	[Arguments(Accessibility.Internal, "internal")]
	[Arguments(Accessibility.Protected, "protected")]
	[Arguments(Accessibility.Private, "private")]
	[Arguments(Accessibility.ProtectedOrInternal, "protected internal")]
	[Arguments(Accessibility.ProtectedAndInternal, "private protected")]
	public async Task GetAccessibilityKeyword_ReturnsExpectedKeyword(Accessibility accessibility, string expected)
	{
		await Assert.That(TypeHelpers.GetAccessibilityKeyword(accessibility)).IsEqualTo(expected);
	}

	[Test]
	[Arguments(Accessibility.Public, TypeDeclarationAccessibility.Public)]
	[Arguments(Accessibility.Internal, TypeDeclarationAccessibility.Internal)]
	[Arguments(Accessibility.Protected, TypeDeclarationAccessibility.Protected)]
	[Arguments(Accessibility.Private, TypeDeclarationAccessibility.Private)]
	[Arguments(Accessibility.ProtectedOrInternal, TypeDeclarationAccessibility.ProtectedInternal)]
	[Arguments(Accessibility.ProtectedAndInternal, TypeDeclarationAccessibility.PrivateProtected)]
	public async Task AccessibilityConversions_GivenMappedValues_RoundTrips(
		Accessibility roslynAccessibility,
		TypeDeclarationAccessibility declarationAccessibility
	)
	{
		// Arrange / Act / Assert
		await Assert.That(roslynAccessibility.ToTypeDeclarationAccessibility()).IsEqualTo(declarationAccessibility);
		await Assert.That(declarationAccessibility.ToRoslynAccessibility()).IsEqualTo(roslynAccessibility);
	}

	[Test]
	public async Task AccessibilityConversions_GivenUnmappedValues_ReturnsSafeFallbacks()
	{
		// Arrange / Act / Assert
		await Assert.That(Accessibility.NotApplicable.ToTypeDeclarationAccessibility()).IsNull();
		await Assert
			.That(TypeDeclarationAccessibility.File.ToRoslynAccessibility())
			.IsEqualTo(Accessibility.NotApplicable);
		await Assert
			.That(((TypeDeclarationAccessibility)int.MaxValue).ToRoslynAccessibility())
			.IsEqualTo(Accessibility.NotApplicable);
	}

	[Test]
	public async Task CreatePartialTypeDeclarationOptions_GivenStaticGenericClass_RecreatesContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			public static partial class Container<T>
				where T : class, new()
			{
			}
			""";
		var symbol = await GetTypeSymbolAsync(source, "Container", cancellationToken);

		// Act
		var declaration = TypeHelpers.CreatePartialTypeDeclarationOptions(symbol);
		var writer = CodeWriterFactory.ForTests();
		using (writer.TypeScope(declaration))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert.That(declaration.Kind).IsEqualTo(TypeDeclarationKind.Class);
		await Assert.That(declaration.IsStatic).IsTrue();
		await Assert.That(declaration.GenericTypes).Count().IsEqualTo(1);
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static partial class Container<T>\n"
					+ "where T : class, new()\n"
					+ "{\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task CreatePartialTypeDeclarationOptions_GivenReadonlyRecordStruct_RecreatesContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			internal readonly partial record struct Container<T>
				where T : unmanaged
			{
			}
			""";
		var symbol = await GetTypeSymbolAsync(source, "Container", cancellationToken);

		// Act
		var declaration = TypeHelpers.CreatePartialTypeDeclarationOptions(symbol);

		// Assert
		await Assert.That(declaration.Kind).IsEqualTo(TypeDeclarationKind.RecordStruct);
		await Assert.That(declaration.Accessibility).IsEqualTo(TypeDeclarationAccessibility.Internal);
		await Assert.That(declaration.IsReadOnly).IsTrue();
		await Assert.That(declaration.GenericTypes[0].Constraints).Contains("unmanaged");
	}

	[Test]
	public async Task CreatePartialTypeDeclarationOptions_GivenBasicMode_OmitsOptionalParts(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			public sealed partial class Container<T>
				where T : class, new()
			{
			}
			""";
		var symbol = await GetTypeSymbolAsync(source, "Container", cancellationToken);

		// Act
		var declaration = TypeHelpers.CreatePartialTypeDeclarationOptions(symbol, includeOptionalParts: false);
		var writer = CodeWriterFactory.ForTests();
		using (writer.TypeScope(declaration))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert.That(declaration.Accessibility).IsNull();
		await Assert.That(declaration.IsSealed).IsFalse();
		await Assert.That(declaration.GenericTypes[0].Constraints).IsEmpty();
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public partial class Container<T>\n{\n}\n");
	}

	[Test]
	public async Task IsAccessibleAsPublicOrInternal_PublicType_ReturnsTrue(CancellationToken cancellationToken)
	{
		var source = "public class MyClass { }";
		var symbol = await GetTypeSymbolAsync(source, "MyClass", cancellationToken);

		await Assert.That(TypeHelpers.IsAccessibleAsPublicOrInternal(symbol)).IsTrue();
	}

	[Test]
	public async Task IsAccessibleAsPublicOrInternal_PrivateNestedType_ReturnsFalse(CancellationToken cancellationToken)
	{
		var source = """
			public class Outer
			{
				private class Inner { }
			}
			""";
		var symbol = await GetTypeSymbolAsync(source, "Outer", cancellationToken);
		var innerSymbol = symbol.GetTypeMembers("Inner").First();

		await Assert.That(TypeHelpers.IsAccessibleAsPublicOrInternal(innerSymbol)).IsFalse();
	}

	[Test]
	[Arguments("System.Collections.Generic.Dictionary<string, int>")]
	[Arguments("System.Collections.Generic.IDictionary<int, bool>")]
	[Arguments("System.Collections.Generic.IReadOnlyDictionary<int, string>")]
	public async Task Is_GivenTypeIsAMatch_ReturnsTrue(string typeName, CancellationToken cancellationToken)
	{
		TypeIdentity dictionaryKV = new(typeof(Dictionary<,>));
		TypeIdentity iDictionaryKV = new(typeof(IDictionary<,>));
		TypeIdentity iReadOnlyDictionaryKV = new(typeof(IReadOnlyDictionary<,>));

		var symbol = GetTypeReferenceSymbol(typeName, cancellationToken);

		await Assert.That(symbol).IsNotNull();
		await Assert.That(TypeHelpers.Is(symbol, dictionaryKV, iDictionaryKV, iReadOnlyDictionaryKV)).IsTrue();
	}
}
