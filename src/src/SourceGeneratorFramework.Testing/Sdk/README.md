# Purview.SourceGeneratorFramework.Testing

Framework-agnostic test runner and assertions for unit testing incremental C# source generators.

## Installation

```bash
dotnet add package Purview.SourceGeneratorFramework.Testing
```

## What's included

- **`SourceGeneratorTestRunner<TGenerator>`** — compiles a snippet of C# source, runs the generator, automatically registers an isolated framework logging sink, and returns a `DriverRunResult` with generated syntax trees, the output compilation, and captured log entries.
- **`SourceGeneratorTestBase<TGenerator>`** — abstract base class that accepts an `ITestOutput` instance for framework-specific logging integration.
- **`SourceGeneratorTestOptions`** — options for configuring references, namespaces, analyzer-config values, output kind, and whether to emit the output compilation to an assembly.
- **`DriverRunResult`** — wrapper around `GeneratorDriverRunResult` that exposes generated trees, the output compilation, emitted assembly, and log entries.
- **`DriverRunResultExtensions`** — assertion helpers such as `AssertNoCompilationErrors`, `AssertNoGenerationExceptions`, `AssertSingleGeneratedSource`, `AssertGeneratedSourceContains`, and more.
- **`ITestOutput`** / **`NullTestOutput`** — abstraction for capturing generator log output during tests.

## Usage

Reference the package from a test project and write a test using the runner directly:

```xml
<ItemGroup>
  <PackageReference Include="Purview.SourceGeneratorFramework.Testing" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
</ItemGroup>
```

```csharp
using Purview.SourceGeneratorFramework.Testing;

public class MyGeneratorTests
{
    [Test]
    public async Task GeneratesExpectedSource()
    {
        var source = """
            [MyNamespace.MyAttribute]
            public partial class MyClass { }
            """;

        var runner = new SourceGeneratorTestRunner<MyGenerator>();
        var result = await runner.RunAsync(source);

        result.AssertNoCompilationErrors();
        var generated = result.AssertSingleGeneratedSource();

        // Use your test framework's assertions, e.g. with TUnit:
        // await Assert.That(generated).Contains("public static partial class MyClass");
    }
}
```

Or derive from `SourceGeneratorTestBase<TGenerator>` and plug in your own `ITestOutput` implementation.

## Running the generator in the test project

Sometimes the test project's own source uses types produced by the generator—for example, an
integration test may attach a generated marker attribute to a fixture class while also passing the
generator type to `SourceGeneratorTestRunner<TGenerator>`.

Reference the generator project twice, once in each role:

```xml
<ItemGroup>
  <!-- Runs the generator during compilation of the test project. -->
  <ProjectReference
    Include="..\..\src\MyGenerator\MyGenerator.csproj"
    PrivateAssets="all"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false"
  />

  <!-- Exposes MyGenerator to SourceGeneratorTestRunner<MyGenerator>. -->
  <ProjectReference
    Include="..\..\src\MyGenerator\MyGenerator.csproj"
    PrivateAssets="all"
    ReferenceOutputAssembly="true"
  />
</ItemGroup>
```

The analyzer reference makes generated declarations available to the test project's compilation.
The normal reference makes the generator's CLR type available to the testing API. These are
separate from the in-memory compilation created by `SourceGeneratorTestRunner`; source supplied to
the runner is still compiled and generated independently.

The normal reference also exposes the generator's assembly dependencies to every target framework
of the test project. This framework is built against Roslyn 5.0, which ships `net8.0` and `net9.0`
package assets, so tests targeting .NET 8, .NET 9, and .NET 10 can all load the test runner. The
Roslyn version used to compile a generator establishes the minimum compiler-host requirement for
projects that consume it as an analyzer — Roslyn 5.0 means `.NET 10` SDK / Visual Studio 2026 or
later. Do not centrally pin `System.Collections.Immutable` to a newer runtime version merely to make
the generator load.

## Options

Configure a test run with `SourceGeneratorTestOptions`:

```csharp
var options = new SourceGeneratorTestOptions
{
    IncludeDefaultNamespaces = true,
    AdditionalNamespaces = ["MyNamespace"],
    AdditionalAssemblyTypes = [typeof(SomeExternalType)],
    EnableLogging = true,
    AnalyzerConfigOptions = { ["MyGenerator_Disable"] = "true" }
};

// Emitting the output to an assembly is opt-in because it is expensive.
var result = await runner.RunAsync(source, options.Compile());
```

`Compile()` is an extension method that preserves the concrete options type. A derived options record
that wants a typed default must hide the inherited `SourceGeneratorTestOptions.Default` with a typed
static, otherwise `Default.Compile()` returns the base type:

```csharp
public record MyTestOptions : SourceGeneratorTestOptions
{
    public static new MyTestOptions Default => new();
}

// Returns MyTestOptions with CompileToAssembly enabled.
var result = await runner.RunAsync(source, MyTestOptions.Default.Compile());
```

### Compiled output

Emission is fully in-memory (no files are written). On .NET 8+ the emitted assembly is loaded into a fresh
**collectible `AssemblyLoadContext`**, so the result is `IDisposable` and the assembly can be unloaded when
you are done with it — keeping repeated `CompileToAssembly` runs from accumulating assemblies in the
process-wide default context:

```csharp
using var result = await runner.RunAsync(source, options.Compile());

result.CompilationResult.Assembly;          // runnable assembly (may execute generated code)
result.CompilationResult.Metadata;          // metadata-only MetadataLoadContext (never executes)
result.CompilationResult.MetadataAssembly;  // emitted assembly reflected within that context
```

