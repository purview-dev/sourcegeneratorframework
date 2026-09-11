using System.Globalization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Generators.Helpers;

static class TypeLibraryModelLibrary
{
	public static IncrementalValuesProvider<GeneratorResult<TypeLibraryModel>> GetTypeLibraryTargetPipeline(
		IncrementalGeneratorInitializationContext context
	)
	{
		// The framework PurviewTypeLibrary shape is fixed for a given compilation, so it is walked once per
		// compilation and cached as a value-equatable model instead of being re-walked for every
		// [GenerateTypeLibrary] spec in the compilation.
		var frameworkTree = context
			.CompilationProvider.Select(static (compilation, _) => BuildFrameworkTree(compilation))
			.WithTrackingName("GetFrameworkTypeLibraryTree");

		return IncrementalPipeline
			.ForAttributeWithMetadataName(
				context,
				GeneratorTypeLibrary.Attirbutes.GenerateTypeLibraryAttribute,
				static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol,
				predicate: static (ctx, _) => ctx is ClassDeclarationSyntax
			)
			.CombineWith(frameworkTree, static (specSymbol, tree, ct) => BuildTarget(specSymbol, tree, ct))
			.WithTrackingName("GetTypeLibraryTargets");
	}

	static GeneratorResult<TypeLibraryModel> BuildTarget(
		INamedTypeSymbol specSymbol,
		EquatableArray<TypeLibraryNamespaceNode> frameworkTree,
		CancellationToken cancellationToken
	)
	{
		// Validation diagnostics are reported by TypeLibraryValidationAnalyzer; the generator carries them
		// on the result so it can gate generation on ShouldProcess without emitting the diagnostics itself.
		List<ReportableDiagnostic> diagnostics = [];
		var generateAttribute = GetAttribute(specSymbol, GeneratorTypeLibrary.Attirbutes.GenerateTypeLibraryAttribute);
		if (generateAttribute is null)
			return GeneratorResult<TypeLibraryModel>.Empty;

		var className = GetNamedArgument(generateAttribute, "ClassName", (string?)null) ?? "TypeLibrary";
		var outputNamespace = GetNamedArgument(generateAttribute, "Namespace", (string?)null);
		var specDocumentation = ExtractDocumentation(specSymbol);

		List<NamespaceNodeBuilder> root = [];
		Dictionary<string, NamespaceNodeBuilder> nodeLookup = new(StringComparer.Ordinal);

		// The generated type library inherits the full framework PurviewTypeLibrary shape, so every
		// framework member is present before the user's [TypeRef] members are merged in. The shape was
		// already walked (once per compilation) by the GetFrameworkTypeLibraryTree stage.
		AddFrameworkTree(frameworkTree, root, nodeLookup);

		foreach (var field in specSymbol.GetMembers().OfType<IFieldSymbol>())
		{
			cancellationToken.ThrowIfCancellationRequested();

			var typeRef = GetAttribute(field, GeneratorTypeLibrary.Attirbutes.TypeRefAttribute);
			if (typeRef is null)
				continue;

			var isTypeIdentity = string.Equals(field.Type.Name, "TypeIdentity", StringComparison.Ordinal);
			var isTypeReference = string.Equals(field.Type.Name, "TypeReference", StringComparison.Ordinal);
			var documentation = ExtractDocumentation(field);
			var includeInGetTypes = GetIncludeInGetTypes(typeRef);

			TypeLibraryMemberModel member;

			if (isTypeReference)
			{
				var initializer = ReadInitializerExpression(field, cancellationToken);
				if (initializer is null)
				{
					diagnostics.Add(
						ReportableDiagnostic.Create(
							TypeLibraryDiagnosticRules.ReferenceMemberMissingInitializer,
							isBlocking: true,
							field,
							field.Name
						)
					);
					continue;
				}

				if (field.DeclaredAccessibility != Accessibility.Internal)
				{
					diagnostics.Add(
						ReportableDiagnostic.Create(
							TypeLibraryDiagnosticRules.MemberAccessibilityInvalid,
							isBlocking: true,
							field,
							field.Name
						)
					);
					continue;
				}

				var placementNamespace = GetPlacementNamespace(typeRef);
				member = new(
					field.Name,
					null,
					null,
					0,
					initializer,
					IsTypeReference: true,
					documentation,
					includeInGetTypes
				);
				AddToTree(root, nodeLookup, placementNamespace ?? string.Empty, member);
			}
			else if (isTypeIdentity && HasRealInitializer(field, cancellationToken))
			{
				// An initialised TypeIdentity follows the same rules as a TypeReference value member.
				var initializer = ReadInitializerExpression(field, cancellationToken);
				if (initializer is null)
				{
					diagnostics.Add(
						ReportableDiagnostic.Create(
							TypeLibraryDiagnosticRules.ReferenceMemberMissingInitializer,
							isBlocking: true,
							field,
							field.Name
						)
					);
					continue;
				}

				if (field.DeclaredAccessibility != Accessibility.Internal)
				{
					diagnostics.Add(
						ReportableDiagnostic.Create(
							TypeLibraryDiagnosticRules.MemberAccessibilityInvalid,
							isBlocking: true,
							field,
							field.Name
						)
					);
					continue;
				}

				var placementNamespace = GetPlacementNamespace(typeRef);
				member = new(
					field.Name,
					null,
					null,
					0,
					initializer,
					IsTypeReference: false,
					documentation,
					includeInGetTypes
				);
				AddToTree(root, nodeLookup, placementNamespace ?? string.Empty, member);
			}
			else if (isTypeIdentity)
			{
				// A plain TypeIdentity is a generation marker: private accessibility.
				if (field.DeclaredAccessibility != Accessibility.Private)
				{
					diagnostics.Add(
						ReportableDiagnostic.Create(
							TypeLibraryDiagnosticRules.MemberAccessibilityInvalid,
							isBlocking: true,
							field,
							field.Name
						)
					);
					continue;
				}

				if (HasNoInitializer(field, cancellationToken))
				{
					diagnostics.Add(
						ReportableDiagnostic.Create(
							TypeLibraryDiagnosticRules.MarkerMissingDefaultInitializer,
							isBlocking: false,
							field,
							field.Name
						)
					);
				}

				var before = diagnostics.Count;
				var (typeName, memberNamespace, genericArity) = ReadTypeRef(typeRef, field, diagnostics);
				if (diagnostics.Count > before)
					continue;

				member = new(
					field.Name,
					typeName,
					memberNamespace,
					genericArity,
					null,
					IsTypeReference: false,
					documentation,
					includeInGetTypes
				);
				AddToTree(root, nodeLookup, memberNamespace, member);
			}
			else
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						TypeLibraryDiagnosticRules.MemberTypeInvalid,
						isBlocking: true,
						field,
						field.Name,
						field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
					)
				);
				continue;
			}
		}

		if (diagnostics.Any(d => d.IsBlocking))
			return GeneratorResult<TypeLibraryModel>.Create([.. diagnostics]);

		TypeLibraryModel model = new(
			Specifier: specSymbol.ContainingNamespace.IsGlobalNamespace
				? specSymbol.Name
				: $"{specSymbol.ContainingNamespace.ToDisplayString()}.{specSymbol.Name}",
			SpecClassName: specSymbol.Name,
			SpecNamespace: specSymbol.ContainingNamespace.IsGlobalNamespace
				? null
				: specSymbol.ContainingNamespace.ToDisplayString(),
			SpecAccessibility: specSymbol.DeclaredAccessibility.ToTypeDeclarationAccessibility(),
			Namespace: string.IsNullOrWhiteSpace(outputNamespace) ? null : outputNamespace,
			ClassName: className,
			Documentation: specDocumentation,
			Namespaces: new EquatableArray<TypeLibraryNamespaceNode>([
				.. root.OrderBy(static n => n.Name, StringComparer.Ordinal).Select(static n => n.ToModel()),
			])
		);

		return GeneratorResult<TypeLibraryModel>.Create(
			model,
			System.Collections.Immutable.ImmutableArray.CreateRange(diagnostics)
		);
	}

	/// <summary>
	/// Resolves the placement namespace for a value member from the <c>[TypeRef]</c> namespace argument
	/// or named property.
	/// </summary>
	static string? GetPlacementNamespace(AttributeData typeRef) =>
		GetNamedArgument(typeRef, "Namespace", (string?)null)
		?? GetConstructorArgument(typeRef, 0, (string?)null)
		?? GetConstructorArgument(typeRef, 1, (string?)null);

	/// <summary>
	/// Resolves the <c>IncludeInGetTypes</c> flag from the named property, the named constructor argument,
	/// or the positional <c>includeInGetTypes</c> constructor argument (the final parameter of either ctor).
	/// </summary>
	static bool GetIncludeInGetTypes(AttributeData typeRef)
	{
		var named =
			GetNamedArgument(typeRef, "IncludeInGetTypes", (bool?)null)
			?? GetNamedArgument(typeRef, "includeInGetTypes", (bool?)null);
		if (named is not null)
			return named.Value;

		if (typeRef.AttributeConstructor is { } constructor)
		{
			for (var index = 0; index < constructor.Parameters.Length; index++)
			{
				if (constructor.Parameters[index].Name == "includeInGetTypes")
					return index < typeRef.ConstructorArguments.Length
						&& typeRef.ConstructorArguments[index].Value is true;
			}
		}

		return false;
	}

	/// <summary>
	/// Walks the framework <c>PurviewTypeLibrary</c> nested namespace classes and their members once,
	/// producing a value-equatable model that is cached by the <c>GetFrameworkTypeLibraryTree</c> pipeline
	/// stage. Each member is emitted as an alias reference to the framework value so arity and generic
	/// construction are preserved exactly.
	/// </summary>
	static EquatableArray<TypeLibraryNamespaceNode> BuildFrameworkTree(Compilation compilation)
	{
		var frameworkType = compilation.GetTypeByMetadataName("Purview.SourceGeneratorFramework.PurviewTypeLibrary");
		if (frameworkType is null)
			return EquatableArray<TypeLibraryNamespaceNode>.Empty;

		List<NamespaceNodeBuilder> root = [];
		Dictionary<string, NamespaceNodeBuilder> lookup = new(StringComparer.Ordinal);

		foreach (var nestedType in frameworkType.GetTypeMembers())
			AddFrameworkNode(root, lookup, nestedType);

		return new EquatableArray<TypeLibraryNamespaceNode>([
			.. root.OrderBy(static n => n.Name, StringComparer.Ordinal).Select(static n => n.ToModel()),
		]);
	}

	static void AddFrameworkNode(
		List<NamespaceNodeBuilder> nodes,
		Dictionary<string, NamespaceNodeBuilder> lookup,
		INamedTypeSymbol nestedType
	)
	{
		var namespaceConst =
			nestedType
				.GetMembers()
				.OfType<IFieldSymbol>()
				.FirstOrDefault(static field => field.IsConst && field.ConstantValue is string)
				?.ConstantValue as string
			?? nestedType.Name;

		var node = GetOrAddNode(nodes, lookup, namespaceConst, nestedType.Name);

		var nestedDisplayName = nestedType.ToDisplayString();
		foreach (var member in nestedType.GetMembers().OfType<IFieldSymbol>())
		{
			if (member.IsConst)
				continue;

			var reference = $"global::{nestedDisplayName}.{member.Name}";
			var isTypeReference = string.Equals(member.Type.Name, "TypeReference", StringComparison.Ordinal);
			node.Members.Add(new TypeLibraryMemberModel(member.Name, null, null, 0, reference, isTypeReference, null));
		}

		foreach (var child in nestedType.GetTypeMembers())
			AddFrameworkNode(node.Children, lookup, child);
	}

	/// <summary>
	/// Materializes the cached framework tree into mutable builder nodes so user <c>[TypeRef]</c> members can
	/// be merged in without any further semantic queries.
	/// </summary>
	static void AddFrameworkTree(
		EquatableArray<TypeLibraryNamespaceNode> frameworkTree,
		List<NamespaceNodeBuilder> root,
		Dictionary<string, NamespaceNodeBuilder> lookup
	)
	{
		foreach (var node in frameworkTree)
			AddFrameworkNode(root, lookup, node);
	}

	static void AddFrameworkNode(
		List<NamespaceNodeBuilder> nodes,
		Dictionary<string, NamespaceNodeBuilder> lookup,
		TypeLibraryNamespaceNode node
	)
	{
		var built = GetOrAddNode(nodes, lookup, node.NamespaceValue, node.Name);
		foreach (var member in node.Members)
			built.Members.Add(member);

		foreach (var child in node.Children)
			AddFrameworkNode(built.Children, lookup, child);
	}

	static void AddToTree(
		List<NamespaceNodeBuilder> root,
		Dictionary<string, NamespaceNodeBuilder> lookup,
		string @namespace,
		TypeLibraryMemberModel member
	)
	{
		NamespaceNodeBuilder? leaf = null;
		var accumulated = string.Empty;
		var nodes = root;
		foreach (var segment in @namespace.Split('.'))
		{
			accumulated = accumulated.Length == 0 ? segment : accumulated + "." + segment;
			leaf = GetOrAddNode(nodes, lookup, accumulated, segment);
			nodes = leaf.Children;
		}

		// A user member shadows an inherited framework member of the same name in the same leaf.
		leaf!.Members.RemoveAll(m => m.MemberName == member.MemberName);
		leaf.Members.Add(member);
	}

	static NamespaceNodeBuilder GetOrAddNode(
		List<NamespaceNodeBuilder> nodes,
		Dictionary<string, NamespaceNodeBuilder> lookup,
		string namespaceValue,
		string segment
	)
	{
		if (lookup.TryGetValue(namespaceValue, out var existing))
			return existing;

		NamespaceNodeBuilder node = new(segment, namespaceValue);
		lookup.Add(namespaceValue, node);
		nodes.Add(node);
		return node;
	}

	static (string TypeName, string Namespace, int Arity) ReadTypeRef(
		AttributeData typeRef,
		IFieldSymbol field,
		List<ReportableDiagnostic> diagnostics
	)
	{
		var fieldName = field.Name;
		var namedNamespace = GetNamedArgument(typeRef, "Namespace", (string?)null);
		var namedArity = GetNamedArgument(typeRef, "Arity", -1);

		// The namespace-only overload declares a single string parameter; when used, the type name
		// defaults to the member (field) name.
		var isNamespaceOnlyForm =
			typeRef.AttributeConstructor is { Parameters.Length: > 0 }
			&& typeRef.AttributeConstructor.Parameters[0].Type.SpecialType == SpecialType.System_String;

		if (isNamespaceOnlyForm)
		{
			var memberName = fieldName;
			var memberNamespace = namedNamespace ?? GetConstructorArgument(typeRef, 0, (string?)null);
			if (string.IsNullOrWhiteSpace(memberNamespace))
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						TypeLibraryDiagnosticRules.MemberTypeNotResolved,
						isBlocking: true,
						field,
						fieldName
					)
				);
				return (memberName, string.Empty, 0);
			}

			var memberArity = namedArity != -1 ? namedArity : GetConstructorArgument(typeRef, 1, 0);
			if (memberArity < 0)
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						TypeLibraryDiagnosticRules.MemberTypeNotResolved,
						isBlocking: true,
						field,
						fieldName
					)
				);
				return (memberName, memberNamespace!, 0);
			}

			return (memberName, memberNamespace!, memberArity);
		}

		if (typeRef.ConstructorArguments.Length == 0)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					TypeLibraryDiagnosticRules.MemberTypeNotResolved,
					isBlocking: true,
					field,
					fieldName
				)
			);
			return (string.Empty, string.Empty, 0);
		}

		var typeArgument = typeRef.ConstructorArguments[0];
		string typeName;
		string? inferredNamespace = null;
		int? detectedArity;

