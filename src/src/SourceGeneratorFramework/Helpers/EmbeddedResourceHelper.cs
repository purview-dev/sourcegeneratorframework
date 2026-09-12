using System.Reflection;

namespace Purview.SourceGeneratorFramework.Helpers;

/// <summary>
/// Provides helpers for loading embedded resources from an assembly.
/// </summary>
public static class EmbeddedResourceHelper
{
	/// <summary>
	/// Loads the specified embedded resource as a string.
	/// </summary>
	public static string Load(string resourceName, Assembly? assembly = null)
	{
		assembly ??= Assembly.GetCallingAssembly();

		using var stream = LoadStream(resourceName, assembly);
		using StreamReader reader = new(stream);
		return reader.ReadToEnd();
	}

	/// <summary>
	/// Loads the specified embedded resource as a stream.
	/// </summary>
	public static Stream LoadStream(string resourceName, Assembly? assembly = null)
	{
		assembly ??= Assembly.GetCallingAssembly();

		var resolvedName = ResolveResourceName(resourceName, assembly);
		var stream =
			assembly.GetManifestResourceStream(resolvedName)
			?? throw new InvalidOperationException(
				$"Embedded resource '{resolvedName}' was not found in assembly '{assembly.FullName}'."
			);

		return stream;
	}

	/// <summary>
	/// Gets the names of all embedded resources in the assembly.
	/// </summary>
	public static IEnumerable<string> GetResourceNames(Assembly? assembly = null)
	{
		assembly ??= Assembly.GetCallingAssembly();
		return assembly.GetManifestResourceNames();
	}

	static string ResolveResourceName(string resourceName, Assembly assembly)
	{
		var names = assembly.GetManifestResourceNames();

		// Try the exact name first.
		if (names.Contains(resourceName, StringComparer.Ordinal))
			return resourceName;

		// Source generators often embed .cs files. Try the common source-file extensions
		// so callers can ask for "Template" and resolve to "Template.cs" automatically.
		foreach (var extension in new[] { ".cs", ".vb" })
		{
			var candidate = resourceName + extension;
			if (names.Contains(candidate, StringComparer.Ordinal))
				return candidate;
		}

		// Try with the assembly name as a prefix.
		var prefix = assembly.GetName().Name;
		if (!string.IsNullOrEmpty(prefix))
		{
			var prefixedName = prefix + "." + resourceName;
			if (names.Contains(prefixedName, StringComparer.Ordinal))
				return prefixedName;

			foreach (var extension in new[] { ".cs", ".vb" })
			{
				var candidate = prefixedName + extension;
				if (names.Contains(candidate, StringComparer.Ordinal))
					return candidate;
			}
		}

		// Try a suffix match, including common source-file extensions, so nested
		// resources such as "Namespace.Template.cs" resolve when asked for "Template".
		var suffixMatch = names.FirstOrDefault(n =>
			n.EndsWith("." + resourceName, StringComparison.Ordinal)
			|| n.EndsWith("." + resourceName + ".cs", StringComparison.Ordinal)
			|| n.EndsWith("." + resourceName + ".vb", StringComparison.Ordinal)
		);
		if (suffixMatch != null)
			return suffixMatch;

		// If we reach here, the resource was not found. Throw an exception with available resources for debugging.
		throw new InvalidOperationException(
			$"Embedded resource '{resourceName}' was not found in assembly '{assembly.FullName}'. Available resources: {string.Join(", ", names)}."
		);
	}
}
