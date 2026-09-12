using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Holds the result of emitting a test compilation to an in-memory assembly: the emitted bytes, the
/// <see cref="Assembly"/> loaded from them, and a metadata-only view (<see cref="MetadataLoadContext"/> and the
/// assembly loaded within it). The runnable assembly is loaded into a collectible load context (where
/// available) so it can be unloaded when the owning result is disposed.
/// </summary>
sealed class EmittedAssembly : IDisposable
{
	readonly byte[]? _bytes;
	readonly Compilation? _compilation;
	MetadataView? _metadataView;
#if NET8_0_OR_GREATER
	readonly TestAssemblyLoadContext? _loadContext;
#endif

	public EmittedAssembly(byte[]? bytes, Compilation? compilation)
	{
		_bytes = bytes;
		_compilation = compilation;
#if NET8_0_OR_GREATER
		if (bytes is not null)
			_loadContext = new TestAssemblyLoadContext();
#endif
	}

	/// <summary>
	/// Gets the diagnostics produced by the emit, or an empty set when no emit was attempted.
	/// </summary>
	public ImmutableArray<Diagnostic> Diagnostics { get; init; }

	/// <summary>
	/// Gets the loaded runnable assembly, or <see langword="null"/> when emission failed or was not requested.
	/// </summary>
	public Assembly? Assembly
	{
		get
		{
			if (_bytes is null)
				return null;

			if (field is null)
			{
				#if NET8_0_OR_GREATER
				field = _loadContext!.LoadFromStream(
					new MemoryStream(_bytes, writable: false)
				);
#else
				field =
					Assembly.Load(_bytes);
#endif
			}

			return field;
		}
		private set;
	}

	/// <summary>
	/// Gets a metadata-only view of the emitted assembly, or <see langword="null"/> when emission failed or
	/// was not requested. The view is created lazily on first access and never executes code.
	/// </summary>
	public MetadataLoadContext? Metadata =>
		_bytes is null ? null : (_metadataView ??= new MetadataView(_bytes, _compilation!)).Context;

	/// <summary>
	/// Gets the emitted assembly loaded within the metadata-only view, or <see langword="null"/> when emission
	/// failed or was not requested. Reflects over the assembly without executing any code.
	/// </summary>
	public Assembly? MetadataAssembly =>
		_bytes is null ? null : (_metadataView ??= new MetadataView(_bytes, _compilation!)).Assembly;

	public void Dispose()
	{
		_metadataView?.Dispose();
#if NET8_0_OR_GREATER
		_loadContext?.Unload();
#endif
	}

	sealed class MetadataView : IDisposable
	{
		public MetadataLoadContext Context { get; }

		public Assembly Assembly { get; }

		public MetadataView(byte[] bytes, Compilation compilation)
		{
			PathAssemblyResolver resolver = new(MetadataPaths(compilation));
			MetadataLoadContext context = new(resolver);

			Assembly = context.LoadFromByteArray(bytes);
			Context = context;
		}

		public void Dispose() => Context.Dispose();
	}

	static HashSet<string> MetadataPaths(Compilation compilation)
	{
		HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
		paths.UnionWith(SourceGeneratorHelpers.TrustedAssemblies);

		foreach (var reference in compilation.References.OfType<PortableExecutableReference>())
		{
			if (reference.FilePath is not null)
				paths.Add(reference.FilePath);
		}

		return paths;
	}
}
