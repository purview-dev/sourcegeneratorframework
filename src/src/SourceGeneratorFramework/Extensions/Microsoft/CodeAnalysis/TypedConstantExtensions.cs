// IDE0005 cannot see references inside extension(...) blocks, so it incorrectly reports these usings as
// unnecessary; they are required for TypeIdentity and TypeHelpers used in the block bodies.
#pragma warning disable IDE0005
#pragma warning disable CS1591
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using Purview.SourceGeneratorFramework;
using Purview.SourceGeneratorFramework.Helpers;

namespace Microsoft.CodeAnalysis;

[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class TypedConstantExtensions
{
	extension(TypedConstant constant)
	{
		/// <summary>
		/// Converts a <see cref="TypedConstant"/> to the specified type.
		/// </summary>
		public T? As<T>()
		{
			if (constant.IsNull)
				return default;

			var targetType = typeof(T);
			if (targetType == typeof(TypedConstant))
				return (T?)(object)constant;

			if (
				targetType == typeof(ITypeSymbol)
				|| targetType == typeof(ISymbol)
				|| targetType == typeof(INamedTypeSymbol)
			)
			{
				return constant.Kind == TypedConstantKind.Type && constant.Value is T typedValue ? typedValue : default;
			}

			if (targetType == typeof(TypeIdentity))
			{
				return
					constant.Kind == TypedConstantKind.Type
					&& constant.Value is ITypeSymbol typeSymbol
					&& TypeIdentity.TryCreate(typeSymbol, out var identity)
					? (T?)(object?)identity
					: default;
			}

			if (constant.Kind == TypedConstantKind.Array)
			{
				if (targetType == typeof(ImmutableArray<TypedConstant>))
				{
					return (T?)(object?)ImmutableArray.CreateRange(constant.Values);
				}

				var values = constant.Values.Select(As<T>).ToArray();
				if (targetType.IsArray)
				{
					var elementType = targetType.GetElementType();
					var array = Array.CreateInstance(elementType, values.Length);
					for (var i = 0; i < values.Length; i++)
						array.SetValue(values[i], i);
					return (T?)(object?)array;
				}
				return (T?)(object?)values;
			}

			var value = constant.Value;
			if (value == null)
				return default;

			if (targetType.IsEnum)
			{
				return value is string stringValue
					? (T?)Enum.Parse(targetType, stringValue)
					: (T?)Enum.ToObject(targetType, value);
			}

			if (value is T t)
				return t;

			try
			{
				return (T?)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
			}
			catch
			{
				return default;
			}
		}

		/// <summary>
		/// Converts the typed constant to a fully-qualified enum member display string (e.g. "Namespace.Type.Member").
		/// </summary>
		public string? ToEnumString()
		{
			if (constant.IsNull || constant.Kind != TypedConstantKind.Enum)
				return null;

			var type = constant.Type;
			if (type is null)
				return null;

			var typeName = TypeHelpers.ToFullyQualifiedDisplayString(type);
			var value = constant.Value;

			foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
			{
				if (field.HasConstantValue && field.ConstantValue?.Equals(value) == true)
					return $"{typeName}.{field.Name}";
			}

			return $"{typeName}.{Convert.ToString(value, CultureInfo.InvariantCulture)}";
		}
	}
}
