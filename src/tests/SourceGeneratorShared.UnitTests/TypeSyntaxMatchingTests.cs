using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework;

public sealed class TypeSyntaxMatchingTests
{
	// ---------------------------------------------------------------------------------------------
	// Declarations — syntactic
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task CouldMatchDeclaration_GivenFileScopedNamespace_ReturnsTrue()
	{
		var (_, root) = TestCompilation.Parse(
			"""
			namespace Sample.Domain;

			public class Order { }
			"""
		);

		var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

		await Assert.That(new TypeIdentity("Order", "Sample.Domain").CouldMatchDeclaration(declaration)).IsTrue();
		await Assert.That(new TypeIdentity("Order", "Sample").CouldMatchDeclaration(declaration)).IsFalse();
		await Assert.That(new TypeIdentity("Order", null).CouldMatchDeclaration(declaration)).IsFalse();
	}

	[Test]
	public async Task CouldMatchDeclaration_GivenNestedTypeInSplitNamespace_ReturnsTrue()
	{
		var (_, root) = TestCompilation.Parse(
			"""
			namespace Sample
			{
				namespace Domain
				{
					public partial class Outer<T>
					{
						public class Inner { }
					}
				}
			}
			"""
		);

		var declaration = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Single(node => node.Identifier.ValueText == "Inner");

		var value = (new TypeIdentity("Outer", "Sample.Domain") with { GenericArity = 1 }).Nested("Inner");
		var wrongArity = new TypeIdentity("Outer", "Sample.Domain").Nested("Inner");

		await Assert.That(value.CouldMatchDeclaration(declaration)).IsTrue();
		await Assert.That(wrongArity.CouldMatchDeclaration(declaration)).IsFalse();
		await Assert.That(new TypeIdentity("Inner", "Sample.Domain").CouldMatchDeclaration(declaration)).IsFalse();
	}

