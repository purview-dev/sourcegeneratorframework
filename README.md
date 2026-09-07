# Purview.SourceGeneratorFramework

A set of libraries for building and testing incremental C# source generators using Roslyn.

## Documentation

- [Source generator & analyser best practices](docs/guide.md)
- [CodeWriter structured API reference](docs/code-writer.md)

## Packages

| Package | Description | Packable |
| --- | --- | --- |
| [`Purview.SourceGeneratorFramework`](src/src/SourceGeneratorFramework) | Core helpers, models, and MSBuild integration for writing incremental source generators. | Yes |
| [`Purview.SourceGeneratorFramework.Testing`](src/src/SourceGeneratorFramework.Testing) | Framework-agnostic test runner and assertions for source generator unit tests. | Yes |
| [`Purview.SourceGeneratorFramework.Testing.TUnit`](src/src/SourceGeneratorFramework.Testing.TUnit) | TUnit-specific test base classes and assertions for source generator tests. | Yes |
| [`Purview.SourceGeneratorFramework.Generators`](src/src/SourceGeneratorFramework.Generators) | Internal Roslyn source generator used by the framework package. | No |
| [`Purview.SourceGeneratorFramework.ExampleGenerator`](src/src/SourceGeneratorFramework.ExampleGenerator) | Reference implementation showing how to build a generator with the framework. | No |

## Requirements

- .NET SDK 10.0 or later
- The test projects target `net8.0`, `net9.0`, and `net10.0`
- Source generators target `netstandard2.0`

## Building

Restore and build the solution:

```bash
dotnet build src/SourceGeneratorFramework.slnx -c Release
```

## Running tests

```bash
dotnet test src/SourceGeneratorFramework.slnx -c Release
```

When a test project must both run its generator (to use generated attributes or types in test
fixtures) and reference the generator type through the testing framework, use two project
references: one with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`, and one
normal reference with `ReferenceOutputAssembly="true"`. The complete pattern is documented in the
[`Purview.SourceGeneratorFramework.Testing` README](src/src/SourceGeneratorFramework.Testing/Sdk/README.md#running-the-generator-in-the-test-project).

## Generators

### AttributeDataModelGenerator

The framework package includes `AttributeDataModelGenerator` (implemented in `Purview.SourceGeneratorFramework.Generators`), which generates `readonly record struct` parser models for .NET attributes. It removes the repetitive boilerplate of hand-writing `FromAttributeData` methods for every attribute you want to inspect in a source generator.

Supported features:

- Manual mapping of named arguments, constructor arguments by index, and constructor arguments by name
- Auto-discovery of all constructor parameters and public named properties
- Nested generated models (e.g., a shared `ValidationAttributeData` model reused inside `RequiredAttributeData`)
- Inheritance matching for base attribute models
- Optional `DefaultValue` runtime fallback when a property is not found on the attribute
- An `Empty` sentinel that uses `default(T)` for every property

See the [`Purview.SourceGeneratorFramework.Generators` README](src/src/SourceGeneratorFramework.Generators) for examples, including validation attributes with nested types.

### TypeLibraryGenerator

`TypeLibraryGenerator` removes the boilerplate of hand-writing the static type library that exposes the `TypeIdentity`/`TypeReference` values a generator needs. From a small declarative spec (`[GenerateTypeLibrary]` + `[TypeRef]` members), it emits a self-contained `public static partial` type library whose nested `public static partial` classes mirror the namespaces of the members, each with a `Namespace` constant and `public static readonly` fields.

See [docs/type-library.md](docs/type-library.md) for the DSL, the member-accessibility rules, and a runnable sample in [`SourceGeneratorFramework.ExampleGenerator`](src/src/SourceGeneratorFramework.ExampleGenerator/TypeLibrarySpec.cs).

## Packaging

```bash
dotnet pack src/SourceGeneratorFramework.slnx -c Release -o ./artifacts
```

## License

This project is licensed under the MIT license.
