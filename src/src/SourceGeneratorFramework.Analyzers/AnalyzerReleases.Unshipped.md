### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PSGF001 | Purview.SourceGeneratorFramework | Error | Generation capabilities must be a record
PSGFR11 | Purview.SourceGeneratorFramework | Warning | Prefer ForAttributeWithMetadataName over CreateSyntaxProvider
PSGFR12 | Purview.SourceGeneratorFramework | Warning | Use IIncrementalGenerator instead of ISourceGenerator
PSGFR14 | Purview.SourceGeneratorFramework | Warning | Avoid RegisterImplementationSourceOutput
PSGFR15 | Purview.SourceGeneratorFramework | Warning | Pipeline model collection lacks sequence equality
PSGFR16 | Purview.SourceGeneratorFramework | Info | Prefer the nullable-context overload
PSGFR17 | Purview.SourceGeneratorFramework | Warning | Consume CodeWriter scopes with a using statement
PSGFR18 | Purview.SourceGeneratorFramework | Info | Prefer a structured CodeWriter declaration API
PSGFR19 | Purview.SourceGeneratorFramework | Info | Prefer a structured CodeWriter statement API
PSGFR20 | Purview.SourceGeneratorFramework | Info | Prefer the minimal CodeWriter overload
PSGFR21 | Purview.SourceGeneratorFramework | Info | Prefer HashDefines for conditional compilation
PSGFR22 | Purview.SourceGeneratorFramework | Info | Prefer PragmaDisable for warning suppression
PSGFR23 | Purview.SourceGeneratorFramework | Info | Prefer the structured CodeWriter conditional API
PSGFR24 | Purview.SourceGeneratorFramework | Warning | CodeFixProvider is not marked with ExportCodeFixProvider
PSGFR25 | Purview.SourceGeneratorFramework | Warning | DiagnosticAnalyzer is not marked with DiagnosticAnalyzer
PSGFR26 | Purview.SourceGeneratorFramework | Error | Source generator is not marked with Generator
PSGFR27 | Purview.SourceGeneratorFramework | Warning | Roslyn component type must be public
PSGFR28 | Purview.SourceGeneratorFramework | Info | Code fixer targets a diagnostic no analyzer produces
PSGFR29 | Purview.SourceGeneratorFramework | Warning | Do not embed a CodeWriter in a string
ADM0001 | Target | Error | Target attribute type cannot be resolved
ADM0002 | Property | Error | Property type is not supported for attribute extraction
ADM0003 | Source | Error | Specified constructor index/name does not exist on the target attribute
ADM0004 | NestedModel | Error | Nested model type is not annotated with GenerateAttributeDataModel
ADM0005 | DefaultValue | Error | Default value cannot be emitted for the property type
ADM0006 | DefaultValue | Error | Non-nullable reference type property requires a default value
ADM0007 | AutoDiscovery | Error | Auto-discovery requires a target attribute type
ADM0008 | TypeArgument | Error | Type argument property type must be TypeIdentity
ADM0009 | Property | Error | IsEnum property must be a string type
ADM0010 | Property | Error | Attribute data model property type is not cacheable |
TLB0001 | TypeLibrary | Error | GenerateTypeLibrary can only be applied to a static class
TLB0002 | TypeLibrary | Error | Type library member type must be TypeIdentity
TLB0003 | TypeLibrary | Error | Type library member requires a resolvable type and namespace
TLB0004 | TypeLibrary | Error | Duplicate type library member
TLB0005 | TypeLibrary | Error | Generated type library class name is not a valid identifier
TLB0006 | TypeLibrary | Error | Generated type library namespace is not valid
TLB0008 | TypeLibrary | Error | Type library member accessibility is invalid
TLB0009 | TypeLibrary | Error | Type library reference member requires an initializer
TLB0010 | TypeLibrary | Info | Type library marker member should be initialized to default
TLB0011 | TypeLibrary | Error | GenerateTypeLibrary spec must be declared partial
TLB0012 | TypeLibrary | Error | Type library spec class name clashes with the generated type library class
TLB0013 | TypeLibrary | Warning | Type library spec class name matches the generated type library class