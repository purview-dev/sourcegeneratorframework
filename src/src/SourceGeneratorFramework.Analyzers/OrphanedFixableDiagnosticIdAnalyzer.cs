using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.SourceGeneratorFramework.Analyzers;

/// <summary>
/// Flags <c>[ExportCodeFixProvider]</c> types whose <c>FixableDiagnosticIds</c> reference a
/// diagnostic ID that no analyzer in the same compilation produces. Because the compiler host only
/// shows fixes for diagnostics an analyzer actually reports, such a fixer is never offered.
/// </summary>
/// <remarks>
/// The rule is deliberately scoped to co-located analyzers: it only fires when the compilation
/// contains at least one <c>[DiagnosticAnalyzer]</c> in source, so fixers that target analyzers
/// supplied by a referenced assembly are never false-flagged.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OrphanedFixableDiagnosticIdAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "PSGFR28";

	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Code fixer targets a diagnostic no analyzer produces",
		"Code fixer '{0}' fixes diagnostic '{1}', which is not produced by any analyzer in this compilation; the fix will never be shown in Visual Studio",
		"Purview.SourceGeneratorFramework",
		DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Visual Studio only offers a code fix when the analyzer that produces the diagnostic is loaded. A FixableDiagnosticIds entry that no analyzer in this compilation produces is dead configuration.",
		customTags: [WellKnownDiagnosticTags.CompilationEnd]
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationAction(AnalyzeCompilation);
	}

	static void AnalyzeCompilation(CompilationAnalysisContext context)
	{
		HashSet<string> descriptorIds = new(StringComparer.Ordinal);
		List<(string TypeName, Location Location, string Id)> fixerTargets = [];
		var hasSourceAnalyzer = false;

		foreach (var tree in context.Compilation.SyntaxTrees)
		{
			var root = tree.GetRoot(context.CancellationToken);

			foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
			{
				var attributeNames = GetAttributeNames(typeDeclaration).ToList();
				if (attributeNames.Contains("ExportCodeFixProvider"))
				{
					foreach (var (location, id) in GetFixableDiagnosticIds(typeDeclaration))
						fixerTargets.Add((typeDeclaration.Identifier.Text, location, id));
				}

				if (!hasSourceAnalyzer && attributeNames.Contains("DiagnosticAnalyzer"))
					hasSourceAnalyzer = true;
			}

			foreach (var node in root.DescendantNodes())
			{
				string? id = null;

				if (node is ObjectCreationExpressionSyntax { ArgumentList: not null } objectCreation)
				{
					if (objectCreation.Type is not NameSyntax name || !IsDiagnosticDescriptorName(name))
						continue;

					id = GetFirstStringArgumentId(objectCreation.ArgumentList);
				}
				else if (
					node is ImplicitObjectCreationExpressionSyntax { ArgumentList: not null } implicitCreation
					&& IsDiagnosticDescriptorDeclaration(implicitCreation)
				)
				{
					id = GetFirstStringArgumentId(implicitCreation.ArgumentList);
				}

				if (id is not null)
					descriptorIds.Add(id);
			}
		}

		if (!hasSourceAnalyzer)
			return;

		foreach (var (typeName, location, id) in fixerTargets)
		{
			if (descriptorIds.Contains(id))
				continue;

			context.ReportDiagnostic(Diagnostic.Create(Rule, location, typeName, id));
		}
	}

	static bool IsDiagnosticDescriptorName(NameSyntax name)
	{
		var simpleName = name switch
		{
			IdentifierNameSyntax identifier => identifier.Identifier.Text,
			QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
			AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
			_ => name.ToString(),
		};

		return simpleName == "DiagnosticDescriptor";
	}

	static string? GetFirstStringArgumentId(ArgumentListSyntax argumentList) =>
		argumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
		&& literal.Kind() == SyntaxKind.StringLiteralExpression
		&& literal.Token.Value is string id
			? id
			: null;

	/// <summary>
	/// True when the target-typed creation is assigned to a field, property, or local declared as
	/// <c>DiagnosticDescriptor</c>, e.g. <c>static readonly DiagnosticDescriptor Rule = new(...)</c>.
	/// </summary>
	static bool IsDiagnosticDescriptorDeclaration(ImplicitObjectCreationExpressionSyntax creation)
	{
		var variableDeclaration = creation.FirstAncestorOrSelf<VariableDeclarationSyntax>();
		if (variableDeclaration is null)
			return false;

		var typeName = variableDeclaration.Type.ToString();
		return typeName == "DiagnosticDescriptor"
			|| typeName.EndsWith(".DiagnosticDescriptor", StringComparison.Ordinal);
	}

	static IEnumerable<string> GetAttributeNames(MemberDeclarationSyntax declaration)
	{
		foreach (var attributeList in declaration.AttributeLists)
		{
			foreach (var attribute in attributeList.Attributes)
			{
				var name = attribute.Name switch
				{
					IdentifierNameSyntax identifier => identifier.Identifier.Text,
					QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
					AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
					_ => attribute.Name.ToString(),
				};

				yield return name;
			}
		}
	}

	static IEnumerable<(Location Location, string Id)> GetFixableDiagnosticIds(TypeDeclarationSyntax typeDeclaration)
	{
		foreach (var member in typeDeclaration.Members)
		{
			if (member is not PropertyDeclarationSyntax { Identifier.Text: "FixableDiagnosticIds" } property)
				continue;

			foreach (var literal in property.DescendantNodes().OfType<LiteralExpressionSyntax>())
			{
				if (literal.Kind() == SyntaxKind.StringLiteralExpression && literal.Token.Value is string id)
					yield return (literal.GetLocation(), id);
			}
		}
	}
}
