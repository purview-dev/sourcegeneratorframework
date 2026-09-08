using Microsoft.CodeAnalysis.CSharp;

namespace Purview.SourceGeneratorFramework;

public class CodeWriterTests
{
	static TypeReference Type(string name) => new(new TypeIdentity(name, null));

	static string GeneratedAttributes(bool includeCoverageExclusion = true) =>
		(includeCoverageExclusion ? "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\n" : "")
		+ "[global::System.Runtime.CompilerServices.CompilerGenerated]\n"
		+ "[global::System.CodeDom.Compiler.GeneratedCode(\"TestGenerator\", \"1.0.0\")]\n";

	static string IndentedGeneratedAttributes(bool includeCoverageExclusion = true) =>
		"\t"
		+ GeneratedAttributes(includeCoverageExclusion).Replace("\n", "\n\t", StringComparison.Ordinal).TrimEnd('\t');

	[Test]
	public async Task MemberDeclarationOptions_AreValueTypes()
	{
		// Arrange / Act / Assert
		await Assert.That(typeof(MethodDeclarationOptions).IsValueType).IsTrue();
		await Assert.That(typeof(PropertyDeclarationOptions).IsValueType).IsTrue();
		await Assert.That(typeof(FieldDeclarationOptions).IsValueType).IsTrue();
		await Assert.That(typeof(EnumFieldDeclarationOptions).IsValueType).IsTrue();
		await Assert.That(typeof(ConstructorDeclarationOptions).IsValueType).IsTrue();
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task TypeValueObject_GivenMissingName_Throws(string? name)
	{
		await Assert.That(() => new TypeIdentity(name!, null)).Throws<ArgumentException>();
	}

	[Test]
	public async Task EmptyTypeReference_IsIgnoredByMemberEmitters()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Field(new FieldDeclarationOptions("field", TypeReference.Empty));
		writer.Property(new PropertyDeclarationOptions("Property", TypeReference.Empty));
		writer.MethodScope(new MethodDeclarationOptions("Method", TypeReference.Empty)).Dispose();

		// Assert
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task ExtensionBlockScope_WritesExtensionBlockWithReceiver()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		TypeIdentity receiver = new("PurviewTypeLibrary", "Purview.SourceGeneratorFramework");

		// Act
		using (writer.ExtensionBlockScope(receiver.AsTypeReference()))
		{
			writer.Property(
				new("Name", TypeReference.Create<string>(), TypeDeclarationAccessibility.Public)
				{
					IsStatic = true,
					ExpressionBody = "\"value\"",
					IncludeGeneratedAttributes = false,
				}
			);
		}

		// Assert
		var result = writer.ToString();
		await Assert.That(result).Contains("extension(global::Purview.SourceGeneratorFramework.PurviewTypeLibrary)");
		await Assert.That(result).Contains("\tpublic static string Name => \"value\";");
		await Assert.That(result).Contains("}");
	}

	[Test]
	public async Task ExtensionBlockScope_NestedReceiver_WritesNestedTypeLibraryPath()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var receiver = new TypeIdentity("PurviewTypeLibrary", "Purview.SourceGeneratorFramework")
			.Nested("System")
			.Nested("Diagnostics");

		// Act
		using (writer.ExtensionBlockScope(receiver.AsTypeReference()))
			writer.Line("public static int Marker => 0;");

