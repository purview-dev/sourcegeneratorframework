using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Analyzers;

public sealed class TypeLibraryValidationAnalyzerTests : TUnitDiagnosticAnalyzerTestBase<TypeLibraryValidationAnalyzer>
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
	public async Task Generate_ValidSpec_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("Test")]
					static readonly TypeIdentity MyAttribute = default!;

					[TypeRef(typeof(global::System.Attribute))]
					static readonly TypeIdentity Attribute = default!;

					[TypeRef("MyAttribute", "Test", 1)]
					static readonly TypeIdentity GenericAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task Generate_NonStaticClass_ReportsSpecNotStaticClass(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				partial class TypeLibraryModel
				{
					[TypeRef("MyAttribute", Namespace = "Test")]
					static readonly TypeIdentity MyAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.SpecNotStaticClass.Id);
	}

	[Test]
	public async Task Generate_MemberTypeNotTypeIdentity_ReportsMemberTypeInvalid(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("MyAttribute", Namespace = "Test")]
					internal static readonly string MyAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberTypeInvalid.Id);
	}

	[Test]
	public async Task Generate_TypeWithoutNamespace_ReportsMemberTypeNotResolved(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("MyAttribute", null)]
					static readonly TypeIdentity MyAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberTypeNotResolved.Id);
	}

	[Test]
	public async Task Generate_EmptyNamespace_ReportsMemberTypeNotResolved(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("")]
					static readonly TypeIdentity MyAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberTypeNotResolved.Id);
	}

	[Test]
	public async Task Generate_NegativeArity_ReportsMemberTypeNotResolved(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("Test", -5)]
					static readonly TypeIdentity MyAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberTypeNotResolved.Id);
	}

	[Test]
	public async Task Generate_MultiMemberGroup_DoesNotReportDuplicate(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("Microsoft.Extensions.Logging")]
					static readonly TypeIdentity ILogger = default!;

					[TypeRef("Microsoft.Extensions.Logging")]
					static readonly TypeIdentity LogLevel = default!;

					[TypeRef("Microsoft.Extensions.Logging")]
					static readonly TypeIdentity EventId = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task Generate_PublicMember_ReportsMemberAccessibilityInvalid(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("Test")]
					public static readonly TypeIdentity MyAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.MemberAccessibilityInvalid.Id);
	}

	[Test]
	public async Task Generate_ReferenceMemberWithoutInitializer_ReportsMissingInitializer(
		CancellationToken cancellationToken
	)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("System.Diagnostics")]
					internal static readonly TypeReference ActivityLinkArray;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.ReferenceMemberMissingInitializer.Id);
	}

	[Test]
	public async Task Generate_ValidReferenceMember_DoesNotReportDiagnostic(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary]
				static partial class TypeLibraryModel
				{
					[TypeRef("System.Diagnostics")]
					internal static readonly TypeReference ActivityLinkArray = new TypeReference(default);
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task Generate_InvalidClassName_ReportsInvalidClassName(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary(ClassName = "1Invalid")]
				static partial class TypeLibraryModel
				{
					[TypeRef("MyAttribute", Namespace = "Test")]
					static readonly TypeIdentity MyAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.InvalidClassName.Id);
	}

	[Test]
	public async Task Generate_InvalidNamespace_ReportsInvalidNamespace(CancellationToken cancellationToken)
	{
		var source =
			AttributeDefinition
			+ """
				[GenerateTypeLibrary(Namespace = "1Bad.Ns")]
				static partial class TypeLibraryModel
				{
					[TypeRef("MyAttribute", Namespace = "Test")]
					static readonly TypeIdentity MyAttribute = default!;
				}
				""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
		await Assert.That(result).HasDiagnostic(TypeLibraryValidationAnalyzer.InvalidNamespace.Id);
	}
}
