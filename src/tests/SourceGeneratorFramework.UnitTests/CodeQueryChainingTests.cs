using Microsoft.CodeAnalysis;
using Purview.SourceGeneratorFramework.Testing.TUnit.Assertions;
using TUnit.Assertions.Exceptions;

namespace Purview.SourceGeneratorFramework;

public class CodeQueryChainingTests
{
	sealed class RichGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterPostInitializationOutput(static output =>
				output.AddSource(
					"Rich.g.cs",
					"""
					namespace Generated;

					public class ResourceDefinition { }

					public class ResourceDefinition<TResourceType> : ResourceDefinition
					{
						public string? Name { get; private set; }
					}

					public class ResourceDefinitionAttribute : System.Attribute { }

					public class Service
					{
						public string Name { get; private set; } = string.Empty;
						public int Count { get; set; }

						public void Run() { }

						void InternalHelper() { }

						private class Builder
						{
							public void Build() { }
						}
					}
					"""
				)
			);
		}
	}

	sealed class GlobalOnlyGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterPostInitializationOutput(static output =>
				output.AddSource(
					"GlobalOnly.g.cs",
					"""
					public class GlobalOnly { }
					"""
				)
			);
		}
	}

	static readonly TypeIdentity ResourceDefinition = new("ResourceDefinition", "Generated", arity: 1);
	static readonly TypeIdentity ResourceDefinitionBase = new("ResourceDefinition", "Generated");
	static readonly TypeIdentity ResourceDefinitionAttribute = new("ResourceDefinitionAttribute", "Generated");

	static async Task<DriverRunResult> RunRichAsync(CancellationToken cancellationToken) =>
		await new SourceGeneratorTestRunner<RichGenerator>().RunAsync(
			"public sealed class Input { }",
			cancellationToken: cancellationToken
		);

	static async Task<DriverRunResult> RunGlobalOnlyAsync(CancellationToken cancellationToken) =>
		await new SourceGeneratorTestRunner<GlobalOnlyGenerator>().RunAsync(
			"public sealed class Input { }",
			cancellationToken: cancellationToken
		);

	[Test]
	public async Task HasGeneratedClass_ThenIsInNamespace_Chains(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		var @class = await Assert.That(result.Generated()).HasGeneratedClass("Service").And.IsInNamespace("Generated");

		await Assert.That(@class.Node.Identifier.ValueText).IsEqualTo("Service");
	}

	[Test]
	public async Task HasGeneratedClass_ThenIsInNamespace_WrongNamespace_Fails(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		await Assert
			.That(async () =>
				await Assert.That(result.Generated()).HasGeneratedClass("Service").And.IsInNamespace("Other")
			)
			.Throws<AssertionException>();
	}

	[Test]
	public async Task HasGeneratedClass_ThenIsInGlobalNamespace_Passes(CancellationToken cancellationToken)
	{
		var result = await RunGlobalOnlyAsync(cancellationToken);

		var @class = await Assert.That(result.Generated()).HasGeneratedClass("GlobalOnly").And.IsInGlobalNamespace();

		await Assert.That(@class.Node.Identifier.ValueText).IsEqualTo("GlobalOnly");
	}

	[Test]
	public async Task HasGeneratedClass_ThenIsInNamespace_GlobalType_Fails(CancellationToken cancellationToken)
	{
		var result = await RunGlobalOnlyAsync(cancellationToken);

		await Assert
			.That(async () =>
				await Assert.That(result.Generated()).HasGeneratedClass("GlobalOnly").And.IsInNamespace("Generated")
			)
			.Throws<AssertionException>();
	}

	[Test]
	public async Task HasPropertyOfType_ThenGetterSetterAccessibility_Chains(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);
		var query = result.Generated();

		var property = await Assert
			.That(query)
			.HasGeneratedClass("Service")
			.And.HasPropertyOfType("Name", query.MakeNullable(TypeReference.Create<string>()))
			.And.WithGetterAccessibility(Accessibility.Public)
			.And.WithSetterAccessibility(Accessibility.Private);

		await Assert.That(property.Node.Identifier.ValueText).IsEqualTo("Name");
	}

	[Test]
	public async Task HasPropertyOfType_ImplicitAccessor_InheritsPropertyAccessibility(
		CancellationToken cancellationToken
	)
	{
		var result = await RunRichAsync(cancellationToken);

		await Assert
			.That(result.Generated())
			.HasGeneratedClass("Service")
			.And.HasPropertyOfType("Count", TypeReference.Create<int>())
			.And.WithSetterAccessibility(Accessibility.Public)
			.And.WithGetterAccessibility(Accessibility.Public);
	}

	[Test]
	public async Task HasPropertyOfType_ThenWrongSetterAccessibility_Fails(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		await Assert
			.That(async () =>
				await Assert
					.That(result.Generated())
					.HasGeneratedClass("Service")
					.And.HasPropertyOfType("Name", TypeReference.Create<string>())
					.And.WithSetterAccessibility(Accessibility.Public)
			)
			.Throws<AssertionException>();
	}

	[Test]
	public async Task HasMethodOfType_ThenWithAccessibility_Chains(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		var method = await Assert
			.That(result.Generated())
			.HasGeneratedClass("Service")
			.And.HasMethodOfType("Run", [])
			.And.WithAccessibility(Accessibility.Public);

		await Assert.That(method.Node.Identifier.ValueText).IsEqualTo("Run");
	}

	[Test]
	public async Task HasNestedType_ThenWithAccessibility_ThenHasMethod_Chains(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		var method = await Assert
			.That(result.Generated())
			.HasGeneratedClass("Service")
			.And.HasNestedType("Builder")
			.And.WithAccessibility(Accessibility.Private)
			.And.HasMethodOfType("Build", []);

		await Assert.That(method.Node.Identifier.ValueText).IsEqualTo("Build");
	}

	[Test]
	public async Task HasNestedType_WithWrongAccessibility_Fails(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		await Assert
			.That(async () =>
				await Assert
					.That(result.Generated())
					.HasGeneratedClass("Service")
					.And.HasNestedType("Builder")
					.And.WithAccessibility(Accessibility.Public)
			)
			.Throws<AssertionException>();
	}

	[Test]
	public async Task HasGeneratedClass_ThenWithBaseType_Chains(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		var attribute = await Assert
			.That(result.Generated())
			.HasGeneratedClass(ResourceDefinitionAttribute)
			.And.WithBaseType(TypeIdentity.Create<Attribute>());

		await Assert.That(attribute.Node.Identifier.ValueText).IsEqualTo("ResourceDefinitionAttribute");
	}

	[Test]
	public async Task HasGeneratedClass_Generic_ThenWithGenericTypeParametersAndBaseType_Chains(
		CancellationToken cancellationToken
	)
	{
		var result = await RunRichAsync(cancellationToken);

		var generic = await Assert
			.That(result.Generated())
			.HasGeneratedClass(ResourceDefinition)
			.And.WithGenericTypeParameters("TResourceType")
			.And.WithBaseType(ResourceDefinitionBase);

		await Assert.That(generic.Node.Identifier.ValueText).IsEqualTo("ResourceDefinition");
		await Assert.That(generic.Node.TypeParameterList!.Parameters.Count).IsEqualTo(1);
	}

	[Test]
	public async Task HasGeneratedClass_WithArity_FindsGenericForm(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		var generic = await Assert.That(result.Generated()).HasGeneratedClass("ResourceDefinition", 1);

		await Assert.That(generic.Node.TypeParameterList!.Parameters.Count).IsEqualTo(1);
	}

	[Test]
	public async Task HasGeneratedClass_Generic_ThenWrongGenericTypeParameter_Fails(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);

		await Assert
			.That(async () =>
				await Assert
					.That(result.Generated())
					.HasGeneratedClass(ResourceDefinition)
					.And.WithGenericTypeParameter("Wrong")
			)
			.Throws<AssertionException>();
	}

	[Test]
	public async Task ThreeWayChain_NamespacePropertyAndAccessibility(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);
		var query = result.Generated();

		await Assert
			.That(query)
			.HasGeneratedClass("Service")
			.And.IsInNamespace("Generated")
			.And.HasPropertyOfType("Name", query.MakeNullable(TypeReference.Create<string>()))
			.And.WithGetterAccessibility(Accessibility.Public);
	}

	[Test]
	public async Task QueryLevel_NamespaceAndGlobalNamespace_Checks(CancellationToken cancellationToken)
	{
		var rich = await RunRichAsync(cancellationToken);
		var globalOnly = await RunGlobalOnlyAsync(cancellationToken);

		await Assert.That(rich.Generated().GetClass("Service").IsInNamespace("Generated")).IsTrue();
		await Assert.That(rich.Generated().GetClass("Service").IsInGlobalNamespace()).IsFalse();
		await Assert.That(globalOnly.Generated().GetClass("GlobalOnly").IsInGlobalNamespace()).IsTrue();
		await Assert.That(globalOnly.Generated().GetClass("GlobalOnly").IsInNamespace("Generated")).IsFalse();
	}

	[Test]
	public async Task QueryLevel_Accessibility_Checks(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);
		var query = result.Generated();

		await Assert.That(query.GetClass("Service").HasAccessibility(Accessibility.Public)).IsTrue();
		await Assert
			.That(query.GetClass("Service").GetMethod("InternalHelper").HasAccessibility(Accessibility.Private))
			.IsTrue();
		await Assert
			.That(query.GetClass("Service").GetNestedType("Builder").HasAccessibility(Accessibility.Private))
			.IsTrue();
		await Assert
			.That(query.GetClass("Service").GetProperty("Name").HasGetterAccessibility(Accessibility.Public))
			.IsTrue();
		await Assert
			.That(query.GetClass("Service").GetProperty("Name").HasSetterAccessibility(Accessibility.Private))
			.IsTrue();
		await Assert
			.That(query.GetClass("Service").GetProperty("Count").HasSetterAccessibility(Accessibility.Public))
			.IsTrue();
	}

	[Test]
	public async Task QueryLevel_NestedTypeAndGenericAndBaseType_Checks(CancellationToken cancellationToken)
	{
		var result = await RunRichAsync(cancellationToken);
		var query = result.Generated();

		await Assert.That(query.GetClass("Service").HasNestedType("Builder")).IsTrue();
		await Assert.That(query.GetClass("Service").GetNestedType("Builder").HasMethod("Build")).IsTrue();

		await Assert.That(query.GetClass("ResourceDefinition", 1).HasGenericTypeParameter("TResourceType")).IsTrue();
		await Assert.That(query.GetClass("ResourceDefinition", 1).HasGenericTypeParameters("TResourceType")).IsTrue();
		await Assert.That(query.GetClass("ResourceDefinition", 1).HasBaseType(ResourceDefinitionBase)).IsTrue();
		await Assert.That(query.HasClass("ResourceDefinition", 2)).IsFalse();
	}
}
