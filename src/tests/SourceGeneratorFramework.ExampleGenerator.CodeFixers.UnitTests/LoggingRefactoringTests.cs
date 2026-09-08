using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.ExampleGenerator.CodeFixers;

public class LoggingRefactoringTests : TUnitRefactoringTestBase<LoggingRefactoringProvider>
{
	[Test]
	public async Task RefactorAsync_GivenNodeSelector_AddsDebugAttributeToMethod(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Test;

			public class Worker
			{
				public void Process(int id, string name) { }
			}
			""";

		var result = await RefactorAsync(
			source,
			new RefactorTestOptions
			{
				NodeSelector = query => query.GetMethod("Process"),
				EquivalenceKey = LoggingRefactoringProvider.EquivalenceKey,
			},
			cancellationToken
		);

		await Assert.That(result.CodeActions).IsNotEmpty();

		var fixedSource = result.FixedSources["Test1.cs"];
		await Assert.That(fixedSource).Contains("[Debug]");

		var method = result.FixedCode().GetMethod("Process");
		await Assert.That(method.Node.AttributeLists).IsNotEmpty();
		await Assert.That(method.Node.AttributeLists[0].ToString()).Contains("Debug");
	}

	[Test]
	public async Task RefactorAsync_FixedMethod_MatchesSignature(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Test;

			public class Worker
			{
				public void Process(int id, string name) { }
			}
			""";

		var result = await RefactorAsync(
			source,
			new RefactorTestOptions
			{
				NodeSelector = query => query.GetMethod("Process"),
				EquivalenceKey = LoggingRefactoringProvider.EquivalenceKey,
			},
			cancellationToken
		);

		var intType = TypeReference.Create<int>();
		var stringType = TypeReference.Create<string>();

		await Assert.That(result.FixedCode().HasMethod("Process", intType, stringType)).IsTrue();
		await Assert.That(result.FixedCode().HasMethod("Process", stringType)).IsFalse();
	}
}
