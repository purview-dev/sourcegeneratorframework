using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Syntax-level matching for <see cref="TypeIdentity"/> and <see cref="TypeReference"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two tiers are provided, mirroring the incremental generator pipeline. The <c>CouldMatch*</c> methods are
/// purely syntactic and are intended for the <c>predicate</c> stage, where no <see cref="SemanticModel"/> is
/// available and the work must be cheap. The <c>Matches*</c> methods resolve symbols and are intended for the
/// <c>transform</c> stage.
/// </para>
/// <para>
/// <b>Directionality.</b> <c>CouldMatch*</c> over-approximates for type <i>references</i>: it may return
/// <see langword="true"/> for a node that does not resolve to this type, and it cannot see <c>using</c>
/// aliases, so a reference written through an alias is not recognised. Where that matters, predicate on syntax
/// kind alone and filter with <c>Matches*</c> in the transform. <c>CouldMatchDeclaration</c> has no such
/// limitation — a declaration's name, arity, containing types and namespace are fully determined by syntax.
/// </para>
/// </remarks>
public static class TypeSyntaxMatchingExtensions
{
	// ---------------------------------------------------------------------------------------------
	// TypeValueObject — references
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines, without a semantic model, whether the node could be a reference to this type.
	/// </summary>
	public static bool CouldMatchTypeReference(this in TypeIdentity type, SyntaxNode? node)
	{
		if (node is not TypeSyntax typeSyntax)
			return false;

		if (!TypeSyntaxFacts.TryGetCore(typeSyntax, out var core, out var modifiers))
			return false;

		// A bare named type identity accepts no array or pointer composition.
		return modifiers is { Count: > 0 } ? false : CoreMatchesNamedType(type, core);
	}

	/// <summary>
	/// Determines whether the node resolves to this type.
	/// </summary>
	public static bool MatchesTypeReference(
		this in TypeIdentity type,
		SyntaxNode? node,
		SemanticModel semanticModel,
		CancellationToken cancellationToken = default
	) => type.Matches(TypeSyntaxFacts.ResolveType(node, semanticModel, cancellationToken));

	// ---------------------------------------------------------------------------------------------
	// TypeReferenceOptions — references
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines, without a semantic model, whether the node could be this composed reference.
	/// </summary>
	/// <remarks>
	/// Nullable modifiers are ignored on both sides: syntax alone cannot distinguish
	/// <c>Nullable&lt;int&gt;</c> from a nullable reference annotation. Array ranks and pointer depth are
	/// compared exactly.
	/// </remarks>
	public static bool CouldMatchTypeReference(this TypeReference? reference, SyntaxNode? node)
	{
		if (reference is null || node is not TypeSyntax typeSyntax)
			return false;

		if (!TypeSyntaxFacts.TryGetCore(typeSyntax, out var core, out var written))
			return false;

		// Compare the significant modifiers, outermost-first on both sides. This runs in the predicate stage
		// for every candidate node, so the comparison is done in place rather than by materialising a list.
		var modifiers = reference.Modifiers;
		var expectedCount = 0;

		if (!modifiers.IsDefaultOrEmpty)
		{
			foreach (var modifier in modifiers)
			{
				if (modifier.Kind != TypeModifierKind.Nullable)
					expectedCount++;
			}
		}

		var writtenCount = written?.Count ?? 0;
		if (expectedCount != writtenCount)
			return false;

		if (expectedCount > 0)
		{
			// Stored innermost-first, written outermost-first, so walk the stored side in reverse.
			var writtenIndex = 0;
			for (var index = modifiers.Length - 1; index >= 0; index--)
			{
				var modifier = modifiers[index];
				if (modifier.Kind == TypeModifierKind.Nullable)
					continue;

				var actual = written![writtenIndex++];
				if (modifier.Kind != actual.Kind || modifier.Rank != actual.Rank)
					return false;
			}
		}

#pragma warning disable IDE0072 // Add missing cases
		return reference.Kind switch
		{
			TypeReferenceKind.Named => CoreMatchesNamedType(reference.Identity, core),
			TypeReferenceKind.TypeParameter => core is IdentifierNameSyntax identifier
				&& string.Equals(
					reference.TypeParameterName,
					identifier.Identifier.ValueText,
					StringComparison.Ordinal
				),
			TypeReferenceKind.Dynamic => core is IdentifierNameSyntax { Identifier.ValueText: "dynamic" },
			_ => false,
		};
#pragma warning restore IDE0072 // Add missing cases
	}

