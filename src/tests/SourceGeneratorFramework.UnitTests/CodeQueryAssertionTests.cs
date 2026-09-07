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

					public static class Simple
					{
						public const string Name = "simple";
						public static int Count { get; set; }

						public static void DoWork(int value, int? optional, object? context) { }
					}
					"""
				)
			);
		}
	}

	[Test]
	public async Task HasGeneratedMethod_ReturnsTheMethodNode(CancellationToken cancellationToken)
	{
		var runner = new SourceGeneratorTestRunner<SimpleGenerator>();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var method = await Assert.That(result).HasGeneratedMethod("DoWork");

		await Assert.That(method).IsNotNull();
		await Assert.That(method.Node.Identifier.ValueText).IsEqualTo("DoWork");
	}

	[Test]
	public async Task HasGeneratedMethod_WithParameterTypes_ReturnsMatchingMethod(CancellationToken cancellationToken)
	{
		var runner = new SourceGeneratorTestRunner<SimpleGenerator>();
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
		var runner = new SourceGeneratorTestRunner<SimpleGenerator>();
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
		var runner = new SourceGeneratorTestRunner<SimpleGenerator>();
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
		var runner = new SourceGeneratorTestRunner<SimpleGenerator>();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var @class = await Assert.That(result).HasGeneratedClass(new TypeIdentity("Simple", "Generated"));

		await Assert.That(@class.Node.Identifier.ValueText).IsEqualTo("Simple");
	}

	[Test]
	public async Task HasGeneratedClass_WithWrongNamespace_Fails(CancellationToken cancellationToken)
	{
		var runner = new SourceGeneratorTestRunner<SimpleGenerator>();
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
		var runner = new SourceGeneratorTestRunner<SimpleGenerator>();
		var result = await runner.RunAsync("public sealed class Input { }", cancellationToken: cancellationToken);

		var field = await Assert.That(result).HasGeneratedField("Name");

		await Assert.That(field.Node.Declaration.Variables[0].Identifier.ValueText).IsEqualTo("Name");
	}
}
