using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeAnalysis;

public class SourceProductionContextExtensionsTests
{
	[Test]
	public async Task ReportDiagnostic_ReportsThroughGenerator()
	{
		DiagnosticGenerator generator = new();
		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[CSharpSyntaxTree.ParseText("class C { }")],
			references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]
		);

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGenerators(compilation);
		var result = driver.GetRunResult();

		var diagnostics = result.Results.SelectMany(r => r.Diagnostics);
		await Assert.That(diagnostics.Any(d => d.Id == "TEST001")).IsTrue();
	}

	sealed class DiagnosticGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterSourceOutput(
				context.CompilationProvider,
				static (spc, _) =>
				{
					DiagnosticDescriptor descriptor = new(
						"TEST001",
						"Test",
						"Test message",
						"Test",
						DiagnosticSeverity.Warning,
						true
					);
					spc.ReportDiagnostic(ReportableDiagnostic.Create(descriptor, isBlocking: false));
				}
			);
		}
	}
}
