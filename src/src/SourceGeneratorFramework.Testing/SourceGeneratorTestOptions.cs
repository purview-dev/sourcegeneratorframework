using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Testing;

/*
 * IMPORTANT!
 *
 * If you add a new property to this record, please also update the copy constructor!
 */

/// <summary>
/// Options that configure a source generator test run.
/// </summary>
public record SourceGeneratorTestOptions
{
	/// <summary>
	/// Gets or sets the template copied by newly constructed source generator test options, including
	/// options records derived from this type.
	/// </summary>
	/// <remarks>
	/// Configure this once during test-assembly initialization, before tests execute in parallel.
	/// Existing options instances are snapshots and are not changed when this property is updated.
	/// <para>
	/// This property is typed as <see cref="SourceGeneratorTestOptions"/>. A derived record that needs
	/// a default of its own type should hide it with a typed static, for example
	/// <c>public static new MyTestOptions Default => new();</c>, so fluent extensions such as
	/// <c>Compile()</c> preserve the derived type.
	/// </para>
	/// </remarks>
	public static SourceGeneratorTestOptions Default
	{
		get;
		set => field = value ?? throw new ArgumentNullException(nameof(value), "Default options cannot be null.");
	} = new(true);

	/// <summary>
	/// Initializes a new instance by copying the current <see cref="Default"/> options.
	/// </summary>
	/// <remarks>
	/// Derived records implicitly call this constructor unless they select another base constructor.
	/// </remarks>
	public SourceGeneratorTestOptions()
		: this(Default) { }

	// Bootstraps Default using the property initializers without recursively reading Default.
	SourceGeneratorTestOptions(bool _) { }

	///// <summary>Initializes an options snapshot by copying another instance.</summary>
	///// <param name="source">The options to copy.</param>
	///// <remarks>
	///// This is also the record copy constructor. Mutable collections are copied so options snapshots
	///// can be customized independently.
	///// </remarks>
	//protected SourceGeneratorTestOptions(SourceGeneratorTestOptions source)
	//{
	//	if (source is null)
	//		throw new ArgumentNullException(nameof(source));

	//	IncludeDefaultNamespaces = source.IncludeDefaultNamespaces;
	//	DefaultNamespaces = source.DefaultNamespaces;
	//	AdditionalNamespaces = source.AdditionalNamespaces;
	//	AdditionalAssemblyTypes = source.AdditionalAssemblyTypes;
	//	AdditionalReferences = source.AdditionalReferences;
	//	PreprocessReferences = source.PreprocessReferences;
	//	ThrowOnGenerationException = source.ThrowOnGenerationException;
	//	CompileToAssembly = source.CompileToAssembly;
	//	ValidateCodeWriterScopes = source.ValidateCodeWriterScopes;
	//	EnableLogging = source.EnableLogging;
	//	ThrowOnLogError = source.ThrowOnLogError;
	//	DisableSourceGeneratorPropertyName = source.DisableSourceGeneratorPropertyName;
	//	DisableSourceGeneratorValue = source.DisableSourceGeneratorValue;

	//	AnalyzerConfigOptions = [with(source.AnalyzerConfigOptions)];

	//	TestOutput = source.TestOutput;
	//	CompilationAssemblyName = source.CompilationAssemblyName;
	//	OutputKind = source.OutputKind;
	//	LanguageVersion = source.LanguageVersion;
	//	ExcludeGeneratedSourceHintNames = source.ExcludeGeneratedSourceHintNames;
	//	AdditionalText = source.AdditionalText;
	//	AdditionalSources = source.AdditionalSources;
	//	AnalyzerTypes = source.AnalyzerTypes;
	//	AnalyzerOptions = source.AnalyzerOptions;
	//	CompilationWithAnalyzersOptions = source.CompilationWithAnalyzersOptions;
	//}

	/// <summary>
	/// Gets a value indicating whether the default namespaces should be prepended to the source.
	/// </summary>
	public bool IncludeDefaultNamespaces { get; init; } = true;

	/// <summary>
	/// Gets the default namespaces to prepend.
	/// </summary>
	public ImmutableArray<string> DefaultNamespaces { get; init; } =
	["System", "System.Collections.Generic", "System.Linq"];

	/// <summary>
	/// Gets additional namespaces to prepend after the defaults.
	/// </summary>
	public ImmutableArray<string> AdditionalNamespaces { get; init; } = [];

	/// <summary>
	/// Gets additional types whose assemblies should be referenced.
	/// </summary>
	public ImmutableArray<Type> AdditionalAssemblyTypes { get; init; } = [];

	/// <summary>
	/// Gets additional metadata references to include.
	/// </summary>
	public ImmutableArray<MetadataReference> AdditionalReferences { get; init; } = [];

	/// <summary>
	/// Gets a callback that can mutate the resolved references before the compilation is created.
	/// </summary>
	public Action<ImmutableArray<MetadataReference>>? PreprocessReferences { get; init; }

