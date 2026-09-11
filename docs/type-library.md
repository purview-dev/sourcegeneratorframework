# TypeLibraryGenerator

`TypeLibraryGenerator` removes the boilerplate of hand-writing a type library — the static class that
exposes the `TypeIdentity` and `TypeReference` values your generator needs to reference framework,
reference, and self-generated types. It is part of `Purview.SourceGeneratorFramework.Generators` and runs
automatically for any spec annotated with `[GenerateTypeLibrary]`.

## What it generates

For a small declarative spec, the generator emits a **self-contained** `public static partial class` (the
generated type library) whose nested `public static partial` classes mirror the namespaces of the declared
members. Every class — the root and each nested namespace class — exposes a `public const string Namespace`
and the leaf classes expose the members as `public static readonly` fields:

```csharp
namespace MyGenerator;

public static partial class TypeLibrary
{
    public const string Namespace = "MyGenerator";

    public static partial class System
    {
        public const string Namespace = "System";
        public static partial class Diagnostics
        {
            public const string Namespace = "System.Diagnostics";
            public static readonly TypeIdentity Activity = new("Activity", "System.Diagnostics");
        }
    }
}
```

No `extension(...)` blocks are emitted. The generated types are `public static partial` so you can expand
them with your own methods in a separate partial file. The framework's own library is
`PurviewTypeLibrary` (the two never collide, and composed members reference it directly).

The generated class **inherits the full `PurviewTypeLibrary` shape**: every nested namespace class and
member of the framework library (`System.String`, `System.Collections.Generic.List`,
`Microsoft.Extensions.DependencyInjection.IServiceCollection`, …) is present, emitted as an alias
reference to the framework value so arity and generic construction are preserved exactly. Your
`[TypeRef]` members are merged into the matching nested classes; a member with the same name as an
inherited member in the same nested class shadows it.

## The DSL

```csharp
namespace Purview.Telemetry.SourceGenerator;

[GenerateTypeLibrary(
    ClassName = "TelemetryTypeLibrary",       // generated type name (default: "TypeLibrary")
    Namespace = "Purview.Telemetry.SourceGenerator")] // generated type's namespace (default: global)
static partial class TypeLibraryModel          // spec — separate from the generated type
{
    // Namespace-only: type name defaults to the member name → nested class .Purview.Telemetry:
    [TypeRef("Purview.Telemetry")]
    static readonly TypeIdentity ActivitySourceGenerationAttribute = default;

    // typeof(...) form → nested class .System.Diagnostics:
    [TypeRef(typeof(global::System.Diagnostics.Activity))]
    static readonly TypeIdentity Activity = default;

    // Explicit type + namespace → nested class .Microsoft.Extensions.Logging:
    [TypeRef("ILogger", "Microsoft.Extensions.Logging")]
    static readonly TypeIdentity ILogger = default;
}
```

The spec class must be declared `static partial` and should use a distinct name from the generated
class (`ClassName`, default `TypeLibrary`) — `TLB0012`/`TLB0013` flag a collision, with a code fix
that renames the spec (e.g. `TypeLibrary` → `TypeLibraryGenerator`).

### Marker attributes

| Attribute | Targets | Purpose |
| --- | --- | --- |
| `[GenerateTypeLibrary]` | class | Marks the spec; configures `ClassName`, `Namespace`. |
| `[TypeRef]` | field | Declares one `TypeIdentity` or `TypeReference` member. |

`[TypeRef]` offers three declaration forms:

| Form | Type name | Namespace |
| --- | --- | --- |
| `[TypeRef("Purview.Telemetry")]` | the member name | the string argument |
| `[TypeRef(typeof(Activity))]` | the symbol name | inferred from the type (or the named `Namespace`/positional argument) |
| `[TypeRef("Activity", "System.Diagnostics")]` | the name string | the second argument |

