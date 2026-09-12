using Microsoft.CodeAnalysis;
using Purview.SourceGeneratorFramework.Testing.TUnit.Assertions;
using TUnit.Assertions.Exceptions;

namespace Purview.SourceGeneratorFramework;

public class CodeQueryAssertionTests
{
	sealed class SimpleGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterPostInitializationOutput(static output =>
				output.AddSource(
					"Simple.g.cs",
					"""
					namespace Generated;

					[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
					public sealed class MarkerAttribute : System.Attribute { }

					[Marker]
					public static class Simple
					{
						public const string Name = "simple";
						public const int Constant = 42;
						public static int Count { get; set; }
						public static string? Description { get; set; }

						public static void DoWork(int value, int? optional, object? context) { }

						public static int Compute(int value) => value;
					}

					public class Service
					{
						public Service() { }
						public Service(string name) { }
						public string Label { get; set; } = string.Empty;
						public void Run() { }
					}
					"""
				)
			);
		}
	}

	[Test]
	public async Task HasGeneratedMethod_ReturnsTheMethodNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var method = await Assert.That(result).HasGeneratedMethod("DoWork");

		await Assert.That(method).IsNotNull();
		await Assert.That(method.Node.Identifier.ValueText).IsEqualTo("DoWork");
	}

	[Test]
	public async Task HasGeneratedMethod_WithParameterTypes_ReturnsMatchingMethod(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		TypeReference[] parameters =
		[
			TypeReference.Create<int>(),
			TypeReference.Create<int>().Nullable(),
			TypeReference.Create<object>().Nullable(),
		];
		var method = await Assert.That(result).HasGeneratedMethod("DoWork", parameters);

		await Assert.That(method.Node.ParameterList.Parameters.Count).IsEqualTo(3);
	}

	[Test]
	public async Task HasGeneratedClass_ReturnsTheClassNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var @class = await Assert.That(result).HasGeneratedClass("Simple");

		await Assert.That(@class.Node.Identifier.ValueText).IsEqualTo("Simple");
		await Assert.That(@class.Node.Members).IsNotEmpty();
	}

	[Test]
	public async Task HasGeneratedClass_WithTypeReferenceIdentity_ReturnsTheClassNode(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var @class = await Assert
			.That(result)
			.HasGeneratedClass(new TypeReference(new TypeIdentity("Simple", "Generated")));

		await Assert.That(@class.Node.Identifier.ValueText).IsEqualTo("Simple");
		await Assert.That(@class.Node.Members).IsNotEmpty();
	}

	[Test]
	public async Task HasGeneratedClass_WithTypeIdentityValue_ImplicitlyConverts(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var @class = await Assert.That(result).HasGeneratedClass(new TypeIdentity("Simple", "Generated"));

		await Assert.That(@class.Node.Identifier.ValueText).IsEqualTo("Simple");
	}

	[Test]
	public async Task HasGeneratedClass_WithWrongNamespace_Fails(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		await Assert
			.That(async () =>
				await Assert.That(result).HasGeneratedClass(new TypeReference(new TypeIdentity("Simple", "Other")))
			)
			.Throws<AssertionException>();
	}

	[Test]
	public async Task HasGeneratedField_ReturnsTheFieldNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var field = await Assert.That(result).HasGeneratedField("Name");

		await Assert.That(field.Node.Declaration.Variables[0].Identifier.ValueText).IsEqualTo("Name");
	}

	[Test]
	public async Task HasGeneratedMethod_FromCodeQuery_ReturnsTheMethodNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var method = await Assert.That(result.Generated()).HasGeneratedMethod("DoWork");

		await Assert.That(method).IsNotNull();
		await Assert.That(method.Node.Identifier.ValueText).IsEqualTo("DoWork");
	}

	[Test]
	public async Task HasGeneratedMethod_WithParameterTypes_FromCodeQuery_ReturnsMatchingMethod(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		TypeReference[] parameters =
		[
			TypeReference.Create<int>(),
			TypeReference.Create<int>().Nullable(),
			TypeReference.Create<object>().Nullable(),
		];
		var method = await Assert.That(result.Generated()).HasGeneratedMethod("DoWork", parameters);

		await Assert.That(method.Node.ParameterList.Parameters.Count).IsEqualTo(3);
	}

	[Test]
	public async Task HasGeneratedMethodReturnType_FromCodeQuery_ReturnsMatchingMethod(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var method = await Assert
			.That(result.Generated())
			.HasGeneratedMethodReturnType("Compute", TypeReference.Create<int>());

		await Assert.That(method.Node.Identifier.ValueText).IsEqualTo("Compute");
	}

	[Test]
	public async Task HasGeneratedClass_FromCodeQuery_ReturnsTheClassNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var @class = await Assert.That(result.Generated()).HasGeneratedClass("Simple");

		await Assert.That(@class.Node.Identifier.ValueText).IsEqualTo("Simple");
		await Assert.That(@class.Node.Members).IsNotEmpty();
	}

	[Test]
	public async Task HasGeneratedProperty_FromCodeQuery_ReturnsThePropertyNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var property = await Assert.That(result.Generated()).HasGeneratedProperty("Count");

		await Assert.That(property.Node.Identifier.ValueText).IsEqualTo("Count");
	}

	[Test]
	public async Task HasGeneratedField_FromCodeQuery_ReturnsTheFieldNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var field = await Assert.That(result.Generated()).HasGeneratedField("Name");

		await Assert.That(field.Node.Declaration.Variables[0].Identifier.ValueText).IsEqualTo("Name");
	}

	[Test]
	public async Task HasGeneratedSyntaxTree_FromCodeQuery_ReturnsTheTree(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var tree = await Assert.That(result.Generated()).HasGeneratedSyntaxTree("Simple.g.cs");

		await Assert.That(tree.FilePath.EndsWith("Simple.g.cs", StringComparison.Ordinal)).IsTrue();
	}

	[Test]
	public async Task HasGeneratedClass_FromCodeQuery_WithWrongNamespace_Fails(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		await Assert
			.That(async () =>
				await Assert
					.That(result.Generated())
					.HasGeneratedClass(new TypeReference(new TypeIdentity("Simple", "Other")))
			)
			.Throws<AssertionException>();
	}

	[Test]
	public async Task HasPropertyOfType_FromScopedResult_ReturnsThePropertyNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);
		var query = result.Generated();

		var @class = await Assert.That(query).HasGeneratedClass("Simple");
		var property = await Assert.That(@class).HasPropertyOfType("Count", TypeReference.Create<int>());

		await Assert.That(property.Node.Identifier.ValueText).IsEqualTo("Count");
	}

	[Test]
	public async Task HasPropertyOfType_WithNullableType_FromScopedResult_ReturnsThePropertyNode(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);
		var query = result.Generated();

		var @class = await Assert.That(query).HasGeneratedClass("Simple");
		var property = await Assert
			.That(@class)
			.HasPropertyOfType("Description", query.MakeNullable(TypeReference.Create<string>()));

		await Assert.That(property.Node.Identifier.ValueText).IsEqualTo("Description");
	}

	[Test]
	public async Task HasPropertyOfType_FromScopedResult_WithWrongType_Fails(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var @class = await Assert.That(result.Generated()).HasGeneratedClass("Simple");

		await Assert
			.That(async () => await Assert.That(@class).HasPropertyOfType("Count", TypeReference.Create<string>()))
			.Throws<AssertionException>();
	}

	[Test]
	public async Task HasFieldOfType_FromScopedResult_ReturnsTheFieldNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);
		var query = result.Generated();

		var @class = await Assert.That(query).HasGeneratedClass("Simple");
		var field = await Assert.That(@class).HasFieldOfType("Constant", TypeReference.Create<int>());

		await Assert.That(field.Node.Declaration.Variables[0].Identifier.ValueText).IsEqualTo("Constant");
	}

	[Test]
	public async Task HasMethodOfType_FromScopedResult_ReturnsTheMethodNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);
		var query = result.Generated();

		var @class = await Assert.That(query).HasGeneratedClass("Simple");
		var method = await Assert
			.That(@class)
			.HasMethodOfType(
				"DoWork",
				[
					TypeReference.Create<int>(),
					query.MakeNullable(TypeReference.Create<int>()),
					query.MakeNullable(TypeReference.Create<object>()),
				]
			);

		await Assert.That(method.Node.Identifier.ValueText).IsEqualTo("DoWork");
	}

	[Test]
	public async Task HasConstructorOfType_FromScopedResult_ReturnsTheConstructorNode(
		CancellationToken cancellationToken
	)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);
		var query = result.Generated();

		var service = query.GetClass("Service");
		var constructor = await Assert.That(service).HasConstructorOfType([TypeReference.Create<string>()]);

		await Assert.That(constructor.Node.ParameterList.Parameters.Count).IsEqualTo(1);
	}

	[Test]
	public async Task HasAttributeOfType_FromScopedResult_ReturnsTheAttributeNode(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);
		var query = result.Generated();

		var @class = await Assert.That(query).HasGeneratedClass("Simple");
		var attribute = await Assert.That(@class).HasAttributeOfType("Marker");

		await Assert.That(attribute.Node.Name.ToString()).IsEqualTo("Marker");
	}

	[Test]
	public async Task CodeQuery_MakeNullable_ResolvesNullableAnnotation(CancellationToken cancellationToken)
	{
		SourceGeneratorTestRunner<SimpleGenerator> runner = new();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var nullable = result.Generated().MakeNullable(TypeReference.Create<string>());

		await Assert.That(nullable.IsNullable).IsTrue();
	}
}
