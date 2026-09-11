#pragma warning disable CS1591
using System.ComponentModel;
using Microsoft.CodeAnalysis.CSharp;

namespace System;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class StringExtensions
{
	extension(string? value)
	{
		/// <summary>
		/// Surrounds the string with the specified string. Default is double quotes,
		/// i.e. <c>Hello, World!</c> becomes <c>"Hello, World!"</c>.
		/// </summary>
		/// <param name="surroundWith">The string to surround the value with.</param>
		/// <returns>The surrounded string.</returns>
		public string Surround(string surroundWith = "\"") => $"{surroundWith}{value}{surroundWith}";

		/// <summary>
		/// Returns a quoted, escaped C# string literal for the value so it can be safely emitted into
		/// generated source. Backslashes, quotes, and other characters requiring escaping are escaped;
		/// e.g. <c>^[\w\-.]+$</c> becomes <c>"^[\\w\\-.]+$"</c>. A null value is emitted as the
		/// <c>null</c> keyword.
		/// </summary>
		/// <returns>The value as a C# string literal, or the <c>null</c> keyword if the value is null.</returns>
		public string StringLiteral() => value is null ? "null" : SymbolDisplay.FormatLiteral(value, true);

		/// <summary>
		/// Returns the string value or "null" if the value is null. If <paramref name="useWhitespaceCheck"/> is true, then it will also return "null" if the value is whitespace.
		/// </summary>
		/// <param name="useWhitespaceCheck">Whether to consider whitespace as null.</param>
		/// <returns>The string value or "null".</returns>
		public string OrNullKeyword(bool useWhitespaceCheck = false) =>
			useWhitespaceCheck
				? string.IsNullOrWhiteSpace(value)
					? "null"
					: value!
				: value ?? "null";

		/// <summary>
		/// Trims the specified suffixes from the string value using <see cref="StringComparison.Ordinal"/>.
		/// If the value is null or no suffixes are provided, it returns null.
		/// If a suffix is found, it returns the string without that suffix; otherwise, it returns the original string.
		/// </summary>
		/// <param name="suffixes">The suffixes to trim.</param>
		/// <returns>The string without the specified suffixes, or null if the value is null or no suffixes are provided.</returns>
		public string? TrimSuffix(params string[] suffixes) => TrimSuffix(value, StringComparison.Ordinal, suffixes);

		/// <summary>
		/// Trims the specified suffixes from the string value using the specified comparison type.
		/// If the value is null or no suffixes are provided, it returns null.
		/// If a suffix is found, it returns the string without that suffix; otherwise, it returns the original string.
		/// </summary>
		/// <param name="comparisonType">The string comparison type to use.</param>
		/// <param name="suffixes">The suffixes to trim.</param>
		/// <returns>The string without the specified suffixes, or null if the value is null or no suffixes are provided.</returns>
		public string? TrimSuffix(StringComparison comparisonType, params string[] suffixes)
		{
			if (value is null || suffixes is null || suffixes.Length == 0)
				return null;

			foreach (var suffix in suffixes)
			{
				if (value.EndsWith(suffix, comparisonType))
					return value.Substring(0, value.Length - suffix.Length);
			}

			return value;
		}
	}
}
