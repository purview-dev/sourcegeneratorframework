# CodeWriter

`CodeWriter` is the structured, allocation-conscious writer used to build generated C# source. Instead
of concatenating strings or writing raw text, generators describe *what* to emit — declarations,
statements, scopes — and the writer handles indentation, blank-line separation, generated attributes,
and deterministic layout.

This page uses the current best-practice API: bare semantic names (`Class`, `Method`, `Property`), the
minimal-parameter overloads with an optional `configure` callback, and structured statements
(`Return`, `MethodCall`, `Assignment`) instead of raw text.

## Primitives

Use the raw primitives for low-level text that has no structured equivalent:

```csharp
writer.Write("partial");              // no trailing line feed
writer.Line("// generated");          // line feed appended
writer.Append("text");                // Write alias
writer.AppendLine("text");            // Line alias
writer.Comment("Explains the next member.");
writer.Indent();                      // increase indentation
writer.NewLine();
```

`Write`/`Line`/`Append`/`AppendLine` are the only methods that retain a verb prefix: everything
semantic drops it because the receiver is already a writer.

## Declarations

Each declaration writer has:

- a **minimal overload** taking name/type/accessibility plus an optional `configure` callback
  (`options => options with { ... }`); and
- a **scope form** (`...Scope`) returning a `BlockScope` for `using` when you need fine-grained control.

Type declarations can also be written **without a body**, terminated with a semicolon instead of an empty
block — useful for marker types, primary-constructor records, and host-kit stubs:

```csharp
// public sealed partial class TestingHostKit;
writer.Class(
    new TypeDeclarationOptions("TestingHostKit", TypeDeclarationAccessibility.Public)
    {
        IsPartial = true,
        Attributes = [new(HostKitAttribute) { Arguments = [new(true, "GenerateOptions", true)] }],
    }
);

// public record class Point(int X, int Y);
writer.RecordClass(
    new TypeDeclarationOptions("Point") { PrimaryConstructorParameters = [new("X", intType), new("Y", intType)] }
);

// writer.Class("C");  // public sealed partial class C;
```

The semicolon-terminated form is valid for `Class`, `Struct`, `RecordClass`, `RecordStruct`, and `Interface`
(primary-constructor parameters, base types, and `where` constraints are still written). Enums and delegates
always require a body / self-terminate.

```csharp
writer.Class(
    "OrderService",
    TypeDeclarationAccessibility.Public,
    options => options with { IsSealed = true, IsPartial = false },
    body =>
    {
        body.Field("_total", TypeIdentity.Create<decimal>().AsTypeReference(), TypeDeclarationAccessibility.Private);

        body.Constructor(
            "OrderService",
            TypeDeclarationAccessibility.Public,
            options => options with
            {
                Parameters = [new("total", TypeIdentity.Create<decimal>().AsTypeReference())],
            },
            constructorBody => constructorBody.Assignment("_total", "total")
        );

        body.Property(
            "Total",
            TypeIdentity.Create<decimal>().AsTypeReference(),
            TypeDeclarationAccessibility.Public
        );
    }
);
```

The same pattern applies to `Struct`, `RecordClass`, `RecordStruct`, `Interface`, `Enum` (+ `EnumField`),
`Type` (kind-driven), `Delegate`, `AttributeClass`, `Method`/`PartialMethod`/`MethodExpression`,
`Property`/`PropertyExpression`, `Indexer`, `Field`, and `Operator`.

### Scope forms

```csharp
using (writer.ClassScope("OrderService", TypeDeclarationAccessibility.Public))
using (writer.MethodScope("Apply", TypeLibrary.System.Void, TypeDeclarationAccessibility.Public))
{
    writer.MethodCall("Validate");
}
```

Scope forms are ideal when a declaration spans multiple calls, loops, or conditional content. The
`using` statement is mandatory — the closing token and indentation are written on dispose, and the
`DiscardedCodeWriterScopeAnalyzer` (PSGFR17) flags scope returns that are dropped.

### C# 14 extension-member blocks

`ExtensionBlockScope`/`ExtensionBlock` emit C# 14 `extension(...)` blocks (Roslyn 5.0 or later), for
generators that need to attach members to a receiver type (note that extension members compile to static
accessor methods such as `get_X`, not CLR properties):

