using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework;

public class CodeQueryResultTests
{
	const string Source = """
		namespace Test;

		public class ComplexType { }

		public sealed class Sample
		{
			public const string Constant = "value";
			public int Count { get; set; }
			public string Name { get; set; } = "";

			public void DoWork(int value, int? optional, ComplexType complex) { }
			public int Compute(int left, int right) => left + right;
			public string Format(string format, object? value) => "";
		}

		public interface IContract { }
		public enum Level { None, Low, High }
		public delegate void Handler(int value);
		public record Person(string Name);
		""";

	static CodeQuery CreateQuery()
	{
		var (compilation, _) = TestCompilation.CreateWithRoot(Source);

		return new([.. compilation.SyntaxTrees], compilation);
	}

	[Test]
	public async Task ImplicitConversion_ToNode_ReturnsUnderlyingSyntaxNode()
	{
		var query = CreateQuery();

		ClassDeclarationSyntax @class = query.GetClass("Sample");

		await Assert.That(@class.Identifier.ValueText).IsEqualTo("Sample");
	}

	[Test]
	public async Task ImplicitConversion_ToQuery_IsScopedToNode()
	{
		var query = CreateQuery();

		CodeQuery scoped = query.GetClass("Sample");

		await Assert.That(scoped.HasProperty("Count")).IsTrue();
		await Assert.That(scoped.HasMethod("DoWork")).IsTrue();
		await Assert.That(scoped.HasClass("Sample")).IsFalse();
	}

	[Test]
	public async Task Node_ReturnsTheMatchedSyntaxNode()
	{
		var query = CreateQuery();

		var result = query.GetMethod("Compute");

		await Assert.That(result.Node.Identifier.ValueText).IsEqualTo("Compute");
	}

	[Test]
	public async Task ChainedMemberQueries_RunWithoutRePassingTheQuery()
	{
		var query = CreateQuery();
		var intType = TypeReference.Create<int>();
		var nullableInt = TypeReference.Create<int>().Nullable();
		var complexType = new TypeReference(new TypeIdentity("ComplexType", "Test"));

		await Assert.That(query.GetClass("Sample").HasProperty("Count", intType)).IsTrue();
		await Assert
			.That(query.GetClass("Sample").GetProperty("Name").HasType(TypeReference.Create<string>()))
			.IsTrue();
		await Assert.That(query.GetClass("Sample").GetMethod("Compute").HasReturnType(intType)).IsTrue();
		await Assert
			.That(query.GetClass("Sample").GetMethod("DoWork").HasParameters(intType, nullableInt, complexType))
			.IsTrue();
	}

	[Test]
	public async Task AttributeExtensions_FindAttributesOnReturnedNode()
	{
		// Arrange
		const string source = """
			using System;

			namespace Test;

			[Obsolete]
			public class Marked { }
			""";
		var (compilation, _) = TestCompilation.CreateWithRoot(source);
		var query = new CodeQuery([.. compilation.SyntaxTrees], compilation);

		// Act / Assert
		var @class = query.GetClass("Marked");

		await Assert.That(@class.HasAttribute("Obsolete")).IsTrue();
		await Assert.That(@class.HasAttribute("Missing")).IsFalse();
		await Assert.That(@class.GetAttribute("Obsolete")).IsNotNull();
		await Assert.That(@class.GetAttribute("Missing")).IsNull();
		await Assert.That(@class.GetAttributes().Length).IsEqualTo(1);
		await Assert.That(@class.GetAttributes()[0].Node.Name.ToString()).IsEqualTo("Obsolete");
	}

	[Test]
	public async Task GetAll_WrapsEachElement()
	{
		var query = CreateQuery();

		var methods = query.GetAll<MethodDeclarationSyntax>();

		await Assert.That(methods.Length).IsEqualTo(3);
		await Assert.That(methods[0].Node.Identifier.ValueText).IsEqualTo("DoWork");
		await Assert.That(methods[1].Node.Identifier.ValueText).IsEqualTo("Compute");
		await Assert.That(methods[2].Node.Identifier.ValueText).IsEqualTo("Format");
	}

	[Test]
	public async Task QueryProperty_ExposesScopedCodeQuery()
	{
		var query = CreateQuery();

		var scoped = query.GetClass("Sample").Query;

		await Assert.That(scoped.HasProperty("Count")).IsTrue();
		await Assert.That(scoped.GetMethod("Compute").Node.Identifier.ValueText).IsEqualTo("Compute");
		await Assert
			.That(scoped.GetField("Constant").Node.Declaration.Variables[0].Identifier.ValueText)
			.IsEqualTo("Constant");
	}
}
