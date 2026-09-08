#if NET8_0_OR_GREATER

using System.Runtime.Loader;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// A collectible <see cref="AssemblyLoadContext"/> used to load an in-memory compiled test assembly so it
/// can be unloaded after the test, keeping assemblies out of the process-wide default context.
/// </summary>
sealed class TestAssemblyLoadContext() : AssemblyLoadContext(isCollectible: true);
#endif