```csharp
using (writer.ExtensionBlockScope(
    new TypeIdentity("PurviewTypeLibrary", "Purview.SourceGeneratorFramework")
        .Nested("System")
        .Nested("Diagnostics")
        .AsTypeReference()))
{
    writer.Property(
        "Activity",
        TypeReference.Create<TypeIdentity>(),
        TypeDeclarationAccessibility.Public,
        options => options with { IsStatic = true, ExpressionBody = "Activity" });
}

// extension(global::Purview.SourceGeneratorFramework.PurviewTypeLibrary.System.Diagnostics)
// {
//     public static global::Purview.SourceGeneratorFramework.TypeIdentity Activity => Activity;
// }
```

The receiver must be a plain named type; composed references (arrays, pointers, nullable annotations,
type parameters and `dynamic`) and the null literal are rejected. Use the callback form
`writer.ExtensionBlock(receiver, body => ...)` for a complete block in one call.

### Enums

Generated enums are emitted with the `[Embedded]` marker attribute by default — the same default as
`AttributeClass` — so the enum is embedded into each consuming assembly rather than leaking as a
reference to the generator's type surface. Opt a specific enum out with
`IncludeEmbeddedAttribute = false` in the `configure` callback:

```csharp
writer.Enum("ServiceLifetime", TypeDeclarationAccessibility.Public,
    options => options with { IncludeEmbeddedAttribute = false });
```

Fields are separated by a blank line, matching the spacing applied to other members, so XML summaries
and attributes stay readable:

```csharp
writer.Enum("Status", TypeDeclarationAccessibility.Public,
    fields:
    [
        new("None", 0),
        new("Ready", 1) { XmlSummary = ["The service is ready."] },
    ]);

// [global::Microsoft.CodeAnalysis.Embedded]
// [global::System.Runtime.CompilerServices.CompilerGenerated]
// [global::System.CodeDom.Compiler.GeneratedCode("TestGenerator", "1.0.0")]
// public enum Status
// {
//     None = 0,
//
//     /// <summary>The service is ready.</summary>
//     Ready = 1,
// }
```

## Statements

Emit executable statements through the structured statement methods rather than raw `Line`:

```csharp
writer.MethodCall("Process", "item");                       // Process(item);
writer.AwaitedMethodCall("SaveAsync", "cancellationToken"); // await SaveAsync(cancellationToken);
writer.MethodCallOn("variable", "Process", "item");         // variable.Process(item);
writer.AwaitedMethodCallOn("service", "LoadAsync", "token"); // await service.LoadAsync(token);
writer.Return("value");                                     // return value;
writer.Throw(TypeIdentity.Create<InvalidOperationException>(), "Failed.");  // throw new ...;
writer.Assignment("_total", "value");                       // _total = value;
writer.Assignment("var hostKit", expression => expression.New("HostKit", "onBuilt")); // var hostKit = new HostKit(onBuilt);
writer.IfBlock("value is null", body => body.Return("null"));
writer.IfBlock("value is null", body => body.Return("null"))
    .ElseIf("value is 0", body => body.Return("zero"))
    .Else(body => body.Return("value"));
writer.Foreach("var item in items", body => body.MethodCallOn("item", "Process"));
```

`MethodCall`/`AwaitedMethodCall` write a call without a receiver — `Process(item);` or
`await SaveAsync(token);`. Use `MethodCallOn`/`AwaitedMethodCallOn` (or the `receiver` parameter on the
`IEnumerable` overloads) for a call on a variable, including generic arguments:

```csharp
writer.MethodCall("Create", ["x"], receiver: "factory", genericArguments: [TypeReference.Create<string>()]);
// factory.Create<string>(x);
```

When a statement or declaration must embed a runtime or user-supplied string — for example a
regular-expression pattern or error message — emit it through the `StringLiteral()` extension rather
than wrapping it in quotes by hand. It returns a quoted, escaped C# string literal:

```csharp
body.Field("regex", regexType, TypeDeclarationAccessibility.Private,
    options => options with { IsStatic = true, Initializer = $"new({pattern.StringLiteral()})" });
// pattern = ^[\w\-.]+$  =>  new("^[\\w\\-.]+$")
```

A **chained** invocation — where the result of each call is the receiver of the next, and a postfix is
applied to the final result — is expressed with `MethodCallChain`/`AwaitedMethodCallChain`. The chain
is written as an expression (no terminating semicolon), so it composes as the value of an
`Assignment`/`Return` expression callback:

