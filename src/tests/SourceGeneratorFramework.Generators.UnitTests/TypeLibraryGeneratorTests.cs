namespace Purview.SourceGeneratorFramework.Generators;

public class TypeLibraryGeneratorTests : TUnitSourceGeneratorTestBase<TypeLibraryGenerator, TypeLibraryTestOptions>
{
	[Test]
	public async Task Generate_NestedNamespaceShape(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("Purview.Telemetry")]
				static readonly TypeIdentity ActivitySourceGenerationAttribute = default;

				[TypeRef(typeof(global::System.Diagnostics.Activity))]
				static readonly TypeIdentity Activity = default;

				[TypeRef("ILogger", "Microsoft.Extensions.Logging")]
				static readonly TypeIdentity ILogger = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("public static partial class SampleTypeLibrary");
		await Assert.That(generated).Contains("public const string Namespace = \"Test\";");
		await Assert.That(generated).Contains("public static partial class Purview");
		await Assert.That(generated).Contains("public const string Namespace = \"Purview\";");
		await Assert.That(generated).Contains("public const string Namespace = \"Purview.Telemetry\";");
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity ActivitySourceGenerationAttribute = new(\"ActivitySourceGenerationAttribute\", \"Purview.Telemetry\");"
			);
		await Assert.That(generated).Contains("public static partial class System");
		await Assert.That(generated).Contains("public const string Namespace = \"System\";");
		await Assert.That(generated).Contains("public static partial class Diagnostics");
		await Assert.That(generated).Contains("public const string Namespace = \"System.Diagnostics\";");
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity Activity = new(\"Activity\", \"System.Diagnostics\");"
			);
		await Assert.That(generated).Contains("public static partial class Microsoft");
		await Assert.That(generated).Contains("public static partial class Extensions");
		await Assert.That(generated).Contains("public const string Namespace = \"Microsoft.Extensions\";");
		await Assert.That(generated).Contains("public static partial class Logging");
		await Assert.That(generated).Contains("public const string Namespace = \"Microsoft.Extensions.Logging\";");
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity ILogger = new(\"ILogger\", \"Microsoft.Extensions.Logging\");"
			);
		await Assert.That(generated).DoesNotContain("extension(");
	}

	[Test]
	public async Task Generate_TypeRefNamespaceOnly_UsesMemberNameAsTypeName(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("Purview.Telemetry")]
				static readonly TypeIdentity ActivitySourceGenerationAttribute = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity ActivitySourceGenerationAttribute = new(\"ActivitySourceGenerationAttribute\", \"Purview.Telemetry\");"
			);
	}

	[Test]
	public async Task Generate_TypeRef_GenericArityFromTypeOf(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef(typeof(global::System.Collections.Generic.List<>))]
				static readonly TypeIdentity List = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity List = new(\"List\", \"System.Collections.Generic\", 1);"
			);
	}

	[Test]
	public async Task Generate_TypeRef_GenericArityFromBacktickString(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("List`1", "System.Collections.Generic")]
				static readonly TypeIdentity List = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity List = new(\"List\", \"System.Collections.Generic\", 1);"
			);
	}

	[Test]
	public async Task Generate_TypeRef_ExplicitArityOnNamespaceForm(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("Test", 2)]
				static readonly TypeIdentity Dictionary = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity Dictionary = new(\"Dictionary\", \"Test\", 2);"
			);
	}

	[Test]
	public async Task Generate_ReferenceMember_CopiesInitializerAndEmitsNestedField(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System.Diagnostics")]
				static readonly TypeIdentity ActivityLink = default;

				[TypeRef("System.Diagnostics")]
				internal static readonly TypeReference ActivityLinkArray = new TypeReference(ActivityLink).MakeArray();

				[TypeRef("System.Collections.Generic")]
				internal static readonly TypeReference ActivityTagIEnumerable =
					global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.Collections.Generic.IEnumerable.MakeGeneric(
						global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.String
					);
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeReference ActivityLinkArray = new TypeReference(ActivityLink).MakeArray();"
			);
		await Assert.That(generated).Contains("public const string Namespace = \"System.Collections.Generic\";");
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeReference ActivityTagIEnumerable = global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.Collections.Generic.IEnumerable.MakeGeneric("
			);
	}

	[Test]
	public async Task Generate_ValueMember_InitialisedTypeIdentity(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System")]
				internal static readonly TypeIdentity StringType = global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.String;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity StringType = global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.String;"
			);
	}

	[Test]
	public async Task Generate_GlobalNamespaceAndCustomName(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			[GenerateTypeLibrary(ClassName = "MyTypeLibrary")]
			static partial class TypeLibraryModel
			{
				[TypeRef("Test")]
				static readonly TypeIdentity MyAttribute = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("public static partial class MyTypeLibrary");
		await Assert.That(generated).Contains("public const string Namespace = \"\";");
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity MyAttribute = new(\"MyAttribute\", \"Test\");"
			);
	}

	[Test]
	public async Task Generate_InheritsFrameworkMembers(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System.Diagnostics")]
				static readonly TypeIdentity Debug = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity String = global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.String;"
			);
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity List = global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.Collections.Generic.List;"
			);
		await Assert.That(generated).Contains("public static partial class DependencyInjection");
		await Assert
			.That(generated)
			.Contains("public const string Namespace = \"Microsoft.Extensions.DependencyInjection\";");
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity IServiceCollection = global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.Microsoft.Extensions.DependencyInjection.IServiceCollection;"
			);
	}

	[Test]
	public async Task Generate_UserMemberShadowsInherited(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System")]
				static readonly TypeIdentity String = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity String = new(\"String\", \"System\");"
			);
		await Assert
			.That(generated)
			.DoesNotContain("= global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.String;");
	}

	[Test]
	public async Task Generate_GetTypes_IncludedMembersOnly(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System.Diagnostics", IncludeInGetTypes = true)]
				static readonly TypeIdentity Activity = default;

				[TypeRef("System.Diagnostics")]
				static readonly TypeIdentity Debug = default;

				[TypeRef("System.Diagnostics", IncludeInGetTypes = true)]
				internal static readonly TypeReference ActivityArray = new TypeReference(Activity).MakeArray();
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"global::System.Collections.Immutable.ImmutableArray<global::Purview.SourceGeneratorFramework.TypeReference> GetTypes("
			);
		await Assert.That(generated).Contains(") => [Activity, ActivityArray];");
		await Assert.That(generated).DoesNotContain("[Activity, ActivityArray, Debug]");
	}

	[Test]
	public async Task Generate_GetTypes_NoIncludedMembers_NoMethod(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System.Diagnostics")]
				static readonly TypeIdentity Debug = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).DoesNotContain("GetTypes()");
	}

	[Test]
	public async Task Generate_GetTypes_PositionalIncludeInGetTypes_NamespaceOnlyForm(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System.Diagnostics", 0, true)]
				static readonly TypeIdentity Activity = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains(") => [Activity];");
	}

	[Test]
	public async Task Generate_GetTypes_PositionalIncludeInGetTypes_ExplicitForm(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("Activity", "System.Diagnostics", -1, true)]
				static readonly TypeIdentity Activity = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains(") => [Activity];");
	}

	[Test]
	public async Task Generate_GetTypes_NamedCtorIncludeInGetTypes(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System.Diagnostics", includeInGetTypes: true)]
				static readonly TypeIdentity Activity = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains(") => [Activity];");
	}

	[Test]
	public async Task Generate_TypeRefAttribute_ExposesIncludeInGetTypesCtorParameter(
		CancellationToken cancellationToken
	)
	{
		const string source = "public sealed class UnrelatedType { }";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var generated = await GetGeneratedStringAsync(result, "TypeRefAttribute.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).Contains("bool includeInGetTypes = false");
		await Assert.That(generated).Contains("IncludeInGetTypes = includeInGetTypes;");
	}

	[Test]
	public async Task Generate_FileScopedNamespaces(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("System.Diagnostics")]
				static readonly TypeIdentity Activity = default;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		var library = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);
		await Assert.That(library).IsNotNull();
		await Assert.That(library).Contains("namespace Test;");
		await Assert.That(library).DoesNotContain("namespace Test\n{");

		var typeRefs = await GetGeneratedStringAsync(
			result,
			"TypeLibrary.SampleTypeLibrary.Test.TypeLibraryModel.TypeRefs.g.cs",
			cancellationToken
		);
		await Assert.That(typeRefs).IsNotNull();
		await Assert.That(typeRefs).Contains("namespace Test;");
		await Assert.That(typeRefs).DoesNotContain("namespace Test\n{");
		await Assert.That(typeRefs).Contains("TypeRefMarkers");
		await Assert.That(typeRefs).Contains("[Activity]");
	}

	[Test]
	public async Task Generate_MarkerWithoutDefaultInitializer_StillGenerates(CancellationToken cancellationToken)
	{
		var source = """
			using Purview.SourceGeneratorFramework;
			using Purview.SourceGeneratorFramework.Generators;

			namespace Test;

			[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Test")]
			static partial class TypeLibraryModel
			{
				[TypeRef("Purview.Telemetry")]
				static readonly TypeIdentity ActivitySourceGenerationAttribute;
			}
			""";

		var result = await GenerateAsync(source, cancellationToken: cancellationToken);

		// TLB0010 is non-blocking and reported by the analyzer; the generator still emits the library.
		await Assert.That(result.DriverResult.Diagnostics).DoesNotContain(d => d.Id == "TLB0010");

		var generated = await GetGeneratedStringAsync(result, "Test.TypeLibraryModel.g.cs", cancellationToken);

		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated)
			.Contains(
				"public static readonly global::Purview.SourceGeneratorFramework.TypeIdentity ActivitySourceGenerationAttribute = new(\"ActivitySourceGenerationAttribute\", \"Purview.Telemetry\");"
			);
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
