using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.SourceGeneratorFramework.CodeFixers;

/// <summary>
/// Converts classic static extension methods (<c>public static T Method(this Receiver r, ...)</c>) in a
/// class into C# 14 <c>extension(Receiver r)</c> blocks, grouping members by receiver and preserving
/// non-extension members.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConvertToExtensionBlockCodeFixProvider))]
public sealed class ConvertToExtensionBlockCodeFixProvider : CodeFixProvider
{
	internal const string EquivalenceKey = "ConvertToExtensionBlock";

	public override ImmutableArray<string> FixableDiagnosticIds => ["PSGFR34"];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var semanticModel = await context
			.Document.GetSemanticModelAsync(context.CancellationToken)
			.ConfigureAwait(false);
		if (semanticModel is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			if (
				root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
				is not MethodDeclarationSyntax method
			)
				continue;

			if (method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault() is not { } classDeclaration)
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Convert to extension block",
					_ => ConvertAsync(context.Document, classDeclaration, semanticModel, context.CancellationToken),
					EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static async Task<Document> ConvertAsync(
		Document document,
		ClassDeclarationSyntax classDeclaration,
		SemanticModel semanticModel,
		CancellationToken cancellationToken
	)
	{
		List<MethodDeclarationSyntax> extensionMethods = [];
		List<MemberDeclarationSyntax> otherMembers = [];
		foreach (var member in classDeclaration.Members)
		{
			if (
				member is MethodDeclarationSyntax method
				&& IsClassicExtension(method, semanticModel, cancellationToken)
			)
				extensionMethods.Add(method);
			else
				otherMembers.Add(member);
		}

		if (extensionMethods.Count == 0)
			return document;

		List<MemberDeclarationSyntax> blocks = [];
		foreach (var group in extensionMethods.GroupBy(static method => GetReceiverKey(method)))
		{
			var first = group.First();
			var receiverParameter = SyntaxFactory.ParameterList(
				SyntaxFactory.SingletonSeparatedList(ConvertReceiverParameter(first))
			);

			TypeParameterListSyntax? typeParameterList = null;
			var constraintClauses = SyntaxFactory.List<TypeParameterConstraintClauseSyntax>();
			List<MemberDeclarationSyntax> convertedMembers = [];
			foreach (var method in group)
			{
				if (HasGenericReceiver(method))
				{
					// The receiver is a method type parameter; move the type parameters and constraints to
					// the extension declaration so the receiver resolves against it.
					typeParameterList ??= method.TypeParameterList;
					if (methodConstraintClauses(method) is { Count: > 0 } constraints)
						constraintClauses = constraints;
				}

				convertedMembers.Add(ConvertMethod(method, typeParameterList));
			}

			blocks.Add(
				SyntaxFactory.ExtensionBlockDeclaration(
					attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
					modifiers: SyntaxFactory.TokenList(),
					keyword: SyntaxFactory.Token(SyntaxKind.ExtensionKeyword),
					typeParameterList: typeParameterList,
					parameterList: receiverParameter,
					constraintClauses: constraintClauses,
					openBraceToken: SyntaxFactory.Token(SyntaxKind.OpenBraceToken),
					members: SyntaxFactory.List(convertedMembers),
					closeBraceToken: SyntaxFactory.Token(SyntaxKind.CloseBraceToken),
					semicolonToken: default
				)
			);
		}

		var newClass = classDeclaration.WithMembers(SyntaxFactory.List(otherMembers.Concat(blocks)));
		var root = await classDeclaration.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
		var newRoot = root.ReplaceNode(classDeclaration, newClass);

		return document.WithSyntaxRoot(newRoot);
	}

	static bool IsClassicExtension(
		MethodDeclarationSyntax method,
		SemanticModel semanticModel,
		CancellationToken cancellationToken
	)
	{
		if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
			return false;
		if (method.ParameterList.Parameters.FirstOrDefault() is not { } firstParameter)
			return false;
		if (!firstParameter.Modifiers.Any(SyntaxKind.ThisKeyword))
			return false;

		return semanticModel.GetDeclaredSymbol(method, cancellationToken) is { IsExtensionMethod: true };
	}

	static string GetReceiverKey(MethodDeclarationSyntax method) =>
		method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() ?? string.Empty;

	static ParameterSyntax ConvertReceiverParameter(MethodDeclarationSyntax method)
	{
		var firstParameter = method.ParameterList.Parameters[0];
		return firstParameter.WithModifiers(
			SyntaxFactory.TokenList(
				firstParameter.Modifiers.Where(static token => !token.IsKind(SyntaxKind.ThisKeyword))
			)
		);
	}

	static bool HasGenericReceiver(MethodDeclarationSyntax method)
	{
		if (
			method.TypeParameterList is null
			|| method.ParameterList.Parameters.FirstOrDefault() is not { } firstParameter
		)
			return false;

		var receiverTypeName = firstParameter.Type?.ToString();
		return receiverTypeName is not null
			&& method.TypeParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == receiverTypeName);
	}

	static MethodDeclarationSyntax ConvertMethod(
		MethodDeclarationSyntax method,
		TypeParameterListSyntax? extensionTypeParameterList
	)
	{
		var updated = method.WithModifiers(
			SyntaxFactory.TokenList(method.Modifiers.Where(static token => !token.IsKind(SyntaxKind.StaticKeyword)))
		);

		if (extensionTypeParameterList is not null)
		{
			// The receiver is a method type parameter that moved to the extension declaration, so the
			// method must drop its own type parameters and constraints.
			updated = updated
				.WithTypeParameterList(null)
				.WithConstraintClauses(SyntaxFactory.List<TypeParameterConstraintClauseSyntax>());
		}

		// The receiver is now the extension block's receiver, so it is removed from the method's
		// parameters; references to it in the body resolve against the block's receiver parameter.
		var parameters = updated.ParameterList.Parameters.RemoveAt(0);
		var newParameterList =
			parameters.Count == 0 ? SyntaxFactory.ParameterList() : updated.ParameterList.WithParameters(parameters);

		return updated.WithParameterList(newParameterList);
	}

	static SyntaxList<TypeParameterConstraintClauseSyntax> methodConstraintClauses(MethodDeclarationSyntax method) =>
		method.ConstraintClauses;
}
