using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// A diagnostic that is ready to be carried through an incremental source generator pipeline and reported.
/// It wraps a <see cref="DiagnosticDescriptor"/> together with an optional location, message arguments, and
/// an explicit blocking decision.
/// </summary>
/// <remarks>
/// The <see cref="DiagnosticDescriptor"/> is retained directly rather than reduced to scalar fields: it is a
/// sealed, immutable, structurally-equatable catalog value with no compilation state, so it never roots old
/// compilations and does not invalidate the incremental cache (its structural equality compares the rule
/// identity fields).
/// </remarks>
/// <param name="Descriptor">The diagnostic descriptor.</param>
/// <param name="FilePath">The file path of the diagnostic, or <see cref="string.Empty"/> when there is no location.</param>
/// <param name="TextSpan">The source span of the diagnostic.</param>
/// <param name="LinePositionSpan">The line position span of the diagnostic.</param>
/// <param name="AdditionalLinePositions">The line position spans of the additional locations.</param>
/// <param name="MessageArgs">The message arguments for the diagnostic descriptor's message format.</param>
/// <param name="IsBlocking">
/// Indicates whether this diagnostic should stop generation when carried by a <see cref="GeneratorResult{T}"/>.
/// This is explicit: the caller decides, independently of the diagnostic's severity, whether processing may continue.
/// </param>
public sealed record ReportableDiagnostic(
	DiagnosticDescriptor Descriptor,
	string FilePath,
	TextSpan TextSpan,
	LinePositionSpan LinePositionSpan,
	EquatableArray<LinePositionSpan> AdditionalLinePositions,
	ImmutableArray<object> MessageArgs,
	bool IsBlocking
)
{
	/// <summary>
	/// Gets the diagnostic descriptor id.
	/// </summary>
	public string Id => Descriptor.Id;

	/// <summary>
	/// Gets the diagnostic descriptor default severity.
	/// </summary>
	public DiagnosticSeverity Severity => Descriptor.DefaultSeverity;

	/// <inheritdoc />
	/// <remarks>
	/// <see cref="MessageArgs"/> is compared by content (<c>object.Equals</c> per argument, which is
	/// value-based for the primitive/string/enum values used as message arguments) so that two diagnostics
	/// created in separate generator runs are equal and do not invalidate the incremental cache. The
	/// <see cref="Descriptor"/> comparison is structural, so recreated descriptors with the same rule
	/// identity also compare equal.
	/// </remarks>
	public bool Equals(ReportableDiagnostic? other) =>
		other is not null
		&& Descriptor.Equals(other.Descriptor)
		&& string.Equals(FilePath, other.FilePath, StringComparison.Ordinal)
		&& TextSpan == other.TextSpan
		&& LinePositionSpan == other.LinePositionSpan
		&& AdditionalLinePositions.Equals(other.AdditionalLinePositions)
		&& MessageArgs.SequenceEqual(other.MessageArgs)
		&& IsBlocking == other.IsBlocking;

	/// <inheritdoc />
	public override int GetHashCode()
	{
		unchecked
		{
			var hash = 17;
			hash = (hash * 31) + Descriptor.GetHashCode();
			hash = (hash * 31) + (FilePath?.GetHashCode() ?? 0);
			hash = (hash * 31) + TextSpan.GetHashCode();
			hash = (hash * 31) + LinePositionSpan.GetHashCode();
			foreach (var span in AdditionalLinePositions.AsImmutableArray())
				hash = (hash * 31) + span.GetHashCode();
			foreach (var argument in MessageArgs)
				hash = (hash * 31) + (argument?.GetHashCode() ?? 0);
			hash = (hash * 31) + IsBlocking.GetHashCode();

			return hash;
		}
	}

	/// <summary>
	/// Formats the diagnostic message using the descriptor's message format and the message arguments.
	/// </summary>
	/// <param name="provider">The format provider, or <see langword="null"/> for the current culture.</param>
	/// <returns>The formatted message.</returns>
	public string GetMessage(IFormatProvider? provider = null)
	{
		var format = Descriptor.MessageFormat.ToString(provider);
		return MessageArgs.IsDefaultOrEmpty ? format : string.Format(provider, format, [.. MessageArgs]);
	}

	/// <summary>
	/// Converts this <see cref="ReportableDiagnostic"/> into a Roslyn <see cref="Diagnostic"/> ready for
	/// <c>SourceProductionContext.ReportDiagnostic</c>.
	/// </summary>
	public Diagnostic ToDiagnostic()
	{
		var location = string.IsNullOrEmpty(FilePath)
			? Location.None
			: Location.Create(FilePath, TextSpan, LinePositionSpan);

		return Diagnostic.Create(Descriptor, location, MessageArgs.ToArray());
	}

	/// <summary>
	/// Creates a <see cref="ReportableDiagnostic"/> from a descriptor and message arguments.
	/// </summary>
	/// <param name="descriptor">The diagnostic descriptor.</param>
	/// <param name="isBlocking">Whether generation must stop when this diagnostic is carried by a <see cref="GeneratorResult{T}"/>.</param>
	/// <param name="messageArgs">The message arguments.</param>
	/// <returns>A <see cref="ReportableDiagnostic"/> instance.</returns>
	/// <exception cref="ArgumentNullException"></exception>
	public static ReportableDiagnostic Create(
		DiagnosticDescriptor descriptor,
		bool isBlocking,
		params object[] messageArgs
	) => Create(descriptor, isBlocking, location: null, additionalLocations: null, messageArgs: messageArgs);

	/// <summary>
	/// Creates a <see cref="ReportableDiagnostic"/> from a descriptor and optional location.
	/// </summary>
	/// <param name="descriptor">The diagnostic descriptor.</param>
	/// <param name="isBlocking">Whether generation must stop when this diagnostic is carried by a <see cref="GeneratorResult{T}"/>.</param>
	/// <param name="location">The location of the diagnostic.</param>
	/// <param name="messageArgs">The message arguments.</param>
	/// <returns>A <see cref="ReportableDiagnostic"/> instance.</returns>
	public static ReportableDiagnostic Create(
		DiagnosticDescriptor descriptor,
		bool isBlocking,
		Location? location,
		params object[] messageArgs
	) => Create(descriptor, isBlocking, location, additionalLocations: null, messageArgs);

	/// <summary>
	/// Creates a <see cref="ReportableDiagnostic"/> from a descriptor and optional locations.
	/// </summary>
	/// <param name="descriptor">The diagnostic descriptor.</param>
	/// <param name="isBlocking">Whether generation must stop when this diagnostic is carried by a <see cref="GeneratorResult{T}"/>.</param>
	/// <param name="locations">The locations of the diagnostic.</param>
	/// <param name="messageArgs">The message arguments.</param>
	/// <returns>A <see cref="ReportableDiagnostic"/> instance.</returns>
	public static ReportableDiagnostic Create(
		DiagnosticDescriptor descriptor,
		bool isBlocking,
		IEnumerable<Location> locations,
		params object[] messageArgs
	)
	{
		var location = locations?.FirstOrDefault();
		var additionalLocations = locations?.Skip(1).ToImmutableArray();

		return Create(descriptor, isBlocking, location, additionalLocations, messageArgs);
	}

	/// <summary>
	/// Creates a <see cref="ReportableDiagnostic"/> from a descriptor and syntax references used to resolve locations.
	/// </summary>
	/// <param name="descriptor">The diagnostic descriptor.</param>
	/// <param name="isBlocking">Whether generation must stop when this diagnostic is carried by a <see cref="GeneratorResult{T}"/>.</param>
	/// <param name="syntaxReferences">Used to retrieve the locations of the diagnostic.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="messageArgs">The message arguments.</param>
	/// <returns>A <see cref="ReportableDiagnostic"/> instance.</returns>
	public static ReportableDiagnostic Create(
		DiagnosticDescriptor descriptor,
		bool isBlocking,
		IEnumerable<SyntaxReference> syntaxReferences,
		CancellationToken cancellationToken,
		params object[] messageArgs
	)
	{
		var location = syntaxReferences?.FirstOrDefault()?.GetSyntax(cancellationToken).GetLocation();
		var additionalLocations = syntaxReferences
			?.Skip(1)
			.Select(s => s.GetSyntax(cancellationToken).GetLocation())
			.ToImmutableArray();

		return Create(descriptor, isBlocking, location, additionalLocations, messageArgs);
	}

	/// <summary>
	/// Creates a <see cref="ReportableDiagnostic"/> from a descriptor and optional location.
	/// </summary>
	/// <param name="descriptor">The diagnostic descriptor.</param>
	/// <param name="isBlocking">Whether generation must stop when this diagnostic is carried by a <see cref="GeneratorResult{T}"/>.</param>
	/// <param name="location">The location of the diagnostic.</param>
	/// <param name="additionalLocations">Additional locations of the diagnostic.</param>
	/// <param name="messageArgs">The message arguments.</param>
	/// <returns>A <see cref="ReportableDiagnostic"/> instance.</returns>
	public static ReportableDiagnostic Create(
		DiagnosticDescriptor descriptor,
		bool isBlocking,
		Location? location,
		ImmutableArray<Location>? additionalLocations = null,
		params object[] messageArgs
	)
	{
		if (descriptor is null)
			throw new ArgumentNullException(nameof(descriptor));

		ImmutableArray<LinePositionSpan> lineSpaces = [];
		if (additionalLocations is not null)
		{
			lineSpaces = [.. additionalLocations.Value.Select(static loc => loc.GetLineSpan().Span)];
		}

		if (location is null)
		{
			return new ReportableDiagnostic(
				Descriptor: descriptor,
				FilePath: string.Empty,
				TextSpan: default,
				LinePositionSpan: default,
				AdditionalLinePositions: lineSpaces,
				MessageArgs: ImmutableArray.Create(messageArgs),
				IsBlocking: isBlocking
			);
		}

		var lineSpan = location.GetLineSpan();
		return new(
			Descriptor: descriptor,
			FilePath: lineSpan.Path,
			TextSpan: location.SourceSpan,
			LinePositionSpan: lineSpan.Span,
			AdditionalLinePositions: lineSpaces,
			MessageArgs: ImmutableArray.Create(messageArgs),
			IsBlocking: isBlocking
		);
	}

	/// <summary>
	/// Creates a <see cref="ReportableDiagnostic"/> from a descriptor and a target symbol.
	/// </summary>
	/// <param name="descriptor">The diagnostic descriptor.</param>
	/// <param name="isBlocking">Whether generation must stop when this diagnostic is carried by a <see cref="GeneratorResult{T}"/>.</param>
	/// <param name="symbol">The symbol whose locations anchor the diagnostic.</param>
	/// <param name="messageArgs">The message arguments.</param>
	/// <returns>A <see cref="ReportableDiagnostic"/> instance.</returns>
	/// <exception cref="ArgumentNullException"></exception>
	public static ReportableDiagnostic Create(
		DiagnosticDescriptor descriptor,
		bool isBlocking,
		ISymbol symbol,
		params object[] messageArgs
	)
	{
		if (symbol is null)
			throw new ArgumentNullException(nameof(symbol));

		var location = symbol.Locations.FirstOrDefault(m => m.IsInSource);
		ImmutableArray<Location>? additionalLocations = null;
		if (location is not null)
			additionalLocations = [.. symbol.Locations.Skip(1).Where(static loc => loc.IsInSource)];

		return Create(descriptor, isBlocking, location, additionalLocations: additionalLocations, messageArgs);
	}
}
