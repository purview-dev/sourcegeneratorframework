namespace Purview.SourceGeneratorFramework.Testing;

/// <summary>
/// Test-only nullable helpers. A test asserting a nullable expected type cannot pass a generation context
/// (there is none in a test), so the bare <c>Nullable()</c>/<c>MakeNullable()</c> calls on
/// <see cref="TypeReference"/>/<see cref="TypeIdentity"/> would be flagged by the <c>PSGFR16</c> analyzer,
/// whose code fix has nothing in scope to apply. These extensions express the same intent through a
/// <see cref="CodeQuery"/>, which carries the compilation a test already has.
/// </summary>
public static class CodeQueryNullableExtensions
{
	/// <summary>
	/// Gets the nullable form of <paramref name="type"/>, resolved against the query's compilation nullable
	/// context when one is available.
	/// </summary>
	public static TypeReference MakeNullable(this CodeQuery query, TypeReference type)
	{
		if (query is null)
			throw new ArgumentNullException(nameof(query));
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		// TypeReference is a reference type, so no null check is needed for it.
		return query.Compilation is { } compilation ? type.Nullable(compilation) : type.Nullable();
	}

	/// <summary>
	/// Gets the nullable form of <paramref name="type"/>, resolved against the query's compilation nullable
	/// context when one is available.
	/// </summary>
	public static TypeReference MakeNullable(this CodeQuery query, TypeIdentity type)
	{
		if (query is null)
			throw new ArgumentNullException(nameof(query));

		// TypeIdentity is a value type, so no null check is needed for it.
		return query.MakeNullable(type.AsTypeReference());
	}

	/// <summary>
	/// Gets the nullable form of <paramref name="type"/>, resolved against the query's compilation nullable
	/// context when one is available.
	/// </summary>
	public static TypeReference MakeNullable(this TypeReference type, CodeQuery query)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (query is null)
			throw new ArgumentNullException(nameof(query));

		// TypeReference is a reference type, so no null check is needed for it.
		return query.Compilation is { } compilation ? type.Nullable(compilation) : type.Nullable();
	}

	/// <summary>
	/// Gets the nullable form of <paramref name="type"/>, resolved against the query's compilation nullable
	/// context when one is available.
	/// </summary>
	public static TypeReference MakeNullable(this TypeIdentity type, CodeQuery query)
	{
		if (query is null)
			throw new ArgumentNullException(nameof(query));

		// TypeIdentity is a value type, so no null check is needed for it.
		return query.MakeNullable(type.AsTypeReference());
	}
}