`CompilationResult.Metadata` / `MetadataAssembly` provide a metadata-only reflection view over the emitted
assembly: inspect types, members and attributes without loading it into the runtime or executing any code.
They are created lazily on first access. Dispose the result (or its `DriverRunResult`) to unload the
collectible context and release the metadata view.

Analyzer options are preserved under their supplied keys. Keys without the Roslyn
`build_property.` prefix are additionally exposed as compiler-visible MSBuild properties, so either
`MyGenerator_Disable` or `build_property.MyGenerator_Disable` can be used in tests.

See [`SourceGeneratorFramework.Testing.TUnit`](../SourceGeneratorFramework.Testing.TUnit) for a ready-made TUnit integration.

## Querying produced code with `CodeQuery`

Every result type exposes a `CodeQuery` so tests can locate syntax nodes in the produced code:

```csharp
result.Generated()          // DriverRunResult: generated trees (generated-first default)
result.Output()             // DriverRunResult: whole output compilation
analyzerResult.Code()       // AnalyzerTestResult / CodeFixTestResult: input compilation
codeFixResult.FixedCode()   // CodeFixTestResult: fixed source
fixAllResult.FixedCode()    // CodeFixFixAllResult / RefactorTestResult: changed documents
```

`CodeQuery` provides a `Get`/`Has`/`TryGet` family for declarations and members, generic `Get<T>`/`Has<T>`,
syntax-tree lookup, and type-aware matching against `TypeReference`. Every `Get` returns a
`CodeQueryResult<T>` — the matched node (`Node`) plus a query scoped to it (`Query`) — with implicit
conversions to both the node and the scoped query, so member queries chain without re-passing the query:

```csharp
var query = result.Generated();
query.GetClass("ServiceCollectionExtensions").HasMethod("Add", TypeReference.Create<int>());
query.GetClass("Service").GetProperty("Count", TypeReference.Create<int>());   // property + type
query.GetClass("Service").GetMethod("DoWork").HasParameters(intType, nullableInt, complexType);
query.GetClass("Widget", "Example.Models");   // namespace-scoped lookup
query.HasClass(new TypeReference(new TypeIdentity("Widget", "Example.Models")));  // type-identity lookup
query.GetClass(TypeIdentity.Create<Widget>()); // a TypeIdentity is implicitly castable to TypeReference
query.GetClass("ResourceDefinition", 1);       // generic lookup by type-parameter count

ClassDeclarationSyntax cls = query.GetClass("Service");   // implicit conversion to the node
query.GetClass("Service").Node.Members;                    // or use .Node for direct syntax access
```

`Get` throws `SyntaxNotFoundException` when nothing matches; `Has` returns `bool`. See the
`source-generator-testing` agent skill for the full reference.

Type lookups accept an optional generic arity — `GetClass(name, arity)` / `HasClass(name, arity)` — and the
`TypeReference`/`TypeIdentity` overloads match arity automatically from the identity, so
`new TypeIdentity("ResourceDefinition", ns, arity: 1)` finds `ResourceDefinition<T>` without matching the
non-generic `ResourceDefinition`.

Scoped results also expose node-inspection checks through `MemberQueryExtensions`:
`HasAccessibility` (resolves C# defaults), `HasGetterAccessibility` / `HasSetterAccessibility`,
`HasBaseType`, `HasGenericTypeParameter(s)`, `GetNestedType` / `HasNestedType`, `IsInNamespace` /
`IsInGlobalNamespace`, and `GetDeclaredNamespace` on the query itself.

### Nullable expected types in tests

Tests asserting a nullable expected type can use the test-only `query.MakeNullable(type)` extension on a
`CodeQuery` (it accepts a `TypeReference` or `TypeIdentity`). It resolves the annotation against the query's
compilation and, unlike `TypeReference.Nullable()`/`TypeIdentity.MakeNullable()`, does not trigger the
`PSGFR16` context-overload suggestion — tests have no generation context to pass.

```csharp
var query = result.Generated();
query.GetClass("Service").HasProperty("Name", query.MakeNullable(TypeReference.Create<string>()));
```

## Refactoring tests

`RefactoringTestRunner<TRefactoring>` runs a `CodeRefactoringProvider` against a test document:

```csharp
var runner = new RefactoringTestRunner<MyRefactoringProvider>();
var result = await runner.RunAsync(
    source,
    new RefactorTestOptions
    {
        NodeSelector = query => query.GetMethod("M"),
        EquivalenceKey = MyRefactoringProvider.EquivalenceKey,
    });

result.FixedCode().HasMethod("M");   // query the refactored output
```

The trigger is a `Span` or a `NodeSelector` (which runs against a `CodeQuery` of the input compilation).

## Incremental cache testing

`SourceGeneratorTestRunner.RunIncrementalAsync` runs the generator over a sequence of source sets using a
single shared driver and captures each run's tracked incremental steps, so tests can prove each pipeline
stage caches correctly:

```csharp
var result = await runner.RunIncrementalAsync([firstSources, secondSources], options);

var reasons = result.Runs[1].Steps["ForAttribute_MyAttribute"]
    .SelectMany(step => step.Outputs.Select(output => output.Reason));
```

`RunIncrementalAsync(sources, options, ct)` runs the same source set twice (the common "unchanged rerun is
cached" case). Per-run MSBuild-property changes use `new IncrementalRunInput(sources, [...])`. Reference
cache tests live in the `Purview.SourceGeneratorFramework` source repository —
`SourceGeneratorShared.UnitTests/IncrementalPipelineCacheTests` (framework stages) and
`SourceGeneratorFramework.ExampleGenerator.UnitTests/ServiceRegistrationCacheTests` (an end-to-end
generator) — and should be replicated into your own test project rather than copied from the package.

## License

This project is licensed under the MIT license.
