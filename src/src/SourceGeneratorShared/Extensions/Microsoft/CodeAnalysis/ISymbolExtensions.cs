using System.ComponentModel;

namespace Microsoft.CodeAnalysis;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class ISymbolExtensions
{
	extension(ISymbol? symbol)
	{
		/// <summary>
		/// Determines whether the symbol declares a <c>null</c> default or constant value.
		/// </summary>
		/// <remarks>
		/// The C# <c>null</c> literal has no type, so it cannot be matched by a <see cref="TypeIdentity"/>.
		/// This is the closest "null-ness" signal: a constant field or local whose value is
		/// <see langword="null"/> (such as <c>const string Value = null!</c>), or an optional parameter with
		/// a <c>null</c> default.
		/// </remarks>
		/// <returns>
		/// <see langword="true"/> when the symbol declares a <c>null</c> constant value or a <c>null</c>
		/// explicit default value; otherwise, <see langword="false"/>.
		/// </returns>
		public bool HasNullDefaultValue() =>
			symbol is null
				? false
				: symbol switch
				{
					IParameterSymbol parameter => parameter.HasExplicitDefaultValue
						&& parameter.ExplicitDefaultValue is null,
					IFieldSymbol field => field.HasConstantValue && field.ConstantValue is null,
					ILocalSymbol local => local.HasConstantValue && local.ConstantValue is null,
					_ => false,
				};
	}
}