	/// <summary>
	/// Determines whether the node resolves to this composed reference.
	/// </summary>
	public static bool MatchesTypeReference(
		this TypeReference? reference,
		SyntaxNode? node,
		SemanticModel semanticModel,
		CancellationToken cancellationToken = default
	) => reference?.Matches(TypeSyntaxFacts.ResolveType(node, semanticModel, cancellationToken)) ?? false;

	// ---------------------------------------------------------------------------------------------
	// Declarations
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines, without a semantic model, whether the node is the declaration of this type.
	/// </summary>
	/// <remarks>
	/// Exact for declarations. Accepts <c>class</c>, <c>struct</c>, <c>interface</c>, <c>record</c>,
	/// <c>record struct</c>, <c>enum</c> and <c>delegate</c> declarations.
	/// </remarks>
	public static bool CouldMatchDeclaration(this in TypeIdentity type, SyntaxNode? node)
	{
		if (node is null)
			return false;

		if (!TypeSyntaxFacts.TryGetDeclarationName(node, out var identifier, out var arity))
			return false;

		if (!string.Equals(type.Name, identifier, StringComparison.Ordinal))
			return false;

		if (type.GenericArity != arity)
			return false;

		if (!DeclaredContainingTypesMatch(type, node))
			return false;

		// The namespace is the only part of a declaration that cannot be determined from the node alone, so
		return string.Equals(type.Namespace, TypeSyntaxFacts.GetDeclaredNamespace(node), StringComparison.Ordinal);
	}

	/// <summary>
	/// Determines whether the node declares this type.
	/// </summary>
	public static bool MatchesDeclaration(
		this in TypeIdentity type,
		SyntaxNode? node,
		SemanticModel semanticModel,
		CancellationToken cancellationToken = default
	)
	{
		if (node is not MemberDeclarationSyntax declaration)
			return false;

		if (semanticModel == null)
			throw new ArgumentNullException(nameof(semanticModel));

		// The declared symbol is always a named type, so the cast is safe.
		return type.Matches(semanticModel.GetDeclaredSymbol(declaration, cancellationToken) as ITypeSymbol);
	}

	// ---------------------------------------------------------------------------------------------
	// Members
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines whether the declared member's type is this type: the declared type for fields, properties,
	/// events and parameters, and the return type for methods.
	/// </summary>
	public static bool MatchesDeclaredType(
		this in TypeIdentity type,
		SyntaxNode? node,
		SemanticModel semanticModel,
		CancellationToken cancellationToken = default
	) => type.Matches(TypeSyntaxFacts.ResolveDeclaredSymbol(node, semanticModel, cancellationToken));

	/// <summary>
	/// Determines whether the declared member's type is this composed reference.
	/// </summary>
	public static bool MatchesDeclaredType(
		this TypeReference? reference,
		SyntaxNode? node,
		SemanticModel semanticModel,
		CancellationToken cancellationToken = default
	) => reference?.Matches(TypeSyntaxFacts.ResolveDeclaredSymbol(node, semanticModel, cancellationToken)) ?? false;

	// ---------------------------------------------------------------------------------------------
	// Attributes
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Determines, without a semantic model, whether the attribute could be an application of this type.
	/// </summary>
	/// <remarks>
	/// The <c>Attribute</c> suffix is optional at the application site, so both spellings are accepted.
	/// </remarks>
	public static bool CouldMatchAttribute(this in TypeIdentity type, SyntaxNode? node)
	{
		var name = node switch
		{
			AttributeSyntax attribute => attribute.Name,
			NameSyntax nameSyntax => nameSyntax,
			_ => null,
		};

		if (name is null)
			return false;

		TypeSyntaxFacts.Split(name, out var simpleName, out var qualifier);

		var written = simpleName.Identifier.ValueText;
		var matchesName =
			string.Equals(type.Name, written, StringComparison.Ordinal)
			|| string.Equals(type.Name, written + TypeSyntaxFacts.AttributeSuffix, StringComparison.Ordinal);

		if (!matchesName)
			return false;

		if (type.GenericArity != TypeSyntaxFacts.GetArity(simpleName))
			return false;

		// The qualifier is optional, so the match is always possible if none is written.
		return QualifierMatches(type, qualifier);
	}

