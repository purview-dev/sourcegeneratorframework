using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Adds an explicit <c>= default</c> initializer to a type library marker member that lacks one,
/// making the inert <c>TypeIdentity</c> marker explicit (fixes <c>TLB0010</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TypeLibraryMarkerDefaultInitializerCodeFixProvider))]
public sealed class TypeLibraryMarkerDefaultInitializerCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "AddDefaultInitializer";

	public override ImmutableArray<string> FixableDiagnosticIds => ["TLB0010"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var node = root.FindNode(diagnostic.Location.SourceSpan);
			if (node.FirstAncestorOrSelf<VariableDeclaratorSyntax>() is not { Initializer: null } declarator)
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Add '= default'",
					_ => AddDefaultInitializerAsync(context.Document, declarator, context.CancellationToken),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static async Task<Document> AddDefaultInitializerAsync(
		Document document,
		VariableDeclaratorSyntax declarator,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var initializer = SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression("default"));
		var updated = declarator.WithInitializer(initializer);

		return document.WithSyntaxRoot(root.ReplaceNode(declarator, updated));
	}
}