```csharp
writer.Assignment(
    "var hostKitOptions",
    expression => expression.MethodCallChain(
        "builder.Configuration.GetSection",
        [$"{name}.SectionName"],
        chain => chain.Method("Get", genericArguments: [optionsType]).Postfix(" ?? new()")));
// var hostKitOptions = builder.Configuration.GetSection("x.SectionName").Get<Options>() ?? new();
```

- `rootMethod` may include the receiver (e.g. `builder.Configuration.GetSection`); each subsequent
  `.Method(...)` call implicitly uses the previous result as its receiver.
- `genericArguments` provides the `<...>` type arguments for a segment.

A chain that starts on a receiver with generic arguments uses `genericArguments` on the root:

```csharp
writer.Assignment("var optionsBuilder", expression =>
    expression.MethodCallChain(
        "builder.Services.AddOptions",
        [],
        chain => chain.Method("BindConfiguration", ["options.SectionName"]),
        genericArguments: [optionsType]));
// var optionsBuilder = builder.Services.AddOptions<Options>().BindConfiguration("options.SectionName");
```

- `Postfix(expression)` appends a trailing expression such as `?? new()` or `!`.

### Object creation

`New` writes an object-creation expression — `new Type(...)` or a target-typed `new(...)` — without a
trailing semicolon, so it composes as the value of an `Assignment`/`Return` expression callback:

```csharp
writer.Assignment("var hostKit", expression =>
    expression.New("HostKit", "onBuilt", "onConfigured"));
// var hostKit = new HostKit(onBuilt, onConfigured);

writer.Assignment("HostKit hostKit", expression =>
    expression.New(["onBuilt", "onConfigured"]));
// HostKit hostKit = new(onBuilt, onConfigured);
```

`New` accepts a verbatim type name, a `TypeReference`, structured `MethodCallArgumentOptions` (preserving
`ref`/`out`/`in` modifiers and named arguments), or no type at all. Use `expression.New()` for `new()`. The
no-type form emits a target-typed `new(...)` expression, which is valid only where the target type is known
(an assignment to a typed local, field, property, parameter, or a `return` statement).

`ObjectCreationOptions` supports the same no-type construction at statement level via its argument-only
constructor:

```csharp
writer.Assignment(
    context.HostKit.HostKitType,
    "hostKit",
    new ObjectCreationOptions("onBuilt", "onConfigured"));
// HostKitType hostKit = new(onBuilt, onConfigured);
```

A null-conditional receiver — `onBuilt?.Invoke(this, builder);` — is written with the `nullConditional`
argument on the structured `MethodCallOn`/`AwaitedMethodCallOn` overloads, which also accept
`genericArguments`:

```csharp
writer.MethodCallOn("onBuilt", "Invoke", ["this", "builder"], nullConditional: true);
// onBuilt?.Invoke(this, builder);

writer.MethodCallOn("builder.Services", "AddOptions", genericArguments: [optionsType]);
// builder.Services.AddOptions<Options>();
```

### Conditional statements

`IfBlock`/`IfBlockScope` write an `if` block. `ElseIf`/`ElseIfScope` chain an `else if` block after an
`if` or another `else if`, and `Else`/`ElseScope` close the chain with an `else` block. The methods
return the writer, so branches can be chained fluently:

```csharp
writer
    .IfBlock("value is null", body => body.Return("null"))
    .ElseIf("value is 0", body => body.Return("zero"))
    .Else(body => body.Return("value"));
```

Emits:

```csharp
if (value is null)
{
    return null;
}
else if (value is 0)
{
    return zero;
}
else
{
    return value;
}
```

`IfElse(condition, ifBody, elseBody)` is the compact two-branch form. The scope forms
`IfBlockScope`, `ElseIfScope`, and `ElseScope` write the header and return the body scope for
content that spans multiple calls.

### Conditional compilation blocks

`HashDefines`/`HashDefinesScope` write a `#if`/`#endif` block with both directives at **column zero**.
The body keeps the surrounding indentation — file-level directives and their content stay at column
zero, while class members inside the block stay at the same indent as their siblings:

