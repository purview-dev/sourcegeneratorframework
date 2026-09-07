using Microsoft.CodeAnalysis.CSharp.Syntax;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework;

public class RefactoringTests : TUnitRefactoringTestBase<AddObsoleteRefactoringProvider>
{
	const string Source = """
		namespace Test;

		public class Sample
		{
			public void DoWork(int value) { }
		}
		""";

	[Test]
	public async Task RefactorAsync_GivenNodeSelector_AddsObsoleteToMethod(CancellationToken cancellationToken)
	{
		var result = await RefactorAsync(
			Source,
			new RefactorTestOptions
			{
				NodeSelector = query => query.GetMethod("DoWork"),
				EquivalenceKey = AddObsoleteRefactoringProvider.EquivalenceKey,
			},
			cancellationToken
		);

		await Assert.That(result.CodeActions).IsNotEmpty();

		var fixedSource = result.FixedSources["Test1.cs"];
		await Assert.That(fixedSource).Contains("[System.Obsolete]");
		await Assert.That(fixedSource).Contains("public void DoWork(int value)");
	}

	[Test]
	public async Task RefactorAsync_GivenSpanTrigger_AppliesRefactoring(CancellationToken cancellationToken)
	{
		var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
			Source,
			cancellationToken: cancellationToken
		);
		var method = (await tree.GetRootAsync(cancellationToken))
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.First(static candidate => candidate.Identifier.ValueText == "DoWork");

		var result = await RefactorAsync(
			Source,
			new RefactorTestOptions
			{
				IncludeDefaultNamespaces = false,
				Span = method.Identifier.Span,
				EquivalenceKey = AddObsoleteRefactoringProvider.EquivalenceKey,
			},
			cancellationToken
		);

		await Assert.That(result.FixedSources["Test1.cs"]).Contains("[System.Obsolete]");
	}

	[Test]
	public async Task RefactorAsync_FixedCodeQuery_LocatesRefactoredMethod(CancellationToken cancellationToken)
	{
		var result = await RefactorAsync(
			Source,
			new RefactorTestOptions
			{
				NodeSelector = query => query.GetMethod("DoWork"),
				EquivalenceKey = AddObsoleteRefactoringProvider.EquivalenceKey,
			},
			cancellationToken
		);

		var method = result.FixedCode().GetMethod("DoWork");

		await Assert.That(method.Node.AttributeLists).IsNotEmpty();
		await Assert.That(method.Node.AttributeLists[0].ToString()).Contains("Obsolete");
	}
}
