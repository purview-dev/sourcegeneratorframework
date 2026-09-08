using Microsoft.CodeAnalysis.CodeFixes;
using Purview.SourceGeneratorFramework.Analyzers;
using Purview.SourceGeneratorFramework.Testing;

namespace Purview.SourceGeneratorFramework.CodeFixers;

public sealed class TypeLibraryCodeFixProviderTests
{
	const string AttributeDefinition = """
		using System;
		using Microsoft.CodeAnalysis;
		using Purview.SourceGeneratorFramework;
		using Purview.SourceGeneratorFramework.Generators;

		namespace Purview.SourceGeneratorFramework.Generators
		{
			[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
			public sealed class GenerateTypeLibraryAttribute : Attribute
			{
				public string? ClassName { get; set; }
				public string? Namespace { get; set; }
							}

			[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
			public sealed class TypeRefAttribute : Attribute
			{
				public TypeRefAttribute(string @namespace, int arity = 0)
				{
					Namespace = @namespace;
					Arity = arity;
				}

				public TypeRefAttribute(object type, string? @namespace = null, int arity = -1)
				{
					TargetType = type;
					Namespace = @namespace;
					Arity = arity;
				}

				public object? TargetType { get; }
				public string? Namespace { get; set; }
				public int Arity { get; set; }
							}
		}

		namespace Purview.SourceGeneratorFramework
		{
			public readonly record struct TypeIdentity;
			public sealed record TypeReference(TypeIdentity Identity);
		}
		""";

	[Test]
	public async Task PublicMarker_MakePrivate_ChangesAccessibility(CancellationToken cancellationToken)
	{
		const string source = """
			[GenerateTypeLibrary]
			static partial class TypeLibraryModel
			{
				[TypeRef("Test")]
				public static readonly TypeIdentity MyAttribute = default!;
			}
			""";

		var result = await ApplyCodeFixAsync<TypeLibraryMemberAccessibilityCodeFixProvider>(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = TypeLibraryMemberAccessibilityCodeFixProvider.MakePrivateEquivalenceKey,
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberAccessibilityInvalid.Id);
		await Assert.That(result.FixedSource).Contains("private static readonly TypeIdentity MyAttribute = default!;");
	}

	[Test]
	public async Task PrivateValueMember_MakeInternal_ChangesAccessibility(CancellationToken cancellationToken)
	{
		const string source = """
			[GenerateTypeLibrary]
			static partial class TypeLibraryModel
			{
				[TypeRef("System.Diagnostics")]
				static readonly TypeReference ActivityLinkArray = new TypeReference(default);
			}
			""";

		var result = await ApplyCodeFixAsync<TypeLibraryMemberAccessibilityCodeFixProvider>(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = TypeLibraryMemberAccessibilityCodeFixProvider.MakeInternalEquivalenceKey,
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberAccessibilityInvalid.Id);
		await Assert
			.That(result.FixedSource)
			.Contains("internal static readonly TypeReference ActivityLinkArray = new TypeReference(default);");
	}

	[Test]
	public async Task MarkerWithoutInitializer_AddDefaultInitializer(CancellationToken cancellationToken)
	{
		const string source = """
			[GenerateTypeLibrary]
			static partial class TypeLibraryModel
			{
				[TypeRef("Test")]
				static readonly TypeIdentity MyAttribute;
			}
			""";

		var result = await ApplyCodeFixAsync<TypeLibraryMarkerDefaultInitializerCodeFixProvider>(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = TypeLibraryMarkerDefaultInitializerCodeFixProvider.EquivalenceKey,
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MarkerMissingDefaultInitializer.Id);
		await Assert.That(result.FixedSource).Contains("static readonly TypeIdentity MyAttribute = default;");
	}

	[Test]
	public async Task NonPartialSpec_MakePartial_AddsModifier(CancellationToken cancellationToken)
	{
		const string source = """
			[GenerateTypeLibrary]
			static class TypeLibraryModel
			{
				[TypeRef("Test")]
				static readonly TypeIdentity MyAttribute = default!;
			}
			""";

		var result = await ApplyCodeFixAsync<MakeTypeLibrarySpecPartialCodeFixProvider>(
			source,
			new CodeFixTestOptions { EquivalenceKey = MakeTypeLibrarySpecPartialCodeFixProvider.EquivalenceKey },
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.SpecMustBePartial.Id);
		await Assert.That(result.FixedSource).Contains("static partial class TypeLibraryModel");
	}

	[Test]
	public async Task InvalidMemberType_UseTypeIdentity_ChangesFieldType(CancellationToken cancellationToken)
	{
		const string source = """
			[GenerateTypeLibrary]
			static partial class TypeLibraryModel
			{
				[TypeRef("Test")]
				internal static readonly string MyAttribute = default!;
			}
			""";

		var result = await ApplyCodeFixAsync<TypeLibraryMemberTypeCodeFixProvider>(
			source,
			new CodeFixTestOptions { EquivalenceKey = TypeLibraryMemberTypeCodeFixProvider.TypeIdentityEquivalenceKey },
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberTypeInvalid.Id);
		await Assert
			.That(result.FixedSource)
			.Contains(
				"internal static readonly global::Purview.SourceGeneratorFramework.TypeIdentity MyAttribute = default!;"
			);
	}

	[Test]
	public async Task InvalidMemberType_UseTypeReference_ChangesFieldType(CancellationToken cancellationToken)
	{
		const string source = """
			[GenerateTypeLibrary]
			static partial class TypeLibraryModel
			{
				[TypeRef("Test")]
				internal static readonly string MyAttribute = default!;
			}
			""";

		var result = await ApplyCodeFixAsync<TypeLibraryMemberTypeCodeFixProvider>(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = TypeLibraryMemberTypeCodeFixProvider.TypeReferenceEquivalenceKey,
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberTypeInvalid.Id);
		await Assert
			.That(result.FixedSource)
			.Contains(
				"internal static readonly global::Purview.SourceGeneratorFramework.TypeReference MyAttribute = default!;"
			);
	}

	[Test]
	public async Task SpecNameClashesWithGeneratedClass_RenameSpec_RenamesClass(CancellationToken cancellationToken)
	{
		const string source = """
			[GenerateTypeLibrary]
			static partial class TypeLibrary
			{
				[TypeRef("Test")]
				static readonly TypeIdentity MyAttribute = default!;
			}
			""";

		var result = await ApplyCodeFixAsync<RenameTypeLibrarySpecCodeFixProvider>(
			source,
			new CodeFixTestOptions { EquivalenceKey = RenameTypeLibrarySpecCodeFixProvider.EquivalenceKey },
			cancellationToken
		);

		await Assert
			.That(result)
			.HasDiagnostic(TypeLibraryValidationAnalyzer.SpecClassNameClashesWithGeneratedClass.Id);
		await Assert.That(result.FixedSource).Contains("static partial class TypeLibraryGenerator");
		await Assert.That(result.FixedSource).DoesNotContain("static partial class TypeLibrary\n");
	}

	static Task<CodeFixTestResult> ApplyCodeFixAsync<TCodeFix>(
		string source,
		CodeFixTestOptions options,
		CancellationToken cancellationToken
	)
		where TCodeFix : CodeFixProvider, new()
	{
		var runner = new CodeFixTestRunner<TypeLibraryValidationAnalyzer, TCodeFix>();
		return runner.RunAsync(AttributeDefinition + "\n" + source, options, cancellationToken);
	}
}
