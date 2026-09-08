---
created: 2026-08-29
updated: 2026-08-29
tags: 
  - source-generator
  - analyser
  - roslyn
  - best-practices
---

# Source Generator & Analyser Best Practices

> Practical guidance for writing Roslyn analysers and incremental source generators that remain fast, deterministic, cache-friendly, IDE-compatible, and safe to distribute.

---

## Contents

- [1. Core Principles](#1-core-principles)
- [2. Analyser or Source Generator?](#2-analyser-or-source-generator)
- [3. Choosing an Analyser Action](#3-choosing-an-analyser-action)
- [4. Syntax vs Symbol vs Operation](#4-syntax-vs-symbol-vs-operation)
- [5. Analyser Best Practices](#5-analyser-best-practices)
- [6. Incremental Generator Golden Rules](#6-incremental-generator-golden-rules)
- [7. Pipeline Value Equality](#7-pipeline-value-equality)
- [8. Designing the Incremental Pipeline](#8-designing-the-incremental-pipeline)
- [9. Syntax Discovery](#9-syntax-discovery)
- [10. `Collect`, `Combine`, and Invalidation](#10-collect-combine-and-invalidation)
- [11. Diagnostics](#11-diagnostics)
- [12. Output Generation](#12-output-generation)
- [13. Testing Incrementally](#13-testing-incrementally)
- [14. Roslyn Version Compatibility](#14-roslyn-version-compatibility)
- [15. Visual Studio, .NET SDK, and Rider](#15-visual-studio-net-sdk-and-rider)
- [16. Multi-Version Roslyn Packaging](#16-multi-version-roslyn-packaging)
- [17. `Microsoft.CodeAnalysis.Analysers`](#17-microsoftcodeanalysisanalysers)
- [18. Recommended Project Configuration](#18-recommended-project-configuration)
- [19. Review Checklist](#19-review-checklist)

---

# 1. Core Principles

The most important rules are:

1. **Use an analyser to validate user code.**
2. **Use an incremental generator to generate code.**
3. **Use `ForAttributeWithMetadataName` for attribute-driven generators.**
4. **Remove Roslyn objects from the incremental pipeline as early as possible.**
5. **Every value crossing a pipeline boundary should have meaningful value equality.**
6. **Prefer many small incremental stages over one large transform.**
7. **Keep broad inputs such as `Compilation` away from downstream generation.**
8. **Generate deterministic output.**
9. **Compile against the oldest Roslyn API version you actually need.**
10. **Test caching, not just generated text.**

The guiding principle for an incremental generator is:

> **Extract semantic information once, convert it into a small value model, and make everything downstream operate only on that value model.**

---

# 2. Analyser or Source Generator?

Analysers and generators have different responsibilities.

A `DiagnosticAnalyser` answers:

> Is the source code valid according to this library's rules?

An `IIncrementalGenerator` answers:

> Given valid source code, what source should be generated?

## Decision Table

| Requirement | Prefer | Reason |
| --- | --- | --- |
| Require a class to be `partial` | Analyser | User-code contract violation |
| Require an attribute on a declaration | Analyser | User-code contract violation |
| Validate a method signature | Analyser | Semantic validation |
| Reject unsupported property types | Analyser | Better IDE feedback |
| Detect invalid attribute arguments | Analyser | Natural diagnostic |
| Detect unsupported API usage | Analyser | Operation analysis |
| Offer an automatic fix | Analyser + `CodeFixProvider` | Code fixes operate on diagnostics |
| Generate members for a marked class | Incremental generator | Generation |
| Generate serializers/validators/mappers | Incremental generator | Generation |
| Generate a registry from discovered types | Incremental generator | Generation |
| Read an additional schema file and generate C# | Incremental generator | Generation |
| Report an unexpected internal generation failure | Generator diagnostic | Generation-specific failure |
| Validate generator-only external input | Generator diagnostic may be appropriate | Analyser may not see equivalent input |

Prefer:

```text
User Source
    │
    ├── Analyser
    │     ├── discovers relevant source
    │     ├── validates the contract
    │     ├── reports diagnostics
    │     └── optionally provides code fixes
    │
    └── Incremental Generator
          ├── discovers relevant source
          ├── extracts semantic values
          ├── creates equatable models
          └── generates deterministic source
```

A useful shorthand is:

> **Analysers protect the contract. Generators implement the contract.**

---

# 3. Choosing an Analyser Action

Use the narrowest analyser API that directly represents the thing being analysed.

Do not start with a broad syntax scan if Roslyn already exposes the concept as a symbol or operation.

## Analyser Action Decision Matrix

| API | Use when | Examples | Recommendation |
| --- | --- | --- | --- |
| `RegisterSyntaxNodeAction` | Exact source syntax matters | Modifier presence, declaration form | Use for syntax rules |
| `RegisterSymbolAction` | A declaration's semantic meaning matters | Accessibility, attributes, implemented interfaces | Preferred for declaration rules |
| `RegisterOperationAction` | Executable behaviour/API usage matters | Invocation, assignment, conversion, object creation | Preferred for semantic usage rules |
| `RegisterOperationBlockStartAction` | Multiple operations inside one body need shared state | Track resource use throughout a method | Use for stateful method analysis |
| `RegisterOperationBlockAction` | Whole executable body must be analysed | Final body-level validation | Prefer narrower actions where possible |
| `RegisterCodeBlockStartAction` | Syntax-oriented body analysis needs state | Stateful syntax rules | Less common than operation-block analysis |
| `RegisterCodeBlockAction` | Entire syntax code block matters | Body-level syntax rules | Use sparingly |
| `RegisterSymbolStartAction` | A symbol and its members must be analysed together | Type-wide analysis across members | Powerful but relatively expensive |
| Symbol end action | Result depends on all child/member analysis | Report once for entire type | Register from symbol-start |
| `RegisterCompilationStartAction` | Expensive semantic setup should happen once | Resolve known framework/library symbols | Good initialization boundary |
| Compilation end action | A result genuinely depends on the entire compilation | Global collision/aggregate rule | Avoid unless necessary |
| `RegisterAdditionalFileAction` | Analyze `AdditionalFiles` | Config/schema validation | Correct abstraction |
| `RegisterSemanticModelAction` | Analysis genuinely applies to an entire semantic model | Rare tree-wide semantic rule | Usually too broad |
| `RegisterSyntaxTreeAction` | Entire raw syntax tree matters | File header / file-level syntax | Prefer node actions when possible |

---

# 4. Syntax vs Symbol vs Operation

The most common analyser design decision is choosing between:

- syntax;
- symbols;
- operations.

## Quick Decision

```text
Does exact source spelling/structure matter?
        │
        ├── Yes ──► Syntax
        │
        └── No
             │
             ├── Is this a declaration?
             │       └── Yes ──► Symbol
             │
             └── Is this executable behaviour?
                     └── Yes ──► Operation
```

---

## Syntax

Use syntax when the literal structure of the user's source matters.

Examples:

- is `partial` explicitly present?
- did the user write a primary constructor?
- is the namespace file-scoped?
- was an explicit modifier specified?
- is a declaration syntactically structured in a particular way?

Example:

```csharp
context.RegisterSyntaxNodeAction(
    AnalyzeClass,
    SyntaxKind.ClassDeclaration
);
```

Syntax is often the cheapest solution when no semantic information is required.

Do not ask the semantic model a question that can be answered directly from syntax.

---

## Symbols

Use symbols when analysing declarations semantically.

Examples:

- does this type implement `IDisposable`?
- does this member have a particular attribute?
- what is the method return type?
- what is the property's accessibility?
- what generic type arguments are present?
- is this type abstract?
- which containing namespace owns this type?

Example:

```csharp
context.RegisterSymbolAction(
    AnalyzeNamedType,
    SymbolKind.NamedType
);
```

When comparing symbols:

```csharp
SymbolEqualityComparer.Default.Equals(left, right)
```

should normally be used rather than reference equality.

---

## Operations

Use `IOperation` when analysing executable semantics.

Examples:

- method invocation;
- constructor invocation;
- assignment;
- conversion;
- property access;
- field access;
- argument passing;
- return values;
- `await`;
- binary/unary operations.

For example:

```csharp
context.RegisterOperationAction(
    AnalyzeInvocation,
    OperationKind.Invocation
);
```

Then:

```csharp
static void AnalyzeInvocation(OperationAnalysisContext context)
{
    var invocation = (IInvocationOperation)context.Operation;

    var method = invocation.TargetMethod;

    // Semantic method information is already available.
}
```

This is normally preferable to:

```csharp
context.RegisterSyntaxNodeAction(
    AnalyzeInvocation,
    SyntaxKind.InvocationExpression
);
```

followed by:

```csharp
context.SemanticModel.GetSymbolInfo(...)
```

for every invocation.

---

## Common Operation Kinds

| Requirement | Operation |
| --- | --- |
| Method invocation | `OperationKind.Invocation` |
| Constructor invocation | `OperationKind.ObjectCreation` |
| Assignment | `OperationKind.SimpleAssignment` |
| Compound assignment | compound assignment operation kinds |
| Argument validation | `OperationKind.Argument` |
| Property access | `OperationKind.PropertyReference` |
| Field access | `OperationKind.FieldReference` |
| Conversion | `OperationKind.Conversion` |
| Return expression | `OperationKind.Return` |
| Await | `OperationKind.Await` |
| Binary expression | `OperationKind.Binary` |
| Unary expression | `OperationKind.Unary` |

Use the semantic operation rather than reconstructing equivalent information from syntax whenever possible.

---

# 5. Analyser Best Practices

## Enable Concurrent Execution

Analysers should normally enable concurrent execution:

```csharp
public override void Initialize(AnalysisContext context)
{
    context.EnableConcurrentExecution();

    context.ConfigureGeneratedCodeAnalysis(
        GeneratedCodeAnalysisFlags.None
    );

    // Register actions.
}
```

Analyser callbacks can execute concurrently.

Avoid shared mutable state.

---

## Configure Generated Code Explicitly

Do not leave generated-code handling implicit.

For most library contract analysers:

```csharp
context.ConfigureGeneratedCodeAnalysis(
    GeneratedCodeAnalysisFlags.None
);
```

is appropriate.

Only inspect generated code if the analyser explicitly needs to.

---

## Resolve Known Types Once

If an analyser needs to repeatedly compare against known framework or library types, resolve them during compilation start.

```csharp
context.RegisterCompilationStartAction(static context =>
{
    var targetType =
        context.Compilation.GetTypeByMetadataName(
            "MyLibrary.SomeType"
        );

    if (targetType is null)
        return;

    context.RegisterOperationAction(
        c => AnalyzeInvocation(c, targetType),
        OperationKind.Invocation
    );
});
```

This is a good reason to use `CompilationStart`.

Do not use `CompilationStart` merely to enumerate the entire compilation.

---

## Prefer Narrow Registration

Prefer:

```csharp
context.RegisterOperationAction(
    AnalyzeInvocation,
    OperationKind.Invocation
);
```

over an action that sees every operation.

Prefer:

```csharp
context.RegisterSyntaxNodeAction(
    AnalyzeClass,
    SyntaxKind.ClassDeclaration
);
```

over scanning an entire `SyntaxTree`.

---

# 6. Incremental Generator Golden Rules

Implement:

```csharp
IIncrementalGenerator
```

rather than the legacy:

```csharp
ISourceGenerator
```

But simply implementing `IIncrementalGenerator` does **not** make a generator meaningfully incremental.

Incrementally depends on the equality behaviour of the values flowing through the pipeline.

## The Golden Rule

> **Pipeline values must be immutable and value-equatable.**

Roslyn needs to determine:

```text
Did this pipeline stage produce the same logical value as last time?
```

If the answer is yes, Roslyn can stop executing downstream stages and reuse cached results.

---

# 7. Pipeline Value Equality

## Pipeline Red List

These should not normally survive into your generator model.

| Type | Verdict | Why |
| --- | --- | --- |
| `ISymbol` | ❌ Never retain | Not suitable for pipeline equality; can retain old compilations |
| `INamedTypeSymbol` | ❌ Never retain | Same problem as `ISymbol` |
| `IMethodSymbol` | ❌ Never retain | Same problem as `ISymbol` |
| `IPropertySymbol` | ❌ Never retain | Same problem as `ISymbol` |
| `Compilation` | ❌ Do not propagate | Huge semantic graph and broad invalidation source |
| `SemanticModel` | ❌ Do not propagate | Bound to compilation/tree |
| `IOperation` | ❌ Do not propagate | Compiler semantic graph object |
| `SyntaxTree` | ❌ Do not propagate | Changes with source edits |
| `SyntaxNode` | ⚠ Remove ASAP | Usually loses equality after edits to its tree |
| `Location` | ⚠ Remove ASAP | Same incrementally problem as syntax |
| `AdditionalText` | ⚠ Project immediately | Host/compiler input object |
| `T[]` | ❌ Avoid in models | Reference equality |
| `List<T>` | ❌ Avoid in models | Mutable and reference equality |
| `ImmutableArray<T>` | ⚠ Wrap/compare explicitly | Immutable but not sequence-value-equatable for model equality |
| Mutable class | ❌ Avoid | Reference equality unless explicitly implemented |

---

## Good Model

```csharp
internal sealed record TypeModel(
    string Namespace,
    string Name,
    string FullyQualifiedName,
    Accessibility Accessibility,
    EquatableArray<PropertyModel> Properties
);

internal sealed record PropertyModel(
    string Name,
    string FullyQualifiedTypeName,
    bool IsNullable
);
```

---

## Bad Model

```csharp
internal sealed record TypeModel(
    INamedTypeSymbol Symbol,
    Compilation Compilation,
    Location Location,
    ImmutableArray<IPropertySymbol> Properties
);
```

Making the outer object a `record` does not magically make its members suitable for incremental equality.

---

## `ImmutableArray<T>` Is Not Enough

`ImmutableArray<T>` solves:

> Can this collection be mutated?

It does not automatically solve:

> Do two separately-created collections containing equivalent elements compare as the same sequence for my pipeline model?

These are different problems.

For incremental models, prefer something such as:

```csharp
EquatableArray<T>
```

with sequence-based equality.

Conceptually:

```csharp
internal readonly struct EquatableArray<T>
    : IEquatable<EquatableArray<T>>
{
    private readonly ImmutableArray<T> _items;

    public bool Equals(EquatableArray<T> other)
    {
        if (_items.Length != other._items.Length)
            return false;

        return _items
            .AsSpan()
            .SequenceEqual(other._items.AsSpan());
    }

    public override bool Equals(object? obj) =>
        obj is EquatableArray<T> other &&
        Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var item in _items)
            hash.Add(item);

        return hash.ToHashCode();
    }
}
```

The precise implementation can vary.

The important requirement is:

```text
same contents => equal pipeline value
```

---

# 8. Designing the Incremental Pipeline

Think of every transformation as a cache checkpoint.

Prefer:

```text
Roslyn Input
    │
    ▼
Cheap discovery
    │
    ▼
Semantic extraction
    │
    ▼
Small equatable model
    │
    ▼
Validation/transformation
    │
    ▼
Generation model
    │
    ▼
Source output
```

Do not do:

```text
Roslyn Input
    │
    ▼
Giant transform containing symbols + syntax + compilation
    │
    ▼
Generate everything
```

---

## Project Early

The semantic transform should usually be the boundary where Roslyn objects disappear.

Example:

```csharp
static TypeModel CreateModel(
    GeneratorAttributeSyntaxContext context,
    CancellationToken cancellationToken
)
{
    var symbol = (INamedTypeSymbol)context.TargetSymbol;

    return new TypeModel(
        Namespace:
            symbol.ContainingNamespace.ToDisplayString(),

        Name:
            symbol.Name,

        FullyQualifiedName:
            symbol.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),

        Accessibility:
            symbol.DeclaredAccessibility
    );
}
```

Everything downstream should receive `TypeModel`, not `INamedTypeSymbol`.

---

## Prefer Static Lambdas

Prefer:

```csharp
.Select(static (value, cancellationToken) =>
{
    return Transform(value, cancellationToken);
});
```

Static callbacks prevent accidental capture of generator instance state.

Generator instances should not be treated as application services or state containers.

---

## Honour Cancellation

For non-trivial transformations:

```csharp
.Select(static (value, cancellationToken) =>
{
    cancellationToken.ThrowIfCancellationRequested();

    return Transform(value, cancellationToken);
});
```

Pass cancellation tokens into Roslyn APIs that accept them.

---

## Split Transformations

Prefer:

```text
Syntax
  ↓
Symbol projection
  ↓
Type model
  ↓
Property models
  ↓
Generation model
  ↓
Output
```

over:

```text
Syntax
  ↓
Do absolutely everything
  ↓
Output
```

More meaningful boundaries give Roslyn more opportunities to short-circuit downstream processing.

---

# 9. Syntax Discovery

## Prefer `ForAttributeWithMetadataName`

Attribute-driven generation should normally start with:

```csharp
context.SyntaxProvider.ForAttributeWithMetadataName(
    fullyQualifiedMetadataName:
        "MyLibrary.GenerateAttribute",

    predicate:
        static (node, _) =>
            node is TypeDeclarationSyntax,

    transform:
        static (context, cancellationToken) =>
            CreateModel(context, cancellationToken)
);
```

Advantages include:

- highly optimized discovery;
- alias support;
- direct `TargetSymbol`;
- matching `AttributeData`;
- obvious user intent;
- easier analyser integration.

For marker-attribute generators, this should be the default.

---

## Use `CreateSyntaxProvider` When Syntax Is Actually the Trigger

Use:

```csharp
context.SyntaxProvider.CreateSyntaxProvider(...)
```

when there is no appropriate marker attribute.

Examples:

- syntax-driven DSL;
- a generator intentionally driven by a language construct;
- a pattern that cannot reasonably use an attribute.

The predicate must be cheap.

Good:

```csharp
predicate:
    static (node, _) =>
        node is ClassDeclarationSyntax
        {
            AttributeLists.Count: > 0
        }
```

Bad:

```csharp
predicate:
    static (node, _) =>
    {
        // expensive walking
        // semantic work
        // allocations
        // string construction
        return true;
    }
```

The predicate runs extremely frequently.

Semantic work belongs in the transformation callback.

---

## Avoid Indirect Discovery

Avoid designs that require discovering:

- every indirect implementation of an interface;
- every indirect subclass;
- inherited marker attributes through arbitrary hierarchies;
- every type in a compilation followed by manual filtering.

A change high in a type hierarchy can invalidate a large arbitrary portion of the compilation.

Prefer explicit intent:

```csharp
[GenerateSchema]
partial class Customer
{
}
```

over:

```text
Generate everything somewhere downstream of IBaseSchemaThing
```

---

# 10. `Collect`, `Combine`, and Invalidation

## `Collect()`

`Collect()` transforms:

```csharp
IncrementalValuesProvider<T>
```

into roughly:

```csharp
IncrementalValueProvider<ImmutableArray<T>>
```

This changes invalidation scope.

Before:

```text
A ──► output A
B ──► output B
C ──► output C
```

After collection:

```text
A ─┐
B ─┼──► [A,B,C] ──► output
C ─┘
```

Changing `B` changes the aggregate `[A,B,C]`.

---

## Prefer Per-Item Output

Prefer:

```csharp
context.RegisterSourceOutput(
    models,
    static (context, model) =>
        Emit(context, model)
);
```

instead of:

```csharp
context.RegisterSourceOutput(
    models.Collect(),
    static (context, models) =>
    {
        foreach (var model in models)
            Emit(context, model);
    }
);
```

unless the generation genuinely requires the complete set.

---

## Good Uses of `Collect()`

Use `Collect()` when generating something intrinsically global:

- one registry containing every handler;
- one lookup containing every generated type;
- duplicate-name detection across all targets;
- one aggregate switch;
- one generated dependency map.

A useful design is:

```text
                    ┌──► Per-type source
Type Models ────────┤
                    │
                    └──► Collect()
                            │
                            ▼
                     Global registry
```

Only the registry should pay the global invalidation cost.

---

## `Combine()`

Use `Combine()` when one output logically depends on two providers.

Example:

```csharp
var generationInput =
    typeModels.Combine(generatorOptions);
```

That means:

```text
type changed ───────┐
                    ├──► generation invalidated
option changed ─────┘
```

This is correct if either input should regenerate the output.

---

## Be Very Careful Combining `CompilationProvider`

This:

```csharp
models.Combine(context.CompilationProvider)
```

is often an incremental performance smell.

Almost any semantic change can replace the compilation.

If possible, project the compilation into the tiny fact you actually need:

```csharp
var capabilities =
    context.CompilationProvider
        .Select(static (compilation, _) =>
            new CompilationCapabilities(
                HasRequiredType:
                    compilation.GetTypeByMetadataName(
                        "MyLibrary.RequiredType"
                    ) is not null
            )
        );
```

Then:

```csharp
models.Combine(capabilities)
```

At least downstream equality can now short-circuit when the relevant capability did not change.

---

## `WithComparer()`

Roslyn provides:

```csharp
.WithComparer(...)
```

when default equality is insufficient.

Example:

```csharp
provider.WithComparer(
    MyModelComparer.Instance
);
```

Use this when your logical equality differs from the default implementation.

Do not use it as a way to justify retaining large compiler objects inside the model.

This is suspicious:

```csharp
record Model(
    INamedTypeSymbol Symbol,
    Compilation Compilation
);
```

followed by an elaborate comparer.

The better solution is usually to redesign `Model`.

---

# 11. Diagnostics

## Prefer a Separate Analyser

Normal user validation belongs in a `DiagnosticAnalyser`.

Benefits include:

- immediate IDE feedback;
- independent execution from generation;
- easier testing;
- code-fix support;
- simpler incremental generator pipelines.

---

## Generator Diagnostics Are Still Valid

Generator diagnostics make sense for things such as:

- malformed additional files;
- invalid generator-only configuration;
- conflicting generated output discovered only during generation;
- failures that cannot naturally be expressed by a separate analyser.

Do not turn the generator pipeline into an analyser pipeline by default.

---

## Location Handling

Analysers should report diagnostics on the most useful user-authored `Location`.

Generators should avoid keeping `Location` inside long-lived pipeline models.

If a generator absolutely requires source position information, convert it into a value model:

```csharp
internal readonly record struct SourceLocationModel(
    string FilePath,
    int Start,
    int Length
);
```

But even this should only be carried downstream if generation actually depends on the location.

---

# 12. Output Generation

## Output Must Be Deterministic

For the same generator model:

```text
input model
    ↓
identical generated source
```

Avoid:

- current timestamps;
- random GUIDs;
- process IDs;
- machine-specific paths;
- unordered dictionary output;
- machine environment variables;
- current culture affecting generation.

---

## Deterministic Hint Names

Good:

```csharp
context.AddSource(
    $"{model.HintName}.g.cs",
    source
);
```

Bad:

```csharp
context.AddSource(
    $"{Guid.NewGuid():N}.g.cs",
    source
);
```

Hint names must be:

- deterministic;
- unique within the generator;
- stable when irrelevant source changes.

---

## Prefer Text Generation

Do not build a complete Roslyn syntax tree merely to generate source unless there is a strong reason.

For generator output, a small code writer or structured string builder is generally easier and faster.

Avoid repeatedly doing:

```csharp
syntax.NormalizeWhitespace().ToFullString()
```

for large generated trees.

Generated output may use C# 14 features — extension-member blocks (`extension(...)`, via
`CodeWriter.ExtensionBlockScope`), the `field` keyword, collection expressions — when the target
compilation supports them. The framework is built against Roslyn 5.x, so its generators may emit C# 14
output; consumers need a matching compiler (`.NET 10` SDK / Roslyn 5.0 or later) to compile it. Gate any
newer-than-baseline features on `GenerationSettings.LanguageVersion` when a generator must also serve
older hosts.

---

## Post-Initialization Output

Use:

```csharp
RegisterPostInitializationOutput
```

for source that is constant regardless of the user's compilation.

Examples:

- marker attributes;
- fixed helper attributes;
- static support types.

Example:

```csharp
context.RegisterPostInitializationOutput(
    static context =>
    {
        context.AddSource(
            "GenerateAttribute.g.cs",
            SourceText.From(
                """
                // <auto-generated/>

                namespace MyLibrary;

                [global::System.AttributeUsage(
                    global::System.AttributeTargets.Class,
                    AllowMultiple = false,
                    Inherited = false)]
                internal sealed class GenerateAttribute
                    : global::System.Attribute
                {
                }
                """,
                Encoding.UTF8
            )
        );
    }
);
```

---

# 13. Testing Incrementally

Snapshot-testing generated source is not sufficient.

A generator can generate perfectly correct code while defeating almost all incremental caching.

Test both:

```text
Correctness
+
Incrementally
```

---

## Test Cases

At minimum test:

- first execution produces expected output;
- identical second execution is cached;
- unrelated source changes remain cached;
- changing one target only invalidates that target;
- changing one property only invalidates dependent stages;
- deleting a target removes its output;
- renaming a target changes the expected hint/source;
- changing global generator options invalidates appropriate output;
- changing an additional file invalidates only dependent output;
- global registry generation invalidates when expected.

---

## Track Incremental Generator Steps

Create the generator driver with tracking enabled.

For example:

```csharp
var driverOptions =
    new GeneratorDriverOptions(
        disabledOutputs:
            IncrementalGeneratorOutputKind.None,

        trackIncrementalGeneratorSteps:
            true
    );
```

Inspect tracked output reasons such as:

```text
New
Modified
Unchanged
Cached
Removed
```

The exact reason expected depends on the stage and test scenario.

The important point is that tests should prove:

> An unrelated edit does not rerun expensive downstream generation.

---

# 14. Roslyn Version Compatibility

The most important packaging rule is:

> **The version of `Microsoft.CodeAnalysis.*` used to compile your analyser/generator establishes a minimum compiler-host API requirement.**

The consumer's:

```xml
<TargetFramework>...</TargetFramework>
```

does not determine analyser compatibility.

Analyser/generator code executes inside a compiler/IDE host.

---

## Roslyn / Visual Studio Compatibility

Microsoft's published compatibility baseline is:

| Roslyn package | Minimum Visual Studio | Language / .NET generation |
| ---: | --- | --- |
| 4.0.1 | VS 2022 17.0 | C# 10 / .NET 6 |
| 4.1 | VS 2022 17.1 | C# 10 / .NET 6 |
| 4.2 | VS 2022 17.2 | C# 10 / .NET 6 |
| 4.3.1 | VS 2022 17.3 | C# 10 / .NET 6 |
| 4.4 | VS 2022 17.4 | C# 11 / .NET 7 |
| 4.5 | VS 2022 17.5 | C# 11 / .NET 7 |
| 4.6 | VS 2022 17.6 | C# 11 / .NET 7 |
| 4.7 | VS 2022 17.7 | C# 11 / .NET 7 |
| 4.8 | VS 2022 17.8 | C# 12 / .NET 8 |
| 4.9.2 | VS 2022 17.9 | C# 12 / .NET 8 |
| 4.10 | VS 2022 17.10 | C# 12 / .NET 8 |
| 4.11 | VS 2022 17.11 | C# 12 / .NET 8 |
| 4.12 | VS 2022 17.12 | C# 13 / .NET 9 |
| 4.13 | VS 2022 17.13 | C# 13 / .NET 9 |
| 4.14 | VS 2022 17.14 | C# 13 / .NET 9 |
| 5.0 | VS 2026 18.0 | C# 14 / .NET 10 |

This table gives the **minimum documented Visual Studio host**.

> **This framework is built against Roslyn 5.0.** The generator, analyzer, and testing assemblies in
> `Purview.SourceGeneratorFramework*` are compiled against `Microsoft.CodeAnalysis` 5.x, so compiler
> hosts that load them must be Roslyn 5.0 or later (`.NET 10` SDK / Visual Studio 2026 18.0). The
> testing packages multi-target `net8.0`–`net10.0`; Roslyn 5.x ships `net8.0`/`net9.0` package assets,
> so those test targets still load the test runner.

Do not interpret it as:

```text
net8.0 application = Roslyn 4.8 analyser
```

That is incorrect.

---

## Example

A project may target:

```xml
<TargetFramework>net8.0</TargetFramework>
```

while being compiled by:

```text
Visual Studio 2026 / Roslyn 5.x
```

An analyser compiled against Roslyn 5.0 may therefore work.

The same `net8.0` project opened in:

```text
Visual Studio 2022 17.8 / Roslyn 4.8
```

cannot be assumed to load that Roslyn-5.0-based analyser.

The application TFM did not change.

The compiler host did.

---

# 15. Visual Studio, .NET SDK, and Rider

## Safe Roslyn Baselines

Choose the oldest Roslyn version containing the APIs you require.

Typical baseline choices are:

| Minimum tooling you intend to support | Maximum baseline you should normally compile against |
| --- | ---: |
| VS 2022 17.8 / initial .NET 8 generation | Roslyn 4.8 |
| VS 2022 17.10 | Roslyn 4.10 |
| VS 2022 17.12 / initial .NET 9 generation | Roslyn 4.12 |
| VS 2022 17.14 | Roslyn 4.14 |
| VS 2026 18.0 / initial .NET 10 generation | Roslyn 5.0 |

If you compile against a later package, you have deliberately raised your minimum host requirement unless you have proven otherwise.

---

## .NET SDK

The .NET SDK contains a compiler toolchain.

Broad release alignment is:

```text
.NET 8  / C# 12 ──► Roslyn 4.8 generation
.NET 9  / C# 13 ──► Roslyn 4.12 generation
.NET 10 / C# 14 ──► Roslyn 5.0 generation
```

However, SDK servicing and feature bands can contain later compiler versions.

Therefore do not use:

```text
TargetFramework == net10.0
```

as proof that a particular Roslyn API is available to your analyser.

Likewise:

```xml
$(TargetFramework)
```

should not be used to choose the analyser binary.

The relevant variable is the compiler host.

---

## Rider

Rider supports:

- Roslyn analysers;
- source generators;
- generated-source navigation;
- analyser diagnostics;
- analyser quick fixes;
- source generator execution.

However, JetBrains does not publish the same simple:

```text
Rider Version => Maximum Microsoft.CodeAnalysis Version
```

matrix that Microsoft publishes for Visual Studio.

Therefore:

> **Do not invent a Rider/Roslyn version mapping.**

If Rider support is part of your package contract:

1. choose a conservative Roslyn baseline;
2. test the oldest Rider version you support;
3. test `dotnet build`;
4. test Rider design-time generation;
5. test generated-source navigation;
6. test analysers and code fixes where applicable.

Build-time compiler compatibility and Rider IDE integration should be tested independently.

---

# 16. Multi-Version Roslyn Packaging

This area is frequently misunderstood.

## NuGet Analyser Assets Are Not Normal TFM Assets

Normal runtime/library assets support selection such as:

```text
lib/net8.0/
lib/net9.0/
lib/net10.0/
```

Analyser assets conventionally live under:

```text
analysers/
    dotnet/
        cs/
            MyGenerator.dll
```

This is not a general-purpose:

```text
Roslyn 4.8
Roslyn 4.14
Roslyn 5.0
```

selection mechanism.

Do not place multiple Roslyn-targeted implementations into the ordinary analyser folder and expect NuGet to automatically choose the correct one.

---

## Strategy 1 — One Conservative Binary

### Recommended Default

Compile against the oldest Roslyn version required by your implementation.

For example:

```xml
<PackageReference
    Include="Microsoft.CodeAnalysis.CSharp"
    Version="4.8.0"
    PrivateAssets="all" />
```

Package:

```text
analysers/
    dotnet/
        cs/
            MyGenerator.dll
```

Advantages:

- simplest;
- predictable;
- broadest compatibility;
- works naturally with IDEs;
- minimal packaging logic.

Disadvantage:

- cannot statically call newer Roslyn APIs.

For most public generators, this is the correct approach.

---

## Strategy 2 — Raise the Package Baseline

If a newer Roslyn feature materially improves the generator, it may be better to explicitly raise the minimum compiler version.

For example:

```text
MyGenerator 2.x
    Roslyn >= 4.8

MyGenerator 3.x
    Roslyn >= 5.0
```

Document the minimum IDE/compiler requirement.

This is much easier for users to reason about than hidden runtime selection.

---

## Strategy 3 — Separate Packages

For significantly different implementations:

```text
MyGenerator
MyGenerator.Roslyn5
```

can be reasonable.

Advantages:

- explicit;
- predictable;
- simple runtime behaviour.

Disadvantages:

- more packages;
- more maintenance;
- users must select correctly.

---

## Strategy 4 — MSBuild-Selected Binary

Advanced packages can store binaries outside the automatically discovered analyser directory:

```text
analysers/
    roslyn4.8/
        MyGenerator.dll

    roslyn5.0/
        MyGenerator.dll

buildTransitive/
    MyGenerator.targets
```

Then a targets file can explicitly add exactly one:

```xml
<Analyser Include="..." />
```

depending on an intentionally selected compatibility band.

Conceptually:

```xml
<ItemGroup Condition="'$(MyGeneratorRoslynBand)' == '4.8'">
    <Analyser
        Include="$(MSBuildThisFileDirectory)..\analysers\roslyn4.8\MyGenerator.dll" />
</ItemGroup>

<ItemGroup Condition="'$(MyGeneratorRoslynBand)' == '5.0'">
    <Analyser
        Include="$(MSBuildThisFileDirectory)..\analysers\roslyn5.0\MyGenerator.dll" />
</ItemGroup>
```

The difficult question is:

> How is `MyGeneratorRoslynBand` determined reliably?

There is no general NuGet analyser-asset negotiation equivalent to normal TFM selection.

Do **not** use:

```xml
$(TargetFramework)
```

for this.

It identifies the application runtime target, not the compiler host.

Using:

```xml
$(NETCoreSdkVersion)
```

may work for a deliberately SDK-bound support model but must not be treated as universally equivalent to the active Roslyn host.

Design-time builds, Visual Studio, Rider, CI, and explicit compiler toolsets all need testing.

---

## Multi-Targeting the Generator Is Not Automatic Selection

This:

```xml
<TargetFrameworks>
    netstandard2.0;net8.0
</TargetFrameworks>
```

may produce two generator assemblies.

It does **not** mean NuGet will choose:

```text
netstandard2.0 analyser for old compiler
net8.0 analyser for new compiler
```

for you.

Building multiple binaries and selecting analyser assets are separate problems.

---

## Recommended Rule

Unless there is a compelling requirement:

> **Ship one `netstandard2.0` analyser/generator binary compiled against the oldest Roslyn API version you need.**

This remains the most robust distribution strategy.

---

# 17. `Microsoft.CodeAnalysis.Analysers`

Do not confuse:

```text
Microsoft.CodeAnalysis.CSharp
```

with:

```text
Microsoft.CodeAnalysis.Analysers
```

They serve different purposes.

---

## `Microsoft.CodeAnalysis.CSharp`

Provides Roslyn compiler APIs used to implement your analyser/generator.

Examples:

```csharp
IIncrementalGenerator
DiagnosticAnalyser
SyntaxNode
Compilation
ISymbol
IOperation
```

---

## `Microsoft.CodeAnalysis.Analysers`

This is a **meta-analyser package**.

It analyses your analyser or source generator.

Its purpose is to detect incorrect or unsafe usage of Roslyn/compiler APIs.

It does not define your source-generator API baseline.

---

## Current Package Version

As of August 2026, the current stable package is:

```text
Microsoft.CodeAnalysis.Analysers 5.9.0
```

Do not assume its version must match:

```text
Microsoft.CodeAnalysis.CSharp
```

For example, it is perfectly reasonable to have:

```xml
<PackageReference
    Include="Microsoft.CodeAnalysis.CSharp"
    Version="4.8.0"
    PrivateAssets="all" />

<PackageReference
    Include="Microsoft.CodeAnalysis.Analysers"
    Version="5.9.0"
    PrivateAssets="all" />
```

provided the meta-analyser version itself works with your build tooling.

These represent separate concerns:

```text
Microsoft.CodeAnalysis.CSharp
        │
        └── minimum Roslyn API used by your generator

Microsoft.CodeAnalysis.Analysers
        │
        └── rules used while developing the generator
```

---

## Transitive Availability

Roslyn compiler packages already bring `Microsoft.CodeAnalysis.Analysers` into the dependency graph as development tooling.

You may nevertheless explicitly reference it when:

- using Central Package Management;
- deliberately pinning meta-analyser behaviour;
- keeping analyser tooling versions consistent across a repository;
- making the analyser-project configuration obvious.

---

## `PrivateAssets="all"`

Roslyn development dependencies should normally use:

```xml
PrivateAssets="all"
```

Example:

```xml
<PackageReference
    Include="Microsoft.CodeAnalysis.CSharp"
    Version="$(RoslynVersion)"
    PrivateAssets="all" />

<PackageReference
    Include="Microsoft.CodeAnalysis.Analysers"
    Version="$(RoslynAnalyserVersion)"
    PrivateAssets="all" />
```

Your consumer should not gain ordinary runtime Roslyn package dependencies simply because it installed your source generator.

---

## `EnforceExtendedAnalyserRules`

Analyser and generator projects should normally enable:

```xml
<EnforceExtendedAnalyserRules>
    true
</EnforceExtendedAnalyserRules>
```

These rules detect implementation patterns that are particularly dangerous inside compiler-hosted code.

Do not immediately suppress an `RSxxxx` diagnostic.

First determine what compiler-host invariant the rule is protecting.

---

## RS1035

`RS1035` bans APIs considered inappropriate for analysers.

A common example is direct access to environment-dependent state.

The underlying principle is:

> Analyser/generator execution should not silently depend on machine-global environment state.

Configuration should normally arrive through explicit compiler inputs such as:

- analyser config options;
- additional files;
- MSBuild properties exposed through analyser config;
- source code;
- metadata references.

---

## RS2008

`RS2008` relates to analyser diagnostic release tracking.

If your analyser publishes public diagnostic IDs, maintain release tracking files such as:

```text
AnalyserReleases.Shipped.md
AnalyserReleases.Unshipped.md
```

This helps detect accidental changes to diagnostic contracts.

Diagnostic IDs are effectively part of your public API.

---

## Treat Diagnostic Descriptors as Public Contracts

Changing:

```text
ZS0001
```

to:

```text
ZS0017
```

may break:

- `.editorconfig`;
- suppressions;
- CI configuration;
- documentation;
- consumer tooling.

Similarly, changing:

- default severity;
- category;
- diagnostic semantics;

should be treated as a compatibility decision.

---

# 18. Recommended Project Configuration

A broadly-compatible generator project might start with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>

    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>

    <IsPackable>true</IsPackable>
    <IncludeBuildOutput>false</IncludeBuildOutput>

    <EnforceExtendedAnalyserRules>true</EnforceExtendedAnalyserRules>

    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>

    <PackageReference
        Include="Microsoft.CodeAnalysis.CSharp"
        Version="$(RoslynVersion)"
        PrivateAssets="all" />

    <PackageReference
        Include="Microsoft.CodeAnalysis.Analysers"
        Version="$(RoslynAnalyserVersion)"
        PrivateAssets="all" />

  </ItemGroup>

  <ItemGroup>

    <None
        Include="$(TargetPath)"
        Pack="true"
        PackagePath="analysers/dotnet/cs"
        Visible="false" />

  </ItemGroup>

</Project>
```

Then centrally define:

```xml
<PropertyGroup>
  <RoslynVersion>4.8.0</RoslynVersion>
  <RoslynAnalyserVersion>5.9.0</RoslynAnalyserVersion>
</PropertyGroup>
```

The exact Roslyn baseline is a product-support decision.

---

## ProjectReference During Development

A consuming project can reference the generator as:

```xml
<ProjectReference
    Include="..\MyGenerator\MyGenerator.csproj"
    OutputItemType="Analyser"
    ReferenceOutputAssembly="false" />
```

---

## Separate Runtime Contracts From Compiler Tooling

Prefer:

```text
MyLibrary.Abstractions
    │
    ├── public attributes
    ├── runtime contracts
    └── shared public APIs

MyLibrary.SourceGenerators
    │
    └── IIncrementalGenerator

MyLibrary.Analysers
    │
    └── DiagnosticAnalyser

MyLibrary.CodeFixes
    │
    └── CodeFixProvider
```

over mixing runtime APIs and compiler tooling into one assembly.

This prevents Roslyn dependencies leaking into runtime package assets.

---

## Roslyn Component Discovery

The compiler host only loads a source generator, diagnostic analyser, or code fix provider when
three conditions hold. Missing any one means the component is **silently ignored**, which is why
"nothing shows up in Visual Studio" is usually a setup problem, not a code problem:

1. **The type is public.** Non-public component types cannot be instantiated by Roslyn
   (`PSGFR27`).
2. **The type is decorated.** A generator needs `[Generator]` (`PSGFR26`), an analyser needs
   `[DiagnosticAnalyzer]` (`PSGFR25`), and a code fix provider needs `[ExportCodeFixProvider]`
   (`PSGFR24`).
3. **The assembly is loaded as an analyser.** In a package the component assembly must be packed
   under `analyzers/dotnet/cs/`; in a project reference it must be referenced with
   `OutputItemType="Analyser"`. A normal library reference never surfaces a component to Roslyn.

A code fix provider also only appears when the diagnostic ID in `FixableDiagnosticIds` is actually
produced by an analyser that is loaded alongside it (`PSGFR28`). Visual Studio MEF-composes fix
providers when the analyser set loads, so after adding or updating a fixer assembly you must
restart Visual Studio or reload the project for the fixes to appear.

---

# 19. Review Checklist

## Analyser

- [ ] Is this rule actually validation rather than generation?
- [ ] Am I using the narrowest appropriate analyser action?
- [ ] Does exact syntax matter?
- [ ] If not, should this use a symbol?
- [ ] If this is executable semantics, should this use `IOperation`?
- [ ] Is `EnableConcurrentExecution()` enabled?
- [ ] Is generated-code analysis explicitly configured?
- [ ] Are known framework/library symbols resolved once where appropriate?
- [ ] Are symbols compared semantically rather than by reference?
- [ ] Is whole-compilation analysis genuinely necessary?
- [ ] Could the diagnostic reasonably have a code fix?
- [ ] Are diagnostic IDs release-tracked?
- [ ] Is the analyser type `public` and decorated with `[DiagnosticAnalyzer]`?
- [ ] Do the code fix's `FixableDiagnosticIds` match an ID the analyser actually produces?

---

## Incremental Generator

- [ ] Uses `IIncrementalGenerator`.
- [ ] Uses `ForAttributeWithMetadataName` where appropriate.
- [ ] Syntax predicates are extremely cheap.
- [ ] Semantic extraction happens once.
- [ ] `ISymbol` never enters persistent model state.
- [ ] `Compilation` does not propagate downstream.
- [ ] `SemanticModel` does not propagate downstream.
- [ ] `IOperation` does not propagate downstream.
- [ ] `SyntaxTree` does not propagate downstream.
- [ ] `SyntaxNode` is removed as early as possible.
- [ ] `Location` is removed as early as possible.
- [ ] Pipeline models are immutable.
- [ ] Pipeline models have value equality.
- [ ] Collection members have sequence equality.
- [ ] Arrays are not relied upon for model equality.
- [ ] `ImmutableArray<T>` is wrapped or explicitly compared where equality matters.
- [ ] Transform callbacks are static where practical.
- [ ] Cancellation is honoured.
- [ ] `Collect()` is only used where global knowledge is necessary.
- [ ] Per-target output remains per-target.
- [ ] `Combine()` does not unnecessarily broaden invalidation.
- [ ] `CompilationProvider` is not casually combined into output.
- [ ] `WithComparer()` represents real logical equality.
- [ ] Hint names are deterministic.
- [ ] Generated text is deterministic.
- [ ] Constant source uses post-initialization output.
- [ ] Normal source validation lives in an analyser.
- [ ] Incremental caching behaviour has tests.

---

## Packaging

- [ ] Generator/analyser binaries are packed as analyser assets.
- [ ] Compiler tooling is not accidentally shipped as runtime `lib` output.
- [ ] Roslyn package dependencies are private.
- [ ] The Roslyn API baseline is intentional.
- [ ] The minimum supported Visual Studio version is documented.
- [ ] The minimum supported SDK/compiler environment is tested.
- [ ] Rider support is tested rather than inferred.
- [ ] Multi-targeting is not being mistaken for analyser asset selection.
- [ ] Multiple Roslyn binaries are not placed in the normal analyser folder expecting automatic selection.
- [ ] Any custom MSBuild analyser selection works during design-time builds.
- [ ] `Microsoft.CodeAnalysis.Analysers` is enabled.
- [ ] `EnforceExtendedAnalyserRules` is enabled.
- [ ] `RSxxxx` diagnostics are investigated rather than reflexively suppressed.

---

# Summary

The shortest version of this guide is:

> **Analyser for validation; generator for generation.**
> **Syntax for syntax, symbols for declarations, operations for executable semantics.**
> **Use `ForAttributeWithMetadataName` whenever possible.**
> **`ISymbol`, `Compilation`, `SemanticModel`, and `IOperation` do not belong in incremental pipeline models.**
> **Remove `SyntaxNode` and `Location` as soon as possible.**
> **Immutable does not mean equatable: arrays, lists, and `ImmutableArray<T>` require deliberate sequence equality.**
> **Use `EquatableArray<T>` or an equivalent value-equatable collection abstraction.**
> **Avoid `Collect()` until global knowledge is genuinely required.**
> **Never combine `CompilationProvider` into the pipeline merely because it is convenient.**
> **Compile against the oldest Roslyn API version containing the functionality you need.**
> **The consumer TFM does not determine analyser compatibility—the compiler host does.**
> **NuGet does not automatically choose between Roslyn-version-specific analyser binaries.**
> **Test incrementally and compatibility, not just generated source.**
