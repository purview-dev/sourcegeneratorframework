using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Adds the missing extension-class metadata: <c>[EditorBrowsable(EditorBrowsableState.Never)]</c> on the
/// class and the <c>System.ComponentModel</c> using when required.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddExtensionClassMetadataCodeFixProvider))]
public sealed class AddExtensionClassMetadataCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "AddExtensionClassMetadata";

	public override ImmutableArray<string> FixableDiagnosticIds => ["PSGFR38"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			if (root.FindNode(diagnostic.Location.SourceSpan) is not TypeDeclarationSyntax typeDeclaration)
				continue;

			if (!typeDeclaration.IsKind(SyntaxKind.ClassDeclaration))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Add extension class metadata",
					_ => AddMetadataAsync(context.Document, root, typeDeclaration),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static Task<Document> AddMetadataAsync(Document document, SyntaxNode root, TypeDeclarationSyntax typeDeclaration)
	{
		if (root is not CompilationUnitSyntax compilationUnit)
			return Task.FromResult(document);

		var hasEditorBrowsable = typeDeclaration
			.AttributeLists.SelectMany(static list => list.Attributes)
			.Any(static attribute => attribute.Name.ToString().Contains("EditorBrowsable", StringComparison.Ordinal));
		var hasComponentModelUsing = compilationUnit.Usings.Any(static usingDirective =>
			usingDirective.Name?.ToString() == "System.ComponentModel"
		);

		var updatedClass = typeDeclaration;
		if (!hasEditorBrowsable)
		{
			var attributeList = SyntaxFactory
				.AttributeList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Attribute(
							SyntaxFactory.ParseName("EditorBrowsable"),
							SyntaxFactory.ParseAttributeArgumentList("(EditorBrowsableState.Never)")
						)
					)
				)
				.NormalizeWhitespace()
				.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

			updatedClass = updatedClass.WithAttributeLists(updatedClass.AttributeLists.Insert(0, attributeList));
		}

		var updatedUnit = compilationUnit.ReplaceNode(typeDeclaration, updatedClass);

		if (!hasComponentModelUsing)
		{
			updatedUnit = updatedUnit.AddUsings(
				SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.ComponentModel")).NormalizeWhitespace()
			);
		}

		return Task.FromResult(document.WithSyntaxRoot(updatedUnit));
	}
}
