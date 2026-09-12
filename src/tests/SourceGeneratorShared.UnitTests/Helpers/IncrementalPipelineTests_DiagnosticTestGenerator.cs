using Purview.SourceGeneratorFramework.TestGenerators;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.Helpers;

public class IncrementalPipelineTests_DiagnosticTestGenerator : TUnitSourceGeneratorTestBase<DiagnosticTestGenerator>
{
	const string TestAttributeSource = """
[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class TestAttribute : System.Attribute { }
""";

	[Test]
	public async Task RegisterSourceOutput_ReportsDiagnosticsAndGeneratesForSuccessfulTargets(
		CancellationToken cancellationToken
	)
	{
		const string source = """
[TestAttribute]
public partial class MyClass;

""";

		var result = await GenerateAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("TEST001");
		await Assert.That(result.GetGeneratedTree("MyClass.g.cs")).IsNotNull();
	}

	protected override SourceGeneratorTestOptions OnBeforeRun(
		IEnumerable<string> sources,
		SourceGeneratorTestOptions options,
		CancellationToken cancellationToken
	)
	{
		return base.OnBeforeRun(
			sources,
			options.WithAdditionalSources(TestAttributeSource).WithExcludeGeneratedSourceHintNames("TestAttribute"),
			cancellationToken
		);
	}
}
