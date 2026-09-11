using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

public class CompilationResultTests
{
	const string Source = """
		namespace Generated;

		public static class Sample
		{
			public const string Name = "sample";

			public static int Add(int a, int b) => a + b;
		}
		""";

	sealed class PassthroughGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterPostInitializationOutput(static output => output.AddSource("Sample.g.cs", Source));
		}
	}

	[Test]
	public async Task CompileToAssembly_ExposesLoadedAssembly_ThatCanExecute(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<PassthroughGenerator> runner = new();
		SourceGeneratorTestOptions options = new() { CompileToAssembly = true };

		var result = await runner.RunAsync("public sealed class Input { }", options, cancellationToken);

		await Assert.That(result.CompilationResult.Assembly).IsNotNull();

		var type = result.CompilationResult.Assembly!.GetType("Generated.Sample");
		await Assert.That(type).IsNotNull();

		var add = type!.GetMethod("Add", BindingFlags.Public | BindingFlags.Static);
		var invoked = add!.Invoke(null, [2, 3]);

		await Assert.That(invoked).IsEqualTo(5);
	}

	[Test]
	public async Task CompileToAssembly_LoadsIntoCollectibleContext(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<PassthroughGenerator> runner = new();
		SourceGeneratorTestOptions options = new() { CompileToAssembly = true };

		var result = await runner.RunAsync("public sealed class Input { }", options, cancellationToken);

		var context = AssemblyLoadContext.GetLoadContext(result.CompilationResult.Assembly!);

		await Assert.That(context).IsNotNull();
		await Assert.That(context!.IsCollectible).IsTrue();
	}

	[Test]
	public async Task CompileToAssembly_Dispose_DoesNotThrow(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<PassthroughGenerator> runner = new();
		SourceGeneratorTestOptions options = new() { CompileToAssembly = true };

		using (var result = await runner.RunAsync("public sealed class Input { }", options, cancellationToken))
		{
			await Assert.That(result.CompilationResult.Assembly).IsNotNull();
		}
	}

	[Test]
	public async Task Metadata_ReflectsGeneratedType_WithoutExecution(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<PassthroughGenerator> runner = new();
		SourceGeneratorTestOptions options = new() { CompileToAssembly = true };

		var result = await runner.RunAsync("public sealed class Input { }", options, cancellationToken);

		await Assert.That(result.CompilationResult.Metadata).IsNotNull();
		await Assert.That(result.CompilationResult.MetadataAssembly).IsNotNull();

		var type = result.CompilationResult.MetadataAssembly!.GetType("Generated.Sample");

		await Assert.That(type).IsNotNull();
		await Assert.That(type!.GetField("Name", BindingFlags.Public | BindingFlags.Static)).IsNotNull();
	}

	[Test]
	public async Task AssemblyAndMetadata_AreNull_WhenNotCompiled(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<PassthroughGenerator> runner = new();

		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		await Assert.That(result.CompilationResult.Assembly).IsNull();
		await Assert.That(result.CompilationResult.Metadata).IsNull();
	}
}
