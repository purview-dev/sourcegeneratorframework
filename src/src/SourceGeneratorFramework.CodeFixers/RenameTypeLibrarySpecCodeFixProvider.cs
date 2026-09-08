using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Renames a <c>[GenerateTypeLibrary]</c> spec class so it no longer collides with the generated
/// type library class of the same name (fixes <c>TLB0012</c> and <c>TLB0013</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RenameTypeLibrarySpecCodeFixProvider))]
public sealed class RenameTypeLibrarySpecCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "RenameSpec";

	public override ImmutableArray<string> FixableDiagnosticIds => ["TLB0012", "TLB0013"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var node = root.FindNode(diagnostic.Location.SourceSpan);
			if (node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } typeDeclaration)
				continue;

			var newName = SuggestName(typeDeclaration.Identifier.Text);

			context.RegisterCodeFix(
				CodeAction.Create(
					$"Rename to '{newName}'",
					_ => RenameSpecAsync(context.Document, typeDeclaration, newName, context.CancellationToken),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static string SuggestName(string currentName) =>
		currentName.EndsWith("Generator", StringComparison.Ordinal) ? currentName + "Spec" : currentName + "Generator";

	static async Task<Solution> RenameSpecAsync(
		Document document,
		TypeDeclarationSyntax typeDeclaration,
		string newName,
		CancellationToken cancellationToken
	)
	{
		var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		if (semanticModel?.GetDeclaredSymbol(typeDeclaration, cancellationToken) is not { } symbol)
			return document.Project.Solution;

		// Use the Renamer API to rename the symbol and update all references in the solution.
		return await Renamer
			.RenameSymbolAsync(document.Project.Solution, symbol, new SymbolRenameOptions(), newName, cancellationToken)
			.ConfigureAwait(false);
	}
}
