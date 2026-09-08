#if NETSTANDARD2_0

using System.ComponentModel;

// netstandard2.0 declares UnconditionalSuppressMessageAttribute as internal, so TUnit's generated
// assertion code (emitted for generic [GenerateAssertion] methods) cannot reference it when targeting
// netstandard2.0.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Diagnostics.CodeAnalysis;

#pragma warning restore IDE0130

[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
sealed class UnconditionalSuppressMessageAttribute(string category, string checkId) : Attribute
{
	public string Category { get; } = category;

	public string CheckId { get; } = checkId;

	public string? Justification { get; set; }

	public string? Scope { get; set; }

	public string? Target { get; set; }
}

#endif
