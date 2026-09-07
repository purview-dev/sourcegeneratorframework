# Purview.SourceGeneratorFramework.Testing.TUnit

TUnit integration for testing incremental C# source generators built with `Purview.SourceGeneratorFramework`.

## Installation

```bash
dotnet add package Purview.SourceGeneratorFramework.Testing.TUnit
```

## What's included

- **`TUnitSourceGeneratorTestBase<TGenerator>`** — ready-made base class for TUnit tests. It wires generator log output to `TestContext.Current.OutputWriter`.
- **Custom TUnit assertions** for inspecting `DriverRunResult` instances directly in TUnit tests.
- **MSBuild `.props`** — automatically adds `global using` directives for `Purview.SourceGeneratorFramework.Testing.TUnit` and `Purview.SourceGeneratorFramework.Testing.TUnit.Assertions`.

## Usage

Reference the package from a TUnit test project:

```xml
<ItemGroup>
  <PackageReference Include="TUnit" />
  <PackageReference Include="Purview.SourceGeneratorFramework.Testing.TUnit" />
</ItemGroup>
```

Derive your test class from `TUnitSourceGeneratorTestBase<TGenerator>` and use the inherited `GenerateAsync` method:

```csharp
using Purview.SourceGeneratorFramework.Testing.TUnit;

public class MyGeneratorTests : TUnitSourceGeneratorTestBase<MyGenerator>
{
    [Test]
    public async Task GeneratesExpectedSource()
    {
        var source = """
            [MyNamespace.MyAttribute]
            public partial class MyClass { }
            """;

        var result = await GenerateAsync(source);

        result.AssertNoCompilationErrors();
        var generated = result.AssertSingleGeneratedSource();

        await Assert.That(generated).Contains("public static partial class MyClass");
    }
}
```

The base class also provides access to the underlying `SourceGeneratorTestRunner<TGenerator>` behavior through `GenerateAsync`.

## Using generated types in the TUnit project

If test source files use generated attributes or other generated declarations while the tests also
derive from `TUnitSourceGeneratorTestBase<TGenerator>`, reference the generator project both as an
analyzer and as a normal assembly:

```xml
<ItemGroup>
  <!-- Generates declarations used by classes in this TUnit project. -->
  <ProjectReference
    Include="..\..\src\MyGenerator\MyGenerator.csproj"
    PrivateAssets="all"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false"
  />

  <!-- Makes MyGenerator available as TGenerator. -->
  <ProjectReference
    Include="..\..\src\MyGenerator\MyGenerator.csproj"
    PrivateAssets="all"
    ReferenceOutputAssembly="true"
  />
</ItemGroup>
```

For example, the analyzer reference allows a test fixture to use `[MyGeneratedAttribute]`, while
the normal reference allows the test class to derive from
`TUnitSourceGeneratorTestBase<MyGenerator>`. Do not add `OutputItemType="Analyzer"` to the normal
reference.

For multi-target TUnit projects, the normal reference means the generator's Roslyn dependencies
participate in reference resolution for every target. Build the generator against the Roslyn version
that supports its API usage; this framework is built against Roslyn 5.0, which ships `net8.0` and
`net9.0` package assets, so a .NET 8–10 test matrix still loads it. Compiler hosts that consume the
generator as an analyzer must be Roslyn 5.0 or later (`.NET 10` SDK / Visual Studio 2026). Do not
force a newer `System.Collections.Immutable` version through central package management.

## Which base class and method

| Roslyn type | Base class | Method |
|---|---|---|
| Generator | `TUnitSourceGeneratorTestBase<TGenerator>` | `GenerateAsync(source, options, ct)` |
| Diagnostic analyzer | `TUnitDiagnosticAnalyzerTestBase<TAnalyzer>` | `AnalyzeAsync(source, options, ct)` |
| Code fix (single) | `TUnitCodeFixTestBase<TAnalyzer, TCodeFix>` | `ApplyCodeFixAsync(source, options, ct)` |
| Code fix (fix-all) | `TUnitCodeFixTestBase<TAnalyzer, TCodeFix>` | `ApplyFixAllAsync(sources, options, ct)` |
| Refactoring | `TUnitRefactoringTestBase<TRefactoring>` | `RefactorAsync(source, options, ct)` |

For cache tests, `TUnitSourceGeneratorTestBase` also exposes `GenerateIncrementalAsync(...)`.

## Easy starting point: derived options

Derive a `SourceGeneratorTestOptions` record that seeds namespaces and additional assemblies, then pass it
to every test:

```csharp
public sealed record MyTestOptions : SourceGeneratorTestOptions
{
    public MyTestOptions()
    {
        AdditionalNamespaces = AdditionalNamespaces.Add("My.Namespace");
        AdditionalAssemblyTypes = AdditionalAssemblyTypes.AddRange(typeof(SomeDependencyType), typeof(TypeIdentity));
        DisableSourceGeneratorPropertyName = "DisableMyGenerator";
    }
}

public class MyGeneratorTests : TUnitSourceGeneratorTestBase<MyGenerator, MyTestOptions> { ... }
```

Use `options.Compile()` for `CompileToAssembly`, and the `OnBeforeRun`/`OnBeforeRunAsync`/`OnAfterRun`
hooks for per-run customisation. Code-fix/refactoring tests select actions with `EquivalenceKey` or
`CodeActionIndex` (and `RefactorTestOptions.NodeSelector`/`Span`).

## Assertion extensions

All assertion extensions are under `Purview.SourceGeneratorFramework.Testing.TUnit.Assertions` (globally
imported). `await Assert.That(...)` is terminal and returns the value:

- `HasGeneratedMethod` / `HasGeneratedMethodReturnType` / `HasGeneratedClass` / `HasGeneratedProperty` /
  `HasGeneratedField` / `HasGeneratedSyntaxTree` — return the syntax node; `HasGeneratedMethod(name, TypeReference[])`
  matches parameter types.
- `HasFixedMethod` — same for code-fix and refactoring results.
- `HasDiagnostic` / `HasDiagnostics` / `HasNoDiagnostics` / `DoesNotHaveDiagnostic` / `HasNoErrorDiagnostics`.
- `HasSymbol(TypeIdentity)` / `HasSymbol("Namespace.Type")`.
- `GeneratesCode(expected)` / `ContainsGeneratedCode(expected)` (whitespace-flattened).

```csharp
MethodDeclarationSyntax method = await Assert.That(result).HasGeneratedMethod("DoWork", [intType, nullableInt]);
await Assert.That(result).HasGeneratedSyntaxTree("Service.g.cs");
```

## Incremental cache tests

`GenerateIncrementalAsync` proves the pipeline caches stage-by-stage (first run `New`, identical rerun
`Cached`/`Unchanged`, targeted changes mark only the affected stage `Modified`). A reference
implementation (`ServiceRegistrationCacheTests`) lives in the `Purview.SourceGeneratorFramework` source
repository's example generator tests; replicate it in your own project with your own stage names.

## License

This project is licensed under the MIT license.
