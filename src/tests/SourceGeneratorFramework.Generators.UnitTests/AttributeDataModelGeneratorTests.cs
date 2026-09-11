using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Purview.SourceGeneratorFramework.Generators;

public class AttributeDataModelGeneratorTests
	: TUnitSourceGeneratorTestBase<AttributeDataModelGenerator, AttributeDataModelTestOptions>
{
	[Test]
	public async Task Generate_RequiredAttributeData_DefaultNamedAndNestedModel(CancellationToken cancellationToken)
	{
		var source = """
			using Microsoft.CodeAnalysis;
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate(typeof(ValidationAttribute), MatchByInheritance = true)]
				public readonly partial record struct ValidationAttributeData(
					string? ErrorMessage,
					string? ErrorMessageResourceName,
					TypeIdentity? ErrorMessageResourceType
				);

				[Generate(typeof(RequiredAttribute))]
				public readonly partial record struct RequiredAttributeData(
					bool AllowEmptyStrings,
					[NestedModel] ValidationAttributeData ValidationAttribute
				);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		await Assert.That(result.LogEntries).IsNotEmpty();
		await Assert
			.That(
				result.LogEntries.Any(entry => entry.Message.Contains("generation context", StringComparison.Ordinal))
			)
			.IsTrue();

		var generated = await GetGeneratedStringAsync(
			result,
			"RequiredAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct RequiredAttributeData");
		await Assert.That(generated).Contains("bool Exists");
		await Assert.That(generated).Contains("bool AllowEmptyStrings");
		await Assert
			.That(generated)
			.Contains(
				"global::System.Collections.Generic.IEnumerable<(RequiredAttributeData Instance, global::Microsoft.CodeAnalysis.AttributeData Attribute)> AllAttributeData("
			);
		await Assert.That(generated).Contains("yield return (instance, attributes[i]);");
		await Assert.That(generated).Contains("global::Microsoft.CodeAnalysis.ISymbol symbol\n\t\t)");
		await Assert.That(generated).Contains("return AllAttributeData(symbol.GetAttributes());");
		await Assert.That(generated).Contains("return FromAttributeData(symbol.GetAttributes());");
		await Assert.That(generated).Contains("return FromAttributeData(symbol.GetAttributes(), out attribute);");
		await Assert.That(generated).Contains("global::Test.ValidationAttributeData ValidationAttribute");
		await Assert
			.That(generated)
			.Contains("attributeData.TryGetNamedArgument<bool>(\"AllowEmptyStrings\", out var allowEmptyStrings)");
		await Assert
			.That(generated)
			.Contains(
				"var validationAttribute = global::Test.ValidationAttributeData.FromAttributeData(attributeData)"
			);
		await Assert
			.That(generated)
			.Contains(
				"public static readonly RequiredAttributeData Empty = new(false, default(bool), default(global::Test.ValidationAttributeData))"
			);
	}

	[Test]
	public async Task Generate_LengthAttributeData_CtorIndex(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate(typeof(LengthAttribute))]
				public readonly partial record struct LengthAttributeData(
					[Argument(0)] int MinimumLength,
					[Argument(1)] int MaximumLength
				);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"LengthAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct LengthAttributeData");
		await Assert.That(generated).Contains("int MinimumLength");
		await Assert.That(generated).Contains("int MaximumLength");
		await Assert.That(generated).Contains("attributeData.TryGetConstructorArgument<int>(0, out var minimumLength)");
		await Assert.That(generated).Contains("attributeData.TryGetConstructorArgument<int>(1, out var maximumLength)");
		await Assert
			.That(generated)
			.Contains("public static readonly LengthAttributeData Empty = new(false, default(int), default(int))");
	}

	[Test]
	public async Task Generate_StringLengthAttributeData_CtorNameAndDefaultValue(CancellationToken cancellationToken)
	{
		var source = """
			using Microsoft.CodeAnalysis;
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate(typeof(ValidationAttribute), MatchByInheritance = true)]
				public readonly partial record struct ValidationAttributeData(
					string? ErrorMessage
				);

				[Generate(typeof(StringLengthAttribute))]
				public readonly partial record struct StringLengthAttributeData(
					[Argument("maximumLength", int.MaxValue)] int MaximumLength,
					int MinimumLength,
					[NestedModel] ValidationAttributeData ValidationAttribute
				);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"StringLengthAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("int MaximumLength");
		await Assert.That(generated).Contains("int MinimumLength");
		await Assert
			.That(generated)
			.Contains("var maximumLength = attributeData.GetConstructorArgument<int>(\"maximumLength\", 2147483647)");
		await Assert
			.That(generated)
			.Contains("attributeData.TryGetNamedArgument<int>(\"MinimumLength\", out var minimumLength)");
		await Assert
			.That(generated)
			.Contains(
				"public static readonly StringLengthAttributeData Empty = new(false, default(int), default(int), default(global::Test.ValidationAttributeData))"
			);
	}

	[Test]
	public async Task Generate_HostKitAttribute_CtorAndNamedCombined(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test
			{
				[Generate("Test.HostKitAttribute")]
				public readonly partial record struct HostKitAttributeData(
					[Argument("name")] string? Name,
					string? ExtensionMethodName,
					[Argument("generateOptions")] [Property(true)] bool GenerateOptions
				);

				public class HostKitAttribute : System.Attribute
				{
					public HostKitAttribute() { }
					public HostKitAttribute(string name, bool generateOptions = true) { }
					public HostKitAttribute(bool generateOptions) { }
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"HostKitAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct HostKitAttributeData");
		await Assert.That(generated).Contains("string? Name");
		await Assert.That(generated).Contains("bool GenerateOptions");
		await Assert
			.That(generated)
			.Contains("attributeData.TryGetConstructorArgument<string>(\"name\", out var name)");
		await Assert
			.That(generated)
			.Contains("if (!attributeData.TryGetConstructorArgument<bool>(\"generateOptions\", out generateOptions))");
		await Assert
			.That(generated)
			.Contains("if (!attributeData.TryGetNamedArgument<bool>(\"GenerateOptions\", out generateOptions))");
		await Assert.That(generated).Contains("generateOptions = true");
	}

	[Test]
	public async Task Generate_AutoDiscover_DiscoversNamedArguments(CancellationToken cancellationToken)
	{
		var source = """
using Purview.SourceGeneratorFramework.Generators;
using System.ComponentModel.DataAnnotations;

namespace Test;

[Generate(typeof(RequiredAttribute), AutoDiscover = true)]
public readonly partial record struct RequiredAttributeData;
""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"RequiredAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct RequiredAttributeData");
		await Assert.That(generated).Contains("bool AllowEmptyStrings");
		await Assert
			.That(generated)
			.Contains("attributeData.TryGetNamedArgument<bool>(\"AllowEmptyStrings\", out var allowEmptyStrings)");
		await Assert
			.That(generated)
			.Contains("public static readonly RequiredAttributeData Empty = new(false, default(bool))");
	}

	[Test]
	public async Task Generate_Exclude_SkipsProperty(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate(typeof(RequiredAttribute))]
				public readonly partial record struct RequiredAttributeData(
					bool AllowEmptyStrings,
					[Exclude] int Ignored
				);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"RequiredAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("bool AllowEmptyStrings");
		await Assert.That(generated).DoesNotContain("int Ignored");
		await Assert
			.That(generated)
			.Contains("public static readonly RequiredAttributeData Empty = new(false, default(bool))");
	}

	[Test]
	public async Task Generate_NestedModelNotGenerated_SkipsGeneration(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate(typeof(RequiredAttribute))]
				public readonly partial record struct RequiredAttributeData(
					[NestedModel] NotAModel NotAModel
				);

				public readonly partial record struct NotAModel;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// ADM0004 is reported by the analyzer; the generator only skips processing the invalid model.
		await Assert.That(result.DriverResult.Diagnostics).DoesNotContain(d => d.Id == "ADM0004");

		var generated = await GetGeneratedStringAsync(
			result,
			"RequiredAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);
		await Assert.That(generated).IsNull();
	}

	[Test]
	public async Task Generate_NonNullableReferenceTypeWithoutDefault_StillGenerates(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate(typeof(RequiredAttribute))]
				public readonly partial record struct RequiredAttributeData(
					string Name
				);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// ADM0006 is non-blocking and reported by the analyzer; the generator still emits the model.
		await Assert.That(result.DriverResult.Diagnostics).DoesNotContain(d => d.Id == "ADM0006");

		var generated = await GetGeneratedStringAsync(
			result,
			"RequiredAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct RequiredAttributeData");
		await Assert.That(generated).Contains("string Name");
		await Assert.That(generated).Contains("new(false, default(string)!)");
	}

	[Test]
	public async Task Generate_StringTargetAttributeData_NamedArgument(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate("System.ComponentModel.DataAnnotations.RequiredAttribute")]
				public readonly partial record struct RequiredAttributeData(
					bool AllowEmptyStrings
				);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"RequiredAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct RequiredAttributeData");
		await Assert.That(generated).Contains("bool AllowEmptyStrings");
		await Assert
			.That(generated)
			.Contains("attributeData.TryGetNamedArgument<bool>(\"AllowEmptyStrings\", out var allowEmptyStrings)");
		await Assert.That(generated).Contains("new(\"RequiredAttribute\", \"System.ComponentModel.DataAnnotations\")");
	}

	[Test]
	public async Task Generate_StringTarget_WithAutoDiscover_SkipsGeneration(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate("System.ComponentModel.DataAnnotations.RequiredAttribute", AutoDiscover = true)]
				public readonly partial record struct RequiredAttributeData;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// ADM0007 is reported by the analyzer; the generator only skips processing the invalid model.
		await Assert.That(result.DriverResult.Diagnostics).DoesNotContain(d => d.Id == "ADM0007");

		var generated = await GetGeneratedStringAsync(
			result,
			"RequiredAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);
		await Assert.That(generated).IsNull();
	}

	[Test]
	public async Task Generate_StringTarget_ConstructorArrayOfTypedConstant(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Immutable;
			using Microsoft.CodeAnalysis;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test
			{
				[Generate("TestAttribute")]
				public readonly partial record struct TestAttributeData(
					[Argument(0)] ImmutableArray<TypedConstant> Values
				);

				public class TestAttribute : System.Attribute
				{
					public TestAttribute(params object?[] values) { }
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"TestAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct TestAttributeData");
		await Assert
			.That(generated)
			.Contains(
				"global::System.Collections.Immutable.ImmutableArray<global::Microsoft.CodeAnalysis.TypedConstant> Values"
			);
		await Assert
			.That(generated)
			.Contains(
				"attributeData.TryGetConstructorArgument<global::System.Collections.Immutable.ImmutableArray<global::Microsoft.CodeAnalysis.TypedConstant>>(0, out var values)"
			);
	}

	[Test]
	public async Task Generate_TypedConstantWithStringDefault_SkipsGeneration(CancellationToken cancellationToken)
	{
		var source = """
			using Microsoft.CodeAnalysis;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test
			{
				[Generate("TestAttribute")]
				public readonly partial record struct TestAttributeData(
					[Argument(0, "Test.Mode.Inherit")]
					[Property("Test.Mode.Inherit", Name = "Mode")]
					TypedConstant Mode
				);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// ADM0005 is reported by the analyzer; the generator only skips processing the invalid model.
		await Assert.That(result.DriverResult.Diagnostics).DoesNotContain(d => d.Id == "ADM0005");

		var generated = await GetGeneratedStringAsync(
			result,
			"TestAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);
		await Assert.That(generated).IsNull();
	}

	[Test]
	public async Task Generate_PlainStructWithPrimaryConstructor_PreservesDeclarationKind(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[Generate("Test.KnownTypeAttribute")]
			readonly partial struct KnownTypesAttributeData(
				[Property("Test.RegistryType.Inherit", IsEnum = true)]
				string Type
			);
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"KnownTypesAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(result.CompilationResult.Diagnostics).DoesNotContain(d => d.Id == "CS0261");
		await Assert.That(result.CompilationResult.Diagnostics).DoesNotContain(d => d.Id == "CS7036");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial struct KnownTypesAttributeData");
		await Assert.That(generated).DoesNotContain("record struct KnownTypesAttributeData");
		await Assert.That(generated).Contains(": this(Type)");
	}

	[Test]
	public async Task Generate_NonNullableReferenceType_GeneratesDefaultSuppress(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;
			using System.ComponentModel.DataAnnotations;

			namespace Test
			{
				[Generate(typeof(RequiredAttribute))]
				public readonly partial record struct RequiredAttributeData(
					string ErrorMessage
				);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"RequiredAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains("public static readonly RequiredAttributeData Empty = new(false, default(string)!)");
	}

	[Test]
	[SuppressMessage("Design", "CA1506:Avoid excessive class coupling")]
	public async Task Generate_ResourceDefinitionAttributeData_PullsDataOut(CancellationToken cancellationToken)
	{
		var source = """
			using Microsoft.CodeAnalysis;
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Aspire.Hosting.ApplicationModel
			{
				public interface IResource { }
			}

			namespace Test
			{
				[global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
				public class ResourceDefinitionAttribute : global::System.Attribute
				{
					public ResourceDefinitionAttribute() { }
					public ResourceDefinitionAttribute(string name, string propertyName) { Name = name; PropertyName = propertyName; }
					public ResourceDefinitionAttribute(string name) { Name = name; }
					public string? Name { get; set; }
					public string? PropertyName { get; set; }
				}

				[global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
				public class ResourceDefinitionAttribute<TResource> : ResourceDefinitionAttribute
					where TResource : class, global::Aspire.Hosting.ApplicationModel.IResource
				{
					public ResourceDefinitionAttribute() { }
					public ResourceDefinitionAttribute(string name, string propertyName) : base(name, propertyName) { }
					public ResourceDefinitionAttribute(string name) : base(name) { }
				}

				[Generate(typeof(ResourceDefinitionAttribute), MatchByInheritance = true)]
				public readonly partial record struct ResourceDefinitionAttributeData(
					[Argument("name")] string? Name,
					[Argument("propertyName")] string? PropertyName,
					[GenericTypeArgument] TypeIdentity AspireResourceType
				);

				[ResourceDefinition("myResource", "MyResource")]
				public class MyResource : global::Aspire.Hosting.ApplicationModel.IResource { }

				[ResourceDefinition<MyResource>("myGenericResource", "MyGenericResource")]
				public class MyGenericResource : global::Aspire.Hosting.ApplicationModel.IResource { }
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		result.AssertNoGenerationExceptions().AssertNoLogErrors();

		var generated = await GetGeneratedStringAsync(
			result,
			"ResourceDefinitionAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct ResourceDefinitionAttributeData");
		await Assert.That(generated).Contains("string? Name");
		await Assert.That(generated).Contains("string? PropertyName");
		await Assert
			.That(generated)
			.Contains("global::Purview.SourceGeneratorFramework.TypeIdentity AspireResourceType");
		await Assert
			.That(generated)
			.Contains(
				"attributeData.TryGetGenericTypeArgument<global::Purview.SourceGeneratorFramework.TypeIdentity>(0, out var aspireResourceType)"
			);
		await Assert
			.That(generated)
			.Contains(
				"(!TargetAttribute.Equals(attributeData.AttributeClass) && !global::Purview.SourceGeneratorFramework.Helpers.TypeHelpers.InheritsFrom(attributeData.AttributeClass, TargetAttribute))"
			);

		var runtimeSource = """
			using Microsoft.CodeAnalysis;
			using Purview.SourceGeneratorFramework;

			namespace Aspire.Hosting.ApplicationModel
			{
				public interface IResource { }
			}

			namespace Test
			{
				[global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
				public class ResourceDefinitionAttribute : global::System.Attribute
				{
					public ResourceDefinitionAttribute() { }
					public ResourceDefinitionAttribute(string name, string propertyName) { Name = name; PropertyName = propertyName; }
					public ResourceDefinitionAttribute(string name) { Name = name; }
					public string? Name { get; set; }
					public string? PropertyName { get; set; }
				}

				[global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
				public class ResourceDefinitionAttribute<TResource> : ResourceDefinitionAttribute
					where TResource : class, global::Aspire.Hosting.ApplicationModel.IResource
				{
					public ResourceDefinitionAttribute() { }
					public ResourceDefinitionAttribute(string name, string propertyName) : base(name, propertyName) { }
					public ResourceDefinitionAttribute(string name) : base(name) { }
				}

				public readonly partial record struct ResourceDefinitionAttributeData(
					string? Name,
					string? PropertyName,
					TypeIdentity AspireResourceType
				);

				[ResourceDefinition("myResource", "MyResource")]
				public class MyResource : global::Aspire.Hosting.ApplicationModel.IResource { }

				[ResourceDefinition<MyResource>("myGenericResource", "MyGenericResource")]
				public class MyGenericResource : global::Aspire.Hosting.ApplicationModel.IResource { }
			}
			""";

		var generatedTree = result.GetGeneratedTree("ResourceDefinitionAttributeData.AttributeDataModel.g.cs");
		var generatedText = (await generatedTree!.GetTextAsync(cancellationToken)).ToString();
		var runtimeSyntaxTree = CSharpSyntaxTree.ParseText(runtimeSource, cancellationToken: cancellationToken);
		var generatedSyntaxTree = CSharpSyntaxTree.ParseText(generatedText, cancellationToken: cancellationToken);

		var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(
			Path.PathSeparator,
			StringSplitOptions.RemoveEmptyEntries
		);

		var references = trustedAssemblies
			.Where(path =>
				!path.EndsWith("Purview.SourceGeneratorFramework.Generators.dll", StringComparison.OrdinalIgnoreCase)
			)
			.Select(path => MetadataReference.CreateFromFile(path))
			.ToArray();

		var runtimeCompilation = CSharpCompilation.Create(
			"RuntimeAssembly",
			[runtimeSyntaxTree, generatedSyntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		var runtimeCompilationDiagnostics = runtimeCompilation.GetDiagnostics(cancellationToken);
		var runtimeErrors = runtimeCompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		await Assert.That(runtimeErrors).IsEmpty();

		await using MemoryStream assemblyStream = new();
		var emitResult = runtimeCompilation.Emit(assemblyStream, cancellationToken: cancellationToken);
		await Assert.That(emitResult.Success).IsTrue();

		assemblyStream.Position = 0;
		var assembly = System.Reflection.Assembly.Load(assemblyStream.ToArray());

		var runtimeCompilationForAttributes = CSharpCompilation.Create(
			"AttributeAssembly",
			[runtimeSyntaxTree, generatedSyntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);
		var myResourceType = runtimeCompilationForAttributes.GetTypeByMetadataName("Test.MyResource");
		var myGenericResourceType = runtimeCompilationForAttributes.GetTypeByMetadataName("Test.MyGenericResource");
		await Assert.That(myResourceType).IsNotNull();
		await Assert.That(myGenericResourceType).IsNotNull();

		var resourceAttribute = myResourceType!
			.GetAttributes()
			.First(a => a.AttributeClass?.Name == "ResourceDefinitionAttribute");
		var genericResourceAttribute = myGenericResourceType!
			.GetAttributes()
			.First(a => a.AttributeClass?.Name == "ResourceDefinitionAttribute");

		var dataType = assembly.GetType("Test.ResourceDefinitionAttributeData");
		await Assert.That(dataType).IsNotNull();

		var fromAttributeDataMethod = dataType!
			.GetMethods()
			.First(m =>
				m.Name == "FromAttributeData"
				&& m.GetParameters().Length == 1
				&& m.GetParameters()[0].ParameterType == typeof(AttributeData)
			);

		var resourceData = fromAttributeDataMethod.Invoke(null, [resourceAttribute]);
		var genericResourceData = fromAttributeDataMethod.Invoke(null, [genericResourceAttribute]);

		var nameProperty = dataType.GetProperty("Name");
		var propertyNameProperty = dataType.GetProperty("PropertyName");
		var aspireResourceTypeProperty = dataType.GetProperty("AspireResourceType");

		await Assert.That(nameProperty!.GetValue(resourceData)).IsEqualTo("myResource");
		await Assert.That(propertyNameProperty!.GetValue(resourceData)).IsEqualTo("MyResource");
		await Assert
			.That((TypeIdentity)aspireResourceTypeProperty!.GetValue(resourceData)!)
			.IsEqualTo(TypeIdentity.Empty);

		await Assert.That(nameProperty.GetValue(genericResourceData)).IsEqualTo("myGenericResource");
		await Assert.That(propertyNameProperty.GetValue(genericResourceData)).IsEqualTo("MyGenericResource");
		var aspireResourceType = (TypeIdentity?)aspireResourceTypeProperty.GetValue(genericResourceData);
		await Assert.That(aspireResourceType).IsNotNull();
		await Assert.That(aspireResourceType!.Value.Matches(myResourceType)).IsTrue();
	}

	[Test]
	public async Task Generate_SystemTypeProperty_MapsToINamedTypeSymbol(CancellationToken cancellationToken)
	{
		var source = """
			using Microsoft.CodeAnalysis;
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test
			{
			[Generate(typeof(TestingAttribute))]
			public readonly partial record struct TestingAttributeData(
				[Argument("typeThing")] [Property] TypeIdentity? TypeThing
			);

				public class TestingAttribute : System.Attribute
				{
					public TestingAttribute(Type typeThing) { TypeThing = typeThing; }
					public Type? TypeThing { get; set; }
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		result.AssertNoGenerationExceptions().AssertNoLogErrors();

		var generated = await GetGeneratedStringAsync(
			result,
			"TestingAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("readonly partial record struct TestingAttributeData");
		await Assert.That(generated).Contains("global::Purview.SourceGeneratorFramework.TypeIdentity? TypeThing");
		await Assert
			.That(generated)
			.Contains(
				"attributeData.TryGetConstructorArgument<global::Purview.SourceGeneratorFramework.TypeIdentity>(\"typeThing\", out typeThing)"
			);
		await Assert
			.That(generated)
			.Contains(
				"attributeData.TryGetNamedArgument<global::Purview.SourceGeneratorFramework.TypeIdentity>(\"TypeThing\", out typeThing)"
			);
	}

	[Test]
	public async Task Generate_NullableReferenceTypeWithMultipleSources_DoesNotEmitCS8600(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test
			{
				[Generate("Test.HostKitAttribute")]
				public readonly partial record struct HostKitAttributeData(
					[Property]
					[Argument("name")]
						string? Name,
					string? ExtensionMethodName,
					[Property]
					[Argument("generateOptions", true)]
						bool GenerateOptions
				);

				public class HostKitAttribute : System.Attribute
				{
					public HostKitAttribute() { }
					public HostKitAttribute(string name, bool generateOptions = true) { }
					public HostKitAttribute(bool generateOptions) { }
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"HostKitAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("string? name;");
		await Assert.That(generated).DoesNotContain("string name;");
	}

	[Test]
	public async Task Generate_IsEnumNamedArgument_WithDefaultValue(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test
			{
				public enum MyEnum { A, B }

				[Generate(typeof(MyAttribute))]
				public readonly partial record struct MyAttributeData(
					[Property(IsEnum = true, DefaultValue = "Test.MyEnum.B")]
						string? Value
				);

				public class MyAttribute : System.Attribute
				{
					public MyEnum Value { get; set; }
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"MyAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("string? Value");
		await Assert.That(generated).Contains("attributeData.GetEnumNamedArgument(\"Value\", \"Test.MyEnum.B\")");
	}

	[Test]
	public async Task Generate_IsEnumMultipleSources_WithDefaultValue(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test
			{
				public enum MyEnum { A, B }

				[Generate(typeof(MyAttribute))]
				public readonly partial record struct MyAttributeData(
					[Property(IsEnum = true)]
					[Argument("value", IsEnum = true, DefaultValue = "Test.MyEnum.B")]
						string? Value
				);

				public class MyAttribute : System.Attribute
				{
					public MyAttribute(MyEnum value) { }
					public MyEnum Value { get; set; }
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(
			result,
			"MyAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("string? Value");
		await Assert.That(generated).Contains("var value = __valueTc.ToEnumString() ?? \"Test.MyEnum.B\";");
	}

	[Test]
	public async Task Generate_EnumLiteralDefaultValue_RendersQualifiedCast(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test
			{
				public enum LogLevel { Trace, Debug, Information }

				[Generate(typeof(MyAttribute))]
				public readonly partial record struct MyAttributeData(
					[Property(DefaultValue = LogLevel.Information)] LogLevel Level
				);

				public class MyAttribute : System.Attribute
				{
					public LogLevel Level { get; set; }
				}
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		result.AssertNoGenerationExceptions().AssertNoLogErrors();

		var generated = await GetGeneratedStringAsync(
			result,
			"MyAttributeData.AttributeDataModel.g.cs",
			cancellationToken
		);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains("attributeData.GetNamedArgument<global::Test.LogLevel>(\"Level\", (global::Test.LogLevel)2);");
		await Assert.That(generated).DoesNotContain("global::global::");
	}

	static async Task<string?> GetGeneratedStringAsync(
		DriverRunResult result,
		string fileName,
		CancellationToken cancellationToken
	)
	{
		var tree = result.GetGeneratedTree(fileName);
		return tree is null ? null : (await tree.GetTextAsync(cancellationToken)).ToString();
	}
}
