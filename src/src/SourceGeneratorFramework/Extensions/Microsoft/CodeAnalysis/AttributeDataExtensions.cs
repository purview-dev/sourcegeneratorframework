// IDE0005 cannot see references inside extension(...) blocks, so it incorrectly reports these usings as
// unnecessary; they are required for TypeIdentity used in the block bodies.
#pragma warning disable IDE0005
#pragma warning disable CS1591
using System.ComponentModel;
using Purview.SourceGeneratorFramework;

namespace Microsoft.CodeAnalysis;

/// <summary>
/// Provides extension methods for extracting values from <see cref="AttributeData"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1708:Identifiers should differ by more than case")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class AttributeDataExtensions
{
	extension(AttributeData attribute)
	{
		/// <summary>
		/// Gets the value of a named argument, returning the default if the argument is not present.
		/// </summary>
		public T? GetNamedArgument<T>(string name, T? defaultValue = default)
		{
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Argument name cannot be null or whitespace.", nameof(name));

			// All valid...
			return TryGetNamedArgument(attribute, name, out T? value) ? value : defaultValue;
		}

		/// <summary>
		/// Tries to get the value of a named argument.
		/// </summary>
		public bool TryGetNamedArgument<T>(string name, out T? value)
		{
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Argument name cannot be null or whitespace.", nameof(name));

			foreach (var namedArg in attribute.NamedArguments)
			{
				if (string.Equals(namedArg.Key, name, StringComparison.Ordinal))
				{
					value = namedArg.Value.As<T>();
					return true;
				}
			}

			value = default;
			return false;
		}

		/// <summary>
		/// Tries to get a constructor argument by its declared parameter name.
		/// </summary>
		public bool TryGetConstructorArgument<T>(string parameterName, out T? value)
		{
			if (string.IsNullOrWhiteSpace(parameterName))
			{
				throw new ArgumentException("Parameter name cannot be null or whitespace.", nameof(parameterName));
			}

			var constructor = attribute.AttributeConstructor;
			if (constructor is null)
			{
				value = default;
				return false;
			}

			for (var index = 0; index < constructor.Parameters.Length; index++)
			{
				if (string.Equals(constructor.Parameters[index].Name, parameterName, StringComparison.Ordinal))
				{
					return attribute.TryGetConstructorArgument(index, out value);
				}
			}

			value = default;
			return false;
		}

		/// <summary>
		/// Gets the value of a constructor argument at the specified index, returning the default if out of range.
		/// </summary>
		public T? GetConstructorArgument<T>(int index, T? defaultValue = default)
		{
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));

			// All valid...
			return TryGetConstructorArgument(attribute, index, out T? value) ? value : defaultValue;
		}

		/// <summary>
		/// Gets the value of a constructor argument at the specified index, returning the default if out of range.
		/// </summary>
		public T? GetConstructorArgument<T>(string name, T? defaultValue = default)
		{
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));

			// All valid...
			return TryGetConstructorArgument(attribute, name, out T? value) ? value : defaultValue;
		}

		/// <summary>
		/// Tries to get the value of a constructor argument at the specified index.
		/// </summary>
		public bool TryGetConstructorArgument<T>(int index, out T? value)
		{
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));

			if (index < 0 || index >= attribute.ConstructorArguments.Length)
			{
				value = default;
				return false;
			}

			value = attribute.ConstructorArguments[index].As<T>();
			return true;
		}

		/// <summary>
		/// Gets the value of a generic type argument on the attribute class, returning the default if not found.
		/// </summary>
		public T? GetGenericTypeArgument<T>(int index, T? defaultValue = default)
		{
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));

			// All valid...
			return TryGetGenericTypeArgument(attribute, index, out T? value) ? value : defaultValue;
		}

		/// <summary>
		/// Gets the value of a generic type argument on the attribute class by type parameter name, returning the default if not found.
		/// </summary>
		public T? GetGenericTypeArgument<T>(string name, T? defaultValue = default)
		{
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));

			// All valid...
			return TryGetGenericTypeArgument(attribute, name, out T? value) ? value : defaultValue;
		}

		/// <summary>
		/// Tries to get the value of a generic type argument on the attribute class.
		/// </summary>
		public bool TryGetGenericTypeArgument<T>(int index, out T? value)
		{
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));

			if (
				attribute.AttributeClass is not INamedTypeSymbol attrClass
				|| index < 0
				|| index >= attrClass.TypeArguments.Length
			)
			{
				value = default;
				return false;
			}

			value = ConvertTypeSymbol<T>(attrClass.TypeArguments[index]);
			return value is not null;
		}

		/// <summary>
		/// Tries to get the value of a generic type argument on the attribute class by type parameter name.
		/// </summary>
		public bool TryGetGenericTypeArgument<T>(string name, out T? value)
		{
			value = default;
			if (attribute == null)
				throw new ArgumentNullException(nameof(attribute));
			if (attribute.AttributeClass is not INamedTypeSymbol attrClass)
				return false;

			var typeParameters = attrClass.ConstructedFrom.TypeParameters;
			for (var i = 0; i < typeParameters.Length; i++)
			{
				if (string.Equals(typeParameters[i].Name, name, StringComparison.Ordinal))
					return TryGetGenericTypeArgument(attribute, i, out value);
			}

			return false;
		}

		/// <summary>
		/// Gets the display name of an enum named argument, returning the default if the argument is not present or not an enum.
		/// </summary>
		public string? GetEnumNamedArgument(string name, string? defaultValue = null)
		{
			if (
				attribute.TryGetNamedArgument<TypedConstant>(name, out var value)
				&& !value.IsNull
				&& value.Kind == TypedConstantKind.Enum
			)
			{
				return value.ToEnumString();
			}

			// Return the default value if the named argument is not present or not an enum.
			return defaultValue;
		}

		/// <summary>
		/// Gets the display name of an enum constructor argument by parameter name, returning the default if the argument is not present or not an enum.
		/// </summary>
		public string? GetEnumConstructorArgument(string name, string? defaultValue = null)
		{
			if (
				attribute.TryGetConstructorArgument<TypedConstant>(name, out var value)
				&& !value.IsNull
				&& value.Kind == TypedConstantKind.Enum
			)
			{
				return value.ToEnumString();
			}

			// Return the default value if the constructor argument is not present or not an enum.
			return defaultValue;
		}

		/// <summary>
		/// Gets the display name of an enum constructor argument by index, returning the default if the argument is not present or not an enum.
		/// </summary>
		public string? GetEnumConstructorArgument(int index, string? defaultValue = null)
		{
			if (
				attribute.TryGetConstructorArgument<TypedConstant>(index, out var value)
				&& !value.IsNull
				&& value.Kind == TypedConstantKind.Enum
			)
			{
				return value.ToEnumString();
			}

			// Return the default value if the constructor argument is not present or not an enum.
			return defaultValue;
		}
	}

	static T? ConvertTypeSymbol<T>(ITypeSymbol? typeSymbol)
	{
		var targetType = typeof(T);
		if (
			targetType == typeof(ITypeSymbol)
			|| targetType == typeof(ISymbol)
			|| targetType == typeof(INamedTypeSymbol)
		)
		{
			return typeSymbol is T typedValue ? typedValue : default;
		}

		if (targetType == typeof(TypeIdentity))
		{
			return typeSymbol is not null && TypeIdentity.TryCreate(typeSymbol, out var identity)
				? (T?)(object?)identity
				: default;
		}

		// If the target type is not a symbol type, we cannot convert it, so we return default.
		return default;
	}
}