	/// <summary>
	/// Determines whether the attribute application resolves to this attribute type.
	/// </summary>
	public static bool MatchesAttribute(
		this in TypeIdentity type,
		SyntaxNode? node,
		SemanticModel semanticModel,
		CancellationToken cancellationToken = default
	)
	{
		if (semanticModel == null)
			throw new ArgumentNullException(nameof(semanticModel));

		// The attribute application is always a method symbol, so the cast is safe.
		return node switch
		{
			AttributeSyntax attribute => type.Matches(
				(semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol as IMethodSymbol)?.ContainingType
					?? semanticModel.GetTypeInfo(attribute, cancellationToken).Type
			),
			AttributeListSyntax list => MatchesAny(type, list.Attributes, semanticModel, cancellationToken),
			_ => false,
		};
	}

	/// <summary>
	/// Determines whether this type is applied as an attribute anywhere on the given declaration.
	/// </summary>
	public static bool HasAttribute(
		this in TypeIdentity type,
		MemberDeclarationSyntax? declaration,
		SemanticModel semanticModel,
		CancellationToken cancellationToken = default
	)
	{
		if (declaration is null)
			return false;

		foreach (var list in declaration.AttributeLists)
		{
			if (MatchesAny(type, list.Attributes, semanticModel, cancellationToken))
				return true;
		}

		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Shared
	// ---------------------------------------------------------------------------------------------

	static bool MatchesAny(
		in TypeIdentity type,
		SeparatedSyntaxList<AttributeSyntax> attributes,
		SemanticModel semanticModel,
		CancellationToken cancellationToken
	)
	{
		foreach (var attribute in attributes)
		{
			if (type.MatchesAttribute(attribute, semanticModel, cancellationToken))
				return true;
		}

		return false;
	}

	static bool CoreMatchesNamedType(in TypeIdentity type, TypeSyntax core)
	{
		if (core is PredefinedTypeSyntax predefined)
		{
			var specialType = TypeSyntaxFacts.GetSpecialType(predefined.Keyword.Kind());

			return specialType != SpecialType.None && specialType == type.SpecialType;
		}

		if (core is not NameSyntax name)
			return false;

		TypeSyntaxFacts.Split(name, out var simpleName, out var qualifier);

		if (!string.Equals(type.Name, simpleName.Identifier.ValueText, StringComparison.Ordinal))
			return false;

		if (type.GenericArity != TypeSyntaxFacts.GetArity(simpleName))
			return false;

		// The qualifier is optional, so the match is always possible if none is written.
		return QualifierMatches(type, qualifier);
	}

	/// <summary>
	/// Compares the written left-hand qualifier of a name against the type's namespace and containing types.
	/// </summary>
	static bool QualifierMatches(in TypeIdentity type, NameSyntax? qualifier)
	{
		// An unqualified reference is always possible via a using directive or the containing scope.
		if (qualifier is null)
			return true;

		var expected = BuildExpectedQualifier(type);

		var written = ImmutableArray.CreateBuilder<string>();
		var rooted = TypeSyntaxFacts.CollectQualifierSegments(qualifier, written);

		if (written.Count > expected.Count)
			return false;

		if (rooted && written.Count != expected.Count)
			return false;

		// The written qualifier must be a trailing run of the full qualifier: with `using System.Collections;`
		// in scope, `Generic.List<int>` is a legal spelling.
		var offset = expected.Count - written.Count;
		for (var index = 0; index < written.Count; index++)
		{
			if (!string.Equals(expected[offset + index], written[index], StringComparison.Ordinal))
				return false;
		}

		return true;
	}

	static List<string> BuildExpectedQualifier(in TypeIdentity type)
	{
		List<string> segments = [];

		if (type.Namespace is { Length: > 0 } @namespace)
		{
			var start = 0;
			for (var index = 0; index <= @namespace.Length; index++)
			{
				if (index != @namespace.Length && @namespace[index] != '.')
					continue;

				segments.Add(@namespace.Substring(start, index - start));
				start = index + 1;
			}
		}

		if (!type.ContainingTypes.IsDefaultOrEmpty)
		{
			foreach (var containingType in type.ContainingTypes)
				segments.Add(containingType.Name);
		}

		return segments;
	}

	static bool DeclaredContainingTypesMatch(in TypeIdentity type, SyntaxNode node)
	{
		var expectedCount = type.ContainingTypes.IsDefaultOrEmpty ? 0 : type.ContainingTypes.Length;

		var index = expectedCount - 1;
		for (var parent = node.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is not TypeDeclarationSyntax containing)
				continue;

			if (index < 0)
				return false;

			var expected = type.ContainingTypes[index];
			if (
				!string.Equals(expected.Name, containing.Identifier.ValueText, StringComparison.Ordinal)
				|| expected.GenericArity != (containing.TypeParameterList?.Parameters.Count ?? 0)
			)
				return false;

			index--;
		}

		return index == -1;
	}
}
