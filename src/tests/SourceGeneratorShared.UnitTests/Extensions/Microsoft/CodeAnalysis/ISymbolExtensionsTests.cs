using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis;

public class ISymbolExtensionsTests
{
	const string Source = """
		namespace Sample;
		public class Holder
		{
			public const string NullConst = null!;
			public const string ValueConst = "value";
			public static string StaticField = null!;

			public void M(string a, string b = null!, int c = 0, string d = "value") { }

			public void L()
			{
				const string nullLocal = null!;
				const string valueLocal = "value";
			}
		}
		""";

	static CSharpCompilation CreateCompilation() => TestCompilation.Create(Source);

	[Test]
	public async Task HasNullDefaultValue_GivenConstNullField_ReturnsTrue()
	{
		var compilation = CreateCompilation();
		var holder = compilation.GetTypeByMetadataName("Sample.Holder")!;

		await Assert.That(holder.GetMembers("NullConst").Single().HasNullDefaultValue()).IsTrue();
	}

	[Test]
	public async Task HasNullDefaultValue_GivenConstNonNullField_ReturnsFalse()
	{
		var compilation = CreateCompilation();
		var holder = compilation.GetTypeByMetadataName("Sample.Holder")!;

		await Assert.That(holder.GetMembers("ValueConst").Single().HasNullDefaultValue()).IsFalse();
	}

	[Test]
	public async Task HasNullDefaultValue_GivenNonConstantField_ReturnsFalse()
	{
		var compilation = CreateCompilation();
		var holder = compilation.GetTypeByMetadataName("Sample.Holder")!;

		await Assert.That(holder.GetMembers("StaticField").Single().HasNullDefaultValue()).IsFalse();
	}

	[Test]
	public async Task HasNullDefaultValue_GivenOptionalNullParameter_ReturnsTrue()
	{
		var compilation = CreateCompilation();
		var holder = compilation.GetTypeByMetadataName("Sample.Holder")!;
		var method = holder.GetMembers("M").OfType<IMethodSymbol>().Single();

		await Assert.That(method.Parameters[1].HasNullDefaultValue()).IsTrue();
	}

	[Test]
	[Arguments(0)]
	[Arguments(2)]
	[Arguments(3)]
	public async Task HasNullDefaultValue_GivenNonNullDefaultOrOptionalParameter_ReturnsFalse(int index)
	{
		var compilation = CreateCompilation();
		var holder = compilation.GetTypeByMetadataName("Sample.Holder")!;
		var method = holder.GetMembers("M").OfType<IMethodSymbol>().Single();

		await Assert.That(method.Parameters[index].HasNullDefaultValue()).IsFalse();
	}

	[Test]
	public async Task HasNullDefaultValue_GivenConstNullLocal_ReturnsTrue(CancellationToken cancellationToken)
	{
		var compilation = CreateCompilation();
		var tree = compilation.SyntaxTrees.Single();
		var semanticModel = compilation.GetSemanticModel(tree);
		var localDeclaration = (await tree.GetRootAsync(cancellationToken))
			.DescendantNodes()
			.OfType<LocalDeclarationStatementSyntax>()
			.Single(local => local.Declaration.Variables.Any(variable => variable.Identifier.ValueText == "nullLocal"));
		var symbol = semanticModel.GetDeclaredSymbol(
			localDeclaration.Declaration.Variables[0],
			cancellationToken: cancellationToken
		)!;

		await Assert.That(symbol.HasNullDefaultValue()).IsTrue();
	}

	[Test]
	public async Task HasNullDefaultValue_GivenConstNonNullLocal_ReturnsFalse(CancellationToken cancellationToken)
	{
		var compilation = CreateCompilation();
		var tree = compilation.SyntaxTrees.Single();
		var semanticModel = compilation.GetSemanticModel(tree);
		var localDeclaration = (await tree.GetRootAsync(cancellationToken))
			.DescendantNodes()
			.OfType<LocalDeclarationStatementSyntax>()
			.Single(local =>
				local.Declaration.Variables.Any(variable => variable.Identifier.ValueText == "valueLocal")
			);
		var symbol = semanticModel.GetDeclaredSymbol(
			localDeclaration.Declaration.Variables[0],
			cancellationToken: cancellationToken
		)!;

		await Assert.That(symbol.HasNullDefaultValue()).IsFalse();
	}

	[Test]
	public async Task HasNullDefaultValue_GivenNullSymbol_ReturnsFalse()
	{
		await Assert.That(((ISymbol?)null).HasNullDefaultValue()).IsFalse();
	}
}
