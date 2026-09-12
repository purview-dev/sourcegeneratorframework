using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Purview.SourceGeneratorFramework.Analyzers;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Reorganizes extension classes into a well-formed, coherent shape: renames the class to
/// <c>{Receiver}Extensions</c>, splits a class extending multiple receiver types into one class per type,
/// and moves the class under an <c>Extensions</c> folder whose path and namespace match the extended type.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReorganizeExtensionClassCodeFixProvider))]
public sealed class ReorganizeExtensionClassCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "ReorganizeExtensionClass";
	internal const string RenameEquivalenceKey = "ReorganizeExtensionClass-Rename";
	internal const string SplitEquivalenceKey = "ReorganizeExtensionClass-Split";
	internal const string MoveEquivalenceKey = "ReorganizeExtensionClass-Move";

	public override ImmutableArray<string> FixableDiagnosticIds =>
		[
			ExtensionClassOrganizationAnalyzer.NameDiagnosticId,
			ExtensionClassOrganizationAnalyzer.PlacementDiagnosticId,
			ExtensionClassOrganizationAnalyzer.SplitDiagnosticId,
		];

	public override FixAllProvider? GetFixAllProvider() => null;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var semanticModel = await context
			.Document.GetSemanticModelAsync(context.CancellationToken)
			.ConfigureAwait(false);
		if (root is null || semanticModel is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			if (root.FindNode(diagnostic.Location.SourceSpan) is not ClassDeclarationSyntax classDeclaration)
				continue;

			if (classDeclaration.Ancestors().OfType<TypeDeclarationSyntax>().Any())
				continue;

			if (
				semanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken)
				is not INamedTypeSymbol type
			)
				continue;

			if (!ExtensionClassDiscovery.IsExtensionClass(type))
				continue;

			var receivers = ExtensionClassDiscovery.ResolveReceivers(type, semanticModel.Compilation);
			if (receivers.Length == 0)
				continue;

			var primary = receivers[0];
			var expectedName = ExtensionClassDiscovery.ExpectedClassName(primary);

			if (diagnostic.Id == ExtensionClassOrganizationAnalyzer.NameDiagnosticId)
			{
				context.RegisterCodeFix(
					CodeAction.Create(
						$"Rename to {expectedName}",
						ct => RenameAsync(context.Document, type, expectedName, ct),
						RenameEquivalenceKey
					),
					diagnostic
				);
			}
			else if (diagnostic.Id == ExtensionClassOrganizationAnalyzer.SplitDiagnosticId)
			{
				context.RegisterCodeFix(
					CodeAction.Create(
						"Split into per-type extension classes",
						ct => SplitAsync(context.Document, classDeclaration, semanticModel, receivers, ct),
						SplitEquivalenceKey
					),
					diagnostic
				);
			}
			else if (diagnostic.Id == ExtensionClassOrganizationAnalyzer.PlacementDiagnosticId)
			{
				context.RegisterCodeFix(
					CodeAction.Create(
						$"Move to {expectedName} in namespace {ExtensionClassDiscovery.ExpectedNamespace(primary)}",
						ct => MoveAsync(context.Document, classDeclaration, semanticModel, type, primary, ct),
						MoveEquivalenceKey
					),
					diagnostic
				);
			}
		}
	}

	static Task<Solution> RenameAsync(
		Document document,
		INamedTypeSymbol symbol,
		string newName,
		CancellationToken cancellationToken
	) =>
		Renamer.RenameSymbolAsync(
			document.Project.Solution,
			symbol,
			new SymbolRenameOptions(),
			newName,
			cancellationToken
		);

	static async Task<Solution> SplitAsync(
		Document document,
		ClassDeclarationSyntax classDeclaration,
		SemanticModel semanticModel,
		ImmutableArray<ITypeSymbol> receivers,
		CancellationToken cancellationToken
	)
	{
		var solution = document.Project.Solution;
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
			return solution;

		var primaryKey = ReceiverKey(receivers[0]);
		List<MemberDeclarationSyntax> remainingMembers = [];
		foreach (var member in classDeclaration.Members)
		{
			if (
				member is MethodDeclarationSyntax method
				&& TryGetReceiverKey(method, semanticModel, cancellationToken, out var methodKey)
				&& methodKey != primaryKey
			)
				continue;
			if (
				member is ExtensionBlockDeclarationSyntax block
				&& TryGetReceiverKey(block, semanticModel, cancellationToken, out var blockKey)
				&& blockKey != primaryKey
			)
				continue;

			remainingMembers.Add(member);
		}

		foreach (var receiver in receivers.Skip(1))
		{
			var receiverKey = ReceiverKey(receiver);
			var members = classDeclaration
				.Members.Where(member =>
					(
						member is MethodDeclarationSyntax method
						&& TryGetReceiverKey(method, semanticModel, cancellationToken, out var methodKey)
						&& methodKey == receiverKey
					)
					|| (
						member is ExtensionBlockDeclarationSyntax block
						&& TryGetReceiverKey(block, semanticModel, cancellationToken, out var blockKey)
						&& blockKey == receiverKey
					)
				)
				.ToList();
			if (members.Count == 0)
				continue;

			var className = ExtensionClassDiscovery.ExpectedClassName(receiver);
			var unit = BuildExtensionClassUnit(
				compilationUnit,
				ExtensionClassDiscovery.ExpectedNamespace(receiver),
				className,
				members
			);

			solution = solution.AddDocument(
				DocumentId.CreateNewId(document.Project.Id),
				className + ".cs",
				SourceText.From(unit.ToFullString(), Encoding.UTF8),
				ExtensionClassDiscovery.ExpectedFolderSegments(receiver)
			);
		}

		var updatedClass = classDeclaration.WithMembers(SyntaxFactory.List(remainingMembers));
		var updatedRoot = compilationUnit.ReplaceNode(classDeclaration, updatedClass);
		return solution.WithDocumentSyntaxRoot(document.Id, updatedRoot);
	}

	static async Task<Solution> MoveAsync(
		Document document,
		ClassDeclarationSyntax classDeclaration,
		SemanticModel semanticModel,
		INamedTypeSymbol type,
		ITypeSymbol receiver,
		CancellationToken cancellationToken
	)
	{
		var solution = document.Project.Solution;
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
			return solution;

		var expectedNamespace = ExtensionClassDiscovery.ExpectedNamespace(receiver);
		var expectedName = ExtensionClassDiscovery.ExpectedClassName(receiver);

		// If a type with the target name already exists in the target namespace, merge members into it.
		var existing = semanticModel.Compilation.GetTypeByMetadataName(
			string.IsNullOrEmpty(expectedNamespace) ? expectedName : $"{expectedNamespace}.{expectedName}"
		);
		if (existing is not null)
		{
			return await MergeIntoExistingAsync(solution, document, classDeclaration, existing, cancellationToken)
				.ConfigureAwait(false);
		}

		var renamedClass =
			expectedName == type.Name ? classDeclaration : RenameClassNode(classDeclaration, expectedName);
		var originalNamespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns
			? ns.ToDisplayString()
			: string.Empty;
		var unit = BuildExtensionClassUnit(
			compilationUnit,
			expectedNamespace,
			renamedClass,
			additionalUsing: string.IsNullOrEmpty(originalNamespace) ? null : originalNamespace
		);

		solution = solution.AddDocument(
			DocumentId.CreateNewId(document.Project.Id),
			expectedName + ".cs",
			SourceText.From(unit.ToFullString(), Encoding.UTF8),
			ExtensionClassDiscovery.ExpectedFolderSegments(receiver)
		);
		solution = solution.RemoveDocument(document.Id);

		// Add the new namespace to referencing documents so call sites still compile.
		var references = await SymbolFinder
			.FindReferencesAsync(type, solution, cancellationToken)
			.ConfigureAwait(false);
		foreach (var reference in references.SelectMany(static referenced => referenced.Locations))
		{
			var referenceDocument = solution.GetDocument(reference.Document.Id);
			if (referenceDocument is null)
				continue;

			var referenceRoot = await referenceDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (referenceRoot is not CompilationUnitSyntax referenceUnit)
				continue;

			if (referenceUnit.Usings.Any(usingDirective => usingDirective.Name?.ToString() == expectedNamespace))
				continue;

			var updatedReferenceUnit = referenceUnit.AddUsings(
				SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(expectedNamespace)).NormalizeWhitespace()
			);
			solution = solution.WithDocumentSyntaxRoot(referenceDocument.Id, updatedReferenceUnit);
		}

		return solution;
	}

	static async Task<Solution> MergeIntoExistingAsync(
		Solution solution,
		Document sourceDocument,
		ClassDeclarationSyntax sourceClass,
		INamedTypeSymbol existing,
		CancellationToken cancellationToken
	)
	{
		var existingReference = existing.DeclaringSyntaxReferences.FirstOrDefault();
		if (existingReference is null)
			return solution;

		var existingDocument = solution.GetDocument(existingReference.SyntaxTree);
		if (existingDocument is null)
			return solution;

		var existingRoot = await existingDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		var sourceRoot = await sourceDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (
			existingRoot is not CompilationUnitSyntax existingUnit
			|| sourceRoot is not CompilationUnitSyntax sourceUnit
		)
			return solution;

		var existingClass = existingUnit
			.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.FirstOrDefault(candidate => candidate.Identifier.ValueText == existing.Name);
		if (existingClass is null)
			return solution;

		var mergedMembers = existingClass.Members.Concat(sourceClass.Members).ToList();
		var updatedExistingClass = existingClass.WithMembers(SyntaxFactory.List(mergedMembers));
		solution = solution.WithDocumentSyntaxRoot(
			existingDocument.Id,
			existingUnit.ReplaceNode(existingClass, updatedExistingClass)
		);

		var emptyClass = sourceClass.WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>());
		solution = solution.WithDocumentSyntaxRoot(sourceDocument.Id, sourceUnit.ReplaceNode(sourceClass, emptyClass));

		return solution;
	}

	static bool TryGetReceiverKey(
		MethodDeclarationSyntax method,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out string receiverKey
	)
	{
		receiverKey = string.Empty;
		if (semanticModel.GetDeclaredSymbol(method, cancellationToken) is not IMethodSymbol symbol)
			return false;

		ITypeSymbol? receiver = null;
		if (symbol.IsExtensionMethod && symbol.Parameters.Length > 0)
		{
			receiver = symbol.Parameters[0].Type;
		}
		else if (ExtensionClassDiscovery.IsDeclaredInExtensionBlock(symbol))
		{
			receiver = method
				.Ancestors()
				.OfType<ExtensionBlockDeclarationSyntax>()
				.FirstOrDefault()
				?.ParameterList?.Parameters.FirstOrDefault()
				?.Type
				is { } receiverType
				? semanticModel.GetTypeInfo(receiverType, cancellationToken).Type
				: null;
		}

		if (receiver is null)
			return false;

		receiverKey = ReceiverKey(receiver);
		return true;
	}

	static bool TryGetReceiverKey(
		ExtensionBlockDeclarationSyntax block,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out string receiverKey
	)
	{
		receiverKey = string.Empty;
		if (block.ParameterList?.Parameters.FirstOrDefault() is not { Type: { } receiverType })
			return false;

		var receiver = semanticModel.GetTypeInfo(receiverType, cancellationToken).Type;
		if (receiver is null)
			return false;

		receiverKey = ReceiverKey(receiver);
		return true;
	}

	static string ReceiverKey(ITypeSymbol receiver) =>
		receiver.ContainingNamespace is { IsGlobalNamespace: false } ns
			? $"{ns.ToDisplayString()}.{receiver.Name}"
			: receiver.Name;

	static ClassDeclarationSyntax RenameClassNode(ClassDeclarationSyntax classDeclaration, string newName) =>
		classDeclaration.WithIdentifier(SyntaxFactory.Identifier(newName));

	static CompilationUnitSyntax BuildExtensionClassUnit(
		CompilationUnitSyntax sourceUnit,
		string namespaceName,
		string className,
		IEnumerable<MemberDeclarationSyntax> members,
		string? additionalUsing = null
	)
	{
		var classDeclaration = SyntaxFactory
			.ClassDeclaration(className)
			.AddModifiers(
				SyntaxFactory.Token(SyntaxKind.PublicKeyword),
				SyntaxFactory.Token(SyntaxKind.StaticKeyword),
				SyntaxFactory.Token(SyntaxKind.PartialKeyword)
			)
			.WithMembers(SyntaxFactory.List(members));

		return BuildExtensionClassUnit(sourceUnit, namespaceName, classDeclaration, additionalUsing);
	}

	static CompilationUnitSyntax BuildExtensionClassUnit(
		CompilationUnitSyntax sourceUnit,
		string namespaceName,
		ClassDeclarationSyntax classDeclaration,
		string? additionalUsing = null
	)
	{
		var usings = sourceUnit.Usings;
		if (
			additionalUsing is not null
			&& !usings.Any(usingDirective => usingDirective.Name?.ToString() == additionalUsing)
		)
		{
			usings = usings.Add(
				SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(additionalUsing)).NormalizeWhitespace()
			);
		}

		if (!usings.Any(usingDirective => usingDirective.Name?.ToString() == "System.ComponentModel"))
		{
			usings = usings.Add(
				SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.ComponentModel")).NormalizeWhitespace()
			);
		}

		var attributedClass = classDeclaration.AddAttributeLists(
			SyntaxFactory
				.AttributeList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Attribute(
							SyntaxFactory.ParseName("EditorBrowsable"),
							SyntaxFactory.ParseAttributeArgumentList("(EditorBrowsableState.Never)")
						)
					)
				)
				.NormalizeWhitespace()
		);

		var unit = SyntaxFactory
			.CompilationUnit()
			.AddUsings(usings.ToArray())
			.AddMembers(
				SyntaxFactory
					.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
					.AddMembers(attributedClass)
			);

		var pragmaTrivia = SyntaxFactory.Trivia(
			SyntaxFactory.PragmaWarningDirectiveTrivia(
				SyntaxFactory.Token(SyntaxKind.HashToken),
				SyntaxFactory.Token(SyntaxKind.PragmaKeyword),
				SyntaxFactory.Token(SyntaxKind.WarningKeyword),
				SyntaxFactory.Token(SyntaxKind.DisableKeyword),
				SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(SyntaxFactory.IdentifierName("CS1591")),
				SyntaxFactory.Token(SyntaxKind.EndOfDirectiveToken),
				isActive: true
			)
		);

		return unit.NormalizeWhitespace()
			.WithLeadingTrivia(SyntaxFactory.TriviaList(pragmaTrivia).Add(SyntaxFactory.CarriageReturnLineFeed));
	}
}
