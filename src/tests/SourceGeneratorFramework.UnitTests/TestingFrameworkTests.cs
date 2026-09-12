using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework;

public class TestingFrameworkTests
{
	sealed record CustomSourceGeneratorTestOptions : SourceGeneratorTestOptions
	{
		public string CustomValue { get; init; } = "custom";

		public static new CustomSourceGeneratorTestOptions Default => new();
	}

	const string GenerateAttributeSource = """
		namespace Test
		{
			[System.AttributeUsage(System.AttributeTargets.Class)]
			public sealed class GenerateAttribute : System.Attribute { }
		}
		""";

	internal sealed class TestGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var provider = context.SyntaxProvider.ForAttributeWithMetadataName(
				"Test.GenerateAttribute",
				(node, _) => node is ClassDeclarationSyntax,
				(ctx, _) => ctx.TargetSymbol.Name
			);

			context.RegisterSourceOutput(
				provider,
				static (spc, name) =>
					spc.AddSource(
						$"{name}.g.cs",
						$@"
namespace Test
{{
	public static class Generated_{name}
	{{
		public const string Name = ""{name}"";
	}}
}}
"
					)
			);
		}
	}

	internal sealed class InvalidSourceGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterPostInitializationOutput(static output =>
				output.AddSource(
					"Broken.g.cs",
					"""
					namespace Generated;
					public sealed class Broken
					{
						public MissingType Value { get; }
					}
					"""
				)
			);
		}
	}

	internal sealed class OptionsGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var validationValue = context.AnalyzerConfigOptionsProvider.Select(
				static (options, _) =>
				{
					options.GlobalOptions.TryGetValue(
						"build_property.PurviewSourceGeneratorFrameworkValidateCodeWriterScopes",
						out var value
					);
					return value;
				}
			);
			var customValue = context.AnalyzerConfigOptionsProvider.Select(
				static (options, _) =>
				{
					options.GlobalOptions.TryGetValue("build_property.CustomOption", out var value);
					return value;
				}
			);

			context.RegisterSourceOutput(
				validationValue,
				static (output, value) =>
					output.AddSource(
						"ScopeValidation.g.cs",
						$"internal static class ScopeValidation {{ internal const string Value = \"{value}\"; }}"
					)
			);
			context.RegisterSourceOutput(
				customValue,
				static (output, value) =>
					output.AddSource(
						"CustomOption.g.cs",
						$"internal static class CustomOption {{ internal const string Value = \"{value}\"; }}"
					)
			);
		}
	}

	[Test]
	public async Task RunAsync_MultipleSources_GeneratesForBoth()
	{
		var source1 =
			"""
				using Test;
				[Generate]
				public class A { }
				"""
			+ "\n"
			+ GenerateAttributeSource;
		var source2 =
			"""
				using Test;
				[Generate]
				public class B { }
				"""
			+ "\n"
			+ GenerateAttributeSource;

		SourceGeneratorTestRunner<TestGenerator> runner = new();
		var result = await runner.RunAsync([source1, source2]);

		result.AssertGeneratedSourceCount(2);
	}

	[Test]
	public async Task AssertSingleGeneratedSource_ReturnsGeneratedSource()
	{
		var source =
			"""
				using Test;
				[Generate]
				public class A { }
				"""
			+ "\n"
			+ GenerateAttributeSource;

		SourceGeneratorTestRunner<TestGenerator> runner = new();
		var result = await runner.RunAsync(source);

		var generated = result.AssertSingleGeneratedSource();

		await Assert.That(generated).Contains("public static class Generated_A");
	}

	[Test]
	public async Task AssertGeneratedSourceContains_MatchesGeneratedText()
	{
		var source =
			"""
				using Test;
				[Generate]
				public class A { }
				"""
			+ "\n"
			+ GenerateAttributeSource;

		SourceGeneratorTestRunner<TestGenerator> runner = new();
		var result = await runner.RunAsync(source);

		result.AssertGeneratedSourceContains("public static class Generated_A");
	}

	[Test]
	public async Task AssertNoCompilationErrors_PassingRun_DoesNotThrow()
	{
		var source =
			"""
				using Test;
				[Generate]
				public class A { }
				"""
			+ "\n"
			+ GenerateAttributeSource;

		SourceGeneratorTestRunner<TestGenerator> runner = new();
		var result = await runner.RunAsync(source);

		result.AssertNoCompilationErrors();
	}

	[Test]
	public async Task EnsureValid_GivenGeneratedCompilationError_ReportsSourceContext(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		SourceGeneratorTestRunner<InvalidSourceGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);
		DriverRunValidationException? exception = null;

		// Act
		try
		{
			result.EnsureValid();
		}
		catch (DriverRunValidationException caught)
		{
			exception = caught;
		}

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.CompilationErrors).IsNotEmpty();
		await Assert.That(exception.Message).Contains("Broken.g.cs (generated)");
		await Assert.That(exception.Message).Contains("CS0246");
		await Assert.That(exception.Message).Contains("public MissingType Value");
		await Assert.That(exception.Message).Contains("^");
	}

	[Test]
	public async Task RunAsync_DefaultOptions_PassesEnabledCodeWriterScopeValidationProperty(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		SourceGeneratorTestRunner<OptionsGenerator> runner = new();
		SourceGeneratorTestOptions options = new();

		// Act
		var result = await runner.RunAsync("public sealed class Input { }", options, cancellationToken);

		// Assert
		await Assert.That(options.ValidateCodeWriterScopes).IsTrue();
		await Assert.That(result.GetSource()).Contains("Value = \"True\"");
	}

	[Test]
	public async Task RunAsync_UnprefixedAnalyzerOption_IsAlsoExposedAsBuildProperty(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<OptionsGenerator> runner = new();
		var options = new SourceGeneratorTestOptions().WithAnalyzerConfigOptions(("CustomOption", "enabled"));

		var result = await runner.RunAsync("public sealed class Input { }", options, cancellationToken);

		var tree = result.GetGeneratedTree("CustomOption.g.cs");
		await Assert.That(tree).IsNotNull();
		await Assert.That((await tree!.GetTextAsync(cancellationToken)).ToString()).Contains("Value = \"enabled\"");
	}

	[Test]
	public async Task RunAsync_ReferencesGeneratorAssemblyThatContainsPublicContracts()
	{
		SourceGeneratorTestRunner<TestGenerator> runner = new();

		var result = await runner.RunAsync("public sealed class Input { }");

		await Assert
			.That(
				result.CompilationResult.Compilation.References.Any(reference =>
					string.Equals(
						reference.Display,
						typeof(TestGenerator).Assembly.Location,
						StringComparison.OrdinalIgnoreCase
					)
				)
			)
			.IsTrue();
	}

	[Test]
	[NotInParallel]
	public async Task Constructor_DerivedOptions_CopiesConfiguredDefaultWithoutSharingMutableCollections()
	{
		var originalDefault = SourceGeneratorTestOptions.Default;
		try
		{
			SourceGeneratorTestOptions.Default = originalDefault.WithAnalyzerConfigOptions(("Shared", "default"));

			CustomSourceGeneratorTestOptions first = new();
			CustomSourceGeneratorTestOptions second = new();

			first = first.WithAnalyzerConfigOptions(("OnlyFirst", "value"));

			await Assert.That(first.AnalyzerConfigOptions["Shared"]).IsEqualTo("default");
			await Assert.That(second.AnalyzerConfigOptions.ContainsKey("OnlyFirst")).IsFalse();
			await Assert.That(first.CustomValue).IsEqualTo("custom");
		}
		finally
		{
			SourceGeneratorTestOptions.Default = originalDefault;
		}
	}

	// ---------------------------------------------------------------------------------------------
	// Compile() preserves the concrete options type for downstream derived records
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Compile_OnBaseOptions_SetsCompileToAssembly()
	{
		SourceGeneratorTestOptions options = new();

		var result = options.Compile();

		await Assert.That(result.CompileToAssembly).IsTrue();
		await Assert.That(result.GetType()).IsEqualTo(typeof(SourceGeneratorTestOptions));
	}

	[Test]
	public async Task Compile_OnAnalyzerOptions_PreservesConcreteType()
	{
		AnalyzerTestOptions options = new();

		var result = options.Compile();

		await Assert.That(result.CompileToAssembly).IsTrue();
		await Assert.That(result.GetType()).IsEqualTo(typeof(AnalyzerTestOptions));
	}

	[Test]
	public async Task Compile_OnCodeFixOptions_PreservesConcreteType()
	{
		CodeFixTestOptions options = new();

		var result = options.Compile();

		await Assert.That(result.CompileToAssembly).IsTrue();
		await Assert.That(result.GetType()).IsEqualTo(typeof(CodeFixTestOptions));
	}

	[Test]
	public async Task Compile_OnCustomDerivedOptions_PreservesConcreteTypeAndProperties()
	{
		CustomSourceGeneratorTestOptions options = new();

		var result = options.Compile();

		await Assert.That(result.CompileToAssembly).IsTrue();
		await Assert.That(result.GetType()).IsEqualTo(typeof(CustomSourceGeneratorTestOptions));
		await Assert.That(result.CustomValue).IsEqualTo("custom");
	}

	[Test]
	public async Task Compile_OnTypedStaticDefault_PreservesConcreteTypeAndProperties()
	{
		var options = CustomSourceGeneratorTestOptions.Default;

		var result = options.Compile();

		await Assert.That(result.CompileToAssembly).IsTrue();
		await Assert.That(result.GetType()).IsEqualTo(typeof(CustomSourceGeneratorTestOptions));
		await Assert.That(result.CustomValue).IsEqualTo("custom");
	}
}
