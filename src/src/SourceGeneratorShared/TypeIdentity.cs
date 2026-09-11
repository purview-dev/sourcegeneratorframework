using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Represents a semantic reference to a named type, independent of any single compilation.
/// </summary>
/// <remarks>
/// <para>
/// This value object models <b>named type identity</b>. Arrays, pointers, function pointers,
/// <see langword="dynamic"/>, generic parameters and error types are not identities and are modelled by
/// <see cref="TypeReference"/>, which is also the element type of <see cref="TypeArguments"/>.
/// </para>
/// <para>
/// Matching against an <see cref="ITypeSymbol"/> is <i>structural</i> — name, namespace, containing-type
/// chain and generic shape — rather than symbolic, so a value created against one compilation can be matched
/// against another.
/// </para>
/// <para>
/// Generic definitions and constructed generic types are intentionally distinct values. For example,
/// <c>new TypeIdentity(typeof(Dictionary&lt;,&gt;))</c> represents the open definition, while calling
/// <see cref="MakeGeneric(TypeIdentity[])"/> supplies concrete arguments. Structural
/// <see cref="Equals(TypeIdentity)"/> requires the same generic construction; symbol
/// <see cref="Matches(ITypeSymbol?)"/> is asymmetric and allows an open definition to match any constructed
/// symbol having the same definition.
/// </para>
/// <para>
/// Construction is on the hot path of every generator pipeline, so it deliberately avoids
/// <c>ToDisplayString</c>, <c>GetGenericArguments</c> and builder growth. The common cases — a non-nested,
/// non-generic type — allocate nothing beyond the namespace string.
/// </para>
/// </remarks>
public readonly record struct TypeIdentity
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TypeIdentity"/> struct from a <see cref="Type"/>.
	/// </summary>
	/// <exception cref="ArgumentNullException">Thrown when the provided type is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when the type is not a named type. Use <see cref="TryCreate(Type, out TypeIdentity)"/> when the
	/// input may not be representable, or <see cref="TypeReference.TryCreate(Type, out TypeReference)"/>
	/// to capture array, pointer and nullable composition.
	/// </exception>
	public TypeIdentity(Type type)
	{
		if (type == null)
			throw new ArgumentNullException(nameof(type));

		if (!IsRepresentable(type))
		{
			throw new ArgumentException(
				$"The type '{type}' is not a named type and cannot be represented by {nameof(TypeIdentity)}. Use {nameof(TypeReference)} for composed references.",
				nameof(type)
			);
		}

		var knownType = KnownLangTypes.Get(type);
		if (knownType != TypeMapping.Empty)
		{
			Name = knownType.Type.Name;
			Namespace = knownType.Type.Namespace;
			Keyword = knownType.Keyword;
			SpecialType = knownType.SpecialType;
			ContainingTypes = [];
			TypeArguments = [];

			return;
		}

		Name = StripArity(type.Name);
		Namespace = string.IsNullOrEmpty(type.Namespace) ? null : type.Namespace;
		ContainingTypes = BuildContainingTypes(type, out var consumed);

		// The metadata name's backtick suffix already encodes the type's *own* arity, excluding any
		// inherited from containing types, so there is no need to materialise GetGenericArguments() here.
		GenericArity = ParseArity(type.Name);

		TypeArguments =
			GenericArity == 0 || type.IsGenericTypeDefinition
				? []
				: BuildArguments(type.GetGenericArguments(), consumed, GenericArity);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeIdentity"/> struct from a type name and namespace.
	/// <para>
	/// <b>Note:</b> This constructor does not validate the provided type name or namespace. It is the caller's
	/// responsibility to ensure the values represent a real type. For keyword types prefer
	/// <see cref="TypeIdentity(SpecialType)"/>, <see cref="TypeIdentity(ITypeSymbol)"/> or
	/// <see cref="TypeIdentity(Type)"/>.
	/// </para>
	/// <para>
	/// This produces a top-level type whose own generic arity is <paramref name="arity"/>. With no type
	/// arguments it represents an open definition when the arity is greater than zero. Use
	/// <see cref="Nested(string, int)"/> for nested types and <see cref="MakeGeneric(TypeReference[])"/> for
	/// constructed generics. Use <see cref="WithArity(int)"/> to create a higher-arity open definition from
	/// an existing value, such as a <c>Func</c> with more type parameters.
	/// </para>
	/// </summary>
	/// <param name="typeName">The type name without namespace, containing types or generic arity suffix.</param>
	/// <param name="namespace">The namespace, or <see langword="null"/> for the global namespace.</param>
	/// <param name="arity">The number of generic type parameters declared by the type.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when the provided arity is negative.</exception>
	public TypeIdentity(string typeName, string? @namespace, int arity = 0)
	{
		if (string.IsNullOrWhiteSpace(typeName))
			throw new ArgumentException("Type name cannot be null, empty or whitespace.", nameof(typeName));
		if (arity < 0)
			throw new ArgumentOutOfRangeException(nameof(arity), arity, "Generic arity must be non-negative.");

		Name = typeName;
		Namespace = string.IsNullOrWhiteSpace(@namespace) ? null : @namespace;
		GenericArity = arity;
		ContainingTypes = [];
		TypeArguments = [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeIdentity"/> struct from a Roslyn type symbol.
	/// </summary>
	/// <exception cref="ArgumentNullException">Thrown when the provided symbol is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when the symbol is not a resolvable named type. Use
	/// <see cref="TryCreate(ITypeSymbol, out TypeIdentity)"/> inside generator pipelines, where unresolved and
	/// composed symbols are routine.
	/// </exception>
	public TypeIdentity(ITypeSymbol typeSymbol)
	{
		if (typeSymbol == null)
			throw new ArgumentNullException(nameof(typeSymbol));

		if (typeSymbol is not INamedTypeSymbol namedType || !IsRepresentable(namedType))
		{
			throw new ArgumentException(
				$"The symbol '{typeSymbol.ToDisplayString()}' is not a resolvable named type and cannot be represented by {nameof(TypeIdentity)}. Use {nameof(TryCreate)} for a non-throwing conversion.",
				nameof(typeSymbol)
			);
		}

		var assembly = namedType.ContainingAssembly;
		if (assembly is not null)
		{
			var cache = AssemblySymbolCache.GetValue(assembly, AssemblyCacheFactory);
			this = cache.GetOrAdd(typeSymbol, SymbolCacheFactory).Value;
		}
		else
		{
			this = CreateFromSymbol(typeSymbol).Value;
		}
	}

	static readonly ConditionalWeakTable<
		IAssemblySymbol,
		ConcurrentDictionary<ITypeSymbol, TypeIdentityCacheEntry>
	> AssemblySymbolCache = new();
	static readonly ConditionalWeakTable<
		IAssemblySymbol,
		ConcurrentDictionary<ITypeSymbol, TypeIdentityCacheEntry>
	>.CreateValueCallback AssemblyCacheFactory = static assembly => new ConcurrentDictionary<
		ITypeSymbol,
		TypeIdentityCacheEntry
	>(TypeSymbolEqualityComparer.Instance);
	static readonly Func<ITypeSymbol, TypeIdentityCacheEntry> SymbolCacheFactory = CreateFromSymbol;

	static TypeIdentityCacheEntry CreateFromSymbol(ITypeSymbol typeSymbol)
	{
		var namedType = (INamedTypeSymbol)typeSymbol;
		var knownType = KnownLangTypes.Get(namedType.SpecialType);

		return knownType == TypeMapping.Empty
			? new TypeIdentityCacheEntry(
				new TypeIdentity
				{
					Name = namedType.Name,
					Namespace = BuildNamespace(namedType.ContainingNamespace),
					ContainingTypes = BuildContainingTypes(namedType),
					GenericArity = namedType.Arity,
					TypeArguments = BuildArguments(namedType),
				}
			)
			: new TypeIdentityCacheEntry(
				new TypeIdentity
				{
					Name = knownType.Type.Name,
					Namespace = knownType.Type.Namespace,
					Keyword = knownType.Keyword,
					SpecialType = knownType.SpecialType,
					ContainingTypes = [],
					TypeArguments = [],
				}
			);
	}

	sealed class TypeSymbolEqualityComparer : IEqualityComparer<ITypeSymbol>
	{
		public static readonly TypeSymbolEqualityComparer Instance = new();

		public bool Equals(ITypeSymbol? x, ITypeSymbol? y) => SymbolEqualityComparer.Default.Equals(x, y);

		public int GetHashCode(ITypeSymbol obj) => SymbolEqualityComparer.Default.GetHashCode(obj);
	}

	sealed class TypeIdentityCacheEntry(TypeIdentity value)
	{
		public TypeIdentity Value = value;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeIdentity"/> struct from a recognized C# keyword
	/// special type.
	/// </summary>
	public TypeIdentity(SpecialType specialType)
	{
		var knownType = KnownLangTypes.Get(specialType);
		if (knownType == TypeMapping.Empty)
		{
			throw new ArgumentException(
				$"The provided special type '{specialType}' is not a recognized C# keyword type.",
				nameof(specialType)
			);
		}

		Name = knownType.Type.Name;
		Namespace = knownType.Type.Namespace;
		Keyword = knownType.Keyword;
		SpecialType = knownType.SpecialType;
		ContainingTypes = [];
		TypeArguments = [];
	}

	/// <summary>
	/// Gets the recognized C# keyword special type, or <see cref="SpecialType.None"/> when the type is not a
	/// recognized keyword type.
	/// </summary>
	/// <remarks>
	/// Populated <b>only</b> for C# keyword types. Roslyn populates <see cref="ITypeSymbol.SpecialType"/> for many
	/// non-keyword types as well — <c>System.DateTime</c>, <c>System.IDisposable</c>, <c>System.Nullable&lt;T&gt;</c>
	/// and others — so a special type mismatch is never on its own grounds for rejecting a match.
	/// </remarks>
	public SpecialType SpecialType { get; init; } = SpecialType.None;

	/// <summary>
	/// Gets the C# keyword for the type, or <see langword="null"/> when the type has no keyword representation.
	/// </summary>
	public string? Keyword { get; init; }

	/// <summary>
	/// Gets the type name without its namespace, containing types or generic arity suffix.
	/// </summary>
	public string Name { get; init; }

	/// <summary>
	/// Gets the namespace, or <see langword="null"/> when the type is in the global namespace.
	/// </summary>
	public string? Namespace { get; init; }

	/// <summary>
	/// Gets the chain of containing types, outermost first, for a nested type.
	/// </summary>
	/// <remarks>
	/// Each entry is a lightweight <see cref="ContainingType"/> carrying only a name and its own generic
	/// arity; the namespace belongs to the chain as a whole and lives on this value instead.
	/// </remarks>
	public ImmutableArray<ContainingType> ContainingTypes { get; init; }

	/// <summary>
	/// Gets the number of generic parameters declared by the type itself, excluding those inherited from
	/// containing types.
	/// </summary>
	public int GenericArity { get; init; }

	/// <summary>
	/// Gets the generic type arguments for a constructed type.
	/// </summary>
	/// <remarks>
	/// Empty for a non-generic type and for an open generic definition; use <see cref="GenericArity"/> to
	/// distinguish those cases. Arguments are <see cref="TypeReference"/> so that composed arguments —
	/// <c>List&lt;int[]&gt;</c>, <c>List&lt;T&gt;</c>, <c>List&lt;string?&gt;</c> — are represented exactly
	/// rather than widened to the open definition.
	/// </remarks>
	public ImmutableArray<TypeReference> TypeArguments { get; init; }

	/// <summary>
	/// Gets a value indicating whether this value represents an open generic type definition.
	/// </summary>
	public bool IsGenericTypeDefinition => GenericArity > 0 && TypeArguments.IsDefaultOrEmpty;

	/// <summary>
	/// Gets a value indicating whether the type is nested inside another type.
	/// </summary>
	public bool IsNested => !ContainingTypes.IsDefaultOrEmpty;

	/// <summary>
	/// Gets a value indicating whether the type is in the global namespace.
	/// </summary>
	public bool IsGlobalNamespace => Namespace is null;

	/// <summary>
	/// Gets a value indicating whether this type's simple name uses the conventional C# attribute suffix.
	/// </summary>
	/// <remarks>
	/// This describes the type name only. Whether the type is emitted as an attribute application is determined
	/// by <see cref="AttributeDeclarationOptions"/>.
	/// </remarks>
	public bool IsAttribute =>
		Name.Length > TypeHelpers.AttributeSuffix.Length
		&& Name.EndsWith(TypeHelpers.AttributeSuffix, StringComparison.Ordinal);

	/// <summary>
	/// Gets the CLR metadata name, including the generic arity suffix when required.
	/// </summary>
	public string MetadataName => GenericArity == 0 ? Name : $"{Name}`{GenericArity}";

	/// <summary>
	/// Gets the fully-qualified CLR metadata name used by Roslyn type lookup, using <c>+</c> to separate nested
	/// types as required by <c>Compilation.GetTypeByMetadataName</c> and <c>ForAttributeWithMetadataName</c>.
	/// </summary>
	public string MetadataFullName
	{
		get
		{
			var name = MetadataName;
			if (IsNested)
				name = $"{string.Join("+", ContainingTypes.Select(static type => type.MetadataName))}+{name}";

			return IsGlobalNamespace ? name : $"{Namespace}.{name}";
		}
	}

	/// <summary>
	/// Gets the fully-qualified global type name for use in generated code.
	/// </summary>
	public string RenderFullName => RenderFullNameForNullable(nullableSupported: true);

	/// <summary>
	/// Gets the fully-qualified global type name for use in generated code.
	/// </summary>
	/// <param name="nullableSupported">
	/// When <see langword="false"/>, nullable reference annotations on generic arguments (such as the
	/// <c>string?</c> in <c>List&lt;string?&gt;</c>) are elided.
	/// </param>
	public string RenderFullNameForNullable(bool nullableSupported) =>
		RenderFullNameCore(omitAttributeSuffix: false, nullableSupported);

	/// <summary>
	/// Gets the fully-qualified name for use in an attribute application, omitting the optional
	/// <c>Attribute</c> suffix from the outer type name.
	/// </summary>
	public string RenderAttributeName => RenderFullNameCore(omitAttributeSuffix: true, nullableSupported: true);

	string RenderFullNameCore(bool omitAttributeSuffix, bool nullableSupported)
	{
		if (SpecialType != SpecialType.None)
			return Keyword!;

		var name = RenderTypeNameCore(omitAttributeSuffix, nullableSupported);
		if (IsNested)
			name = $"{string.Join(".", ContainingTypes.Select(static type => type.RenderTypeName))}.{name}";

		return IsGlobalNamespace ? name : $"global::{Namespace}.{name}";
	}

	/// <summary>
	/// Gets the type name suitable for use in generated code, without namespace or containing types.
	/// </summary>
	public string RenderTypeName => RenderTypeNameCore(omitAttributeSuffix: false, nullableSupported: true);

	/// <summary>
	/// Gets the unqualified type name for use in an attribute application, omitting the optional
	/// <c>Attribute</c> suffix while retaining generic arguments.
	/// </summary>
	public string RenderAttributeTypeName => RenderTypeNameCore(omitAttributeSuffix: true, nullableSupported: true);

	string RenderTypeNameCore(bool omitAttributeSuffix, bool nullableSupported)
	{
		if (SpecialType != SpecialType.None)
			return Keyword!;

		var name =
			omitAttributeSuffix && IsAttribute
				? Name.Substring(0, Name.Length - TypeHelpers.AttributeSuffix.Length)
				: Name;

		if (GenericArity == 0)
			return name;

		// Render the open generic definition with commas for each type parameter, or the constructed form with
		return TypeArguments.IsDefaultOrEmpty
			? $"{name}<{new string(',', GenericArity - 1)}>"
			: $"{name}<{string.Join(", ", TypeArguments.Select(argument => argument.RenderFullNameForNullable(nullableSupported)))}>";
	}

	/// <summary>
	/// Returns the rendered full name.
	/// </summary>
	public override string ToString() => RenderFullName;

	// ---------------------------------------------------------------------------------------------
	// Matching
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines whether the specified <see cref="ITypeSymbol"/> represents this type.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> when the symbol represents this type; otherwise <see langword="false"/>. An open
	/// generic definition matches every constructed form of that definition.
	/// </returns>
	public bool Matches(ITypeSymbol? other)
	{
		if (other is null)
			return false;

		// Special types are unique, so a positive match here is conclusive and cheap.
		// A mismatch is NOT conclusive: this value only stamps SpecialType for C# keyword types, whereas
		// Roslyn stamps it for DateTime, IDisposable, Nullable<T>, IEnumerable<T> and more.
		if (SpecialType != SpecialType.None && SpecialType == other.SpecialType)
			return true;

		// Arrays, pointers, function pointers and dynamic are modelled by TypeReferenceOptions. Their symbols
		// also expose a null ContainingNamespace, so they must be excluded before it is read.
		if (other is not INamedTypeSymbol namedType || !IsRepresentable(namedType))
			return false;

		// Ordered cheapest-first: name and arity reject almost everything before any chain walking.
		if (!string.Equals(Name, namedType.Name, StringComparison.Ordinal))
			return false;

		if (GenericArity != namedType.Arity)
			return false;

		if (!ContainingTypesMatch(namedType.ContainingType))
			return false;

		if (!NamespaceMatches(Namespace, namedType.ContainingNamespace))
			return false;

		// An open definition represents every constructed form of that definition.
		if (TypeArguments.IsDefaultOrEmpty)
			return true;

		var otherArguments = namedType.TypeArguments;
		if (TypeArguments.Length != otherArguments.Length)
			return false;

		for (var index = 0; index < TypeArguments.Length; index++)
		{
			if (!TypeArguments[index].Matches(otherArguments[index]))
				return false;
		}

		return true;
	}

	/// <summary>
	/// Determines whether the type of the specified symbol is this type.
	/// </summary>
	/// <remarks>
	/// Fields, properties, events, parameters and locals resolve to their declared type; methods resolve to
	/// their return type; aliases resolve to their target. Type symbols are matched directly.
	/// </remarks>
	public bool Matches(ISymbol? other) => Matches(SymbolTypeResolver.Resolve(other));

	/// <summary>
	/// Determines whether the specified <see cref="ITypeSymbol"/> represents this type.
	/// </summary>
	/// <remarks>
	/// An alias for <see cref="Matches(ITypeSymbol?)"/> retained for call-site ergonomics. It is deliberately
	/// not surfaced through <see cref="IEquatable{T}"/>: the relation is asymmetric — an open definition
	/// matches its constructions — and cannot be made consistent with a symbol's own hash code.
	/// </remarks>
	public bool Equals(ITypeSymbol? other) => Matches(other);

	/// <summary>
	/// Determines whether the specified runtime type represents the same semantic type.
	/// </summary>
	public bool Equals(Type? other) => other is not null && TryCreate(other, out var value) && Equals(value);

	/// <summary>
	/// Determines whether the specified structured reference is an unmodified reference to this type.
	/// </summary>
	public bool Equals(TypeReference? other) => other is not null && other.Equals(this);

	/// <summary>
	/// Determines whether the specified value represents the same type.
	/// </summary>
	public bool Equals(TypeIdentity other) =>
		string.Equals(Name, other.Name, StringComparison.Ordinal)
		&& GenericArity == other.GenericArity
		&& SpecialType == other.SpecialType
		&& string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
		&& string.Equals(Keyword, other.Keyword, StringComparison.Ordinal)
		&& ContainingTypesEqual(ContainingTypes, other.ContainingTypes)
		&& TypeArgumentsEqual(TypeArguments, other.TypeArguments);

	/// <summary>
	/// Determines whether the specified value represents the same type, ignoring nullable <i>reference</i>
	/// annotations on generic arguments.
	/// </summary>
	/// <remarks>
	/// Nullable reference annotations are metadata rather than identity, so a provable reference annotation
	/// is ignored. Nullable <i>value</i> types remain significant, so <c>int?</c> is never similar to
	/// <c>int</c>.
	/// </remarks>
	public bool Similar(TypeIdentity other) =>
		string.Equals(Name, other.Name, StringComparison.Ordinal)
		&& GenericArity == other.GenericArity
		&& SpecialType == other.SpecialType
		&& string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
		&& string.Equals(Keyword, other.Keyword, StringComparison.Ordinal)
		&& ContainingTypesEqual(ContainingTypes, other.ContainingTypes)
		&& TypeArgumentsSimilar(TypeArguments, other.TypeArguments);

	/// <summary>
	/// Determines whether the type of the specified symbol is similar to this type, ignoring nullable
	/// reference annotations.
	/// </summary>
	/// <remarks>
	/// An alias for <see cref="Matches(ITypeSymbol?)"/> retained for call-site ergonomics.
	/// </remarks>
	public bool Similar(ITypeSymbol? other) => Matches(other);

	/// <summary>
	/// Determines whether the type of the specified member symbol is similar to this type, ignoring nullable
	/// reference annotations.
	/// </summary>
	/// <remarks>
	/// An alias for <see cref="Matches(ISymbol?)"/> retained for call-site ergonomics.
	/// </remarks>
	public bool Similar(ISymbol? other) => Matches(other);

	/// <summary>
	/// Compares this identity against a reference. Because both types define implicit conversions to one
	/// another, this operator pins the comparison so that <c>identity == reference</c> resolves without the
	/// ambiguity that the two record <c>==</c> operators would otherwise introduce. The reference matches only
	/// when it is an unmodified reference to this identity.
	/// </summary>
	public static bool operator ==(TypeIdentity left, TypeReference? right) =>
		right is not null && right.IsPlainNamedType && right.Identity.Equals(left);

	/// <summary>
	/// Negates <see cref="operator ==(TypeIdentity, TypeReference?)"/>.
	/// </summary>
	public static bool operator !=(TypeIdentity left, TypeReference? right) => !(left == right);

	/// <summary>
	/// Returns a structural hash code for this type, its containing types and its generic arguments.
	/// </summary>
	public override int GetHashCode()
	{
		unchecked
		{
			var hashCode = Name is null ? 0 : StringComparer.Ordinal.GetHashCode(Name);
			hashCode = (hashCode * 397) ^ (Namespace is null ? 0 : StringComparer.Ordinal.GetHashCode(Namespace));
			hashCode = (hashCode * 397) ^ (Keyword is null ? 0 : StringComparer.Ordinal.GetHashCode(Keyword));
			hashCode = (hashCode * 397) ^ (int)SpecialType;
			hashCode = (hashCode * 397) ^ GenericArity;

			if (!ContainingTypes.IsDefaultOrEmpty)
			{
				foreach (var containingType in ContainingTypes)
					hashCode = (hashCode * 397) ^ containingType.GetHashCode();
			}

			if (!TypeArguments.IsDefaultOrEmpty)
			{
				foreach (var argument in TypeArguments)
				{
					if (argument is null)
						continue;
					hashCode = (hashCode * 397) ^ TypeReferenceHash(argument);
				}
			}

			return hashCode;
		}
	}

	// Hashes a type argument by its identity and modifiers without calling TypeReference.GetHashCode, which
	// would re-enter TypeIdentity.GetHashCode and create an unbounded TypeIdentity <-> TypeReference recursion
	// when the arguments are themselves constructed generics.
	static int TypeReferenceHash(TypeReference reference)
	{
		unchecked
		{
			var hashCode = (int)reference.Kind;
			hashCode =
				(hashCode * 397)
				^ (
					reference.TypeParameterName is null
						? 0
						: StringComparer.Ordinal.GetHashCode(reference.TypeParameterName)
				);
			hashCode = (hashCode * 397) ^ reference.Identity.GetHashCode();

			if (!reference.Modifiers.IsDefaultOrEmpty)
			{
				foreach (var modifier in reference.Modifiers)
					hashCode = (hashCode * 397) ^ modifier.GetHashCode();
			}

			return hashCode;
		}
	}

	/// <summary>
	/// Implicitly converts a <see cref="TypeIdentity"/> to its rendered full name.
	/// </summary>
	public static implicit operator string(TypeIdentity typeValueObject) => typeValueObject.RenderFullName;

	// ---------------------------------------------------------------------------------------------
	// Composition
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Creates the canonical source-generation type reference for this type.
	/// </summary>
	public TypeReference AsTypeReference() => new(this);

	/// <summary>
	/// Creates a nullable structured type reference.
	/// </summary>
	public TypeReference MakeNullable() => AsTypeReference().Nullable();

	/// <summary>
	/// Creates a nullable structured type reference, using the specified generation settings to determine whether
	/// nullable context is enabled or unknown.
	/// </summary>
	/// <param name="settings">The generation settings to use.</param>
	/// <returns>The modified type reference.</returns>
	public TypeReference MakeNullable(GenerationSettings settings) => AsTypeReference().Nullable(settings);

	/// <summary>
	/// Creates a nullable structured type reference, using the specified code writer to determine whether
	/// nullable context is enabled or unknown.
	/// </summary>
	/// <param name="writer">The code writer to use.</param>
	/// <returns>The modified type reference.</returns>
	public TypeReference MakeNullable(CodeWriter writer) => AsTypeReference().Nullable(writer);

	/// <summary>
	/// Creates a nullable structured type reference, using the specified compilation to determine whether
	/// nullable context is enabled or unknown.
	/// </summary>
	/// <param name="compilation">The compilation to use.</param>
	/// <returns>The modified type reference.</returns>
	public TypeReference MakeNullable(Compilation compilation) => AsTypeReference().Nullable(compilation);

	/// <summary>
	/// Creates an array structured type reference with the specified rank.
	/// </summary>
	public TypeReference MakeArray(int rank = 1) => AsTypeReference().MakeArray(rank);

	/// <summary>
	/// Creates a pointer structured type reference.
	/// </summary>
	public TypeReference MakePointer() => AsTypeReference().MakePointer();

	/// <summary>
	/// Creates an open generic definition of this type with the specified generic arity, or narrows an
	/// existing value. The returned value carries no type arguments, so it matches every construction of
	/// the type having that arity.
	/// </summary>
	/// <remarks>
	/// This is primarily for types whose arity is a family rather than a fixed shape, such as
	/// <c>Func&lt;TResult&gt;</c> through <c>Func&lt;T1…T16, TResult&gt;</c>:
	/// <c>PurviewTypeLibrary.System.Func.WithArity(17)</c>.
	/// </remarks>
	/// <param name="arity">The number of generic type parameters declared by the type.</param>
	/// <returns>An open generic definition with the specified arity.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when the provided arity is negative.</exception>
	public TypeIdentity WithArity(int arity)
	{
		if (arity < 0)
			throw new ArgumentOutOfRangeException(nameof(arity), arity, "Generic arity must be non-negative.");

		// The new generic arity is the existing arity if already set, otherwise the number of arguments supplied.
		return this with
		{
			GenericArity = arity,
		};
	}

	/// <summary>
	/// Creates a value describing a type nested inside this one.
	/// </summary>
	/// <param name="typeName">The nested type's simple name.</param>
	/// <param name="genericArity">The nested type's own generic arity.</param>
	public TypeIdentity Nested(string typeName, int genericArity = 0)
	{
		if (typeName == null)
			throw new ArgumentNullException(nameof(typeName));

		if (SpecialType != SpecialType.None)
			throw new InvalidOperationException($"Cannot nest a type inside the special type '{SpecialType}'.");

		var existing = ContainingTypes.IsDefaultOrEmpty ? 0 : ContainingTypes.Length;
		var chain = ImmutableArray.CreateBuilder<ContainingType>(existing + 1);

		if (existing > 0)
			chain.AddRange(ContainingTypes);

		chain.Add(new ContainingType(Name, GenericArity));

		return new TypeIdentity
		{
			Name = typeName,
			Namespace = Namespace,
			ContainingTypes = chain.MoveToImmutable(),
			GenericArity = genericArity,
			TypeArguments = [],
		};
	}

	/// <summary>
	/// Creates a generic variant of this type from type argument names.
	/// </summary>
	/// <remarks>
	/// Each string is a literal named type argument, not a wildcard. For example,
	/// <c>MakeGeneric("TKey", "TValue")</c> describes arguments literally named <c>TKey</c> and
	/// <c>TValue</c>; it does not mean “any two arguments.” Leave an identity created from
	/// <c>typeof(Dictionary&lt;,&gt;)</c> unconstructed to match any construction of that definition.
	/// </remarks>
	public TypeIdentity MakeGeneric(params string[] typeArguments)
	{
		if (typeArguments == null)
			throw new ArgumentNullException(nameof(typeArguments));

		var references = new TypeReference[typeArguments.Length];
		for (var index = 0; index < typeArguments.Length; index++)
			references[index] = new TypeReference(new TypeIdentity(typeArguments[index], null));

		return MakeGeneric(references);
	}

	/// <summary>
	/// Creates a constructed generic type using the specified named type arguments.
	/// </summary>
	/// <remarks>
	/// Supplying arguments creates a typed construction used by structural equality and matching. To represent
	/// an open generic definition, do not call this method; construct the identity from an open runtime type or
	/// Roslyn original definition instead.
	/// </remarks>
	public TypeIdentity MakeGeneric(params TypeIdentity[] typeArguments)
	{
		if (typeArguments == null)
			throw new ArgumentNullException(nameof(typeArguments));

		var references = new TypeReference[typeArguments.Length];
		for (var index = 0; index < typeArguments.Length; index++)
			references[index] = typeArguments[index].AsTypeReference();

		return MakeGeneric(references);
	}

	/// <summary>
	/// Creates a constructed generic type using the specified composed type arguments.
	/// </summary>
	/// <remarks>
	/// Use this overload when an argument is itself composed, such as an array, nullable type, pointer, generic
	/// parameter or nested generic construction. Arguments are compared as real type references, not placeholders.
	/// </remarks>
	public TypeIdentity MakeGeneric(params TypeReference[] typeArguments)
	{
		if (typeArguments == null)
			throw new ArgumentNullException(nameof(typeArguments));

		if (typeArguments.Length == 0)
			throw new ArgumentException("At least one type argument must be provided.", nameof(typeArguments));

		if (GenericArity > 0 && typeArguments.Length != GenericArity)
		{
			throw new ArgumentException(
				$"Type '{MetadataFullName}' requires {GenericArity} type arguments, but {typeArguments.Length} were supplied.",
				nameof(typeArguments)
			);
		}

		if (SpecialType != SpecialType.None)
			throw new InvalidOperationException($"Cannot create a generic type from the special type '{SpecialType}'.");

		// The new generic arity is the existing arity if already set, otherwise the number of arguments supplied.
		return this with
		{
			GenericArity = GenericArity == 0 ? typeArguments.Length : GenericArity,
			TypeArguments = [.. typeArguments],
		};
	}

	// ---------------------------------------------------------------------------------------------
	// Factories
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Gets an empty <see cref="TypeIdentity"/>.
	/// </summary>
	public static readonly TypeIdentity Empty;

	/// <summary>
	/// The C# <c>null</c> literal, presented as an identity for use in value positions.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>null</c> is a literal value, not a named type, so this identity renders as the bare keyword
	/// <c>null</c> and never matches a symbol — Roslyn exposes no type for the null literal. Prefer it in
	/// value positions that accept an expression: initializers, default values, arguments and return
	/// values, where the implicit conversion to <see cref="string"/> yields <c>"null"</c>.
	/// </para>
	/// <para>
	/// This is distinct from <see cref="Empty"/>, which represents the absence of a type and must not be
	/// emitted. A null literal in a <i>type</i> position is rejected by the <see cref="CodeWriter"/>
	/// validators.
	/// </para>
	/// </remarks>
	public static readonly TypeIdentity Null = new("null", null);

	/// <summary>
	/// Creates a <see cref="TypeIdentity"/> from a runtime type.
	/// </summary>
	/// <remarks>
	/// Runtime <see cref="Type"/> values do not retain nullable-reference annotations. To represent
	/// <c>string?</c>, create the <c>string</c> value then call <see cref="MakeNullable()"/>, or use
	/// <see cref="TypeReference.TryCreate(ITypeSymbol?, out TypeReference)"/> when working
	/// with Roslyn symbols.
	/// </remarks>
	public static TypeIdentity Create<T>() => new(typeof(T));

	/// <summary>
	/// Attempts to create a <see cref="TypeIdentity"/> from a type symbol, returning <see langword="false"/>
	/// for symbols that cannot be represented.
	/// </summary>
	/// <remarks>
	/// Prefer this inside generator and analyzer pipelines, where array, pointer, type-parameter and unresolved
	/// error symbols are routine and must not throw.
	/// </remarks>
	public static bool TryCreate(ITypeSymbol? typeSymbol, out TypeIdentity value)
	{
		if (typeSymbol is INamedTypeSymbol namedType && IsRepresentable(namedType))
		{
			value = new TypeIdentity(namedType);

			return true;
		}

		value = Empty;

		return false;
	}

	/// <summary>
	/// Attempts to create a <see cref="TypeIdentity"/> from a runtime type, returning
	/// <see langword="false"/> for types that cannot be represented.
	/// </summary>
	public static bool TryCreate(Type? type, out TypeIdentity value)
	{
		if (type is not null && IsRepresentable(type))
		{
			value = new TypeIdentity(type);

			return true;
		}

		value = Empty;

		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Internals
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines whether a symbol is a resolvable named type this value object can describe.
	/// </summary>
	static bool IsRepresentable(ITypeSymbol? typeSymbol) =>
		typeSymbol is INamedTypeSymbol
		&& typeSymbol.TypeKind
			is not (
				TypeKind.Error
				or TypeKind.Dynamic
				or TypeKind.Pointer
				or TypeKind.FunctionPointer
				or TypeKind.Array
				or TypeKind.TypeParameter
				or TypeKind.Submission
			);

	static bool IsRepresentable(Type type) =>
		!type.IsArray && !type.IsPointer && !type.IsByRef && !type.IsGenericParameter;

	/// <summary>
	/// Builds a dotted namespace string from a namespace symbol chain in a single allocation.
	/// </summary>
	/// <remarks>
	/// Replaces <c>ContainingNamespace.ToDisplayString()</c>, which routes through Roslyn's full symbol-display
	/// machinery — a per-symbol cost that dominates construction in a generator processing thousands of types.
	/// </remarks>
	static string? BuildNamespace(INamespaceSymbol? @namespace)
	{
		if (@namespace is null || @namespace.IsGlobalNamespace)
			return null;

		// Overwhelmingly the common case: a single segment, or a namespace whose name is already interned.
		if (@namespace.ContainingNamespace is null or { IsGlobalNamespace: true })
			return @namespace.Name;

		// Measure, then fill back-to-front. One char[] and one string, no intermediate concatenation.
		var length = -1;
		for (
			var segment = @namespace;
			segment is not null && !segment.IsGlobalNamespace;
			segment = segment.ContainingNamespace
		)
			length += segment.Name.Length + 1;

		if (length <= 0)
			return null;

		var characters = new char[length];
		var position = length;

		for (
			var segment = @namespace;
			segment is not null && !segment.IsGlobalNamespace;
			segment = segment.ContainingNamespace
		)
		{
			var name = segment.Name;
			position -= name.Length;
			name.CopyTo(0, characters, position, name.Length);

			if (position > 0)
				characters[--position] = '.';
		}

		return new string(characters);
	}

	/// <summary>
	/// Compares a dotted namespace string against a namespace symbol chain without allocating.
	/// </summary>
	static bool NamespaceMatches(string? expected, INamespaceSymbol? actual)
	{
		if (actual is null || actual.IsGlobalNamespace)
			return expected is null;

		if (expected is null)
			return false;

		var end = expected.Length;
		for (
			var segment = actual;
			segment is not null && !segment.IsGlobalNamespace;
			segment = segment.ContainingNamespace
		)
		{
			var name = segment.Name;
			var start = end - name.Length;

			if (start < 0 || string.CompareOrdinal(expected, start, name, 0, name.Length) != 0)
				return false;

			if (start == 0)
				return segment.ContainingNamespace is null or { IsGlobalNamespace: true };

			if (expected[start - 1] != '.')
				return false;

			end = start - 1;
		}

		return false;
	}

	/// <summary>
	/// Compares the expected containing-type chain against a symbol chain without allocating.
	/// </summary>
	bool ContainingTypesMatch(INamedTypeSymbol? containingType)
	{
		// Fast path: neither side is nested, which is the majority of comparisons.
		if (containingType is null)
			return ContainingTypes.IsDefaultOrEmpty;

		if (ContainingTypes.IsDefaultOrEmpty)
			return false;

		// Walk the symbol chain innermost-first against the expected chain in reverse.
		var index = ContainingTypes.Length - 1;
		for (var symbol = containingType; symbol is not null; symbol = symbol.ContainingType, index--)
		{
			if (index < 0)
				return false;

			var expected = ContainingTypes[index];
			if (expected.GenericArity != symbol.Arity)
				return false;

			if (!string.Equals(expected.Name, symbol.Name, StringComparison.Ordinal))
				return false;
		}

		return index == -1;
	}

	static bool ContainingTypesEqual(ImmutableArray<ContainingType> left, ImmutableArray<ContainingType> right)
	{
		var leftCount = left.IsDefaultOrEmpty ? 0 : left.Length;
		var rightCount = right.IsDefaultOrEmpty ? 0 : right.Length;

		if (leftCount != rightCount)
			return false;

		for (var index = 0; index < leftCount; index++)
		{
			if (!left[index].Equals(right[index]))
				return false;
		}

		return true;
	}

	static bool TypeArgumentsEqual(ImmutableArray<TypeReference> left, ImmutableArray<TypeReference> right)
	{
		var leftCount = left.IsDefaultOrEmpty ? 0 : left.Length;
		var rightCount = right.IsDefaultOrEmpty ? 0 : right.Length;

		if (leftCount != rightCount)
			return false;

		for (var index = 0; index < leftCount; index++)
		{
			if (!TypeReferenceEqual(left[index], right[index]))
				return false;
		}

		return true;
	}

	static bool TypeArgumentsSimilar(ImmutableArray<TypeReference> left, ImmutableArray<TypeReference> right)
	{
		var leftCount = left.IsDefaultOrEmpty ? 0 : left.Length;
		var rightCount = right.IsDefaultOrEmpty ? 0 : right.Length;

		if (leftCount != rightCount)
			return false;

		for (var index = 0; index < leftCount; index++)
		{
			if (!TypeReferenceSimilar(left[index], right[index]))
				return false;
		}

		return true;
	}

	// Compares a type argument by its identity and modifiers without calling TypeReference.Equals, which would
	// re-enter TypeIdentity.Equals and create an unbounded TypeIdentity <-> TypeReference recursion when the
	// arguments are themselves constructed generics.
	static bool TypeReferenceEqual(TypeReference left, TypeReference right)
	{
		if (ReferenceEquals(left, right))
			return true;

		if (left.Kind != right.Kind)
			return false;

		if (!string.Equals(left.TypeParameterName, right.TypeParameterName, StringComparison.Ordinal))
			return false;

		if (left.Kind == TypeReferenceKind.Named && !left.Identity.Equals(right.Identity))
			return false;

		// Nullable reference annotations are metadata rather than identity, so a provable reference annotation is
		return TypeReferenceModifiersEqual(left.Modifiers, right.Modifiers);
	}

	static bool TypeReferenceSimilar(TypeReference left, TypeReference right)
	{
		if (ReferenceEquals(left, right))
			return true;

		if (left.Kind != right.Kind)
			return false;

		if (!string.Equals(left.TypeParameterName, right.TypeParameterName, StringComparison.Ordinal))
			return false;

		if (left.Kind == TypeReferenceKind.Named && !left.Identity.Similar(right.Identity))
			return false;

		// Nullable reference annotations are metadata rather than identity, so a provable reference annotation is
		return TypeReferenceModifiersSimilar(left.Modifiers, right.Modifiers);
	}

	static bool TypeReferenceModifiersEqual(ImmutableArray<TypeModifier> left, ImmutableArray<TypeModifier> right)
	{
		var leftCount = left.IsDefaultOrEmpty ? 0 : left.Length;
		var rightCount = right.IsDefaultOrEmpty ? 0 : right.Length;

		if (leftCount != rightCount)
			return false;

		for (var index = 0; index < leftCount; index++)
		{
			if (!left[index].Equals(right[index]))
				return false;
		}

		return true;
	}

	static bool TypeReferenceModifiersSimilar(ImmutableArray<TypeModifier> left, ImmutableArray<TypeModifier> right)
	{
		var leftCount = left.IsDefaultOrEmpty ? 0 : left.Length;
		var rightCount = right.IsDefaultOrEmpty ? 0 : right.Length;

		var index = 0;
		var otherIndex = 0;
		while (index < leftCount || otherIndex < rightCount)
		{
			var modifier = index < leftCount ? left[index] : (TypeModifier?)null;
			var otherModifier = otherIndex < rightCount ? right[otherIndex] : (TypeModifier?)null;

			if (modifier is { Kind: TypeModifierKind.Nullable, NullableKind: NullableModifierKind.Reference })
			{
				index++;
				continue;
			}

			if (otherModifier is { Kind: TypeModifierKind.Nullable, NullableKind: NullableModifierKind.Reference })
			{
				otherIndex++;
				continue;
			}

			if (modifier is null || otherModifier is null)
				return false;

			if (
				modifier.Value.Kind != otherModifier.Value.Kind
				|| modifier.Value.Rank != otherModifier.Value.Rank
				|| modifier.Value.NullableKind != otherModifier.Value.NullableKind
			)
				return false;

			index++;
			otherIndex++;
		}

		return true;
	}

	/// <summary>
	/// Builds the containing-type chain from a symbol: one length pass, one fill pass, one allocation.
	/// </summary>
	static ImmutableArray<ContainingType> BuildContainingTypes(INamedTypeSymbol typeSymbol)
	{
		var containing = typeSymbol.ContainingType;
		if (containing is null)
			return [];

		var depth = 0;
		for (var symbol = containing; symbol is not null; symbol = symbol.ContainingType)
			depth++;

		var builder = ImmutableArray.CreateBuilder<ContainingType>(depth);
		builder.Count = depth;

		// The symbol chain runs innermost-first; the stored chain is outermost-first, so fill backwards
		// rather than appending and reversing.
		var index = depth - 1;
		for (var symbol = containing; symbol is not null; symbol = symbol.ContainingType)
			builder[index--] = new ContainingType(symbol.Name, symbol.Arity);

		return builder.MoveToImmutable();
	}

	/// <summary>
	/// Builds the containing-type chain from a runtime type, and reports how many generic arguments the
	/// containing types consume from the flattened argument list.
	/// </summary>
	/// <remarks>
	/// Arity is parsed from the metadata name rather than obtained from <c>GetGenericArguments()</c>, which
	/// allocates a <see cref="Type"/> array on every call and was previously invoked twice per link.
	/// </remarks>
	static ImmutableArray<ContainingType> BuildContainingTypes(Type type, out int consumed)
	{
		consumed = 0;

		var declaring = type.DeclaringType;
		if (declaring is null)
			return [];

		var depth = 0;
		for (var link = declaring; link is not null; link = link.DeclaringType)
			depth++;

		var builder = ImmutableArray.CreateBuilder<ContainingType>(depth);
		builder.Count = depth;

		var index = depth - 1;
		for (var link = declaring; link is not null; link = link.DeclaringType)
		{
			var arity = ParseArity(link.Name);
			builder[index--] = new ContainingType(StripArity(link.Name), arity);
			consumed += arity;
		}

		return builder.MoveToImmutable();
	}

	static ImmutableArray<TypeReference> BuildArguments(INamedTypeSymbol typeSymbol)
	{
		var arguments = typeSymbol.TypeArguments;
		if (arguments.Length == 0)
			return [];

		// An unbound or original definition carries its own type parameters as arguments; treat it as open.
		if (
			typeSymbol.IsUnboundGenericType
			|| SymbolEqualityComparer.Default.Equals(typeSymbol, typeSymbol.OriginalDefinition)
		)
			return [];

		var builder = ImmutableArray.CreateBuilder<TypeReference>(arguments.Length);
		foreach (var argument in arguments)
		{
			// Only genuinely unrepresentable arguments (function pointers, error types) widen to the definition.
			if (!TypeReference.TryCreate(argument, out var value))
				return [];

			builder.Add(value);
		}

		return builder.MoveToImmutable();
	}

	static ImmutableArray<TypeReference> BuildArguments(Type[] allArguments, int offset, int count)
	{
		if (count == 0 || allArguments.Length < offset + count)
			return [];

		var builder = ImmutableArray.CreateBuilder<TypeReference>(count);
		for (var index = offset; index < offset + count; index++)
		{
			if (!TypeReference.TryCreate(allArguments[index], out var value))
				return [];

			builder.Add(value);
		}

		return builder.MoveToImmutable();
	}

	/// <summary>
	/// Reads the generic arity encoded in a CLR metadata name's backtick suffix.
	/// </summary>
	/// <remarks>
	/// The suffix records the type's <i>own</i> arity, excluding parameters inherited from containing types —
	/// <c>Outer&lt;T&gt;.Inner</c> is <c>Inner</c> and <c>Outer&lt;T&gt;.Inner&lt;U&gt;</c> is <c>Inner`1</c> —
	/// which is exactly the value needed, and it costs no allocation.
	/// </remarks>
	static int ParseArity(string metadataName)
	{
		var separator = metadataName.LastIndexOf('`');
		if (separator < 0 || separator == metadataName.Length - 1)
			return 0;

		var arity = 0;
		for (var index = separator + 1; index < metadataName.Length; index++)
		{
			var character = metadataName[index];
			if (character is < '0' or > '9')
				return 0;

			arity = (arity * 10) + (character - '0');
		}

		return arity;
	}

	static string StripArity(string metadataName)
	{
		var separator = metadataName.IndexOf('`');

		return separator < 0 ? metadataName : metadataName.Substring(0, separator);
	}
}
