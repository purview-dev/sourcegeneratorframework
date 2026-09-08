namespace Purview.SourceGeneratorFramework.ExampleGenerator;

/// <summary>
/// Demonstrates the <c>[GenerateTypeLibrary]</c> DSL. The generator emits a self-contained
/// <c>SampleTypeLibrary</c> whose nested <c>public static partial</c> classes mirror the namespaces of
/// the declared members.
/// </summary>
[GenerateTypeLibrary(ClassName = "SampleTypeLibrary", Namespace = "Purview.SourceGeneratorFramework.ExampleGenerator")]
static partial class TypeLibrarySpec
{
	/// <summary>
	/// A type emitted by the sample generator itself, declared with the namespace-only overload — the
	/// type name defaults to the member name.
	/// </summary>
	[TypeRef("Purview.SourceGeneratorFramework.Examples")]
	static readonly TypeIdentity GenerateTypeLibrarySampleAttribute = default;

	/// <summary>
	/// A framework type resolved through <c>typeof(...)</c>.
	/// </summary>
	[TypeRef(typeof(System.Diagnostics.Debug))]
	static readonly TypeIdentity Debug = default;

	/// <summary>
	/// Types declared explicitly, forming the <c>SampleTypeLibrary.Microsoft.Extensions.Logging</c>
	/// nested class that represents the logging namespace's classes. <c>ILogger</c> is included in the
	/// namespace's generated <c>GetTypes()</c> call.
	/// </summary>
	[TypeRef("Microsoft.Extensions.Logging", IncludeInGetTypes = true)]
	static readonly TypeIdentity ILogger = default;

	[TypeRef("Microsoft.Extensions.Logging")]
	static readonly TypeIdentity LogLevel = default;

	[TypeRef("Microsoft.Extensions.Logging")]
	static readonly TypeIdentity EventId = default;

	[TypeRef("Microsoft.Extensions.Logging")]
	static readonly TypeIdentity LoggerMessage = default;

	/// <summary>
	/// A composed <c>TypeReference</c> value member: the initializer expression becomes the generated
	/// value, exposing <c>SampleTypeLibrary.System.Collections.Generic.SampleItems</c>.
	/// </summary>
	[TypeRef("System.Collections.Generic")]
	internal static readonly TypeReference SampleItems =
		SourceGeneratorFramework.PurviewTypeLibrary.System.Collections.Generic.IEnumerable.MakeGeneric(
			SourceGeneratorFramework.PurviewTypeLibrary.System.String
		);
}