All forms accept an optional generic `arity` argument (`[TypeRef("Test", 1)]`, or the third argument of
the explicit form, e.g. `[TypeRef("List`1", "System.Collections.Generic")]`); `typeof(...)` derives the
arity from the symbol automatically.

Every form also accepts an optional `includeInGetTypes` argument that follows `arity` —
`[TypeRef("Test", 0, true)]` or `[TypeRef("ILogger", "Microsoft.Extensions.Logging", -1, true)]` — or as
the named argument `includeInGetTypes: true`. It controls whether the member is included in the
namespace's generated `GetTypes()` call (see below).

### Member accessibility

Members are inert declarations read by the generator at compile time:

- **Plain `TypeIdentity` members are markers** and must be declared `private` (an unmodified
  `static readonly` field is private). The generator produces `new("Name", "Namespace"[, arity])`.
- **`TypeReference` members, and `TypeIdentity` members that declare an initializer** (value members),
  must be declared `internal`; their initializer expression becomes the generated value.

The analyzer reports `TLB0008` for invalid accessibility and `TLB0009` when a value member has no
initializer; both are fixable (`Make private`/`Make internal` for `TLB0008`). Marker members without an
explicit `= default` initializer are flagged by `TLB0010` (with an `Add '= default'` fix). The generator
also emits a small partial of the spec class that references the marker fields, so the compiler's
unused-member analysis does not flag them — the spec must therefore be declared `partial`
(`TLB0011`, with a `Make partial` fix).

### Value members (composed references)

A `TypeReference` field — or a `TypeIdentity` field with a real initializer — declares a composed value
(generic constructions with arguments, arrays, nullable). The initializer may reference other
`[TypeRef]` members by name and the framework `PurviewTypeLibrary`:

```csharp
[TypeRef(typeof(ActivityLink))]
static readonly TypeIdentity ActivityLink = default;

[TypeRef("System.Diagnostics")]
internal static readonly TypeReference ActivityLinkArray = new TypeReference(ActivityLink).MakeArray();

[TypeRef("System.Collections.Generic")]
internal static readonly TypeReference ActivityTagIEnumerable =
    global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.Collections.Generic.IEnumerable.MakeGeneric(
        global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.String);
```

The generated nested class then exposes
`TypeLibrary.System.Diagnostics.ActivityLinkArray` and
`TypeLibrary.System.Collections.Generic.ActivityTagIEnumerable` as `TypeReference` fields. Use the
fully-qualified `PurviewTypeLibrary.System...` form in initializers so the spec compiles regardless of
local `TypeLibrary` names.

### Including types in `GetTypes()`

Mark a member with `includeInGetTypes: true` to include it in its namespace's generated `GetTypes()`
method, which returns an `ImmutableArray<TypeReference>` of the included members:

```csharp
[TypeRef("ILogger", "Microsoft.Extensions.Logging", includeInGetTypes: true)]
static readonly TypeIdentity ILogger = default;
```

The nested class then exposes:

```csharp
public static ImmutableArray<TypeReference> GetTypes() => [ ILogger ];
```

Plain `TypeIdentity` members and value members both participate. A namespace with no included members
emits no `GetTypes()` method.

### XML documentation

XML documentation on the spec class and each `[TypeRef]` field is copied onto the corresponding
generated class and member.

## Requirements

The generated output uses C# 14 features (nested partial types, collection expressions), so consumers
must compile with a C# 14 compiler (.NET SDK 10 / Roslyn 5.0 or later).

## Disabling

Set the MSBuild property `DisablePurviewTypeLibraryGenerator` to `true` to disable the generator.

## Validation

`TypeLibraryValidationAnalyzer` reports `TLB0001`–`TLB0013` for invalid specs
(non-static class, member type that is not `TypeIdentity`/`TypeReference`, unresolvable type/namespace,
duplicate members, invalid class name, invalid namespace, invalid member accessibility, value members
without an initializer, marker members without an explicit `= default`, a spec that is not declared
`partial`, and a spec class whose name collides with the generated type library class — `TLB0012` when
they share a namespace, `TLB0013` when they do not). `TLB0002`, `TLB0008`, `TLB0010`, `TLB0011`,
`TLB0012`, and `TLB0013` have code fixes.

The generator carries these same diagnostics on its `GeneratorResult` and gates generation on
`ShouldProcess`. Most are blocking (`IsBlocking: true`) and stop generation, but the non-blocking rules —
`TLB0010` (marker without `= default`) and `TLB0013` (warning) — allow generation to continue, so a spec
with those issues still produces the type library. See
[`GeneratorResult` diagnostics that don't stop generation](../src/src/SourceGeneratorFramework/Sdk/README.md#diagnostics-that-dont-stop-generation).