```csharp
using (writer.HashDefinesScope("!EXCLUDE_PURVIEW_TELEMETRY_LOGGING"))
{
    writer.FileScopedNamespace("Example");
    writer.Enum("Mode", TypeDeclarationAccessibility.Public, fields: [new("Default", 0)]);
}

// Equivalent action form:
writer.HashDefines("NET", body => body.Line("// NET only"));
```

Emits:

```csharp
#if !EXCLUDE_PURVIEW_TELEMETRY_LOGGING
namespace Example;
...
#endif
```

At file level these blocks are self-spacing: a blank line is ensured before the `#if` and after the
`#endif`, so directive sections remain separated without explicit `NewLine()` calls.

`HashElse()` writes the `#else` directive at column zero between the two bodies:

```csharp
using (writer.HashDefinesScope("NET48_OR_GREATER || PURVIEW_TELEMETRY_NON_NULLABLE"))
{
    writer.Property("name", TypeIdentity.Create<string>().AsTypeReference(), TypeDeclarationAccessibility.Public,
        options => options with { HasSetter = true, IncludeGeneratedAttributes = false });
    writer.HashElse();
    writer.Property("name", TypeIdentity.Create<string>().MakeNullable(writer), TypeDeclarationAccessibility.Public,
        options => options with { HasSetter = true, IncludeGeneratedAttributes = false });
}
```

Emits:

```csharp
#if NET48_OR_GREATER || PURVIEW_TELEMETRY_NON_NULLABLE
public string name { get; set; }
#else
public string? name { get; set; }
#endif
```

`EmptyScope()` returns a no-op scope so a block can be wrapped only when a guard requires it:

```csharp
using var scope = wrapInExcludeLoggingGuard
    ? writer.EmptyScope()
    : writer.HashDefinesScope("EXCLUDE_PURVIEW_TELEMETRY_LOGGING");
```

### Pragma warning suppression

`PragmaDisable` writes a single `#pragma warning disable` directive at column zero for one or more
warning codes. At file level it is self-spacing (blank lines are ensured around the directive):

```csharp
writer.PragmaDisable("CS8625", "CS0618");
// #pragma warning disable CS8625 CS0618
```

For a scoped disable that restores the warnings when the scope is disposed, use `OpenPragmasScope`:

```csharp
using (writer.OpenPragmasScope("CS0618"))
{
    writer.Line("ObsoleteCall();");
}
// #pragma warning disable CS0618
//     ObsoleteCall();
// #pragma warning restore CS0618
```

The full header pattern — nullable directive, conditional `#nullable enable`, and a disabled warning —
can be expressed entirely through the structured APIs (the file-level directives are self-spacing, so
no explicit `NewLine()` calls are needed):

```csharp
writer.AutoGeneratedHeader(nullableDirective: NullableDirectiveMode.Disable);
writer.HashDefines("!NET48_OR_GREATER && !PURVIEW_TELEMETRY_NON_NULLABLE", hashWriter => hashWriter.Line("#nullable enable"));
writer.PragmaDisable("CS8625");
writer.FileScopedNamespace("Purview.Telemetry");
```

Emits:

```csharp
// <auto-generated />
// This code was generated by ExampleGenerator (version 1.0.0).
// Changes to this file will be lost when the source generator runs again.

#if !NET48_OR_GREATER && !PURVIEW_TELEMETRY_NON_NULLABLE
#nullable enable
#endif

#pragma warning disable CS8625

namespace Purview.Telemetry;
```

The generator version in the header and the `GeneratedCode` attribute comes from the
`GenerationSettings` used to create the writer. When settings are created via
`GenerationSettings.Create<TGenerator>()`, the full assembly informational version is used, so any
pre-release suffix (such as `-alpha`) and build metadata (such as `+commit-hash`) are preserved rather
than being reduced to the numeric assembly version.

### Conditional compilation returns

`NetConditionalReturn` writes a `return` for an interpolated string using the best invariant-culture
API on each target framework, guarded by `#if NET`:

```csharp
writer.Method(
    "Format",
    TypeIdentity.Create<string>().AsTypeReference(),
    TypeDeclarationAccessibility.Public,
    null,
    body => body.NetConditionalReturn("Value: {_value}")
);
```

Emits:

```csharp
#if NET
    return string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $"Value: {_value}");
#else
    return global::System.FormattableString.Invariant($"Value: {_value}");
#endif
```

## Default accessibility