	[Test]
	[Arguments("public class Target { }")]
	[Arguments("public struct Target { }")]
	[Arguments("public interface Target { }")]
	[Arguments("public record Target { }")]
	[Arguments("public record struct Target { }")]
	[Arguments("public enum Target { A }")]
	[Arguments("public delegate void Target();")]
	public async Task CouldMatchDeclaration_SupportsEveryTypeDefiningDeclaration(string declarationText)
	{
		var (_, root) = TestCompilation.Parse($"namespace Sample;\n\n{declarationText}");

		var declaration = root.DescendantNodes()
			.First(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

		await Assert.That(new TypeIdentity("Target", "Sample").CouldMatchDeclaration(declaration)).IsTrue();
		await Assert.That(new TypeIdentity("Other", "Sample").CouldMatchDeclaration(declaration)).IsFalse();
	}

	[Test]
	public async Task CouldMatchDeclaration_GivenGenericDelegate_ComparesArity()
	{
		var (_, root) = TestCompilation.Parse(
			"namespace Sample;\n\npublic delegate TResult Projector<T, TResult>(T value);"
		);

		var declaration = root.DescendantNodes().OfType<DelegateDeclarationSyntax>().Single();

		await Assert
			.That(
				(new TypeIdentity("Projector", "Sample") with { GenericArity = 2 }).CouldMatchDeclaration(declaration)
			)
			.IsTrue();

		await Assert.That(new TypeIdentity("Projector", "Sample").CouldMatchDeclaration(declaration)).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Declarations — semantic
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task MatchesDeclaration_ResolvesDeclaredSymbol()
	{
		var (compilation, root) = TestCompilation.CreateWithRoot(
			"""
			namespace Sample.Domain;

			public class Outer
			{
				public class Inner { }
			}
			"""
		);

		var model = compilation.GetSemanticModel(root.SyntaxTree);
		var inner = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Single(node => node.Identifier.ValueText == "Inner");

		var value = new TypeIdentity("Outer", "Sample.Domain").Nested("Inner");

		await Assert.That(value.MatchesDeclaration(inner, model)).IsTrue();
		await Assert.That(new TypeIdentity("Inner", "Sample.Domain").MatchesDeclaration(inner, model)).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// References — syntactic
	// ---------------------------------------------------------------------------------------------

	[Test]
	[Arguments("global::System.Collections.Generic.List<int>", true)]
	[Arguments("System.Collections.Generic.List<int>", true)]
	[Arguments("Generic.List<int>", true)]
	[Arguments("List<int>", true)]
	[Arguments("Other.List<int>", false)]
	[Arguments("System.Collections.List<int>", false)]
	[Arguments("global::Generic.List<int>", false)]
	[Arguments("List<int, string>", false)]
	[Arguments("List", false)]
	[Arguments("Queue<int>", false)]
	public async Task CouldMatchTypeReference_ChecksNameArityAndQualifier(string written, bool expected)
	{
		var typeSyntax = SyntaxFactory.ParseTypeName(written);
		var value = new TypeIdentity("List", "System.Collections.Generic") with { GenericArity = 1 };

		await Assert.That(value.CouldMatchTypeReference(typeSyntax)).IsEqualTo(expected);
	}

	[Test]
	[Arguments("int", true)]
	[Arguments("string", false)]
	[Arguments("System.Int32", true)]
	public async Task CouldMatchTypeReference_GivenPredefinedType_MatchesKeyword(string written, bool expected)
	{
		var typeSyntax = SyntaxFactory.ParseTypeName(written);
		TypeIdentity value = new(SpecialType.System_Int32);

		await Assert.That(value.CouldMatchTypeReference(typeSyntax)).IsEqualTo(expected);
	}

	[Test]
	public async Task CouldMatchTypeReference_GivenNamedType_RejectsComposedSyntax()
	{
		var value = new TypeIdentity("List", "System.Collections.Generic") with { GenericArity = 1 };

		await Assert.That(value.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("List<int>[]"))).IsFalse();
		await Assert.That(value.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("(List<int>, int)"))).IsFalse();
	}

	[Test]
	public async Task CouldMatchTypeReference_GivenNestedTypeQualifier_ReturnsTrue()
	{
		var value = new TypeIdentity("Outer", "Sample").Nested("Inner");

		await Assert.That(value.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("Sample.Outer.Inner"))).IsTrue();
		await Assert.That(value.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("Outer.Inner"))).IsTrue();
		await Assert.That(value.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("Inner"))).IsTrue();
		await Assert.That(value.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("Sample.Inner"))).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Composed references — syntactic
	// ---------------------------------------------------------------------------------------------

	[Test]
	[Arguments("int[]", true)]
	[Arguments("int[,]", false)]
	[Arguments("int", false)]
	[Arguments("int[][]", false)]
	public async Task CouldMatchTypeReference_GivenArrayReference_ComparesRanks(string written, bool expected)
	{
		var reference = new TypeIdentity(SpecialType.System_Int32).MakeArray();

		await Assert.That(reference.CouldMatchTypeReference(SyntaxFactory.ParseTypeName(written))).IsEqualTo(expected);
	}

	[Test]
	public async Task CouldMatchTypeReference_GivenJaggedArray_ComparesRunOrder()
	{
		// A rank-1 array of rank-2 arrays.
		var reference = new TypeIdentity(SpecialType.System_Int32).MakeArray(2).MakeArray(1);

		await Assert.That(reference.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("int[][,]"))).IsTrue();
		await Assert.That(reference.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("int[,][]"))).IsFalse();
	}

	[Test]
	public async Task CouldMatchTypeReference_IgnoresNullableAnnotationOnBothSides()
	{
		var reference = new TypeIdentity(SpecialType.System_String).MakeNullable();

		await Assert.That(reference.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("string?"))).IsTrue();
		await Assert.That(reference.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("string"))).IsTrue();
	}

	[Test]
	public async Task CouldMatchTypeReference_GivenTypeParameterOrDynamic_MatchesCore()
	{
		await Assert
			.That(TypeReference.ForTypeParameter("T").CouldMatchTypeReference(SyntaxFactory.ParseTypeName("T")))
			.IsTrue();

		await Assert
			.That(
				TypeReference
					.ForTypeParameter("T")
					.MakeArray()
					.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("T[]"))
			)
			.IsTrue();

		await Assert
			.That(TypeReference.ForTypeParameter("T").CouldMatchTypeReference(SyntaxFactory.ParseTypeName("TOther")))
			.IsFalse();

		await Assert
			.That(TypeReference.Dynamic.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("dynamic")))
			.IsTrue();
	}

	[Test]
	public async Task CouldMatchTypeReference_GivenPointerReference_ComparesDepth()
	{
		var reference = new TypeIdentity(SpecialType.System_Byte).MakePointer();

		await Assert.That(reference.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("byte*"))).IsTrue();
		await Assert.That(reference.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("byte**"))).IsFalse();
		await Assert.That(reference.CouldMatchTypeReference(SyntaxFactory.ParseTypeName("byte"))).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// References — semantic
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task MatchesTypeReference_ResolvesThroughUsings()
	{
		var (compilation, root) = TestCompilation.CreateWithRoot(
			"""
			using System.Collections.Generic;

			namespace Sample;

			public class Holder
			{
				public List<int> Values = null!;
				public List<string> Names = null!;
			}
			"""
		);

		var model = compilation.GetSemanticModel(root.SyntaxTree);
		var declarations = root.DescendantNodes().OfType<VariableDeclarationSyntax>().ToArray();

		var value = new TypeIdentity("List", "System.Collections.Generic").MakeGeneric(
			new TypeIdentity(SpecialType.System_Int32)
		);

		await Assert.That(value.MatchesTypeReference(declarations[0].Type, model)).IsTrue();
		await Assert.That(value.MatchesTypeReference(declarations[1].Type, model)).IsFalse();
	}

	[Test]
	public async Task MatchesTypeReference_GivenComposedReference_ResolvesArrays()
	{
		var (compilation, root) = TestCompilation.CreateWithRoot(
			"""
			namespace Sample;

			public class Holder
			{
				public int[] Ranked = null!;
				public int[,] Rectangular = null!;
			}
			"""
		);

		var model = compilation.GetSemanticModel(root.SyntaxTree);
		var declarations = root.DescendantNodes().OfType<VariableDeclarationSyntax>().ToArray();
		var reference = new TypeIdentity(SpecialType.System_Int32).MakeArray();

		await Assert.That(reference.MatchesTypeReference(declarations[0].Type, model)).IsTrue();
		await Assert.That(reference.MatchesTypeReference(declarations[1].Type, model)).IsFalse();
	}

	[Test]
	public async Task MatchesDeclaredType_ResolvesMemberTypes()
	{
		var (compilation, root) = TestCompilation.CreateWithRoot(
			"""
			using System;

			namespace Sample;

			public class Holder
			{
				public Guid Field;
				public Guid Property { get; set; }
				public Guid Method() => default;
				public string Other = null!;
			}
			"""
		);

		var model = compilation.GetSemanticModel(root.SyntaxTree);
		TypeIdentity guid = new("Guid", "System");

		var field = root.DescendantNodes().OfType<FieldDeclarationSyntax>().First();
		var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
		var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
		var other = root.DescendantNodes().OfType<FieldDeclarationSyntax>().Last();

		await Assert.That(guid.MatchesDeclaredType(field, model)).IsTrue();
		await Assert.That(guid.MatchesDeclaredType(property, model)).IsTrue();
		await Assert.That(guid.MatchesDeclaredType(method, model)).IsTrue();
		await Assert.That(guid.MatchesDeclaredType(other, model)).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// Attributes
	// ---------------------------------------------------------------------------------------------

	[Test]
	[Arguments("[Sentinel]", true)]
	[Arguments("[SentinelAttribute]", true)]
	[Arguments("[Sample.Sentinel]", true)]
	[Arguments("[global::Sample.SentinelAttribute]", true)]
	[Arguments("[Other.Sentinel]", false)]
	[Arguments("[Sentinels]", false)]
	public async Task CouldMatchAttribute_AcceptsBothSpellings(string attributeText, bool expected)
	{
		var (_, root) = TestCompilation.Parse($"namespace Sample;\n\n{attributeText}\npublic class Target {{ }}");

		var attribute = root.DescendantNodes().OfType<AttributeSyntax>().Single();
		TypeIdentity value = new("SentinelAttribute", "Sample");

		await Assert.That(value.CouldMatchAttribute(attribute)).IsEqualTo(expected);
	}

	[Test]
	public async Task HasAttribute_ResolvesAppliedAttribute()
	{
		var (compilation, root) = TestCompilation.CreateWithRoot(
			"""
			using System;

			namespace Sample;

			[AttributeUsage(AttributeTargets.Class)]
			public sealed class SentinelAttribute : Attribute { }

			[Sentinel]
			public class Flagged { }

			public class Unflagged { }
			"""
		);

		var model = compilation.GetSemanticModel(root.SyntaxTree);
		TypeIdentity value = new("SentinelAttribute", "Sample");

		var flagged = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Single(node => node.Identifier.ValueText == "Flagged");
		var unflagged = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Single(node => node.Identifier.ValueText == "Unflagged");

		await Assert.That(value.HasAttribute(flagged, model)).IsTrue();
		await Assert.That(value.HasAttribute(unflagged, model)).IsFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// TypeSyntaxFacts
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task GetDeclaredNamespace_GivenNoNamespace_ReturnsNull()
	{
		var (_, root) = TestCompilation.Parse("public class Rootless { }");
		var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

		await Assert.That(TypeSyntaxFacts.GetDeclaredNamespace(declaration)).IsNull();
	}

	[Test]
	public async Task TryGetCore_PeelsCompositionOutermostFirst()
	{
		await Assert
			.That(TypeSyntaxFacts.TryGetCore(SyntaxFactory.ParseTypeName("int[][,]"), out var core, out var modifiers))
			.IsTrue();

		await Assert.That(core.ToString()).IsEqualTo("int");

		await Assert.That(modifiers).IsNotNull();
		await Assert.That(modifiers.Count).IsEqualTo(2);
		await Assert.That(modifiers[0].Rank).IsEqualTo(1);
		await Assert.That(modifiers[1].Rank).IsEqualTo(2);
	}

	[Test]
	public async Task TryGetCore_GivenUnrepresentableSyntax_ReturnsFalse()
	{
		await Assert
			.That(TypeSyntaxFacts.TryGetCore(SyntaxFactory.ParseTypeName("(int, string)"), out _, out _))
			.IsFalse();
	}
}
