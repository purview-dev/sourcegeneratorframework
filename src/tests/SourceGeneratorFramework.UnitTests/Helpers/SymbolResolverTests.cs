using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Purview.SourceGeneratorFramework.Helpers;

public class SymbolResolverTests
{
	static CSharpCompilation CreateCompilation()
	{
		var syntaxTree = CSharpSyntaxTree.ParseText("class C { }");
		return CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]
		);
	}

	[Test]
	public async Task Resolve_KnownType_ReturnsSymbol()
	{
		var compilation = CreateCompilation();

		var symbol = SymbolResolver.Resolve(compilation, "System.Object");

		await Assert.That(symbol).IsNotNull();
		await Assert.That(symbol!.Name).IsEqualTo("Object");
	}

	[Test]
	public async Task Resolve_UnknownType_ReturnsNull()
	{
		var compilation = CreateCompilation();

		var symbol = SymbolResolver.Resolve(compilation, "Does.Not.Exist");

		await Assert.That(symbol).IsNull();
	}

	[Test]
	public async Task Resolve_TypeValueObject_ReturnsSymbol()
	{
		var compilation = CreateCompilation();
		TypeIdentity type = new("Object", "System");

		var symbol = SymbolResolver.Resolve(compilation, type);

		await Assert.That(symbol).IsNotNull();
		await Assert.That(symbol!.Name).IsEqualTo("Object");
	}

	[Test]
	public async Task TryResolve_KnownType_ReturnsTrue()
	{
		var compilation = CreateCompilation();

		var found = SymbolResolver.TryResolve(compilation, "System.String", out var symbol);

		await Assert.That(found).IsTrue();
		await Assert.That(symbol).IsNotNull();
	}

	[Test]
	public async Task TryResolve_UnknownType_ReturnsFalse()
	{
		var compilation = CreateCompilation();

		var found = SymbolResolver.TryResolve(compilation, "Does.Not.Exist", out var symbol);

		await Assert.That(found).IsFalse();
		await Assert.That(symbol).IsNull();
	}

	[Test]
	public void Resolve_NullCompilation_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => SymbolResolver.Resolve(null!, "System.Object"));
	}
}
