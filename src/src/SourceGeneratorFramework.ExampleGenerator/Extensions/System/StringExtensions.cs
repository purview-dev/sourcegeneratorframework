#pragma warning disable CS1591
using System.ComponentModel;

namespace System;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class StringExtensions
{
	extension(string? value)
	{
		/// <summary>
		/// Surrounds the string with the specified string. Default is double quotes.
		/// </summary>
		/// <param name="surroundWith">The string to surround the value with.</param>
		/// <returns>The surrounded string.</returns>
		public string Surround(string surroundWith = "\"") => $"{surroundWith}{value}{surroundWith}";
	}
}
