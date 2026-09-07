using Purview.SourceGeneratorFramework.Examples;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;
using Purview.SourceGeneratorFramework.Testing.TUnit.Assertions;

namespace Purview.SourceGeneratorFramework.ExampleGenerator;

public class CodeWriterSampleGeneratorTests
	: TUnitSourceGeneratorTestBase<CodeWriterSampleGenerator, CodeWriterSampleTestOptions>
{
	[Test]
	public async Task GenerateSample_GeneratesDemonstrativeClass(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			[GenerateCodeWriterSample]
			public class SampleTarget { }
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasGeneratedClass("SampleTargetCodeWriterSample");
		await Assert.That(result).HasGeneratedField("_value");
		await Assert.That(result).HasGeneratedProperty("Value");
		await Assert.That(result).HasGeneratedProperty("DefaultAccessibility");
		await Assert.That(result).HasGeneratedProperty("Items");
		await Assert.That(result).HasGeneratedProperty("Fallback");
		await Assert.That(result).HasGeneratedMethod("Describe");
		await Assert.That(result).HasGeneratedMethod("Format");
		await Assert.That(result).HasGeneratedMethod("Categorize");
		await Assert.That(result).HasGeneratedMethod("Configure");
		await Assert.That(result).HasGeneratedMethod("Create");

		var defaultAccessibility = await Assert.That(result).HasGeneratedProperty("DefaultAccessibility");
		await Assert.That(defaultAccessibility.Node.Modifiers.ToString()).IsEqualTo("public");

		var classText = (
			await result.Generated().GetSyntaxTree("SampleTarget.CodeWriterSample.g.cs").GetTextAsync(cancellationToken)
		).ToString();
		await Assert
			.That(classText)
			.Contains("public global::System.Collections.Generic.IReadOnlyList<string> Items { get; }");
		await Assert.That(classText).Contains("public string? Fallback { get; set; } = null;");
		await Assert.That(classText).Contains("#if NET\n\t// This member is emitted only for .NET targets.\n#endif");
		await Assert.That(classText).Contains("#pragma warning disable CS8625");
		await Assert.That(classText).Contains("#pragma warning disable CS0618");
		await Assert.That(classText).Contains("#pragma warning restore CS0618");
	}

	[Test]
	public async Task GenerateSample_UsesStructuredStatements(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			[GenerateCodeWriterSample]
			public class SampleTarget { }
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		var describe = await Assert.That(result).HasGeneratedMethod("Describe");
		var describeText = describe.Node.ToString();
		await Assert.That(describeText).Contains("global::System.Console.WriteLine(\"Describe\");");
		await Assert.That(describeText).Contains("return value.ToString();");

		var constructor = result.Generated().GetConstructor("SampleTargetCodeWriterSample");
		await Assert.That(constructor.Node.ToString()).Contains("_value = value;");
	}

	[Test]
	public async Task GenerateSample_EmitsChainedInvocationAndNullConditional(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			[GenerateCodeWriterSample]
			public class SampleTarget { }
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		var configure = await Assert.That(result).HasGeneratedMethod("Configure");
		var configureText = configure.Node.ToString();
		await Assert.That(configureText).Contains("var hostKitOptions = source.Trim().ToUpper() ?? string.Empty;");
		await Assert
			.That(configureText)
			.Contains("var optionsBuilder = global::System.Array.Empty<global::System.String>().Clone();");
		await Assert.That(configureText).Contains("onBuilt?.Invoke();");
	}

	[Test]
	public async Task GenerateSample_EmitsConditionalBranches(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			[GenerateCodeWriterSample]
			public class SampleTarget { }
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		var categorize = await Assert.That(result).HasGeneratedMethod("Categorize");
		var categorizeText = categorize.Node.ToString();
		await Assert.That(categorizeText).Contains("if (value < 0)\n\t\t{\n\t\t\treturn \"negative\";\n\t\t}");
		await Assert.That(categorizeText).Contains("else if (value == 0)");
		await Assert.That(categorizeText).Contains("return \"zero\";");
		await Assert.That(categorizeText).Contains("return \"positive\";");
	}

	[Test]
	public async Task GenerateSample_EmitsNetConditionalReturn(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			[GenerateCodeWriterSample]
			public class SampleTarget { }
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		var format = await Assert.That(result).HasGeneratedMethod("Format");
		var formatText = format.Node.ToString();
		await Assert.That(formatText).Contains("#if NET");
		await Assert
			.That(formatText)
			.Contains(
				"return string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $\"Value: {_value}\");"
			);
		await Assert.That(formatText).Contains("#else");
		await Assert
			.That(formatText)
			.Contains("return global::System.FormattableString.Invariant($\"Value: {_value}\");");
		await Assert.That(formatText).Contains("#endif");
	}

	[Test]
	public async Task GenerateSample_EmitsObjectCreationExpressions(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			[GenerateCodeWriterSample]
			public class SampleTarget { }
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		var create = await Assert.That(result).HasGeneratedMethod("Create");
		var createText = create.Node.ToString();
		await Assert.That(createText).Contains("var builder = new global::System.Text.StringBuilder(onBuilt);");
		await Assert.That(createText).Contains("global::System.Text.StringBuilder options = new(onBuilt);");
		await Assert.That(createText).Contains("return options.ToString();");
	}
}