`CodeWriter` applies a default accessibility for each member kind when a declaration does not specify
one. Set the defaults on `GenerationSettings` (to apply across a generation) or on the writer itself
(to override per writer). Each value is `null`-able, so setting a kind back to `null` omits the
modifier entirely.

| Setting | Default |
|---|---|
| `DefaultTypeAccessibility` | `Public` |
| `DefaultPropertyAccessibility` | `Public` |
| `DefaultPropertyGetterAccessibility` | `Public` |
| `DefaultPropertySetterAccessibility` | `Public` |
| `DefaultFieldAccessibility` | `Private` |
| `DefaultMethodAccessibility` | `Public` |
| `DefaultConstructorAccessibility` | `Public` |
| `DefaultIndexerAccessibility` | `Public` |
| `DefaultOperatorAccessibility` | `Public` |

```csharp
var writer = generationContext.CreateCodeWriter();
writer.Field("_total", TypeReference.Create<decimal>()); // private int _total; (DefaultFieldAccessibility)
writer.Property("Total", TypeReference.Create<decimal>()); // public decimal Total { get; }
```

An explicit accessibility always wins over the default:

```csharp
writer.Property("Total", TypeReference.Create<decimal>(), TypeDeclarationAccessibility.Internal);
// internal decimal Total { get; }
```

Accessor (getter/setter) defaults are emitted only when they are **more restrictive** than the
property's own accessibility — C# forbids an accessor modifier that is equal to or more permissive
than the property (CS0273). With the public defaults, a public property keeps bare `{ get; set; }`:

```csharp
writer.DefaultPropertySetterAccessibility = TypeDeclarationAccessibility.Private;
writer.Property("Name", TypeReference.Create<string>(), TypeDeclarationAccessibility.Public,
    options => options with { HasSetter = true });
// public string Name { get; private set; }
```

## Guidance

- Prefer the minimal overloads with a `configure` callback over constructing `*DeclarationOptions`
  values manually — the `PreferMinimalCodeWriterOverloadAnalyzer` (PSGFR20) flags the verbose form.
- Prefer structured declarations and statements over raw text — `PreferStructuredCodeWriterApiAnalyzer`
  (PSGFR18) and `PreferStructuredCodeWriterStatementAnalyzer` (PSGFR19) flag raw emission.
- Prefer `IfBlock`/`ElseIf`/`Else` over generic block methods for conditional content — the
  `PreferStructuredCodeWriterIfBlockAnalyzer` (PSGFR23) flags `OpenBlockScope`/`OpenBlock` headers that
  write an `if`, `else if`, or `else` statement, and its code fix rewrites them.
- Always consume scope-returning methods with `using` (PSGFR17).
- Never embed a `CodeWriter` in a string. The XML block-writing methods (`XmlCode`, `XmlSummary`,
  `XmlCodeBlock`, ...) return the `CodeWriter`, so interpolating or concatenating them implicitly calls
  `ToString()` and dumps the writer's possibly-incomplete buffer — `CodeWriterInStringContextAnalyzer`
  (PSGFR29) flags it. For inline XML tags in documentation text, use the static helpers instead:
  `XmlCommentWriter.XmlInlineCode("value")` → `<c>value</c>`, or `XmlInlineCodeBlock(...)` for a `<code>`
  block; `writer.XmlCode(...)` writes to the buffer and returns the writer, it does not produce a string.
  When the writer is genuinely complete, call `ToString()` explicitly.
- Never write an open generic as a type. A `TypeIdentity` with a generic arity but no type arguments renders
  a placeholder such as `List<>` or `List<,>`, which is invalid C# in a type position (base type, return
  type, parameter, property, ...). `CodeWriter` rejects it when it is emitted as a type — construct it with
  `MakeGeneric(...)` first. An arity mismatch (`MakeGeneric` supplying the wrong number of arguments) is
  also rejected, so the mismatch surfaces as a clear exception rather than corrupted generated code.
- Keep every value emitted through the structured API so layout stays deterministic and the analyzers
  can guide callers back to the best practice.

## Samples

The [`SourceGeneratorFramework.ExampleGenerator`](../src/src/SourceGeneratorFramework.ExampleGenerator)
reference implementation demonstrates these APIs end-to-end, including the `CodeWriterSampleGenerator`,
which compiles a best-practice sample class for every `[GenerateCodeWriterSample]` target.