		// Assert
		await Assert
			.That(writer.ToString())
			.Contains("extension(global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.Diagnostics)");
	}

	[Test]
	public async Task ExtensionBlock_InvokesBodyCallback()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		TypeIdentity receiver = new("PurviewTypeLibrary", "Purview.SourceGeneratorFramework");

		// Act
		writer.ExtensionBlock(receiver.AsTypeReference(), body => body.Line("public static int Marker => 0;"));

		// Assert
		var result = writer.ToString();
		await Assert.That(result).Contains("extension(global::Purview.SourceGeneratorFramework.PurviewTypeLibrary)");
		await Assert.That(result).Contains("\tpublic static int Marker => 0;");
	}

	[Test]
	public async Task ExtensionBlockScope_ComposedReceiver_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act / Assert
		await Assert
			.That(() =>
				writer.ExtensionBlockScope(
					new TypeIdentity("List", "System.Collections.Generic")
						.MakeGeneric(new TypeIdentity("T", null))
						.AsTypeReference()
						.MakeArray()
				)
			)
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task ExtensionBlockScope_EmptyReceiver_IsNoOp()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.ExtensionBlockScope(TypeReference.Empty))
			writer.Line("public int P { get; set; }");

		// Assert
		await Assert.That(writer.ToString()).Contains("public int P { get; set; }");
		await Assert.That(writer.ToString()).DoesNotContain("extension(");
	}

	[Test]
	public async Task ExtensionBlockScope_NullReceiver_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act / Assert
		await Assert.That(() => writer.ExtensionBlockScope(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Line_AppendsLineWithIndent()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Line("public class C");
		using (writer.OpenBlockScope())
		{
			writer.Line("public int P { get; set; }");
		}

		var result = writer.ToString();

		await Assert.That(result).Contains("public class C");
		await Assert.That(result).Contains("\tpublic int P { get; set; }");
		await Assert.That(result).Contains("}");
	}

	[Test]
	public async Task Quote_WrapsValueInDoubleQuotes()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Quote("value");

		await Assert.That(writer.ToString()).IsEqualTo("\"value\"");
	}

	[Test]
	public async Task ToString_EmptyWriter_ReturnsEmpty()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task Append_AliasForWrite()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Append("value");

		await Assert.That(writer.ToString()).IsEqualTo("value");
	}

	[Test]
	public async Task AppendLine_AliasForLine()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AppendLine("value");

		await Assert.That(writer.ToString()).Contains("value");
	}

	[Test]
	public async Task If_True_WritesValue()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.If(true, "value");

		await Assert.That(writer.ToString()).IsEqualTo("value");
	}

	[Test]
	public async Task If_False_DoesNotWrite()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.If(false, "value");

		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task LineIf_True_WritesLine()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.LineIf(true, "value");

		await Assert.That(writer.ToString()).Contains("value");
	}

	[Test]
	public async Task LineIf_False_DoesNotWrite()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.LineIf(false, "value");

		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task EnsureNewLine_WhenNotAtLineStart_AppendsNewLine()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Write("value");
		writer.EnsureNewLine();

		await Assert.That(writer.ToString()).EndsWith("value\n");
	}

	[Test]
	public async Task EnsureBlankLine_AddsSeparatorAfterCompletedLine()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCall("Run").EnsureBlankLine().Comment("Explains the next member.");

		await Assert.That(writer.ToString()).IsEqualTo("Run();\n\n// Explains the next member.\n");
	}

	[Test]
	public async Task EnsureBlankLine_CompletesPartialLineAndIsIdempotent()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Write("Run();").EnsureBlankLine().EnsureBlankLine().Write("Next");

		await Assert.That(writer.ToString()).IsEqualTo("Run();\n\nNext");
	}

	[Test]
	public async Task EnsureBlankLine_OnEmptyWriterDoesNotWrite()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.EnsureBlankLine();

		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task Lines_WritesMultipleLines()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Lines(["line1", "line2"]);

		await Assert.That(writer.ToString()).Contains("line1");
		await Assert.That(writer.ToString()).Contains("line2");
	}

	[Test]
	public async Task Delimited_WritesItemsWithDelimiter()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Delimited(["a", "b", "c"], ", ");

		await Assert.That(writer.ToString()).IsEqualTo("a, b, c");
	}

	[Test]
	public async Task Block_WithBody_WritesBodyInsideBlock()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Block("public class C", w => w.Line("public int P { get; set; }"));

		var result = writer.ToString();

		await Assert.That(result).Contains("public class C");
		await Assert.That(result).Contains("\tpublic int P { get; set; }");
		await Assert.That(result).Contains("}");
	}

	[Test]
	public async Task Using_WritesUsingDirective()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Using("System");

		await Assert.That(writer.ToString()).IsEqualTo("using System;\n");
	}

	[Test]
	public async Task BlockNamespace_WritesNamespaceBlock()
	{
		var writer = CodeWriterFactory.ForTests();

		using (writer.BlockNamespaceScope("Test"))
		{
			writer.Line("public class C { }");
		}

		var result = writer.ToString();

		await Assert.That(result).Contains("namespace Test");
		await Assert.That(result).Contains("\tpublic class C { }");
		await Assert.That(result).Contains("}");
	}

	[Test]
	public async Task BlockNamespaces_GivenMultipleNamespaces_InsertsBlankLineBetweenThem()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.BlockNamespace("First", body => body.Line("class A { }"));
		writer.BlockNamespace("Second", body => body.Line("class B { }"));

		await Assert
			.That(writer.ToString())
			.IsEqualTo("namespace First\n{\n\tclass A { }\n}\n\n" + "namespace Second\n{\n\tclass B { }\n}\n");
	}

	[Test]
	public async Task BlockNamespaceAndTopLevelType_InsertsBlankLineBetweenDeclarations()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("TopLevel");

		writer.BlockNamespace("First", body => body.Line("class Nested { }"));
		writer.Class(declaration, static _ => { });
		writer.BlockNamespace("Second", body => body.Line("class Other { }"));

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"namespace First\n{\n\tclass Nested { }\n}\n\n"
					+ GeneratedAttributes()
					+ "public sealed partial class TopLevel\n{\n}\n\n"
					+ "namespace Second\n{\n\tclass Other { }\n}\n"
			);
	}

	[Test]
	public async Task BlockNamespace_TypeValueObject_WritesNamespaceBlock()
	{
		var writer = CodeWriterFactory.ForTests();
		var typeValue = new TypeIdentity("C", "Test");

		using (writer.BlockNamespaceScope(typeValue))
		{
			writer.Line("public class C { }");
		}

		var result = writer.ToString();

		await Assert.That(result).Contains("namespace Test");
		await Assert.That(result).Contains("\tpublic class C { }");
		await Assert.That(result).Contains("}");
	}

	[Test]
	public async Task BlockNamespace_TypeValueObjectWithGlobalNamespace_ReturnsNoOpScope()
	{
		var writer = CodeWriterFactory.ForTests();
		var typeValue = new TypeIdentity("C", null);

		using (writer.BlockNamespaceScope(typeValue))
		{
			writer.Line("public class C { }");
		}

		var result = writer.ToString();

		await Assert.That(result).DoesNotContain("namespace");
		await Assert.That(result).Contains("public class C { }");
	}

	[Test]
	public async Task FileScopedNamespace_TypeValueObject_WritesNamespace()
	{
		var writer = CodeWriterFactory.ForTests();
		var typeValue = new TypeIdentity("C", "Test");

		writer.FileScopedNamespace(typeValue);

		var result = writer.ToString();

		await Assert.That(result).Contains("namespace Test;");
	}

	[Test]
	public async Task FileScopedNamespace_TypeValueObjectWithGlobalNamespace_WritesNothing()
	{
		var writer = CodeWriterFactory.ForTests();
		var typeValue = new TypeIdentity("C", null);

		writer.FileScopedNamespace(typeValue);

		var result = writer.ToString();

		await Assert.That(result).DoesNotContain("namespace");
	}

	[Test]
	public async Task Class_WritesClassBlock()
	{
		var writer = CodeWriterFactory.ForTests();

		using (
			writer.ClassScope(
				new TypeDeclarationOptions("C")
				{
					Accessibility = TypeDeclarationAccessibility.Public,
					IsPartial = false,
					IsSealed = false,
				}
			)
		)
		{
			writer.Line("public int P { get; set; }");
		}

		var result = writer.ToString();

		await Assert.That(result).Contains("public class C");
		await Assert.That(result).Contains("\tpublic int P { get; set; }");
		await Assert.That(result).Contains("}");
	}

	[Test]
	public async Task Class_WithOptions_WritesModifiersInheritanceAndConstraints()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Repository")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			BaseType = Type("RepositoryBase").Identity.MakeGeneric(Type("T")),
			Interfaces = [Type("IRepository").Identity.MakeGeneric(Type("T")), Type("IDisposable")],
			GenericTypes = [new GenericTypeParameterOptions("T") { Constraints = ["class", "new()"] }],
		};

		using (writer.ClassScope(declaration))
		{
			writer.Line("public T Value { get; } = new();");
		}

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public sealed partial class Repository<T> : RepositoryBase<T>, IRepository<T>, IDisposable\n"
					+ "where T : class, new()\n"
					+ "{\n"
					+ "\tpublic T Value { get; } = new();\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Class_WithoutBody_WritesSemicolonTerminatedDeclaration()
	{
		var writer = CodeWriter.CreateTestWriter();

		writer.Class(new TypeDeclarationOptions("C"));

		await Assert.That(writer.ToString()).IsEqualTo("public sealed partial class C;\n");
	}

	[Test]
	public async Task Class_WithoutBody_WithBaseType_WritesSemicolonTerminatedDeclaration()
	{
		var writer = CodeWriter.CreateTestWriter();

		writer.Class(new TypeDeclarationOptions("C") { BaseType = Type("Base") });

		await Assert.That(writer.ToString()).IsEqualTo("public sealed partial class C : Base;\n");
	}

	[Test]
	public async Task RecordClass_WithoutBody_WithPrimaryConstructor_WritesSemicolonTerminatedDeclaration()
	{
		var writer = CodeWriter.CreateTestWriter();

		writer.RecordClass(
			new TypeDeclarationOptions("R")
			{
				Accessibility = TypeDeclarationAccessibility.Public,
				IsPartial = false,
				IsSealed = false,
				PrimaryConstructorParameters = [new ParameterDeclarationOptions("value", Type("string"))],
			}
		);

		await Assert.That(writer.ToString()).IsEqualTo("public record class R(string value);\n");
	}

	[Test]
	public async Task Interface_WithoutBody_WritesSemicolonTerminatedDeclaration()
	{
		var writer = CodeWriter.CreateTestWriter();

		writer.Interface(
			new TypeDeclarationOptions("IService") { Accessibility = TypeDeclarationAccessibility.Public }
		);

		await Assert.That(writer.ToString()).IsEqualTo("public partial interface IService;\n");
	}

	[Test]
	public async Task TypeDeclarationOptions_NullableTypeIdentity_UsesIdentityName()
	{
		TypeIdentity? identity = new TypeIdentity("TestingHostKit", "Testing.HostKitNamespace");

		var declaration = new TypeDeclarationOptions(identity, TypeDeclarationAccessibility.Public);

		await Assert.That(declaration.Name).IsEqualTo("TestingHostKit");
		await Assert.That(declaration.Name).IsNotEqualTo("global::Testing.HostKitNamespace.TestingHostKit");
	}

	[Test]
	public async Task BaseType_ConstructedArityTwoGeneric_RendersAllArguments()
	{
		var writer = CodeWriterFactory.ForTests();
		var resourceKitBase = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2).MakeGeneric(
			new TypeIdentity("TestingHostKit", "Testing.HostKitNamespace"),
			new TypeIdentity("DefaultAspireResource", "Purview.Aspire.ResourceKit")
		);

		var declaration = new TypeDeclarationOptions("RedisResourceKit")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsPartial = true,
			IsSealed = true,
			BaseType = resourceKitBase,
		};

		using (writer.ClassScope(declaration))
		{
			// Empty body.
		}

		await Assert
			.That(writer.ToString())
			.Contains(
				": global::Purview.Aspire.ResourceKit.ResourceKitBase<global::Testing.HostKitNamespace.TestingHostKit, global::Purview.Aspire.ResourceKit.DefaultAspireResource>"
			);
	}

	[Test]
	public async Task BaseType_NestedConstructedGenericArgument_RendersAndDoesNotOverflow()
	{
		var writer = CodeWriterFactory.ForTests();
		var resourceKitBase = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2).MakeGeneric(
			new TypeIdentity("HostKitBase", "Purview.Aspire.ResourceKit", arity: 1).MakeGeneric(
				new TypeIdentity("TestingHostKit", "Testing.HostKitNamespace")
			),
			new TypeIdentity("RedisResourceKit", "Testing")
		);

		var declaration = new TypeDeclarationOptions("RedisResourceKit")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsPartial = true,
			IsSealed = true,
			BaseType = resourceKitBase,
		};

		using (writer.ClassScope(declaration))
		{
			// Empty body.
		}

		await Assert
			.That(writer.ToString())
			.Contains(
				": global::Purview.Aspire.ResourceKit.ResourceKitBase<global::Purview.Aspire.ResourceKit.HostKitBase<global::Testing.HostKitNamespace.TestingHostKit>, global::Testing.RedisResourceKit>"
			);
	}

	[Test]
	public async Task BaseType_OpenGeneric_ThrowsValidationError()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("C")
		{
			BaseType = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2),
		};

		await Assert.That(() => writer.ClassScope(declaration)).Throws<ArgumentException>();
	}

	[Test]
	public async Task MakeGeneric_ArityMismatch_Throws()
	{
		var open = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2);

		await Assert
			.That(() => open.MakeGeneric(new TypeIdentity("DefaultAspireResource", "Purview.Aspire.ResourceKit")))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task GenerateResourceKit_OpenGenericTypeArgument_ThrowsClearError()
	{
		var writer = CodeWriter.CreateTestWriter();
		var resourceKit = new TypeIdentity("RedisResourceKit", "Testing");
		var resourceKitBase = new TypeIdentity("ResourceKitBase", "Purview.Aspire.ResourceKit", arity: 2).MakeGeneric(
			new TypeIdentity("HostKitBase", "Purview.Aspire.ResourceKit", arity: 1),
			resourceKit
		);
		var resourceDefinitionAttribute = new AttributeDeclarationOptions(
			new TypeIdentity("ResourceDefinitionAttribute", "Purview.Aspire.ResourceKit")
		);

		await Assert
			.That(() =>
				writer
					.FileScopedNamespace(resourceKit)
					.Class(
						new(resourceKit, TypeDeclarationAccessibility.Public)
						{
							BaseType = resourceKitBase,
							IsPartial = true,
							Attributes = [resourceDefinitionAttribute],
						},
						_ => { }
					)
			)
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task RecordStruct_WithOptions_WritesReadonlyRecordStruct()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Identifier")
		{
			Accessibility = TypeDeclarationAccessibility.Internal,
			IsReadOnly = true,
			Interfaces = [Type("IEquatable").Identity.MakeGeneric(Type("Identifier"))],
		};

		using (writer.RecordStructScope(declaration))
		{
			// Here to prevent IDE0555
		}

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "internal readonly partial record struct Identifier : IEquatable<Identifier>\n"
					+ "{\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Type_WithoutAccessibility_OmitsAccessibility()
	{
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultTypeAccessibility = null;

		using (
			writer.TypeScope(
				new TypeDeclarationOptions("State")
				{
					Kind = TypeDeclarationKind.RecordClass,
					IsPartial = false,
					IsSealed = false,
				}
			)
		)
		{
			// Here to prevent IDE0555
		}

		await Assert.That(writer.ToString()).StartsWith(GeneratedAttributes() + "record class State\n");
	}

	[Test]
	public async Task Class_GivenStaticDeclaration_WritesStaticClass()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Extensions")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsStatic = true,
		};

		// Act
		using (writer.ClassScope(declaration))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public static partial class Extensions\n{\n}\n");
	}

	[Test]
	public async Task Class_GivenAbstractDeclaration_WritesAbstractInsteadOfDefaultSealed()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("ServiceBase")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsAbstract = true,
		};

		// Act
		using (writer.ClassScope(declaration))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public abstract partial class ServiceBase\n{\n}\n");
	}

	[Test]
	public async Task Struct_GivenAbstractDeclaration_ThrowsArgumentException()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Invalid") { IsAbstract = true };

		// Act
		CodeWriter.BlockScope Action() => writer.StructScope(declaration);

		// Assert
		await Assert.That(Action).Throws<ArgumentException>();
	}

	[Test]
	public async Task Type_GivenStaticStruct_ThrowsArgumentException()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Invalid") { Kind = TypeDeclarationKind.Struct, IsStatic = true };

		// Act
		CodeWriter.BlockScope Action() => writer.TypeScope(declaration);

		// Assert
		await Assert.That(Action).Throws<ArgumentException>();
	}

	[Test]
	public async Task Struct_WithBaseType_Throws()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Invalid") { BaseType = Type("BaseType") };

		await Assert.That(() => writer.StructScope(declaration)).Throws<ArgumentException>();
	}

	[Test]
	public async Task Class_WithPrimaryConstructor_WritesParametersBeforeBaseType()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Repository")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			PrimaryConstructorParameters = [new("connectionString", Type("string")), new("logger", Type("ILogger"))],
			BaseType = Type("RepositoryBase(connectionString)"),
		};

		using (writer.ClassScope(declaration))
		{
			// To stop IDE0055
		}

		await Assert
			.That(writer.ToString())
			.StartsWith(
				GeneratedAttributes()
					+ "public sealed partial class Repository(string connectionString, ILogger logger) : RepositoryBase(connectionString)\n"
			);
	}

	[Test]
	public async Task Class_WithEmptyBaseType_DoesNotWriteBaseListColon()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("ResourceKit") { BaseType = TypeReference.Empty };

		writer.Class(declaration, static _ => { });

		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public sealed partial class ResourceKit\n{\n}\n");
	}

	[Test]
	public async Task Class_WithEmptyBaseAndInterfaces_WritesOnlyNonEmptyInterfaces()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("ResourceKit")
		{
			BaseType = TypeReference.Empty,
			Interfaces =
			[
				TypeReference.Empty,
				new TypeReference(new TypeIdentity("IResourceKit", null)),
				TypeReference.Empty,
			],
		};

		writer.Class(declaration, static _ => { });

		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public sealed partial class ResourceKit : IResourceKit\n{\n}\n");
	}

	[Test]
	public async Task Constructor_WritesParametersInitializerAndBody()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new ConstructorDeclarationOptions("Repository")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			Parameters = [new("connectionString", Type("string")), new("logger", Type("ILogger"))],
			Initializer = "base(connectionString)",
		};

		using (writer.ConstructorScope(declaration))
		{
			writer.Line("_logger = logger;");
		}

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public Repository(string connectionString, ILogger logger)\n"
					+ "\t: base(connectionString)\n"
					+ "{\n"
					+ "\t_logger = logger;\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Constructor_StaticConstructor_WritesStaticConstructor()
	{
		var writer = CodeWriterFactory.ForTests();

		using (writer.ConstructorScope(new ConstructorDeclarationOptions("Repository") { IsStatic = true }))
		{
			// To stop IDE0055
		}

		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "static Repository()\n{\n}\n");
	}

	[Test]
	public async Task Method_GivenShortParameters_WritesSingleLineDeclaration()
	{
		var writer = CodeWriterFactory.ForTests();

		using (
			writer.MethodScope(
				new MethodDeclarationOptions("Execute", Type("void"))
				{
					Accessibility = TypeDeclarationAccessibility.Public,
					IsStatic = true,
					Parameters = [new("name", Type("string")), new("enabled", Type("bool"))],
				}
			)
		)
		{
			writer.Line("Run(name, enabled);");
		}

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static void Execute(string name, bool enabled)\n"
					+ "{\n"
					+ "\tRun(name, enabled);\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Interface_WithInheritanceAndConstraints_WritesInterface()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("IRepository")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			Interfaces = [Type("IAsyncDisposable")],
			GenericTypes = [new GenericTypeParameterOptions("T") { Constraints = ["class"] }],
		};

		// Act
		writer.Interface(declaration, body => body.Line("T Get();"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false)
					+ "public partial interface IRepository<T> : IAsyncDisposable\n"
					+ "where T : class\n"
					+ "{\n"
					+ "\tT Get();\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Enum_WithUnderlyingType_WritesEnum()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Status")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			EnumUnderlyingType = Type("byte"),
		};

		// Act
		writer.Enum(declaration, body => body.Line("None = 0,").Line("Ready = 1,"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"[global::Microsoft.CodeAnalysis.Embedded]\n"
					+ GeneratedAttributes(includeCoverageExclusion: false)
					+ "public enum Status : byte\n{\n\tNone = 0,\n\tReady = 1,\n}\n"
			);
	}

	[Test]
	public async Task AttributeClass_WithDefaults_WritesAttributeUsageAndSystemAttributeBase()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.AttributeClass(
			new TypeDeclarationOptions("RegistryAttribute")
			{
				Accessibility = TypeDeclarationAccessibility.Public,
				IsPartial = false,
			},
			AttributeTargets.Class,
			body => body.Line("public string? Name { get; init; }")
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"[global::Microsoft.CodeAnalysis.Embedded]\n"
					+ GeneratedAttributes()
					+ "[global::System.AttributeUsage(global::System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]\n"
					+ "public sealed class RegistryAttribute : global::System.Attribute\n"
					+ "{\n"
					+ "\tpublic string? Name { get; init; }\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task AttributeClass_WithOptions_WritesCombinedTargetsFlagsAttributesAndCustomBase()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.AttributeClass(
			new TypeDeclarationOptions("KnownTypeAttribute")
			{
				Accessibility = TypeDeclarationAccessibility.Internal,
				IsPartial = false,
				BaseType = Type("CustomAttributeBase"),
				Attributes = [new(new TypeIdentity("Obsolete", null))],
			},
			AttributeTargets.Class | AttributeTargets.Property,
			_ => { },
			inherited: true,
			allowMultiple: true
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"[global::Microsoft.CodeAnalysis.Embedded]\n"
					+ GeneratedAttributes()
					+ "[global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Property, Inherited = true, AllowMultiple = true)]\n"
					+ "[Obsolete]\n"
					+ "internal sealed class KnownTypeAttribute : CustomAttributeBase\n"
					+ "{\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task AttributeClass_WithEmbeddedAttributeDisabled_OmitsEmbeddedAttribute()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AttributeClass(
			new TypeDeclarationOptions("LocalAttribute") { IsPartial = false, IncludeEmbeddedAttribute = false },
			AttributeTargets.Class,
			_ => { }
		);

		await Assert.That(writer.ToString()).DoesNotContain("[global::Microsoft.CodeAnalysis.Embedded]");
	}

	[Test]
	public async Task AttributeClass_GivenNoTargets_ThrowsWithoutWriting()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert
			.That(() => writer.AttributeClass(new("InvalidAttribute"), 0, _ => { }))
			.Throws<ArgumentOutOfRangeException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task Enum_WithStructuredFields_WritesSummariesAttributesAndValues()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Status") { Accessibility = TypeDeclarationAccessibility.Public };

		// Act
		writer.Enum(
			declaration,
			new EnumFieldDeclarationOptions("None", 0)
			{
				XmlSummary = ["No status has been selected."],
				Attributes = [new(new TypeIdentity("Obsolete", null))],
			},
			new EnumFieldDeclarationOptions("Ready", (object)"1 << 0"),
			new EnumFieldDeclarationOptions("Unknown")
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"[global::Microsoft.CodeAnalysis.Embedded]\n"
					+ GeneratedAttributes(includeCoverageExclusion: false)
					+ "public enum Status\n"
					+ "{\n"
					+ "\t/// <summary>No status has been selected.</summary>\n"
					+ "\t[Obsolete]\n"
					+ "\tNone = 0,\n"
					+ "\n"
					+ "\tReady = 1 << 0,\n"
					+ "\n"
					+ "\tUnknown,\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Enum_WithEmbeddedAttributeDisabled_OmitsEmbeddedAttribute()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Enum(
			new TypeDeclarationOptions("Status") { IncludeEmbeddedAttribute = false },
			body => body.EnumField(new EnumFieldDeclarationOptions("Ready", 1))
		);

		await Assert.That(writer.ToString()).DoesNotContain("[global::Microsoft.CodeAnalysis.Embedded]");
	}

	[Test]
	public async Task Enum_WithFieldSummaries_SeparatesFieldsWithBlankLines()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Status") { Accessibility = TypeDeclarationAccessibility.Public };

		// Act
		writer.Enum(
			declaration,
			new EnumFieldDeclarationOptions("None", 0),
			new EnumFieldDeclarationOptions("Ready", 1) { XmlSummary = ["The service is ready."] }
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"[global::Microsoft.CodeAnalysis.Embedded]\n"
					+ GeneratedAttributes(includeCoverageExclusion: false)
					+ "public enum Status\n"
					+ "{\n"
					+ "\tNone = 0,\n"
					+ "\n"
					+ "\t/// <summary>The service is ready.</summary>\n"
					+ "\tReady = 1,\n"
					+ "}\n"
			);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task EnumFieldDeclarationOptions_GivenMissingName_Throws(string? fieldName)
	{
		await Assert.That(() => new EnumFieldDeclarationOptions(fieldName!)).Throws<ArgumentException>();
	}

	[Test]
	public async Task EnumField_GivenDefaultOptions_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act / Assert
		await Assert.That(() => writer.EnumField(default)).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task Delegate_WithGenericConstraints_WritesCompleteDeclaration()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Factory")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			DelegateReturnType = Type("TResult"),
			DelegateParameters = [new("value", Type("T"))],
			GenericTypes =
			[
				new GenericTypeParameterOptions("T") { Constraints = ["class"] },
				new GenericTypeParameterOptions("TResult"),
			],
		};

		// Act
		writer.Delegate(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false)
					+ "public delegate TResult Factory<T, TResult>(T value)\nwhere T : class;\n"
			);
	}

	[Test]
	public async Task Method_GivenLongParameters_WritesOneParameterPerLine()
	{
		var writer = CodeWriterFactory.ForTests();

		using (
			writer.MethodScope(
				new MethodDeclarationOptions(
					"AddAspireResourceKit",
					Type("global::Aspire.Hosting.IDistributedApplicationBuilder")
				)
				{
					Accessibility = TypeDeclarationAccessibility.Public,
					Parameters =
					[
						new(
							"onBuilt",
							Type(
									"global::System.Action<global::Testing.HostKitNamespace.TestingHostKit, global::Aspire.Hosting.IDistributedApplicationBuilder>"
								)
								.Nullable()
						)
						{
							DefaultValue = "null",
						},
						new(
							"onConfigured",
							Type("global::System.Action<global::Testing.HostKitNamespace.TestingHostKit>").Nullable()
						)
						{
							DefaultValue = "null",
						},
						new(
							"configureOptions",
							Type(
									"global::System.Action<global::Microsoft.Extensions.Options.OptionsBuilder<global::Testing.HostKitNamespace.TestingHostKit.TestingHostKitOptions>>"
								)
								.Nullable()
						)
						{
							DefaultValue = "null",
						},
					],
				}
			)
		)
		{
			writer.Line("return builder;");
		}

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public global::Aspire.Hosting.IDistributedApplicationBuilder AddAspireResourceKit(\n"
					+ "\tglobal::System.Action<global::Testing.HostKitNamespace.TestingHostKit, global::Aspire.Hosting.IDistributedApplicationBuilder>? onBuilt = null,\n"
					+ "\tglobal::System.Action<global::Testing.HostKitNamespace.TestingHostKit>? onConfigured = null,\n"
					+ "\tglobal::System.Action<global::Microsoft.Extensions.Options.OptionsBuilder<global::Testing.HostKitNamespace.TestingHostKit.TestingHostKitOptions>>? configureOptions = null\n"
					+ ")\n"
					+ "{\n"
					+ "\treturn builder;\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Method_GivenStructuredOptions_WritesModifiersGenericsAndBody()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("CreateAsync", Type("Task").Identity.MakeGeneric(Type("T")))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsStatic = true,
			IsAsync = true,
			Parameters = [new("value", Type("T")), new("cancellationToken", Type("CancellationToken"))],
			GenericTypes = [new GenericTypeParameterOptions("T") { Constraints = ["class"] }],
		};

		// Act
		writer.Method(declaration, body => body.Line("return await SaveAsync(value);"));

		// Assert
		await Assert
			.That(writer)
			.Generates(
				GeneratedAttributes()
					+ "public static async Task<T> CreateAsync<T>(T value, CancellationToken cancellationToken)\n"
					+ "where T : class\n"
					+ "{\n"
					+ "\treturn await SaveAsync(value);\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task MethodExpression_GivenExpressionBody_WritesExpressionBodiedMethod()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int")) { ExpressionBody = "items.Count" };

		// Act
		writer.MethodExpression(declaration);

		// Assert
		await Assert.That(writer).Generates(GeneratedAttributes() + "public int Count() => items.Count;\n");
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task MethodExpression_GivenWhitespaceExpressionBody_ThrowsWithoutWriting(string? expressionBody)
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int")) { ExpressionBody = expressionBody };

		// Act / Assert
		await Assert.That(() => writer.MethodExpression(declaration)).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task MethodExpression_GivenCallback_WritesExpressionBodiedMethod()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int"));

		// Act
		writer.MethodExpression(declaration, expression => expression.Write("items.Count"));

		// Assert
		await Assert.That(writer).Generates(GeneratedAttributes() + "public int Count() => items.Count;\n");
	}

	[Test]
	public async Task MethodExpression_GivenNullCallback_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int"));

		// Act / Assert
		await Assert.That(() => writer.MethodExpression(declaration, null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task MethodExpression_GivenExpressionBodyAndCallback_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int")) { ExpressionBody = "items.Count" };

		// Act / Assert
		await Assert
			.That(() => writer.MethodExpression(declaration, expression => expression.Write("items.Count")))
			.Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task MethodExpression_GivenPartialDeclaration_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int"))
		{
			IsPartial = true,
			ExpressionBody = "items.Count",
		};

		// Act / Assert
		await Assert.That(() => writer.MethodExpression(declaration)).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task MethodExpression_GivenPartialDeclarationAndCallback_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int")) { IsPartial = true };

		// Act / Assert
		await Assert
			.That(() => writer.MethodExpression(declaration, expression => expression.Write("items.Count")))
			.Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task PartialMethod_GivenExpressionBody_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int")) { ExpressionBody = "items.Count" };

		// Act / Assert
		await Assert.That(() => writer.PartialMethod(declaration)).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task Method_GivenExpressionBody_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int")) { ExpressionBody = "items.Count" };

		// Act / Assert
		await Assert
			.That(() => writer.Method(declaration, body => body.Line("return items.Count;")))
			.Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task Method_GivenBodyAndNoExpressionBody_WritesBlockBody()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Count", Type("int"));

		// Act
		writer.Method(declaration, body => body.Line("return items.Count;"));

		// Assert
		await Assert
			.That(writer)
			.Generates(GeneratedAttributes() + "public int Count()\n" + "{\n" + "\treturn items.Count;\n" + "}\n");
	}

	[Test]
	public async Task PartialMethod_GivenPartialMethods_WritesDeclaration()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		MethodDeclarationOptions declaration = new("CreateAsync", Type("Task").Identity.MakeGeneric(Type("T")))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsAsync = true,
			IsPartial = true,
			Parameters = [new("value", Type("T")), new("cancellationToken", Type("CancellationToken"))],
			GenericTypes = [new("T") { Constraints = ["class"] }],
		};

		// Act
		writer.PartialMethod(declaration);

		// Assert
		await Assert
			.That(writer)
			.Generates(
				GeneratedAttributes()
					+ "public async partial Task<T> CreateAsync<T>(T value, CancellationToken cancellationToken)\n"
					+ "where T : class;\n"
			);
	}

	[Test]
	public async Task StructuredDeclarations_GivenAttributes_WritesTypeMemberReturnAndParameterAttributes()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var generatedCode = new AttributeDeclarationOptions(new TypeIdentity("GeneratedCode", null))
		{
			Arguments =
			[
				new("\"Generator\""),
				new("\"1.0\"") { Name = "version" },
				new(false) { Name = "Enabled", IsPropertyAssignment = true },
			],
		};
		var type = new TypeDeclarationOptions("Service")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			Attributes = [generatedCode],
		};
		var method = new MethodDeclarationOptions("TryGet", Type("bool"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			Attributes = [new(new TypeIdentity("Obsolete", null))],
			ReturnAttributes = [new(new TypeIdentity("NotNull", null))],
			Parameters =
			[
				new("value", Type("string").Nullable())
				{
					Modifier = ParameterModifier.Out,
					Attributes = [new(new TypeIdentity("NotNullWhen", null)) { Arguments = [new(true)] }],
				},
			],
		};

		// Act
		writer.Class(type, body => body.Method(method, methodBody => methodBody.Line("throw null;")));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "[GeneratedCode(\"Generator\", version: \"1.0\", Enabled = false)]\n"
					+ "public sealed partial class Service\n"
					+ "{\n"
					+ IndentedGeneratedAttributes()
					+ "\t[Obsolete]\n"
					+ "\t[return: NotNull]\n"
					+ "\tpublic bool TryGet([NotNullWhen(true)] out string? value)\n"
					+ "\t{\n"
					+ "\t\tthrow null;\n"
					+ "\t}\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task AttributeDeclaration_RendersRetainedTypeReferenceSyntax()
	{
		var writer = CodeWriterFactory.ForTests();
		var attributeType = new TypeIdentity("MarkerAttribute", "Example")
			.MakeGeneric(TypeIdentity.Create<string>().MakeNullable())
			.AsTypeReference();

		writer.Class(new("C") { Attributes = [new(attributeType)] }, _ => { });

		await Assert.That(writer).ContainsGenerated("[global::Example.Marker<string?>]");
	}

	[Test]
	public async Task PublicScopeReturningMethods_EndWithScope()
	{
		// Arrange / Act
		var invalidMethodNames = typeof(CodeWriter)
			.GetMethods()
			.Where(static method =>
				method.ReturnType == typeof(CodeWriter.BlockScope)
				|| method.ReturnType == typeof(CodeWriter.IndentScope)
			)
			.Where(static method => !method.Name.EndsWith("Scope", StringComparison.Ordinal))
			.Select(static method => method.Name)
			.ToArray();

		// Assert
		await Assert.That(invalidMethodNames).IsEmpty();
	}

	[Test]
	public async Task PublicScopeReturningMethods_HaveCallbackCounterparts()
	{
		// Arrange
		var methods = typeof(CodeWriter).GetMethods();

		// Act
		var missingCounterparts = methods
			.Where(static method =>
				method.ReturnType == typeof(CodeWriter.BlockScope)
				|| method.ReturnType == typeof(CodeWriter.IndentScope)
			)
			.Where(scopeMethod =>
			{
				var scopeParameters = scopeMethod.GetParameters();
				var counterpartName = scopeMethod.Name[..^"Scope".Length];
				return !methods.Any(method =>
				{
					if (method.Name != counterpartName || method.ReturnType != typeof(CodeWriter))
						return false;

					var parameters = method.GetParameters();
					if (
						parameters.Length != scopeParameters.Length + 1
						|| parameters[^1].ParameterType != typeof(Action<CodeWriter>)
					)
						return false;

					for (var index = 0; index < scopeParameters.Length; index++)
					{
						if (parameters[index].ParameterType != scopeParameters[index].ParameterType)
							return false;
					}
					return true;
				});
			})
			.Select(static method => method.Name)
			.ToArray();

		// Assert
		await Assert.That(missingCounterparts).IsEmpty();
	}

	[Test]
	public async Task Class_GivenAttributeTypeValueObject_DoesNotDuplicateAttributeBrackets()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var attribute = new AttributeDeclarationOptions(
			new TypeIdentity("HostKitAttribute", "Purview.Aspire.ResourceKit")
		)
		{
			Arguments = [new AttributeArgumentOptions(true) { Name = "GenerateOptions", IsPropertyAssignment = true }],
		};

		// Act
		writer.ClassScope(new TypeDeclarationOptions("Host") { Attributes = [attribute] }).Dispose();

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "[global::Purview.Aspire.ResourceKit.HostKit(GenerateOptions = true)]\n"
					+ "public sealed partial class Host\n"
					+ "{\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task AttributeTypeValueObject_GivenDeclarationContexts_RendersUnderlyingType()
	{
		// Arrange
		var attributeType = new TypeIdentity("RegistryAttribute", "Example");
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.AttributeClass(
			new TypeDeclarationOptions(attributeType) { IsPartial = false },
			AttributeTargets.Class,
			body =>
			{
				body.XmlSummary($"Creates a {CodeWriter.XmlSee(attributeType)} instance.");
				body.Constructor(new ConstructorDeclarationOptions(attributeType), _ => { });
				body.Property(new PropertyDeclarationOptions("Parent", attributeType));
			}
		);

		// Assert
		var result = writer.ToString();
		await Assert.That(result).Contains("class RegistryAttribute : global::System.Attribute");
		await Assert.That(result).Contains("<see cref=\"global::Example.RegistryAttribute\" />");
		await Assert.That(result).Contains("RegistryAttribute()");
		await Assert.That(result).Contains("global::Example.RegistryAttribute Parent");
		await Assert.That(result).DoesNotContain("[global::Example.Registry]");
	}

	[Test]
	public async Task TypeReference_GivenNestedNullableGenericAndArray_RendersStructuredType()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var valueType = Type("global::System.Collections.Generic.Dictionary")
			.Identity.MakeGeneric(Type("string"), Type("Widget").Nullable())
			.MakeArray()
			.Nullable();
		MethodDeclarationOptions method = new("Load", valueType)
		{
			Parameters =
			[
				new(
					"items",
					Type("global::System.Collections.Generic.List")
						.Identity.MakeGeneric(Type("Widget").Nullable())
						.AsTypeReference()
						.Nullable()
				),
			],
			ExpressionBody = "items.ToArray()",
		};

		// Act
		writer.MethodScope(method);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public global::System.Collections.Generic.Dictionary<string, Widget?>[]? Load(\n"
					+ "\tglobal::System.Collections.Generic.List<Widget?>? items\n"
					+ ") => items.ToArray();\n"
			);
	}

	[Test]
	public async Task Method_GivenNullableParameterOption_WritesNullableTypeOnce()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var method = new MethodDeclarationOptions("Use", Type("void"))
		{
			Parameters =
			[
				new ParameterDeclarationOptions("value", Type("Widget").Nullable()) { DefaultValue = "null" },
			],
			ExpressionBody = "Consume(value)",
		};

		// Act
		writer.MethodScope(method);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public void Use(Widget? value = null) => Consume(value);\n");
	}

	[Test]
	public async Task Property_GivenAutoAccessorsAndInitializer_WritesProperty()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Name", Type("string"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			HasSetter = true,
			IsInitOnly = true,
			Initializer = "string.Empty",
		};

		// Act
		writer.Property(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string Name { get; init; } = string.Empty;\n");
	}

	[Test]
	public async Task Property_GivenIsInitOnlyOnly_WritesInitAccessor()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Name", Type("string"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsInitOnly = true,
			Initializer = "string.Empty",
		};

		// Act
		writer.Property(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string Name { get; init; } = string.Empty;\n");
	}

	[Test]
	public async Task Property_GivenAccessorBodies_WritesScopedAccessors()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Value", Type("int"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			HasSetter = true,
			SetterAccessibility = TypeDeclarationAccessibility.Private,
		};

		// Act
		writer.Property(declaration, getter => getter.Line("return _value;"), setter => setter.Line("_value = value;"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public int Value\n"
					+ "{\n"
					+ "\tget\n\t{\n\t\treturn _value;\n\t}\n"
					+ "\tprivate set\n\t{\n\t\t_value = value;\n\t}\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Property_GivenIsInitOnlyOnlyWithAccessorBodies_WritesInitAccessor()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Value", Type("int"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsInitOnly = true,
		};

		// Act
		writer.Property(declaration, getter => getter.Line("return _value;"), setter => setter.Line("_value = value;"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public int Value\n"
					+ "{\n"
					+ "\tget\n\t{\n\t\treturn _value;\n\t}\n"
					+ "\tinit\n\t{\n\t\t_value = value;\n\t}\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Property_GivenExpressionBody_WritesExpressionProperty()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Count", Type("int"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			ExpressionBody = "_items.Count",
		};

		// Act
		writer.Property(declaration);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public int Count => _items.Count;\n");
	}

	[Test]
	public async Task RecordStruct_GivenIsInitOnlyProperty_WritesReadonlyCompatibleProperty()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (
			writer.RecordStructScope(
				new TypeDeclarationOptions("Sample")
				{
					Accessibility = TypeDeclarationAccessibility.Public,
					IsReadOnly = true,
				}
			)
		)
		{
			writer.Property(
				new PropertyDeclarationOptions("Name", Type("string"))
				{
					Accessibility = TypeDeclarationAccessibility.Public,
					IsInitOnly = true,
				}
			);
		}

		// Assert
		var result = writer.ToString();
		await Assert
			.That(result)
			.Contains("public string Name { get; init; }")
			.Because("init-only properties are valid in readonly structs");
	}

	[Test]
	public async Task Field_GivenReadonlyStaticField_WritesField()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new FieldDeclarationOptions("Empty", Type("Example"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsStatic = true,
			IsReadOnly = true,
			Initializer = "new()",
		};

		// Act
		writer.Field(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false) + "public static readonly Example Empty = new();\n"
			);
	}

	[Test]
	public async Task StructuredMembers_GivenConsecutiveFields_DoesNotAddBlankLine()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer
			.Field(new FieldDeclarationOptions("_first", Type("int")))
			.Field(new FieldDeclarationOptions("_second", Type("int")));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false)
					+ "private int _first;\n"
					+ GeneratedAttributes(includeCoverageExclusion: false)
					+ "private int _second;\n"
			);
	}

	[Test]
	public async Task StructuredMembers_GivenDifferentMemberKinds_AddsBlankLine()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Field(new FieldDeclarationOptions("_value", Type("int")));
		writer.Property(
			new PropertyDeclarationOptions("Value", Type("int")) { Accessibility = TypeDeclarationAccessibility.Public }
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false)
					+ "private int _value;\n"
					+ "\n"
					+ GeneratedAttributes()
					+ "public int Value { get; }\n"
			);
	}

	[Test]
	public async Task StructuredMembers_GivenScopedMethods_AddsBlankLineAfterScopeCloses()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var first = new MethodDeclarationOptions("First", Type("void"));
		var second = new MethodDeclarationOptions("Second", Type("void"));

		// Act
		using (writer.MethodScope(first))
		{
			writer.Line("Execute();");
		}
		using (writer.MethodScope(second))
		{
			writer.Line("Execute();");
		}

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public void First()\n{\n\tExecute();\n}\n"
					+ "\n"
					+ GeneratedAttributes()
					+ "public void Second()\n{\n\tExecute();\n}\n"
			);
	}

	[Test]
	public async Task StructuredMembers_GivenDocumentationTrivia_InsertsSeparatorBeforeTrivia()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Field(new("_value", Type("int")));
		writer.XmlSummary("Gets the value.");
		writer.Property(new("Value", Type("int")));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false)
					+ "private int _value;\n"
					+ "\n"
					+ "/// <summary>Gets the value.</summary>\n"
					+ GeneratedAttributes()
					+ "public int Value { get; }\n"
			);
	}

	[Test]
	public async Task StructuredMembers_GivenExistingBlankLine_DoesNotAddAnother()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Field(new FieldDeclarationOptions("_value", Type("int"))).NewLine();
		writer.Property(new PropertyDeclarationOptions("Value", Type("int")));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false)
					+ "private int _value;\n"
					+ "\n"
					+ GeneratedAttributes()
					+ "public int Value { get; }\n"
			);
	}

	[Test]
	public async Task Block_WithBodyAndCustomSeparators_WritesDelimitedBody()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.DelimitedBlock(
			"Create",
			"(",
			");",
			body =>
			{
				body.Quote("value").Line(",");
				body.Line("EmptyPath");
			}
		);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("Create\n(\n\t\"value\",\n\tEmptyPath\n);\n");
	}

	[Test]
	public async Task Block_WithBodyLast_WritesBodyInsideCustomSeparators()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.DelimitedBlock("Create", "(", ");", body => body.Quote("value").Line());

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("Create\n(\n\t\"value\"\n);\n");
	}

	[Test]
	public async Task MethodCall_WritesSimpleInvocation()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCall("Run", "value", "cancellationToken");

		await Assert.That(writer.ToString()).IsEqualTo("Run(value, cancellationToken);\n");
	}

	[Test]
	public async Task AwaitedMethodCall_WritesAwaitPrefix()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AwaitedMethodCall("LoadAsync", "cancellationToken");

		await Assert.That(writer.ToString()).IsEqualTo("await LoadAsync(cancellationToken);\n");
	}

	[Test]
	public async Task MethodCallOn_GivenReceiver_WritesReceiverDot()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCallOn("variable", "Process", "item");

		await Assert.That(writer.ToString()).IsEqualTo("variable.Process(item);\n");
	}

	[Test]
	public async Task AwaitedMethodCallOn_GivenReceiver_WritesAwaitAndDot()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AwaitedMethodCallOn("service", "LoadAsync", "token");

		await Assert.That(writer.ToString()).IsEqualTo("await service.LoadAsync(token);\n");
	}

	[Test]
	public async Task AwaitedMethodCall_WithStructuredArguments_WritesReceiverAndModifiers()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AwaitedMethodCall(
			"LoadAsync",
			new MethodCallArgumentOptions[]
			{
				new("token"),
				new("result") { Modifier = ParameterModifier.Out },
			},
			receiver: "service",
			writeArgumentsOnSeparateLines: true
		);

		await Assert.That(writer.ToString()).IsEqualTo("await service.LoadAsync(\n\ttoken,\n\tout result\n);\n");
	}

	[Test]
	public async Task MethodCall_WritesReceiverGenericArgumentsAndMultilineArguments()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCall(
			"Create",
			["firstArgumentWithANameThatMakesTheCallLong", "secondArgumentWithANameThatMakesTheCallLong"],
			receiver: "factory",
			genericArguments: [Type("string")]
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"factory.Create<string>(\n"
					+ "\tfirstArgumentWithANameThatMakesTheCallLong,\n"
					+ "\tsecondArgumentWithANameThatMakesTheCallLong\n"
					+ ");\n"
			);
	}

	[Test]
	public async Task MethodCall_WithStructuredArguments_WritesModifiersAndMultilineArguments()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCall(
			"AMethodCallWithLotsOfParams",
			new MethodCallArgumentOptions[]
			{
				new("a-long-a-param") { Modifier = ParameterModifier.Ref },
				new("another-long-param") { Modifier = ParameterModifier.Out },
			},
			writeArgumentsOnSeparateLines: true
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo("AMethodCallWithLotsOfParams(\n" + "\tref a-long-a-param,\n" + "\tout another-long-param\n);\n");
	}

	[Test]
	public async Task MethodCall_WithStructuredArgument_WritesNamedArgument()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCall("Configure", new MethodCallArgumentOptions[] { new("value") { Name = "option" } });

		await Assert.That(writer.ToString()).IsEqualTo("Configure(option: value);\n");
	}

	[Test]
	public async Task MethodCallOn_WithNullConditional_WritesNullConditionalOperator()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCallOn("onBuilt", "Invoke", ["this", "builder"], nullConditional: true);

		await Assert.That(writer.ToString()).IsEqualTo("onBuilt?.Invoke(this, builder);\n");
	}

	[Test]
	public async Task AwaitedMethodCallOn_WithNullConditional_WritesAwaitAndNullConditionalOperator()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AwaitedMethodCallOn("service", "LoadAsync", ["token"], nullConditional: true);

		await Assert.That(writer.ToString()).IsEqualTo("await service?.LoadAsync(token);\n");
	}

	[Test]
	public async Task MethodCallOn_WithGenericArguments_WritesGenericInvocation()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCallOn("builder.Services", "AddOptions", genericArguments: [Type("Options")]);

		await Assert.That(writer.ToString()).IsEqualTo("builder.Services.AddOptions<Options>();\n");
	}

	[Test]
	public async Task MethodCallOn_WithGenericArgumentsAndArguments_WritesGenericInvocation()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCallOn("service", "Add", ["value"], genericArguments: [Type("Options")]);

		await Assert.That(writer.ToString()).IsEqualTo("service.Add<Options>(value);\n");
	}

	[Test]
	public async Task AwaitedMethodCallOn_WithGenericArguments_WritesAwaitAndGenericInvocation()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AwaitedMethodCallOn("service", "LoadAsync", ["token"], genericArguments: [Type("Options")]);

		await Assert.That(writer.ToString()).IsEqualTo("await service.LoadAsync<Options>(token);\n");
	}

	[Test]
	public async Task Assignment_WithMethodCallChainRootGenericArguments_WritesChainedGenericInvocation()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment(
			"var",
			"optionsBuilder",
			expression =>
				expression.MethodCallChain(
					"builder.Services.AddOptions",
					[],
					chain => chain.Method("BindConfiguration", ["\"Purview.SectionName\""]),
					genericArguments: [Type("Options")]
				)
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"var optionsBuilder = builder.Services.AddOptions<Options>().BindConfiguration(\"Purview.SectionName\");\n"
			);
	}

	[Test]
	public async Task MethodCallChain_WritesChainedInvocations()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCallChain(
			"builder.Configuration.GetSection",
			["x.SectionName"],
			chain => chain.Method("Get", genericArguments: [Type("Options")]).Postfix(" ?? new()")
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo("builder.Configuration.GetSection(x.SectionName).Get<Options>() ?? new()");
	}

	[Test]
	public async Task MethodCallChain_AsAssignmentValue_WritesDeclarationAndSemicolon()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment(
			"var value",
			value =>
				value.MethodCallChain(
					"builder.Configuration.GetSection",
					["x.SectionName"],
					chain => chain.Method("Get", genericArguments: [Type("Options")]).Postfix(" ?? new()")
				)
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo("var value = builder.Configuration.GetSection(x.SectionName).Get<Options>() ?? new();\n");
	}

	[Test]
	public async Task AwaitedMethodCallChain_WritesAwaitPrefix()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Return(value =>
			value.AwaitedMethodCallChain("service.LoadAsync", ["token"], chain => chain.Method("Configure"))
		);

		await Assert.That(writer.ToString()).IsEqualTo("return await service.LoadAsync(token).Configure();\n");
	}

	[Test]
	public async Task MethodCallChain_WithMultipleSegments_WritesEachInvocation()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodCallChain(
			"items.Where",
			["value => value.Enabled"],
			chain => chain.Method("OrderBy", ["value => value.Name"]).Method("ToList")
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo("items.Where(value => value.Enabled).OrderBy(value => value.Name).ToList()");
	}

	[Test]
	public async Task MethodCallChain_GivenWhitespaceMethodName_Throws()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert
			.That(() => writer.MethodCallChain("   ", [], chain => chain.Method("Get")))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task MethodCallChain_GivenWhitespaceArgument_Throws()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert
			.That(() => writer.MethodCallChain("GetSection", ["   "], chain => chain.Method("Get")))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Assignment_WithObjectCreationOptions_WritesOptionalVarAndMixedArguments()
	{
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(
			Type("ASpecificType"),
			"propVal1",
			new MethodCallArgumentOptions("propVal2") { Name = "second" }
		)
		{
			WriteArgumentsOnSeparateLines = true,
		};

		writer.Assignment("var", "@event", creation);
		writer.Assignment("existingEvent", creation);

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"var @event = new ASpecificType(\n"
					+ "\tpropVal1,\n"
					+ "\tsecond: propVal2\n"
					+ ");\n"
					+ "existingEvent = new ASpecificType(\n"
					+ "\tpropVal1,\n"
					+ "\tsecond: propVal2\n"
					+ ");\n"
			);
	}

	[Test]
	public async Task ToString_GivenOpenBlockAndValidationEnabled_ThrowsScopeValidationException()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests(throwOnUnclosedScopes: true);
		var scope = writer.OpenBlockScope("public sealed class Example");

		// Act
		string Action() => writer.ToString();

		// Assert
		await Assert.That(writer.OpenScopeCount).IsEqualTo(1);
		var exception = await Assert.That(Action).Throws<CodeWriterScopeValidationException>();
		await Assert.That(exception!.OpenScopeCount).IsEqualTo(1);
		await Assert.That(exception.OpenScopes[0].Kind).IsEqualTo("block");
		await Assert.That(exception.OpenScopes[0].Header).IsEqualTo("public sealed class Example");
		await Assert
			.That(exception.OpenScopes[0].OpeningStackTrace)
			.Contains(nameof(ToString_GivenOpenBlockAndValidationEnabled_ThrowsScopeValidationException));
		await Assert.That(exception.Message).Contains("public sealed class Example");

		scope.Dispose();
		await Assert.That(writer.OpenScopeCount).IsEqualTo(0);
		await Assert.That(writer.ToString()).Contains("public sealed class Example");
	}

	[Test]
	public async Task ToString_GivenOpenBlockAndValidationDisabled_ReturnsPartialSource()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests(throwOnUnclosedScopes: false);
		var scope = writer.OpenBlockScope("public sealed class Example");

		// Act
		var source = writer.ToString();

		// Assert
		await Assert.That(writer.OpenScopeCount).IsEqualTo(1);
		await Assert.That(source).Contains("public sealed class Example");

		scope.Dispose();
	}

	[Test]
	public async Task ToString_GivenOpenIndentScopeAndValidationEnabled_ThrowsScopeValidationException()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests(throwOnUnclosedScopes: true);
		var scope = writer.IndentedScope();

		// Act
		string Action() => writer.ToString();

		// Assert
		await Assert.That(writer.OpenScopeCount).IsEqualTo(1);
		var exception = await Assert.That(Action).Throws<CodeWriterScopeValidationException>();
		await Assert.That(exception!.OpenScopeCount).IsEqualTo(1);
		await Assert.That(exception.OpenScopes[0].Kind).IsEqualTo("indentation");
		await Assert
			.That(exception.OpenScopes[0].OpeningStackTrace)
			.Contains(nameof(ToString_GivenOpenIndentScopeAndValidationEnabled_ThrowsScopeValidationException));

		scope.Dispose();
		await Assert.That(writer.OpenScopeCount).IsEqualTo(0);
	}

	[Test]
	public async Task AutoGeneratedHeader_WritesHeader()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AutoGeneratedHeader("TestGenerator", "1.0");

		var result = writer.ToString();

		await Assert.That(result).Contains("// <auto-generated />");
		await Assert.That(result).Contains("TestGenerator");
		await Assert.That(result).Contains("version 1.0");
		await Assert.That(result).DoesNotContain("// Generated at ");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenDefaultSettings_WritesNullableEnableDirective()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.AutoGeneratedHeader();

		await Assert.That(writer.ToString()).Contains("#nullable enable");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenNullableDirectiveDisable_OmitsDirective()
	{
		var writer = CodeWriterFactory.ForTests(
			settings: new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Disable,
			}
		);

		writer.AutoGeneratedHeader();

		await Assert.That(writer.ToString()).DoesNotContain("#nullable enable");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenNullableDirectiveAlways_WritesDirective()
	{
		var writer = CodeWriterFactory.ForTests(
			settings: new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Always,
			}
		);

		writer.AutoGeneratedHeader();

		await Assert.That(writer.ToString()).Contains("#nullable enable");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenAutoAndNullableEnabled_WritesDirective()
	{
		var writer = CodeWriterFactory.ForTests(
			settings: new GenerationSettings("TestGenerator", "1.0.0") { IsNullableContextEnabled = true }
		);

		writer.AutoGeneratedHeader();

		await Assert.That(writer.ToString()).Contains("#nullable enable");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenAutoAndNullableDisabled_OmitsDirective()
	{
		var writer = CodeWriterFactory.ForTests(
			settings: new GenerationSettings("TestGenerator", "1.0.0") { IsNullableContextEnabled = false }
		);

		writer.AutoGeneratedHeader();

		await Assert.That(writer.ToString()).DoesNotContain("#nullable enable");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenParameterOverride_OverridesSettings()
	{
		var writer = CodeWriterFactory.ForTests(
			settings: new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Disable,
			}
		);

		writer.AutoGeneratedHeader(nullableDirective: NullableDirectiveMode.Always);

		await Assert.That(writer.ToString()).Contains("#nullable enable");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenDisabledDirective_WritesExactHeaderWithoutDirective()
	{
		var writer = CodeWriterFactory.ForTests(
			settings: new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Disable,
			}
		);

		writer.AutoGeneratedHeader("TestGenerator", "1.0");

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"// <auto-generated />\n"
					+ "// This code was generated by TestGenerator (version 1.0).\n"
					+ "// Changes to this file will be lost when the source generator runs again.\n"
					+ "\n"
			);
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenEnabledDirective_WritesExactHeaderWithDirective()
	{
		var writer = CodeWriterFactory.ForTests(
			settings: new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Always,
			}
		);

		writer.AutoGeneratedHeader("TestGenerator", "1.0");

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"// <auto-generated />\n"
					+ "// This code was generated by TestGenerator (version 1.0).\n"
					+ "// Changes to this file will be lost when the source generator runs again.\n"
					+ "\n"
					+ "#nullable enable\n"
					+ "\n"
			);
	}

	[Test]
	public async Task Type_GivenNullableDisabledContext_StripsReferenceAnnotations()
	{
		var writer = new CodeWriter(
			new GenerationSettings("TestGenerator", "1.0.0") { IsNullableContextEnabled = false }
		);

		writer.Type(TypeIdentity.Create<string>().MakeNullable());
		writer.Write(" ");
		writer.Type(TypeIdentity.Create<int>().MakeNullable());
		writer.Write(" ");
		writer.Type(TypeIdentity.Create<string>().MakeNullable().MakeArray());

		await Assert.That(writer.ToString()).IsEqualTo("string int? string[]");
	}

	[Test]
	public async Task Type_GivenNullableEnabledOrUnknownContext_KeepsAnnotations()
	{
		var enabled = new CodeWriter(
			new GenerationSettings("TestGenerator", "1.0.0") { IsNullableContextEnabled = true }
		);
		var unknown = new CodeWriter(new GenerationSettings("TestGenerator", "1.0.0"));

		enabled.Type(TypeIdentity.Create<string>().MakeNullable());
		unknown.Type(TypeIdentity.Create<string>().MakeNullable());

		await Assert.That(enabled.ToString()).IsEqualTo("string?");
		await Assert.That(unknown.ToString()).IsEqualTo("string?");
	}

	[Test]
	public async Task Type_GivenAlwaysModeAndDisabledContext_KeepsAnnotations()
	{
		var writer = new CodeWriter(
			new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Always,
				IsNullableContextEnabled = false,
			}
		);

		writer.Type(TypeIdentity.Create<string>().MakeNullable());
		writer.Write(" ");
		writer.Type(TypeIdentity.Create<int>().MakeNullable());

		await Assert.That(writer.ToString()).IsEqualTo("string? int?");
	}

	[Test]
	public async Task Type_GivenDisableModeAndEnabledContext_StripsReferenceAnnotations()
	{
		var writer = new CodeWriter(
			new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Disable,
				IsNullableContextEnabled = true,
			}
		);

		writer.Type(TypeIdentity.Create<string>().MakeNullable());
		writer.Write(" ");
		writer.Type(TypeIdentity.Create<int>().MakeNullable());

		await Assert.That(writer.ToString()).IsEqualTo("string int?");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenAlwaysModeAndDisabledContext_WritesDirective()
	{
		var writer = new CodeWriter(
			new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Always,
				IsNullableContextEnabled = false,
			}
		);

		writer.AutoGeneratedHeader();

		await Assert.That(writer.ToString()).Contains("#nullable enable");
	}

	[Test]
	public async Task AutoGeneratedHeader_GivenDisableModeAndEnabledContext_OmitsDirective()
	{
		var writer = new CodeWriter(
			new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Disable,
				IsNullableContextEnabled = true,
			}
		);

		writer.AutoGeneratedHeader();

		await Assert.That(writer.ToString()).DoesNotContain("#nullable enable");
	}

	[Test]
	public async Task Type_GivenNullReference_Throws()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert.That(() => writer.Type(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task GeneratorIdentity_GivenNoHeaderArguments_UsesDefaultsAndDecoratesDeclarations()
	{
		var writer = new CodeWriter(new("HostKitGenerator", "2.3.4"), throwOnUnclosedScopes: false);

		writer.AutoGeneratedHeader();
		writer.Class(
			new TypeDeclarationOptions("GeneratedType"),
			body => body.Property(new PropertyDeclarationOptions("Value", TypeIdentity.Create<string>()))
		);

		var result = writer.ToString();
		await Assert.That(result).Contains("HostKitGenerator (version 2.3.4)");
		await Assert.That(result).DoesNotContain("// Generated at ");
		await Assert.That(result).DoesNotContain("[global::Microsoft.CodeAnalysis.Embedded]");
		await Assert.That(result).Contains("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
		await Assert.That(result).Contains("[global::System.Runtime.CompilerServices.CompilerGenerated]");
		await Assert
			.That(result)
			.Contains("[global::System.CodeDom.Compiler.GeneratedCode(\"HostKitGenerator\", \"2.3.4\")]");
	}

	[Test]
	public async Task Constructor_WithMultilineParameters_WritesInitializerOnNewLine()
	{
		var writer = CodeWriterFactory.ForTests();
		var declaration = new ConstructorDeclarationOptions("Repository")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			WriteParametersOnSeparateLines = true,
			Parameters = [new("connectionString", Type("string")), new("logger", Type("ILogger"))],
			Initializer = "this(connectionString, logger, true)",
		};

		writer.Constructor(declaration, static _ => { });

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public Repository(\n"
					+ "\tstring connectionString,\n"
					+ "\tILogger logger\n"
					+ ")\n"
					+ "\t: this(connectionString, logger, true)\n"
					+ "{\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task GeneratorIdentity_GivenConstField_DoesNotWriteInvalidCoverageAttribute()
	{
		var writer = new CodeWriter(new("HostKitGenerator", "2.3.4"), throwOnUnclosedScopes: false);

		writer.Field(
			new FieldDeclarationOptions("SectionName", TypeIdentity.Create<string>())
			{
				Accessibility = TypeDeclarationAccessibility.Public,
				IsConst = true,
				Initializer = "\"TestingHostKit\"",
			}
		);

		var result = writer.ToString();
		await Assert.That(result).DoesNotContain("ExcludeFromCodeCoverage");
		await Assert.That(result).Contains("CompilerGenerated");
		await Assert.That(result).Contains("GeneratedCode(");
	}

	[Test]
	public async Task GeneratedCodeAttribute_WritesAttribute()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.GeneratedCodeAttribute("TestGenerator", "1.0.0.0");

		var result = writer.ToString();

		await Assert
			.That(result)
			.Contains("[global::System.CodeDom.Compiler.GeneratedCode(\"TestGenerator\", \"1.0.0.0\")]");
	}

	[Test]
	public async Task Determinism_SameInputProducesIdenticalOutputAndNoTimestamp()
	{
		// Arrange
		static string Generate()
		{
			var writer = CodeWriterFactory.ForTests();
			writer.AutoGeneratedHeader();
			writer.Class(
				new TypeDeclarationOptions("Sample") { Accessibility = TypeDeclarationAccessibility.Public },
				body =>
					body.Method(
						new MethodDeclarationOptions("M", Type("void"))
						{
							Accessibility = TypeDeclarationAccessibility.Public,
						},
						methodBody => methodBody.Line("return;")
					)
			);
			return writer.ToString();
		}

		// Act
		var first = Generate();
		var second = Generate();

		// Assert
		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first).DoesNotContain("// Generated at ");
	}

	[Test]
	public async Task ScopeBalance_ThrowsWhenScopeLeftOpen()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests(throwOnUnclosedScopes: true);
		writer.OpenBlockScope("public class Example");

		// Act
		string Action() => writer.ToString();

		// Assert
		await Assert.That(Action).Throws<CodeWriterScopeValidationException>();
	}

	[Test]
	public async Task PragmaScope_EmitsDisableAndRestore()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Line("// before");
		using (writer.OpenPragmasScope("CS0618", "CS1591"))
		{
			writer.Line("// inside");
		}
		writer.Line("// after");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"// before\n"
					+ "\n"
					+ "#pragma warning disable CS0618\n"
					+ "#pragma warning disable CS1591\n"
					+ "// inside\n"
					+ "\n"
					+ "#pragma warning restore CS0618\n"
					+ "#pragma warning restore CS1591\n"
					+ "// after\n"
			);
	}

	[Test]
	public async Task OpenScope_CapturesOpeningStackTrace()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests(throwOnUnclosedScopes: true);
		writer.OpenBlockScope("public class Example");

		// Act
		string Action() => writer.ToString();

		// Assert
		var exception = await Assert.That(Action).Throws<CodeWriterScopeValidationException>();
		await Assert
			.That(exception!.OpenScopes[0].OpeningStackTrace)
			.Contains(nameof(OpenScope_CapturesOpeningStackTrace));
	}

	[Test]
	public async Task MemberDeclarations_GivenIncludeGeneratedAttributesFalse_OmitsGeneratedAttributes()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Class(
			new TypeDeclarationOptions("Sample")
			{
				Accessibility = TypeDeclarationAccessibility.Public,
				IncludeGeneratedAttributes = false,
			},
			body =>
			{
				body.Field(new FieldDeclarationOptions("_field", Type("int")) { IncludeGeneratedAttributes = false });
				body.Property(
					new PropertyDeclarationOptions("Property", Type("int")) { IncludeGeneratedAttributes = false }
				);
				body.Method(
					new MethodDeclarationOptions("Method", Type("void")) { IncludeGeneratedAttributes = false },
					methodBody => methodBody.Line("return;")
				);
				body.Constructor(
					new ConstructorDeclarationOptions("Sample") { IncludeGeneratedAttributes = false },
					constructorBody => constructorBody.Line("// ctor")
				);
			}
		);

		// Assert
		var result = writer.ToString();
		await Assert
			.That(result)
			.DoesNotContain("[global::System.CodeDom.Compiler.GeneratedCode")
			.Because("no generated attributes should be emitted");
		await Assert.That(result).Contains("int _field;");
		await Assert.That(result).Contains("int Property { get; }");
		await Assert.That(result).Contains("void Method()");
		await Assert.That(result).Contains("Sample()");
	}

	[Test]
	public async Task ClassDeclaration_GivenDefaultCodeWriterAndNoOverride_EmitsGeneratedAttributes()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Class(new("Sample", TypeDeclarationAccessibility.Public), body => body.Comment("Empty"));

		// Assert
		var result = writer.ToString();
		await Assert
			.That(result)
			.Contains(GeneratedAttributes())
			.Because("default CodeWriter emits generated attributes when not overridden");
	}

	[Test]
	public async Task ClassDeclaration_GivenDefaultIncludeGeneratedAttributesFalseAndNoOverride_OmitsGeneratedAttributes()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultIncludeGeneratedAttributes = false;

		// Act
		writer.Class(
			new TypeDeclarationOptions("Sample") { Accessibility = TypeDeclarationAccessibility.Public },
			body => body.Comment("Empty")
		);

		// Assert
		var result = writer.ToString();
		await Assert
			.That(result)
			.DoesNotContain("[global::System.CodeDom.Compiler.GeneratedCode")
			.Because("CodeWriter default set to false suppresses generated attributes");
	}

	[Test]
	public async Task ClassDeclaration_GivenDefaultIncludeGeneratedAttributesFalseAndOverrideTrue_EmitsGeneratedAttributes()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultIncludeGeneratedAttributes = false;

		// Act
		writer.Class(
			new TypeDeclarationOptions("Sample")
			{
				Accessibility = TypeDeclarationAccessibility.Public,
				IncludeGeneratedAttributes = true,
			},
			body => body.Comment("Empty")
		);

		// Assert
		var result = writer.ToString();
		await Assert
			.That(result)
			.Contains(GeneratedAttributes())
			.Because("explicit override on the declaration takes precedence");
	}

	[Test]
	public async Task MemberDeclarations_GivenDefaultIncludeGeneratedAttributesFalseAndNoOverride_OmitsGeneratedAttributes()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultIncludeGeneratedAttributes = false;

		// Act
		writer.Class(
			new TypeDeclarationOptions("Sample") { Accessibility = TypeDeclarationAccessibility.Public },
			body =>
			{
				body.Field(new FieldDeclarationOptions("_field", Type("int")));
				body.Property(new PropertyDeclarationOptions("Property", Type("int")));
				body.Method(
					new MethodDeclarationOptions("Method", Type("void")),
					methodBody => methodBody.Line("return;")
				);
				body.Constructor(
					new ConstructorDeclarationOptions("Sample"),
					constructorBody => constructorBody.Line("// ctor")
				);
			}
		);

		// Assert
		var result = writer.ToString();
		await Assert
			.That(result)
			.DoesNotContain("[global::System.CodeDom.Compiler.GeneratedCode")
			.Because("members inherit the CodeWriter default");
		await Assert.That(result).Contains("int _field;");
		await Assert.That(result).Contains("int Property { get; }");
		await Assert.That(result).Contains("void Method()");
		await Assert.That(result).Contains("Sample()");
	}

	[Test]
	public async Task CreateTestWriter_WithDefaultParameters_SetsDefaultIncludeGeneratedAttributesFalse()
	{
		// Arrange / Act
		var writer = CodeWriter.CreateTestWriter();

		// Assert
		await Assert.That(writer.DefaultIncludeGeneratedAttributes).IsFalse();
	}

	[Test]
	public async Task CreateTestWriter_WithTrueParameters_SetsDefaultIncludeGeneratedAttributesTrue()
	{
		// Arrange / Act
		var writer = CodeWriter.CreateTestWriter(includeGeneratedAttributes: true);

		// Assert
		await Assert.That(writer.DefaultIncludeGeneratedAttributes).IsTrue();
	}

	[Test]
	public async Task IfBlock_WritesSingleLineConditionAndScopedBody()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.IfBlock("enabled", body => body.Return());

		await Assert.That(writer.ToString()).IsEqualTo("if (enabled)\n{\n\treturn;\n}\n");
	}

	[Test]
	public async Task IfBlock_WritesMultilineConditionWithContinuationIndent()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.IfBlock("value != null\n&& value.IsValid", body => body.Return("value"));

		await Assert
			.That(writer.ToString())
			.IsEqualTo("if (value != null\n\t&& value.IsValid)\n{\n\treturn value;\n}\n");
	}

	[Test]
	public async Task ElseIf_WritesChainedIfElseIfElse()
	{
		var writer = CodeWriterFactory.ForTests();

		writer
			.IfBlock("value is null", body => body.Return("null"))
			.ElseIf("value is 0", body => body.Return("zero"))
			.Else(body => body.Return("value"));

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"if (value is null)\n"
					+ "{\n"
					+ "\treturn null;\n"
					+ "}\n"
					+ "else if (value is 0)\n"
					+ "{\n"
					+ "\treturn zero;\n"
					+ "}\n"
					+ "else\n"
					+ "{\n"
					+ "\treturn value;\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task ElseIf_WritesMultipleElseIfBranches()
	{
		var writer = CodeWriterFactory.ForTests();

		writer
			.IfBlock("kind is 1", body => body.Return("\"one\""))
			.ElseIf("kind is 2", body => body.Return("\"two\""))
			.ElseIf("kind is 3", body => body.Return("\"three\""));

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"if (kind is 1)\n"
					+ "{\n"
					+ "\treturn \"one\";\n"
					+ "}\n"
					+ "else if (kind is 2)\n"
					+ "{\n"
					+ "\treturn \"two\";\n"
					+ "}\n"
					+ "else if (kind is 3)\n"
					+ "{\n"
					+ "\treturn \"three\";\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task ElseIfScope_WritesConditionAndScopedBody()
	{
		var writer = CodeWriterFactory.ForTests();

		using (writer.IfBlockScope("enabled"))
			writer.Return("value");
		using (writer.ElseIfScope("retry"))
			writer.Return("retry");

		await Assert
			.That(writer.ToString())
			.IsEqualTo("if (enabled)\n{\n\treturn value;\n}\nelse if (retry)\n{\n\treturn retry;\n}\n");
	}

	[Test]
	public async Task ElseIf_WritesMultilineConditionWithContinuationIndent()
	{
		var writer = CodeWriterFactory.ForTests();

		writer
			.IfBlock("value != null", body => body.Return("value"))
			.ElseIf("value < 0\n|| value > 100", body => body.Return("\"invalid\""));

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"if (value != null)\n"
					+ "{\n"
					+ "\treturn value;\n"
					+ "}\n"
					+ "else if (value < 0\n"
					+ "\t|| value > 100)\n"
					+ "{\n"
					+ "\treturn \"invalid\";\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task ElseIf_GivenWhitespaceCondition_Throws()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert.That(() => writer.ElseIf("  ", _ => { })).Throws<ArgumentException>();
	}

	[Test]
	public async Task ElseIf_GivenNullBody_Throws()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert
			.That(() => writer.ElseIf("enabled", null!))
			.Throws<ArgumentNullException>()
			.WithParameterName("bodyWriter");
	}

	[Test]
	public async Task Assignment_WritesDeclarationAndMultilineInitializer()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment(
			"var value",
			value =>
			{
				value.Line("new()");
				value.OpenBlock(
					null,
					block =>
					{
						block.Line("X = 1,");
						block.Line("Y = 2,");
						block.Line("Z = 3");
					}
				);
			}
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo("var value = new()\n" + "{\n" + "\tX = 1,\n" + "\tY = 2,\n" + "\tZ = 3\n" + "};\n");
	}

	[Test]
	public async Task ReturnAndThrow_WritesStatements()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Return("value");
		writer.Throw(throwExpression => throwExpression.Line("new InvalidOperationException()"));

		await Assert.That(writer.ToString()).IsEqualTo("return value;\nthrow new InvalidOperationException();\n");
	}

	[Test]
	public async Task ExpressionMembers_WritesMultilineExpressions()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.MethodScope(
			new MethodDeclarationOptions("Load", Type("Value")) { ExpressionBody = "Create()\n.Configure()" }
		);
		writer.PropertyExpression(
			new PropertyDeclarationOptions("Current", Type("Value")),
			property => property.Line("GetCurrent()")
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public Value Load() => Create()\n\t.Configure();\n"
					+ "\n"
					+ GeneratedAttributes()
					+ "public Value Current => GetCurrent();\n"
			);
	}

	[Test]
	public async Task Method_WithPartialDeclarationWithBody_WritesBodyOutsideMethod()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultIncludeGeneratedAttributes = false;

		// Act
		writer.Class(
			new("Example") { IsPartial = true },
			body => body.Method(new("Apply") { IsPartial = true }, methodBody => methodBody.Return())
		);

		// Assert
		await Assert
			.That(writer)
			.ContainsGenerated(
				"""
partial void Apply()
{
	return;
}
"""
			);
	}

	[Test]
	public async Task ModernizationOptions_AreValueTypes()
	{
		// Arrange / Act / Assert
		await Assert.That(typeof(OperatorDeclarationOptions).IsValueType).IsTrue();
		await Assert.That(typeof(ObjectInitializerMemberOptions).IsValueType).IsTrue();
	}

	[Test]
	public async Task Operator_GivenBlockBody_WritesAccessibilityStaticTokenAndParameters()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new OperatorDeclarationOptions(
			"==",
			Type("bool"),
			new("left", Type("global::Testing.Name")),
			new("right", Type("global::Testing.Name"))
		)
		{
			Accessibility = TypeDeclarationAccessibility.Public,
		};

		// Act
		writer.Operator(declaration, body => body.Line("return left.Equals(right);"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static bool operator ==(global::Testing.Name left, global::Testing.Name right)\n"
					+ "{\n"
					+ "\treturn left.Equals(right);\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Operator_GivenExpressionBody_WritesExpressionAndBalancesScope()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new OperatorDeclarationOptions(
			"<",
			Type("bool"),
			new("left", Type("global::Testing.Money")),
			new("right", Type("global::Testing.Money"))
		)
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			ExpressionBody = "left.CompareTo(right) < 0",
		};

		// Act
		using (writer.OperatorScope(declaration))
		{
			// Intentionally empty: an expression-bodied operator returns an empty scope.
		}

		// Assert
		await Assert.That(writer.OpenScopeCount).IsEqualTo(0);
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static bool operator <(global::Testing.Money left, global::Testing.Money right) => left.CompareTo(right) < 0;\n"
			);
	}

	[Test]
	public async Task OperatorScope_GivenBlockBody_TracksOpenScopeCount()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new OperatorDeclarationOptions(
			"==",
			Type("bool"),
			new("left", Type("Name")),
			new("right", Type("Name"))
		);

		// Act
		using (writer.OperatorScope(declaration))
		{
			writer.Line("return left.Equals(right);");
			await Assert.That(writer.OpenScopeCount).IsEqualTo(1);
		}

		// Assert
		await Assert.That(writer.OpenScopeCount).IsEqualTo(0);
	}

	[Test]
	public async Task Operator_GivenNullBody_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new OperatorDeclarationOptions(
			"==",
			Type("bool"),
			new("left", Type("Name")),
			new("right", Type("Name"))
		);

		// Act / Assert
		await Assert.That(() => writer.Operator(declaration, null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Operator_GivenExpressionBodyAndCallback_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new OperatorDeclarationOptions(
			"==",
			Type("bool"),
			new("left", Type("Name")),
			new("right", Type("Name"))
		)
		{
			ExpressionBody = "left.Equals(right)",
		};

		// Act / Assert
		await Assert.That(() => writer.Operator(declaration, _ => { })).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task PartialMethod_GivenIsReadOnly_WritesReadonlyModifier()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("OnValidate", Type("void"))
		{
			IsPartial = true,
			IsReadOnly = true,
			Parameters =
			[
				new("id", Type("global::System.Guid")),
				new("displayName", Type("string").Nullable()),
				new("isActive", Type("bool")),
			],
		};

		// Act
		writer.PartialMethod(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public readonly partial void OnValidate(global::System.Guid id, string? displayName, bool isActive);\n"
			);
	}

	[Test]
	public async Task PartialMethod_GivenIsReadOnlyFalse_OmitsReadonlyModifier()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Apply", Type("void")) { IsPartial = true };

		// Act
		writer.PartialMethod(declaration);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public partial void Apply();\n");
	}

	[Test]
	public async Task Method_GivenIsReadOnlyAndIsStatic_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new MethodDeclarationOptions("Invalid", Type("void")) { IsReadOnly = true, IsStatic = true };

		// Act / Assert
		await Assert.That(() => writer.MethodScope(declaration)).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task Assignment_WithObjectInitializerMembers_WritesBlockInitializer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("global::Testing.OrderAggregate"))
		{
			InitializerMembers =
			[
				new("Details", "jsonModel.Details ?? new global::Purview.EventSourcing.Aggregates.AggregateDetails()"),
				new("CustomerId", "jsonModel.CustomerId"),
			],
		};

		// Act
		writer.Assignment("var", "aggregate", creation);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"var aggregate = new global::Testing.OrderAggregate\n"
					+ "{\n"
					+ "\tDetails = jsonModel.Details ?? new global::Purview.EventSourcing.Aggregates.AggregateDetails(),\n"
					+ "\tCustomerId = jsonModel.CustomerId,\n"
					+ "};\n"
			);
	}

	[Test]
	public async Task Assignment_WithObjectInitializerMembersAndConstructorArguments_WritesArgumentsBeforeInitializer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(
			Type("global::Testing.OrderEvents.OrderCreatedEvent"),
			"customerId",
			"total"
		)
		{
			InitializerMembers = [new("CustomerId", "customerId"), new("Total", "total")],
		};

		// Act
		writer.Assignment("var", "@event", creation);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"var @event = new global::Testing.OrderEvents.OrderCreatedEvent(customerId, total)\n"
					+ "{\n"
					+ "\tCustomerId = customerId,\n"
					+ "\tTotal = total,\n"
					+ "};\n"
			);
	}

	[Test]
	public async Task Assignment_WithInlineInitializerMembers_WritesSingleLineInitializer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"))
		{
			InitializerMembers = [new("A", "1"), new("B", "2")],
			WriteInitializerMembersOnSeparateLines = false,
		};

		// Act
		writer.Assignment("var order", creation);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("var order = new Order { A = 1, B = 2, };\n");
	}

	[Test]
	public async Task Assignment_WithEmptyInitializerMembers_RendersAsToday()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"));

		// Act
		writer.Assignment("var order", creation);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("var order = new Order();\n");
	}

	[Test]
	public async Task Assignment_WithEmptyArgumentsAndForceNotNull_WritesBangAfterParentheses()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"));

		// Act
		writer.Assignment("var order", creation, forceNotNull: true);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("var order = new Order()!;\n");
	}

	[Test]
	public async Task Assignment_WithInitializerMembersAndForceNotNull_WritesBangAfterBrace()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"))
		{
			InitializerMembers = [new("A", "1")],
			WriteInitializerMembersOnSeparateLines = false,
		};

		// Act
		writer.Assignment("var order", creation, forceNotNull: true);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("var order = new Order { A = 1, }!;\n");
	}

	[Test]
	public async Task Assignment_WithMultilineArgumentsAndInitializerMembers_WritesClosingBraceAndSemicolon()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"), "aParameterNameThatForcesTheArgumentsOntoTheirOwnLine")
		{
			WriteArgumentsOnSeparateLines = true,
			InitializerMembers = [new("A", "1")],
		};

		// Act
		writer.Assignment("var order", creation);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"var order = new Order(\n"
					+ "\taParameterNameThatForcesTheArgumentsOntoTheirOwnLine\n"
					+ ")\n"
					+ "{\n"
					+ "\tA = 1,\n"
					+ "};\n"
			);
	}

	[Test]
	public async Task Return_WithObjectInitializerMembers_WritesBlockInitializer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("global::Testing.OrderAggregateJsonModel"))
		{
			InitializerMembers = [new("Details", "Details"), new("CustomerId", "CustomerId")],
		};

		// Act
		writer.Return(creation);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"return new global::Testing.OrderAggregateJsonModel\n"
					+ "{\n"
					+ "\tDetails = Details,\n"
					+ "\tCustomerId = CustomerId,\n"
					+ "};\n"
			);
	}

	[Test]
	public async Task Return_WithConstructorArgumentsAndInitializerMembers_WritesArgumentsBeforeInitializer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"), "customerId", "total")
		{
			InitializerMembers = [new("CustomerId", "customerId"), new("Total", "total")],
		};

		// Act
		writer.Return(creation);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"return new Order(customerId, total)\n"
					+ "{\n"
					+ "\tCustomerId = customerId,\n"
					+ "\tTotal = total,\n"
					+ "};\n"
			);
	}

	[Test]
	public async Task Return_WithInlineInitializerMembers_WritesSingleLineInitializer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"))
		{
			InitializerMembers = [new("A", "1"), new("B", "2")],
			WriteInitializerMembersOnSeparateLines = false,
		};

		// Act
		writer.Return(creation);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("return new Order { A = 1, B = 2, };\n");
	}

	[Test]
	public async Task Return_WithoutArgumentsOrInitializer_WritesEmptyConstruction()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"));

		// Act
		writer.Return(creation);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("return new Order();\n");
	}

	[Test]
	public async Task Return_WithForceNotNull_WritesBang()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var creation = new ObjectCreationOptions(Type("Order"));

		// Act
		writer.Return(creation, forceNotNull: true);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("return new Order()!;\n");
	}

	[Test]
	public async Task Assignment_WithNewExpressionCallback_WritesConstruction()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Assignment("var hostKit", expression => expression.New("HostKit", "onBuilt", "onConfigured"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("var hostKit = new HostKit(onBuilt, onConfigured);\n");
	}

	[Test]
	public async Task Assignment_WithNewExpressionCallbackAndTypeOnly_WritesEmptyConstruction()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment("var order", expression => expression.New("Order"));

		await Assert.That(writer.ToString()).IsEqualTo("var order = new Order();\n");
	}

	[Test]
	public async Task Assignment_WithTargetTypedNewExpressionCallback_WritesTargetTypedConstruction()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment("HostKit", "hostKit", expression => expression.New(["onBuilt", "onConfigured"]));

		await Assert.That(writer.ToString()).IsEqualTo("HostKit hostKit = new(onBuilt, onConfigured);\n");
	}

	[Test]
	public async Task Assignment_WithEmptyTargetTypedNewExpressionCallback_WritesEmptyTargetTypedConstruction()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment("Order", "order", expression => expression.New());

		await Assert.That(writer.ToString()).IsEqualTo("Order order = new();\n");
	}

	[Test]
	public async Task Assignment_WithNewExpressionCallbackAndTypeReference_WritesConstruction()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment("var order", expression => expression.New(Type("global::Testing.Order"), "customerId"));

		await Assert.That(writer.ToString()).IsEqualTo("var order = new global::Testing.Order(customerId);\n");
	}

	[Test]
	public async Task Assignment_WithNewExpressionCallbackAndStructuredArguments_WritesModifiersAndNamedArguments()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment(
			"var handler",
			expression =>
				expression.New(
					"HostKit",
					[
						new MethodCallArgumentOptions("onBuilt"),
						new MethodCallArgumentOptions("onConfigured") { Name = "configured" },
						new MethodCallArgumentOptions("options", ParameterModifier.In),
					]
				)
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo("var handler = new HostKit(onBuilt, configured: onConfigured, in options);\n");
	}

	[Test]
	public async Task Return_WithNewExpressionCallback_WritesReturnConstruction()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Return(expression => expression.New("Order", "customerId"));

		await Assert.That(writer.ToString()).IsEqualTo("return new Order(customerId);\n");
	}

	[Test]
	public async Task New_GivenWhitespaceType_Throws()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert.That(() => writer.New("   ", "value")).Throws<ArgumentException>();
	}

	[Test]
	public async Task New_GivenEmptyTypeReference_Throws()
	{
		var writer = CodeWriterFactory.ForTests();

		await Assert.That(() => writer.New(TypeReference.Empty, "value")).Throws<ArgumentException>();
	}

	[Test]
	public async Task Assignment_WithTargetTypedObjectCreationOptions_WritesTargetTypedConstruction()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment("var", "hostKit", new ObjectCreationOptions("onBuilt", "onConfigured"));

		await Assert.That(writer.ToString()).IsEqualTo("var hostKit = new(onBuilt, onConfigured);\n");
	}

	[Test]
	public async Task Return_WithTargetTypedObjectCreationOptions_WritesTargetTypedConstruction()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Return(new ObjectCreationOptions("customerId"));

		await Assert.That(writer.ToString()).IsEqualTo("return new(customerId);\n");
	}

	[Test]
	public async Task Assignment_WithTargetTypedObjectCreationOptionsAndInitializer_WritesAnonymousTypeInitializer()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Assignment(
			"var",
			"aggregate",
			new ObjectCreationOptions { InitializerMembers = [new("A", "1"), new("B", "2")] }
		);

		await Assert.That(writer.ToString()).IsEqualTo("var aggregate = new { A = 1, B = 2, };\n");
	}

	[Test]
	public async Task Return_WithNewExpressionCallbackAndSeparateLineArguments_WritesMultilineConstruction()
	{
		var writer = CodeWriterFactory.ForTests();

		writer.Return(expression =>
			expression.New(
				"Order",
				[new MethodCallArgumentOptions("aParameterNameThatForcesTheArgumentsOntoTheirOwnLine")],
				writeArgumentsOnSeparateLines: true
			)
		);

		await Assert
			.That(writer.ToString())
			.IsEqualTo("return new Order(\n" + "\taParameterNameThatForcesTheArgumentsOntoTheirOwnLine\n" + ");\n");
	}

	[Test]
	public async Task Assignment_WithNewExpressionCallbackAndSeparateLineArguments_WritesNestedIndentation()
	{
		var writer = CodeWriterFactory.ForTests();
		writer.Indent();

		writer.Assignment(
			"var",
			"optionsBuilder",
			expression =>
				expression.New(
					"Order",
					[new MethodCallArgumentOptions("aParameterNameThatForcesTheArgumentsOntoTheirOwnLine")],
					writeArgumentsOnSeparateLines: true
				)
		);
		writer.Unindent();

		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"\tvar optionsBuilder = new Order(\n"
					+ "\t\taParameterNameThatForcesTheArgumentsOntoTheirOwnLine\n"
					+ "\t);\n"
			);
	}

	[Test]
	public async Task Throw_GivenExceptionTypeAndMessage_WritesThrow()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Throw(
			new TypeIdentity("InvalidOperationException", "System"),
			"Collection property 'Tags' cannot be null."
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"throw new global::System.InvalidOperationException(\"Collection property 'Tags' cannot be null.\");\n"
			);
	}

	[Test]
	public async Task Throw_GivenMessageWithQuotesAndBackslashes_EscapesMessage()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Throw(new TypeIdentity("InvalidOperationException", "System"), "He said \"hi\" to C:\\temp\\file.");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"throw new global::System.InvalidOperationException(\"He said \\\"hi\\\" to C:\\\\temp\\\\file.\");\n"
			);
	}

	[Test]
	public async Task Throw_GivenNullMessage_WritesEmptyConstructor()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Throw(new TypeIdentity("InvalidOperationException", "System"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("throw new global::System.InvalidOperationException();\n");
	}

	[Test]
	public async Task Throw_WithMessageAndConstructorArgument_WritesEscapedMessageAndRawArgument()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Throw(TypeIdentity.Create<ArgumentNullException>(), "Value cannot be null.", "nameof(value)");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo("throw new global::System.ArgumentNullException(\"Value cannot be null.\", nameof(value));\n");
	}

	[Test]
	public async Task Throw_WithNullMessageAndConstructorArgument_WritesRawArgumentOnly()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Throw(TypeIdentity.Create<ArgumentNullException>(), null, "nameof(value)");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo("throw new global::System.ArgumentNullException(nameof(value));\n");
	}

	[Test]
	public async Task Throw_WithControlCharactersInMessage_EscapesNewlinesTabsAndCarriageReturns()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Throw(new TypeIdentity("InvalidOperationException", "System"), "Line 1\r\n\tTabbed \"C:\\path\".\nEnd");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"throw new global::System.InvalidOperationException(\"Line 1\\r\\n\\tTabbed \\\"C:\\\\path\\\".\\nEnd\");\n"
			);
	}

	[Test]
	public async Task Operator_GivenImplicitConversion_WritesConversionOperator()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new OperatorDeclarationOptions(
			"implicit",
			Type("global::Testing.Widget"),
			new("source", Type("global::Testing.RawWidget"))
		)
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			Kind = OperatorDeclarationKind.ImplicitConversion,
			ExpressionBody = "new global::Testing.Widget(source.Value)",
		};

		// Act
		writer.OperatorScope(declaration);

		// Assert
		await Assert.That(writer.OpenScopeCount).IsEqualTo(0);
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static implicit operator global::Testing.Widget(global::Testing.RawWidget source) => new global::Testing.Widget(source.Value);\n"
			);
	}

	[Test]
	public async Task Operator_GivenExplicitConversion_WritesExplicitKeyword()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new OperatorDeclarationOptions(
			"explicit",
			Type("global::Testing.RawWidget"),
			new("widget", Type("global::Testing.Widget"))
		)
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			Kind = OperatorDeclarationKind.ExplicitConversion,
		};

		// Act
		writer.Operator(declaration, body => body.Line("return new global::Testing.RawWidget(widget.Value);"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static explicit operator global::Testing.RawWidget(global::Testing.Widget widget)\n"
					+ "{\n"
					+ "\treturn new global::Testing.RawWidget(widget.Value);\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Operator_GivenUnaryOperator_WritesSingleOperand()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new OperatorDeclarationOptions(
			"-",
			Type("global::Testing.Money"),
			new("value", Type("global::Testing.Money"))
		)
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			Kind = OperatorDeclarationKind.Unary,
		};

		// Act
		writer.Operator(declaration, body => body.Line("return new global::Testing.Money(-value.Amount);"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static global::Testing.Money operator -(global::Testing.Money value)\n"
					+ "{\n"
					+ "\treturn new global::Testing.Money(-value.Amount);\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task Property_GivenRequired_WritesRequiredModifier()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Name", Type("string"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsRequired = true,
			IsInitOnly = true,
		};

		// Act
		writer.Property(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public required string Name { get; init; }\n");
	}

	[Test]
	public async Task Field_GivenRequired_WritesRequiredModifier()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new FieldDeclarationOptions("_name", Type("string"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsRequired = true,
		};

		// Act
		writer.Field(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes(includeCoverageExclusion: false) + "public required string _name;\n");
	}

	[Test]
	public async Task Indexer_GivenAutoAccessors_WritesIndexer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new IndexerDeclarationOptions(Type("string"), [new("index", Type("int"))])
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			HasSetter = true,
		};

		// Act
		writer.Indexer(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string this[int index] { get; set; }\n");
	}

	[Test]
	public async Task Indexer_GivenExpressionBody_WritesExpressionIndexer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new IndexerDeclarationOptions(Type("string"), [new("index", Type("int"))])
		{
			ExpressionBody = "_items[index]",
		};

		// Act
		writer.Indexer(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string this[int index] => _items[index];\n");
	}

	[Test]
	public async Task Indexer_GivenAccessorBodies_WritesScopedAccessors()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new IndexerDeclarationOptions(Type("string"), [new("index", Type("int"))])
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			HasSetter = true,
		};

		// Act
		writer.Indexer(
			declaration,
			getter => getter.Line("return _items[index];"),
			setter => setter.Line("_items[index] = value;")
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public string this[int index]\n"
					+ "{\n"
					+ "\tget\n\t{\n\t\treturn _items[index];\n\t}\n"
					+ "\tset\n\t{\n\t\t_items[index] = value;\n\t}\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task StatementFamily_WritesStructuredBlocks()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Try(tryBody =>
			tryBody.Foreach(
				"var item in items",
				foreachBody =>
					foreachBody.IfElse(
						"item is null",
						ifBody => ifBody.Throw(TypeIdentity.Create<InvalidOperationException>(), "Null item"),
						elseBody => elseBody.MethodCall("Process", "item")
					)
			)
		);
		writer.Catch(TypeIdentity.Create<Exception>(), "ex", catchBody => catchBody.MethodCall("Log", "ex"));
		writer.Finally(finallyBody => finallyBody.MethodCall("Dispose"));
		writer.While("!finished", whileBody => whileBody.MethodCall("Advance"));
		writer.UsingStatement("var stream = Open()", usingBody => usingBody.MethodCall("Read", "stream"));
		writer.LockStatement("_gate", lockBody => lockBody.MethodCall("Run"));

		// Assert
		var result = writer.ToString();
		await Assert
			.That(result)
			.Contains(
				"try\n{\n\tforeach (var item in items)\n\t{\n\t\tif (item is null)\n\t\t{\n\t\t\tthrow new global::System.InvalidOperationException(\"Null item\");\n\t\t}\n\t\telse\n\t\t{\n\t\t\tProcess(item);\n\t\t}\n\t}"
			);
		await Assert.That(result).Contains("catch (global::System.Exception ex)\n{\n\tLog(ex);\n}");
		await Assert.That(result).Contains("finally\n{\n\tDispose();\n}");
		await Assert.That(result).Contains("while (!finished)\n{\n\tAdvance();\n}");
		await Assert.That(result).Contains("using (var stream = Open())\n{\n\tRead(stream);\n}");
		await Assert.That(result).Contains("lock (_gate)\n{\n\tRun();\n}");
		await Assert.That(writer.OpenScopeCount).IsEqualTo(0);
	}

	[Test]
	public async Task DoWhile_GivenCondition_WritesTrailingCondition()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.DoWhile("!finished", body => body.MethodCall("Advance"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("do\n{\n\tAdvance();\n} while (!finished);\n");
	}

	[Test]
	public async Task OpenRegion_GivenName_WritesRegionDirectives()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.OpenRegion("Generated members", body => body.Line("public int Value { get; }"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo("#region Generated members\n\tpublic int Value { get; }\n#endregion\n");
	}

	[Test]
	public async Task Using_GivenGlobal_WritesGlobalUsing()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Using("System.Linq", isGlobal: true);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("global using System.Linq;\n");
	}

	[Test]
	public async Task UsingAlias_WritesAliasDirective()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.UsingAlias("Events", "global::Purview.Events");

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("using Events = global::Purview.Events;\n");
	}

	[Test]
	public async Task Method_GivenSpacesIndentation_UsesConfiguredSize()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests(
			settings: new GenerationSettings("TestGenerator", "1.0.0")
			{
				IndentationStyle = IndentationStyle.Spaces,
				IndentationSize = 2,
			}
		);

		// Act
		using (
			writer.MethodScope(
				new MethodDeclarationOptions("M", Type("void")) { Accessibility = TypeDeclarationAccessibility.Public }
			)
		)
		{
			writer.Line("return;");
		}

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public void M()\n{\n  return;\n}\n");
	}

	[Test]
	public async Task RenderFullName_GivenTypeParameterNullableAndDisabledContext_ElidesAnnotation()
	{
		// Arrange
		var typeParameter = TypeReference.ForTypeParameter("T").Nullable();
		var dynamic = TypeReference.Dynamic.Nullable();

		// Act / Assert
		await Assert.That(typeParameter.RenderFullNameForNullable(nullableSupported: false)).IsEqualTo("T");
		await Assert.That(dynamic.RenderFullNameForNullable(nullableSupported: false)).IsEqualTo("dynamic");
	}

	[Test]
	public async Task RenderFullName_GivenTypeParameterNullableAndEnabledContext_KeepsAnnotation()
	{
		// Arrange
		var typeParameter = TypeReference.ForTypeParameter("T").Nullable();

		// Act / Assert
		await Assert.That(typeParameter.RenderFullNameForNullable(nullableSupported: true)).IsEqualTo("T?");
	}

	[Test]
	public async Task GenerationSettings_GivenLanguageVersion_StoresIt()
	{
		// Arrange / Act
		var settings = new GenerationSettings("G") { LanguageVersion = LanguageVersion.CSharp12 };

		// Assert
		await Assert.That(settings.LanguageVersion).IsEqualTo(LanguageVersion.CSharp12);
	}

	[Test]
	public async Task Property_GivenIsFieldBacked_WritesFieldKeywordAccessors()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Value", Type("int"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			HasSetter = true,
			IsFieldBacked = true,
		};

		// Act
		writer.Property(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public int Value { get => field; set => field = value; }\n");
	}

	[Test]
	public async Task Property_GivenIsFieldBackedInitOnly_WritesFieldKeywordInit()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Name", Type("string"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsInitOnly = true,
			IsFieldBacked = true,
		};

		// Act
		writer.Property(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string Name { get => field; init => field = value; }\n");
	}

	[Test]
	public async Task Property_GivenIsFieldBackedAndExpressionBody_ThrowsWithoutWriting()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Value", Type("int"))
		{
			IsFieldBacked = true,
			ExpressionBody = "field",
		};

		// Act / Assert
		await Assert.That(() => writer.Property(declaration)).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	[Test]
	public async Task Struct_GivenIsRefStruct_WritesRefStruct()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Buffer")
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			Kind = TypeDeclarationKind.Struct,
			IsRefStruct = true,
			IsPartial = false,
		};

		// Act
		using (writer.StructScope(declaration))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public ref struct Buffer\n{\n}\n");
	}

	[Test]
	public async Task Struct_GivenIsRefStructOnRecordStruct_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new TypeDeclarationOptions("Invalid")
		{
			Kind = TypeDeclarationKind.RecordStruct,
			IsRefStruct = true,
		};

		// Act / Assert
		await Assert.That(() => writer.RecordStructScope(declaration)).Throws<ArgumentException>();
	}

	[Test]
	public async Task Field_GivenIsRefField_WritesRefField()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new FieldDeclarationOptions("_value", Type("int"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			IsRefField = true,
		};

		// Act
		writer.Field(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes(includeCoverageExclusion: false) + "public ref int _value;\n");
	}

	[Test]
	public async Task Field_GivenIsRefFieldAndInitializer_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new FieldDeclarationOptions("_value", Type("int")) { IsRefField = true, Initializer = "0" };

		// Act / Assert
		await Assert.That(() => writer.Field(declaration)).Throws<ArgumentException>();
	}

	[Test]
	public async Task CollectionExpression_GivenItems_WritesInlineExpression()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.CollectionExpression(["first", "second", "..rest"]);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("[first, second, ..rest]");
	}

	[Test]
	public async Task CollectionExpression_GivenSeparateLines_WritesMultilineExpression()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.CollectionExpression(["first", "second"], writeOnSeparateLines: true);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("[\n\tfirst,\n\tsecond\n]");
	}

	// ---------------------------------------------------------------------------------------------
	// Declaration overloads
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task MethodOverload_GivenMinimalProperties_WritesMethod()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Method("Run", Type("void"), TypeDeclarationAccessibility.Public, null, body => body.Line("return;"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public void Run()\n{\n\treturn;\n}\n");
	}

	[Test]
	public async Task MethodOverload_WithConfigure_WritesConfiguredMethod()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Method(
			"Run",
			Type("void"),
			TypeDeclarationAccessibility.Public,
			options => options with { IsStatic = true },
			body => body.Line("Execute();")
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public static void Run()\n{\n\tExecute();\n}\n");
	}

	[Test]
	public async Task MethodScopeOverload_GivenMinimalProperties_ReturnsBodyScope()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.MethodScope("Run", Type("void"), TypeDeclarationAccessibility.Public))
			writer.Line("return;");

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public void Run()\n{\n\treturn;\n}\n");
	}

	[Test]
	public async Task PartialMethodOverload_GivenMinimalProperties_WritesPartialMethod()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.PartialMethod("OnChanged", Type("void"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public partial void OnChanged();\n");
	}

	[Test]
	public async Task MethodExpressionOverload_GivenExpressionBody_WritesExpressionMethod()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.MethodExpression("Count", Type("int"), TypeDeclarationAccessibility.Public, "items.Count");

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public int Count() => items.Count;\n");
	}

	[Test]
	public async Task MethodExpressionOverload_GivenCallback_WritesExpressionMethod()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.MethodExpression(
			"Count",
			Type("int"),
			TypeDeclarationAccessibility.Public,
			expression => expression.Write("items.Count")
		);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public int Count() => items.Count;\n");
	}

	[Test]
	public async Task OperatorScopeOverload_GivenBinaryOperator_ReturnsBodyScope()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var left = new ParameterDeclarationOptions("left", Type("global::Testing.Money"));
		var right = new ParameterDeclarationOptions("right", Type("global::Testing.Money"));

		// Act
		using (writer.OperatorScope("==", Type("bool"), left, right, TypeDeclarationAccessibility.Public))
			writer.Return("left.Equals(right)");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static bool operator ==(global::Testing.Money left, global::Testing.Money right)\n"
					+ "{\n"
					+ "\treturn left.Equals(right);\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task OperatorOverload_WithConfigure_WritesConversionOperator()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var source = new ParameterDeclarationOptions("source", Type("global::Testing.RawWidget"));

		// Act
		writer.Operator(
			"implicit",
			Type("global::Testing.Widget"),
			source,
			default,
			TypeDeclarationAccessibility.Public,
			options => options with { Kind = OperatorDeclarationKind.ImplicitConversion },
			body => body.Line("return new global::Testing.Widget(source.Value);")
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public static implicit operator global::Testing.Widget(global::Testing.RawWidget source)\n"
					+ "{\n"
					+ "\treturn new global::Testing.Widget(source.Value);\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task PropertyOverload_GivenMinimalProperties_WritesProperty()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Property("Name", Type("string"), TypeDeclarationAccessibility.Public);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public string Name { get; }\n");
	}

	[Test]
	public async Task PropertyOverload_WithConfigure_WritesConfiguredProperty()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Property(
			"Name",
			Type("string"),
			TypeDeclarationAccessibility.Public,
			options => options with { HasSetter = true, Initializer = "string.Empty" }
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string Name { get; set; } = string.Empty;\n");
	}

	[Test]
	public async Task PropertyOverload_GivenAccessorBodies_WritesScopedAccessors()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Property(
			"Value",
			Type("int"),
			TypeDeclarationAccessibility.Public,
			getter => getter.Line("return _value;"),
			setter => setter.Line("_value = value;"),
			options => options with { HasSetter = true }
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public int Value\n"
					+ "{\n"
					+ "\tget\n\t{\n\t\treturn _value;\n\t}\n"
					+ "\tset\n\t{\n\t\t_value = value;\n\t}\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task PropertyExpressionOverload_WritesExpressionProperty()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.PropertyExpression(
			"Count",
			Type("int"),
			TypeDeclarationAccessibility.Public,
			expression => expression.Write("_items.Count")
		);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public int Count => _items.Count;\n");
	}

	[Test]
	public async Task IndexerOverload_GivenMinimalProperties_WritesIndexer()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Indexer(
			Type("string"),
			TypeDeclarationAccessibility.Public,
			[new("index", Type("int"))],
			options => options with { HasSetter = true }
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string this[int index] { get; set; }\n");
	}

	[Test]
	public async Task IndexerOverload_GivenAccessorBodies_WritesScopedAccessors()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Indexer(
			Type("string"),
			TypeDeclarationAccessibility.Public,
			[new("index", Type("int"))],
			getter => getter.Line("return _items[index];"),
			setter => setter.Line("_items[index] = value;"),
			options => options with { HasSetter = true }
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public string this[int index]\n"
					+ "{\n"
					+ "\tget\n\t{\n\t\treturn _items[index];\n\t}\n"
					+ "\tset\n\t{\n\t\t_items[index] = value;\n\t}\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task FieldOverload_GivenMinimalProperties_WritesField()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Field("_value", Type("int"), TypeDeclarationAccessibility.Private);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes(includeCoverageExclusion: false) + "private int _value;\n");
	}

	[Test]
	public async Task ConstructorScopeOverload_GivenMinimalProperties_ReturnsBodyScope()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.ConstructorScope("Repository", TypeDeclarationAccessibility.Public))
			writer.Line("// body");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public Repository()\n{\n\t// body\n}\n");
	}

	[Test]
	public async Task ConstructorOverload_GivenMinimalProperties_WritesConstructor()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Constructor(
			"Repository",
			TypeDeclarationAccessibility.Public,
			null,
			body => body.Line("Connection = connection;")
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public Repository()\n{\n\tConnection = connection;\n}\n");
	}

	[Test]
	public async Task ClassOverload_GivenMinimalProperties_WritesClass()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Class("Sample", TypeDeclarationAccessibility.Public, null, body => body.Comment("Empty"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public sealed partial class Sample\n{\n\t// Empty\n}\n");
	}

	[Test]
	public async Task ClassScopeOverload_GivenMinimalProperties_ReturnsBodyScope()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.ClassScope("Sample", TypeDeclarationAccessibility.Public))
			writer.Line("// body");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public sealed partial class Sample\n{\n\t// body\n}\n");
	}

	[Test]
	public async Task StructOverload_GivenMinimalProperties_WritesStruct()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Struct("Value", TypeDeclarationAccessibility.Public, null, body => body.Line("// body"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public partial struct Value\n{\n\t// body\n}\n");
	}

	[Test]
	public async Task RecordClassOverload_GivenMinimalProperties_WritesRecordClass()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.RecordClass("Model", TypeDeclarationAccessibility.Public, null, body => body.Line("// body"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public sealed partial record class Model\n{\n\t// body\n}\n");
	}

	[Test]
	public async Task RecordStructOverload_GivenMinimalProperties_WritesRecordStruct()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.RecordStruct("Value", TypeDeclarationAccessibility.Public, null, body => body.Line("// body"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public partial record struct Value\n{\n\t// body\n}\n");
	}

	[Test]
	public async Task InterfaceOverload_GivenMinimalProperties_WritesInterface()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Interface("IService", TypeDeclarationAccessibility.Public, null, body => body.Line("// body"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false)
					+ "public partial interface IService\n{\n\t// body\n}\n"
			);
	}

	[Test]
	public async Task EnumOverload_GivenBody_WritesEnum()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Enum("Status", TypeDeclarationAccessibility.Public, null, body => body.Line("Ready = 1,"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"[global::Microsoft.CodeAnalysis.Embedded]\n"
					+ GeneratedAttributes(includeCoverageExclusion: false)
					+ "public enum Status\n{\n\tReady = 1,\n}\n"
			);
	}

	[Test]
	public async Task EnumOverload_GivenFields_WritesEnumWithFields()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Enum("Status", TypeDeclarationAccessibility.Public, [new("Ready", 1), new("Processing", 2)]);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"[global::Microsoft.CodeAnalysis.Embedded]\n"
					+ GeneratedAttributes(includeCoverageExclusion: false)
					+ "public enum Status\n{\n\tReady = 1,\n\n\tProcessing = 2,\n}\n"
			);
	}

	[Test]
	public async Task TypeOverload_GivenKindAndBody_WritesType()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Type(
			TypeDeclarationKind.RecordClass,
			"Model",
			TypeDeclarationAccessibility.Public,
			null,
			body => body.Line("// body")
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public sealed partial record class Model\n{\n\t// body\n}\n");
	}

	[Test]
	public async Task AttributeClassOverload_GivenMinimalProperties_WritesAttributeClass()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.AttributeClass(
			"RegistryAttribute",
			TypeDeclarationAccessibility.Public,
			AttributeTargets.Class,
			body => body.Line("public string? Name { get; init; }"),
			configure: options => options with { IsPartial = false }
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"[global::Microsoft.CodeAnalysis.Embedded]\n"
					+ GeneratedAttributes()
					+ "[global::System.AttributeUsage(global::System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]\n"
					+ "public sealed class RegistryAttribute : global::System.Attribute\n"
					+ "{\n"
					+ "\tpublic string? Name { get; init; }\n"
					+ "}\n"
			);
	}

	[Test]
	public async Task DelegateOverload_GivenMinimalProperties_WritesDelegate()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Delegate("Factory", Type("TResult"), TypeDeclarationAccessibility.Public, [new("value", Type("T"))]);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes(includeCoverageExclusion: false) + "public delegate TResult Factory(T value);\n"
			);
	}

	[Test]
	public async Task EnumFieldOverload_GivenValue_WritesEnumField()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.EnumField("Ready", 1);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("Ready = 1,\n");
	}

	[Test]
	public async Task NetConditionalReturn_WritesConditionalBlock()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.NetConditionalReturn("Argument '{value}' is required");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"#if NET\n"
					+ "return string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $\"Argument '{value}' is required\");\n"
					+ "#else\n"
					+ "return global::System.FormattableString.Invariant($\"Argument '{value}' is required\");\n"
					+ "#endif\n"
			);
	}

	[Test]
	public async Task NetConditionalReturn_GivenCustomSymbol_WritesConditionalBlock()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.NetConditionalReturn("Value: {value}", "NET8_0_OR_GREATER");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"#if NET8_0_OR_GREATER\n"
					+ "return string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $\"Value: {value}\");\n"
					+ "#else\n"
					+ "return global::System.FormattableString.Invariant($\"Value: {value}\");\n"
					+ "#endif\n"
			);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task NetConditionalReturn_GivenWhitespaceMessage_Throws(string? message)
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act / Assert
		await Assert.That(() => writer.NetConditionalReturn(message!)).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	// ---------------------------------------------------------------------------------------------
	// Default accessibility settings
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Type_WithoutAccessibility_UsesDefaultPublic()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.ClassScope("Sample"))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public sealed partial class Sample\n{\n}\n");
	}

	[Test]
	public async Task Property_WithoutAccessibility_UsesDefaultPublic()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Property("Value", Type("int"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public int Value { get; }\n");
	}

	[Test]
	public async Task Field_WithoutAccessibility_UsesDefaultPrivate()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Field("_value", Type("int"));

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes(includeCoverageExclusion: false) + "private int _value;\n");
	}

	[Test]
	public async Task Method_WithoutAccessibility_UsesDefaultPublic()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Method("Run", Type("void"), null, null, body => body.Return());

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public void Run()\n{\n\treturn;\n}\n");
	}

	[Test]
	public async Task Constructor_WithoutAccessibility_UsesDefaultPublic()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.ConstructorScope("Repository"))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public Repository()\n{\n}\n");
	}

	[Test]
	public async Task Indexer_WithoutAccessibility_UsesDefaultPublic()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Indexer(Type("string"), parameters: [new("index", Type("int"))]);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string this[int index] { get; }\n");
	}

	[Test]
	public async Task Operator_WithoutAccessibility_UsesDefaultPublic()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var left = new ParameterDeclarationOptions("left", Type("int"));
		var right = new ParameterDeclarationOptions("right", Type("int"));

		// Act
		using (writer.OperatorScope("+", Type("int"), left, right, null))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public static int operator +(int left, int right)\n{\n}\n");
	}

	[Test]
	public async Task ExplicitAccessibility_OverridesDefault()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Property("Value", Type("int"), TypeDeclarationAccessibility.Internal);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "internal int Value { get; }\n");
	}

	[Test]
	public async Task SettingDefaultToNull_OmitsAccessibility()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultPropertyAccessibility = null;

		// Act
		writer.Property("Value", Type("int"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "int Value { get; }\n");
	}

	[Test]
	public async Task PropertyAccessors_WithPublicDefaults_StayBare()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Property(
			"Name",
			Type("string"),
			TypeDeclarationAccessibility.Public,
			options => options with { HasSetter = true }
		);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "public string Name { get; set; }\n");
	}

	[Test]
	public async Task PropertySetterDefault_MoreRestrictive_WritesModifier()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultPropertySetterAccessibility = TypeDeclarationAccessibility.Private;

		// Act
		writer.Property(
			"Name",
			Type("string"),
			TypeDeclarationAccessibility.Public,
			options => options with { HasSetter = true }
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string Name { get; private set; }\n");
	}

	[Test]
	public async Task PropertyAccessorDefault_MorePermissive_IsInherited()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultPropertyAccessibility = TypeDeclarationAccessibility.Internal;

		// Act
		writer.Property(
			"Name",
			Type("string"),
			TypeDeclarationAccessibility.Internal,
			options => options with { HasSetter = true }
		);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo(GeneratedAttributes() + "internal string Name { get; set; }\n");
	}

	[Test]
	public async Task GenerationSettings_DefaultAccessibility_FlowsIntoWriter()
	{
		// Arrange
		var settings = new GenerationSettings("G") { DefaultFieldAccessibility = TypeDeclarationAccessibility.Public };
		var writer = new CodeWriter(settings);

		// Act
		writer.Field("_value", Type("int"));

		// Assert
		await Assert.That(writer.ToString()).Contains("public int _value;\n");
		await Assert.That(writer.ToString()).DoesNotContain("private int _value;");
	}

	[Test]
	public async Task WriterDefaultAccessibility_CanBeOverridden()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		writer.DefaultTypeAccessibility = TypeDeclarationAccessibility.Internal;

		// Act
		using (writer.ClassScope("Sample"))
		{
			// Intentionally empty.
		}

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "internal sealed partial class Sample\n{\n}\n");
	}

	// ---------------------------------------------------------------------------------------------
	// HashDefines (conditional compilation)
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task HashDefinesScope_GivenExpression_WritesDirectivesAtColumnZero()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.HashDefinesScope("!EXCLUDE_PURVIEW_TELEMETRY_LOGGING"))
		{
			writer.Line("public const string Value = \"1\";");
		}

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo("#if !EXCLUDE_PURVIEW_TELEMETRY_LOGGING\npublic const string Value = \"1\";\n#endif\n\n");
	}

	[Test]
	public async Task HashDefines_GivenBody_WritesConditionalBlock()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.HashDefines("NET", body => body.Line("// NET only"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("#if NET\n// NET only\n#endif\n\n");
	}

	[Test]
	public async Task HashDefines_InsideClass_WritesDirectivesAtColumnZero()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Class(
			"Sample",
			TypeDeclarationAccessibility.Public,
			null,
			body => body.HashDefines("NET", conditional => conditional.Property("Value", Type("int")))
		);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				GeneratedAttributes()
					+ "public sealed partial class Sample\n"
					+ "{\n"
					+ "#if NET\n"
					+ "\t[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\n"
					+ "\t[global::System.Runtime.CompilerServices.CompilerGenerated]\n"
					+ "\t[global::System.CodeDom.Compiler.GeneratedCode(\"TestGenerator\", \"1.0.0\")]\n"
					+ "\tpublic int Value { get; }\n"
					+ "#endif\n"
					+ "}\n"
			);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task HashDefines_GivenWhitespaceExpression_Throws(string? expression)
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act / Assert
		await Assert.That(() => writer.HashDefines(expression!, _ => { })).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	// ---------------------------------------------------------------------------------------------
	// PragmaDisable (warning suppression)
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task PragmaDisable_GivenSingleCode_WritesDirective()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.PragmaDisable("CS8625");

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("#pragma warning disable CS8625\n\n");
	}

	[Test]
	public async Task PragmaDisable_GivenMultipleCodes_WritesSingleDirective()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.PragmaDisable("CS8625", "CS0618");

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("#pragma warning disable CS8625 CS0618\n\n");
	}

	[Test]
	public async Task PragmaDisable_GivenNoCodes_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act / Assert
		await Assert.That(() => writer.PragmaDisable()).Throws<ArgumentException>();
		await Assert.That(writer.ToString()).IsEmpty();
	}

	// ---------------------------------------------------------------------------------------------
	// HashElse and EmptyScope
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task HashElse_InsideHashDefinesScope_WritesElseDirective()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.HashDefinesScope("NET48_OR_GREATER || PURVIEW_TELEMETRY_NON_NULLABLE"))
		{
			writer.Property(
				"name",
				Type("string"),
				TypeDeclarationAccessibility.Public,
				options => options with { HasSetter = true, IncludeGeneratedAttributes = false }
			);
			writer.HashElse();
			writer.Property(
				"name",
				Type("string").Nullable(),
				TypeDeclarationAccessibility.Public,
				options => options with { HasSetter = true, IncludeGeneratedAttributes = false }
			);
		}

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"#if NET48_OR_GREATER || PURVIEW_TELEMETRY_NON_NULLABLE\n"
					+ "public string name { get; set; }\n"
					+ "#else\n"
					+ "public string? name { get; set; }\n"
					+ "#endif\n\n"
			);
	}

	[Test]
	public async Task EmptyScope_GivenDisposal_WritesNothing()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		using (writer.EmptyScope())
		{
			writer.Line("value");
		}

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("value\n");
	}

	[Test]
	public async Task EmptyScope_TernaryWithHashDefinesScope_WrapsConditionally()
	{
		// Arrange
		var wrapped = CodeWriterFactory.ForTests();
		var guarded = CodeWriterFactory.ForTests();

		// Act — the guard is on: EmptyScope writes nothing around the body.
		using (var scope = true ? wrapped.EmptyScope() : wrapped.HashDefinesScope("NET"))
		{
			_ = scope;
			wrapped.Line("value");
		}

		// Act — the guard is off: HashDefinesScope wraps the body.
		using (var scope = false ? guarded.EmptyScope() : guarded.HashDefinesScope("NET"))
		{
			_ = scope;
			guarded.Line("value");
		}

		// Assert
		await Assert.That(wrapped.ToString()).IsEqualTo("value\n");
		await Assert.That(guarded.ToString()).IsEqualTo("#if NET\nvalue\n#endif\n\n");
	}

	[Test]
	public async Task Empty_GivenBody_InvokesWithoutScope()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Empty(body => body.Line("value"));

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("value\n");
	}

	[Test]
	public async Task FileLevelDirectives_AreSelfSpacingAndColumnZero()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.AutoGeneratedHeader(nullableDirective: NullableDirectiveMode.Disable);
		writer.HashDefines(
			"!NET48_OR_GREATER && !PURVIEW_TELEMETRY_NON_NULLABLE",
			hashWriter => hashWriter.Line("#nullable enable")
		);
		writer.PragmaDisable("CS8625");
		writer.FileScopedNamespace("Purview.Telemetry");

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(
				"// <auto-generated />\n"
					+ "// This code was generated by TestGenerator (version 1.0.0).\n"
					+ "// Changes to this file will be lost when the source generator runs again.\n"
					+ "\n"
					+ "#if !NET48_OR_GREATER && !PURVIEW_TELEMETRY_NON_NULLABLE\n"
					+ "#nullable enable\n"
					+ "#endif\n"
					+ "\n"
					+ "#pragma warning disable CS8625\n"
					+ "\n"
					+ "namespace Purview.Telemetry;\n"
					+ "\n"
			);
	}

	// ---------------------------------------------------------------------------------------------
	// Null literal
	// ---------------------------------------------------------------------------------------------

	[Test]
	public async Task Property_GivenNullInitializer_WritesNull()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new PropertyDeclarationOptions("Name", Type("string"))
		{
			Accessibility = TypeDeclarationAccessibility.Public,
			HasSetter = true,
			IsInitOnly = true,
			Initializer = TypeIdentity.Null,
		};

		// Act
		writer.Property(declaration);

		// Assert
		await Assert
			.That(writer.ToString())
			.IsEqualTo(GeneratedAttributes() + "public string Name { get; init; } = null;\n");
	}

	[Test]
	public async Task Return_GivenNullExpression_WritesNull()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act
		writer.Return(TypeIdentity.Null);

		// Assert
		await Assert.That(writer.ToString()).IsEqualTo("return null;\n");
	}

	[Test]
	public async Task Field_GivenNullLiteralAsType_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();
		var declaration = new FieldDeclarationOptions("_value", TypeReference.Null)
		{
			Accessibility = TypeDeclarationAccessibility.Private,
		};

		// Act / Assert
		await Assert.That(() => writer.Field(declaration)).Throws<ArgumentException>();
	}

	[Test]
	public async Task Method_GivenNullParameterType_Throws()
	{
		// Arrange
		var writer = CodeWriterFactory.ForTests();

		// Act / Assert
		await Assert
			.That(() =>
				writer.Method(
					new MethodDeclarationOptions("M", Type("void"))
					{
						Accessibility = TypeDeclarationAccessibility.Public,
						Parameters = [new("value", TypeReference.Null)],
					},
					_ => { }
				)
			)
			.Throws<ArgumentException>();
	}
}