	/// <summary>
	/// Gets a value indicating whether generation automatically calls <see cref="DriverRunResult.EnsureValid"/>, which throws on errors.
	/// </summary>
	public bool ThrowOnGenerationException { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether the output compilation should be emitted to an assembly.
	/// </summary>
	/// <remarks>
	/// The default is <see langword="false"/> because emitting an assembly is expensive and most
	/// generator tests only need to inspect generated source. Use the <c>Compile</c> extension method
	/// to opt-in when the test needs to load or reflect over the compiled output.
	/// </remarks>
	public bool CompileToAssembly { get; init; }

	/// <summary>
	/// Gets whether code writers created by the generation context should throw when generated
	/// source is materialized while disposable scopes remain open.
	/// </summary>
	/// <remarks>
	/// The default is <see langword="true"/> for generator tests so incomplete generated source is
	/// detected at its point of creation. This value is passed to the generator driver as the
	/// <c>PurviewSourceGeneratorFrameworkValidateCodeWriterScopes</c> compiler-visible property.
	/// </remarks>
	public bool ValidateCodeWriterScopes { get; init; } = true;

	/// <summary>
	/// Gets whether framework source-generator logging is captured for this test run.
	/// </summary>
	public bool EnableLogging { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether generator log errors should cause <see cref="DriverRunResult.EnsureValid"/> to throw.
	/// </summary>
	public bool ThrowOnLogError { get; init; } = true;

	/// <summary>
	/// Gets the analyzer-config property name used to disable the source generator.
	/// </summary>
	public string? DisableSourceGeneratorPropertyName { get; init; }

	/// <summary>
	/// Gets the value for <see cref="DisableSourceGeneratorPropertyName"/>.
	/// </summary>
	public bool? DisableSourceGeneratorValue { get; init; }

	/// <summary>
	/// Gets additional analyzer-config options to pass to the generator driver, such as <c>build_property.*</c> or <c>build_metadata.*</c> values.
	/// </summary>
	[SuppressMessage("Style", "IDE0301:Use collection expression for empty")]
	public ImmutableDictionary<string, string> AnalyzerConfigOptions { get; init; } =
		ImmutableDictionary<string, string>.Empty;

	/// <summary>
	/// Gets the test output receiver used for generator logging.
	/// </summary>
	public ITestOutput TestOutput { get; init; } = NullTestOutput.Instance;

	/// <summary>
	/// Gets the assembly name used for the test compilation.
	/// </summary>
	public string CompilationAssemblyName { get; init; } = "TestAssembly";

	/// <summary>
	/// Gets the output kind of the test compilation.
	/// </summary>
	public OutputKind OutputKind { get; init; } = OutputKind.DynamicallyLinkedLibrary;

	/// <summary>
	/// Gets the nullable context of the test compilation. The default is
	/// <see cref="NullableContextOptions.Enable"/>, so test compilations accept nullable annotations
	/// and the framework's auto-detection emits the <c>#nullable enable</c> directive in generated headers.
	/// </summary>
	public NullableContextOptions NullableContextOptions { get; init; } = NullableContextOptions.Enable;

	/// <summary>
	/// Gets the language version of the test compilation. The default is C# 14, the framework's
	/// Roslyn 5.x baseline, keeping in-memory test compilations deterministic.
	/// </summary>
	public LanguageVersion LanguageVersion { get; init; } = LanguageVersion.CSharp14;

	/// <summary>
	/// Gets generated hint names to exclude from the syntax tree collection. These
	/// are usually marker attributes or other generated content added during
	/// <see cref="IncrementalGeneratorInitializationContext.RegisterPostInitializationOutput(Action{IncrementalGeneratorPostInitializationContext})"/>.
	/// </summary>
	public ImmutableArray<string> ExcludeGeneratedSourceHintNames { get; init; } = [];

	/// <summary>
	/// Gets additional files to include in the test compilation, such as configuration files or other content that can be read by the generator.
	/// </summary>
	public ImmutableArray<AdditionalText> AdditionalText { get; init; } = [];

	/// <summary>
	/// Gets additional source text to include in the test compilation, such as generated code or other content that can be read by the generator.
	/// </summary>
	/// <remarks>
	/// This will be added to the compilation as additional source files, along with the source provided.
	/// </remarks>
	public ImmutableArray<string> AdditionalSources { get; init; } = [];

	/// <summary>
	/// Gets additional analyzer types to include in the test compilation, such as diagnostic
	/// analyzers or other Roslyn analyzers that can be run alongside the generator.
	/// </summary>
	/// <remarks>When this is populated, the compilation will also include the specified analyzers
	/// and populate <see cref="DriverRunResult.AnalyzerResult"/>.</remarks>
	public ImmutableArray<Type> AnalyzerTypes { get; init; } = [];

	/// <summary>
	/// Gets the options to use when running the compilation with analyzers.
	/// </summary>
	/// <remarks>
	/// This is mutually exclusive with <see cref="CompilationWithAnalyzersOptions"/>.
	/// </remarks>
	public AnalyzerOptions? AnalyzerOptions { get; init; }

	/// <summary>
	/// Gets the options to use when running the compilation with analyzers.
	/// </summary>
	/// <remarks>
	/// This is mutually exclusive with <see cref="AnalyzerOptions"/>.
	/// </remarks>
	public CompilationWithAnalyzersOptions? CompilationWithAnalyzersOptions { get; init; }
}