#pragma warning disable format
		switch (typeArgument.Value)
		{
			case ITypeSymbol typeSymbol:
			{
				if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
				{
					diagnostics.Add(
						ReportableDiagnostic.Create(
							TypeLibraryDiagnosticRules.MemberTypeNotResolved,
							isBlocking: true,
							field,
							fieldName
						)
					);
					return (typeSymbol.Name, string.Empty, 0);
				}

				typeName = typeSymbol.Name;
				inferredNamespace = typeSymbol.ContainingNamespace.ToDisplayString();
				detectedArity = (typeSymbol as INamedTypeSymbol)?.Arity;
				break;
			}
			case string typeNameString:
			{
				(typeName, detectedArity) = ParseStringTypeName(typeNameString);
				break;
			}
			default:
				diagnostics.Add(
					ReportableDiagnostic.Create(
						TypeLibraryDiagnosticRules.MemberTypeNotResolved,
						isBlocking: true,
						field,
						fieldName
					)
				);
				return (string.Empty, string.Empty, 0);
		}
#pragma warning restore format

		var resolvedNamespace =
			namedNamespace ?? GetConstructorArgument(typeRef, 1, (string?)null) ?? inferredNamespace;
		if (string.IsNullOrWhiteSpace(resolvedNamespace))
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					TypeLibraryDiagnosticRules.MemberTypeNotResolved,
					isBlocking: true,
					field,
					fieldName
				)
			);
			return (typeName, string.Empty, 0);
		}

		var explicitArity = namedArity != -1 ? namedArity : GetConstructorArgument(typeRef, 2, -1);
		if (explicitArity is < 0 and not -1)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					TypeLibraryDiagnosticRules.MemberTypeNotResolved,
					isBlocking: true,
					field,
					fieldName
				)
			);
			return (typeName, resolvedNamespace!, 0);
		}

		var arity = explicitArity >= 0 ? explicitArity : (detectedArity ?? 0);
		return (typeName, resolvedNamespace!, arity);
	}

	/// <summary>
	/// Parses a type-name string, extracting any CLR generic-arity suffix such as
	/// <c>List`1</c> into the arity.
	/// </summary>
	static (string TypeName, int Arity) ParseStringTypeName(string typeName)
	{
		var backtick = typeName.IndexOf('`');
		if (backtick < 0 || backtick == typeName.Length - 1)
			return (typeName, 0);

		if (int.TryParse(typeName.Substring(backtick + 1), out var arity))
			return (typeName.Substring(0, backtick), arity);

		return (typeName, 0);
	}

	/// <summary>
	/// Determines whether the field declares a meaningful value initializer rather than a plain
	/// <c>default</c> marker.
	/// </summary>
	static bool HasRealInitializer(IFieldSymbol field, CancellationToken cancellationToken)
	{
		foreach (var reference in field.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator)
				return !IsDefaultExpression(declarator.Initializer?.Value);
		}

		return false;
	}

	/// <summary>
	/// Determines whether the field declares no initializer at all, rather than an explicit
	/// <c>= default</c> marker.
	/// </summary>
	static bool HasNoInitializer(IFieldSymbol field, CancellationToken cancellationToken)
	{
		foreach (var reference in field.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator)
				return declarator.Initializer is null;
		}

		return false;
	}

	static bool IsDefaultExpression(ExpressionSyntax? expression) =>
		expression switch
		{
			null => true,
			LiteralExpressionSyntax { RawKind: (int)SyntaxKind.DefaultLiteralExpression } => true,
			PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppression =>
				IsDefaultExpression(suppression.Operand),
			DefaultExpressionSyntax => true,
			_ => false,
		};

	/// <summary>
	/// Reads a field's initializer expression, rewriting bare <c>PurviewTypeLibrary</c> references to
	/// the fully-qualified framework type so the copied expression resolves inside the generated class.
	/// </summary>
	static string? ReadInitializerExpression(IFieldSymbol field, CancellationToken cancellationToken)
	{
		foreach (var reference in field.DeclaringSyntaxReferences)
		{
			if (
				reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator
				&& declarator.Initializer?.Value is { } value
			)
			{
				var rewritten = (ExpressionSyntax)new PurviewTypeLibraryReferenceRewriter().Visit(value);
				return rewritten.NormalizeWhitespace().ToString();
			}
		}

		return null;
	}

	/// <summary>
	/// Captures a symbol's XML documentation, returning the inner elements (summary, remarks, ...)
	/// without the surrounding <c>&lt;member&gt;</c> wrapper.
	/// </summary>
	static string? ExtractDocumentation(ISymbol symbol)
	{
		var xml = symbol.GetDocumentationCommentXml();
		if (string.IsNullOrWhiteSpace(xml))
			return null;

		try
		{
			var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
			var elements = document.Root?.Elements().ToList();
			if (elements is null || elements.Count == 0)
				return null;

			// Return the inner XML of the <member> element, preserving whitespace and formatting.
			var result = string.Join(
					"\n",
					elements.Select(static element => element.ToString(SaveOptions.DisableFormatting))
				)
				.Trim();

			return result.Length == 0 ? null : result;
		}
		catch
		{
			return null;
		}
	}

	sealed class PurviewTypeLibraryReferenceRewriter : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
		{
			// Only rewrite PurviewTypeLibrary when used as a standalone value or the root of a member
			// access, never as a member-access name such as Foo.PurviewTypeLibrary.
			var isMemberAccessName =
				node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node;

			if (!isMemberAccessName && node.Identifier.ValueText == "PurviewTypeLibrary")
			{
				return SyntaxFactory
					.ParseExpression("global::Purview.SourceGeneratorFramework.PurviewTypeLibrary")
					.WithTriviaFrom(node);
			}

			// If the identifier is not a standalone PurviewTypeLibrary, continue visiting its children.
			return base.VisitIdentifierName(node);
		}
	}

	static AttributeData? GetAttribute(ISymbol symbol, TypeIdentity attributeType)
	{
		foreach (var attribute in symbol.GetAttributes())
		{
			if (attribute.AttributeClass is not null && attributeType.Equals(attribute.AttributeClass))
				return attribute;
		}

		return null;
	}

	static T? GetConstructorArgument<T>(AttributeData attributeData, int index, T? defaultValue)
	{
		if (index < 0 || index >= attributeData.ConstructorArguments.Length)
			return defaultValue;

		var value = attributeData.ConstructorArguments[index].Value;
		if (value is T typedValue)
			return typedValue;
		if (value is null)
			return defaultValue;

		try
		{
			return (T?)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
		}
		catch
		{
			return defaultValue;
		}
	}

	static T? GetNamedArgument<T>(AttributeData attributeData, string name, T? defaultValue)
	{
		foreach (var arg in attributeData.NamedArguments)
		{
			if (arg.Key != name)
				continue;

			var value = arg.Value.Value;
			if (value is T typedValue)
				return typedValue;

			if (value is null)
				return defaultValue;

			try
			{
				return (T?)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
			}
			catch
			{
				return defaultValue;
			}
		}
		return defaultValue;
	}

	sealed class NamespaceNodeBuilder(string name, string namespaceValue)
	{
		public string Name { get; } = name;

		public string NamespaceValue { get; } = namespaceValue;

		public List<TypeLibraryMemberModel> Members { get; } = [];

		public List<NamespaceNodeBuilder> Children { get; } = [];

		public TypeLibraryNamespaceNode ToModel() =>
			new(
				Name,
				NamespaceValue,
				new EquatableArray<TypeLibraryMemberModel>([.. Members]),
				new EquatableArray<TypeLibraryNamespaceNode>([.. Children.Select(static c => c.ToModel())])
			);
	}
}
