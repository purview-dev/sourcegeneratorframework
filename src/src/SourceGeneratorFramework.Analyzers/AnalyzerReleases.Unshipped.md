; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PSGFR30 | Purview.SourceGeneratorFramework | Warning | Prefer static lambdas in incremental generator pipelines
PSGFR31 | Purview.SourceGeneratorFramework | Warning | Prefer TargetSymbol over GetDeclaredSymbol(TargetNode)
PSGFR32 | Purview.SourceGeneratorFramework | Warning | Avoid NormalizeWhitespace when generating source
PSGFR33 | Purview.SourceGeneratorFramework | Warning | Pipeline model retains a Roslyn object
PSGFR34 | Purview.SourceGeneratorFramework | Info | Prefer extension blocks over classic extension methods
PSGFR35 | Purview.SourceGeneratorFramework | Warning | Extension class name does not match the extended type
PSGFR36 | Purview.SourceGeneratorFramework | Warning | Extension class is not placed in the extended type's namespace/folder
PSGFR37 | Purview.SourceGeneratorFramework | Warning | Extension class extends multiple receiver types
PSGFR38 | Purview.SourceGeneratorFramework | Warning | Extension class is missing EditorBrowsable or CS1591 suppression