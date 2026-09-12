using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Shared discovery for extension classes (classic static <c>this</c>-parameter methods and C# 14
/// <c>extension(...)</c> blocks): whether a type is an extension class, which receiver types it extends,
/// and the expected name/namespace/folder for a well-formed, coherent extension shape.
/// </summary>
static class ExtensionClassDiscovery
{
	public const string FolderName = "Extensions";

	/// <summary>
	/// Determines whether the type is an extension class: a static type containing at least one classic
	/// <c>this</c> method or one method inside an <c>extension(...)</c> block.
	/// </summary>
	public static bool IsExtensionClass(INamedTypeSymbol type) =>
		type.IsStatic && type.GetMembers().Any(member => member is IMethodSymbol method && IsExtensionMethod(method));

	/// <summary>
	/// Determines whether the method is an extension member: a classic <c>this</c> method or a method
	/// declared inside an <c>extension(...)</c> block.
	/// </summary>
	public static bool IsExtensionMethod(IMethodSymbol method) =>
		method.IsExtensionMethod || IsDeclaredInExtensionBlock(method);

	/// <summary>
	/// Determines whether the method is declared inside an <c>extension(...)</c> block.
	/// </summary>
	public static bool IsDeclaredInExtensionBlock(IMethodSymbol method) =>
		method.DeclaringSyntaxReferences.Any(static reference =>
			reference.GetSyntax() is BaseMethodDeclarationSyntax declaration
			&& declaration.Ancestors().OfType<ExtensionBlockDeclarationSyntax>().Any()
		);

	/// <summary>
	/// Resolves the receiver types a class extends, deduplicated by symbol equality. Classic <c>this</c>
	/// methods expose the receiver as their first parameter; extension-block methods do not, so their
	/// receiver is resolved from the block's syntax.
	/// </summary>
	public static ImmutableArray<ITypeSymbol> ResolveReceivers(INamedTypeSymbol type, Compilation compilation)
	{
		var distinct = ImmutableArray.CreateBuilder<ITypeSymbol>();
		foreach (var member in type.GetMembers())
		{
			if (member is not IMethodSymbol method)
				continue;

			ITypeSymbol? receiver = null;
			if (method.IsExtensionMethod && method.Parameters.Length > 0)
			{
				receiver = GetNamingReceiver(method.Parameters[0].Type);
			}
			else if (IsDeclaredInExtensionBlock(method))
			{
				receiver = ResolveBlockReceiver(method, compilation);
			}

			if (receiver is null)
				continue;

			var isDuplicate = distinct.Any(existing => SymbolEqualityComparer.Default.Equals(existing, receiver));
			if (!isDuplicate)
				distinct.Add(receiver);
		}

		return distinct.ToImmutable();
	}

	/// <summary>
	/// Resolves the naming receiver of an <c>extension(...)</c> block containing the method.
	/// </summary>
	static ITypeSymbol? ResolveBlockReceiver(IMethodSymbol method, Compilation compilation)
	{
		foreach (var reference in method.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax() is not BaseMethodDeclarationSyntax declaration)
				continue;

			var block = declaration.Ancestors().OfType<ExtensionBlockDeclarationSyntax>().FirstOrDefault();
			if (block is null || block.ParameterList?.Parameters.FirstOrDefault() is not { Type: { } receiverType })
				continue;

			var semanticModel = compilation.GetSemanticModel(declaration.SyntaxTree);
			var receiver = semanticModel.GetTypeInfo(receiverType).Type;
			return receiver is null ? null : GetNamingReceiver(receiver);
		}

		return null;
	}

	/// <summary>
	/// For generic receivers (<c>extension&lt;T&gt;(T)</c>) the class is named after the constraint type;
	/// otherwise the receiver type itself is used.
	/// </summary>
	static ITypeSymbol? GetNamingReceiver(ITypeSymbol receiver)
	{
		if (receiver is not ITypeParameterSymbol typeParameter || typeParameter.ConstraintTypes.Length == 0)
			return receiver;

		var typeConstraint = typeParameter.ConstraintTypes.OfType<INamedTypeSymbol>().FirstOrDefault();
		return typeConstraint ?? receiver;
	}

	/// <summary>
	/// The canonical spelling of well-known acronyms, applied when deriving the expected extension class
	/// name so that e.g. <c>SQL</c> produces <c>SqlExtensions</c> while <c>API</c> and <c>AI</c> keep their
	/// uppercase forms (<c>APIExtensions</c>, <c>AIExtensions</c>).
	/// </summary>
	static readonly System.Collections.Generic.Dictionary<string, string> AcronymCanonicalNames = new(
		System.StringComparer.OrdinalIgnoreCase
	)
	{
		{ "SQL", "Sql" },
		{ "GUID", "Guid" },
		{ "UUID", "Uuid" },
		{ "URL", "Url" },
		{ "API", "API" },
		{ "AI", "AI" },
		{ "XML", "Xml" },
		{ "HTML", "Html" },
		{ "HTTP", "Http" },
		{ "HTTPS", "Https" },
		{ "JSON", "Json" },
		{ "ASP", "Asp" },
		{ "DNS", "Dns" },
		{ "FTP", "Ftp" },
		{ "SMTP", "Smtp" },
		{ "SSH", "Ssh" },
		{ "CPU", "Cpu" },
		{ "GPU", "Gpu" },
		{ "RAM", "Ram" },
	};

	public static string ExpectedClassName(ITypeSymbol receiver) => ToCanonicalName(receiver.Name) + "Extensions";

	/// <summary>
	/// Normalizes well-known acronyms in an identifier to their canonical spelling by replacing each word
	/// (an uppercase run such as <c>SQL</c> or a title-case word such as <c>Api</c>) that matches a known
	/// acronym (for example <c>SQLValue</c> becomes <c>SqlValue</c> and <c>ApiClient</c> becomes
	/// <c>APIClient</c>). Unknown words and already-canonical names are left unchanged.
	/// </summary>
	public static string ToCanonicalName(string name)
	{
		System.Text.StringBuilder result = new(name.Length);
		var index = 0;
		while (index < name.Length)
		{
			if (!char.IsUpper(name[index]))
			{
				result.Append(name[index]);
				index++;
				continue;
			}

			var end = index;
			if (index + 1 < name.Length && char.IsUpper(name[index + 1]))
			{
				// An uppercase run such as API, SQL, or GUID.
				while (end < name.Length && char.IsUpper(name[end]))
					end++;
			}
			else
			{
				// A title-case word such as Api, Sql, or Value.
				end++;
				while (end < name.Length && char.IsLower(name[end]))
					end++;
			}

			var word = name.Substring(index, end - index);
			result.Append(AcronymCanonicalNames.TryGetValue(word, out var canonical) ? canonical : word);
			index = end;
		}

		return result.ToString();
	}

	public static string ExpectedNamespace(ITypeSymbol receiver) =>
		receiver.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : string.Empty;

	/// <summary>
	/// The expected folder path segments under the <c>Extensions</c> root, derived from the receiver's
	/// namespace (for example <c>["Extensions", "Microsoft", "CodeAnalysis"]</c>).
	/// </summary>
	public static ImmutableArray<string> ExpectedFolderSegments(ITypeSymbol receiver)
	{
		var builder = ImmutableArray.CreateBuilder<string>();
		builder.Add(FolderName);
		if (receiver.ContainingNamespace is { IsGlobalNamespace: false } ns)
		{
			foreach (var segment in ns.ToDisplayString().Split('.'))
				builder.Add(segment);
		}

		return builder.ToImmutable();
	}

	/// <summary>
	/// Determines whether the compilation's language version supports C# 14 <c>extension</c> blocks.
	/// </summary>
	public static bool SupportsExtensionKeyword(Compilation compilation) =>
		compilation is CSharpCompilation csharpCompilation
		&& csharpCompilation.LanguageVersion >= LanguageVersion.CSharp14;
}
