#pragma warning disable CS1591
using System.ComponentModel;

namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Fluent extension methods that preserve the concrete options type for downstream derived records.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SourceGeneratorTestOptionsExtensions
{
	extension<TOptions>(TOptions options)
		where TOptions : SourceGeneratorTestOptions
	{
		/// <summary>
		/// Creates a copy of these options with
		/// <see cref="SourceGeneratorTestOptions.CompileToAssembly"/> set to <see langword="true"/>.
		/// </summary>
		/// <remarks>
		/// The concrete options type is preserved, so derived records such as
		/// <see cref="AnalyzerTestOptions"/> and <see cref="CodeFixTestOptions"/> can opt into compiling
		/// the output assembly without losing their derived properties.
		/// <para>
		/// The inherited <see cref="SourceGeneratorTestOptions.Default"/> is typed as the base
		/// <see cref="SourceGeneratorTestOptions"/>, so calling <c>Compile()</c> on it returns the
		/// base type. A derived record that wants a typed default should hide <c>Default</c> with its
		/// own typed static property:
		/// <code>
		/// public record MyTestOptions : SourceGeneratorTestOptions
		/// {
		///     public static new MyTestOptions Default => new();
		/// }
		/// </code>
		/// Calling <c>MyTestOptions.Default.Compile()</c> then returns a <c>MyTestOptions</c>.
		/// </para>
		/// </remarks>
		public TOptions Compile() => options with { CompileToAssembly = true };
	}
}
