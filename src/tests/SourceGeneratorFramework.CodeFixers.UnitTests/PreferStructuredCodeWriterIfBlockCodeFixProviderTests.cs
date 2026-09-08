using Purview.SourceGeneratorFramework.Analyzers;
using Purview.SourceGeneratorFramework.Testing;
using Purview.SourceGeneratorFramework.Testing.TUnit;

namespace Purview.SourceGeneratorFramework.CodeFixers;

public sealed class PreferStructuredCodeWriterIfBlockCodeFixProviderTests
	: TUnitCodeFixTestBase<PreferStructuredCodeWriterIfBlockAnalyzer, PreferStructuredCodeWriterIfBlockCodeFixProvider>
{
	[Test]
	public async Task OpenBlockScope_WithIfHeader_UsesIfBlockScope(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					using (writer.OpenBlockScope("if (enabled)"))
						writer.Return();
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("using (writer.IfBlockScope(\"enabled\"))");
	}

	[Test]
	public async Task OpenBlock_WithIfHeader_UsesIfBlock(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.OpenBlock("if (enabled)", body => body.Return());
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("writer.IfBlock(\"enabled\", body => body.Return())");
	}

	[Test]
	public async Task Block_WithIfHeader_UsesIfBlock(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.Block("if (enabled)", body => body.Return());
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("writer.IfBlock(\"enabled\", body => body.Return())");
	}

	[Test]
	public async Task OpenDelimitedBlock_WithIfHeaderAndBraceDelimiters_UsesIfBlock(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.OpenDelimitedBlock("if (enabled)", "{", "}", body => body.Return());
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("writer.IfBlock(\"enabled\", body => body.Return())");
	}

	[Test]
	public async Task OpenDelimitedBlockScope_WithIfHeaderAndBraceDelimiters_UsesIfBlockScope(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					using (writer.OpenDelimitedBlockScope("if (enabled)", "{", "}"))
						writer.Return();
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("using (writer.IfBlockScope(\"enabled\"))");
	}

	[Test]
	public async Task OpenBlockScope_WithElseIfHeader_UsesElseIfScope(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					using (writer.OpenBlockScope("else if (retry)"))
						writer.Return();
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("using (writer.ElseIfScope(\"retry\"))");
	}

	[Test]
	public async Task OpenBlockScope_WithElseHeader_UsesElseScope(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					using (writer.OpenBlockScope("else"))
						writer.Return();
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("using (writer.ElseScope())");
	}

	[Test]
	public async Task OpenBlock_WithElseHeader_UsesElse(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.OpenBlock("else", body => body.Return());
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("writer.Else(body => body.Return())");
	}

	[Test]
	public async Task OpenDelimitedBlock_WithElseHeaderAndBraceDelimiters_UsesElse(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					writer.DelimitedBlock("else", "{", "}", body => body.Return());
				}
			}
			""";

		var result = await ApplyCodeFixAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result).HasDiagnostic(PreferStructuredCodeWriterIfBlockAnalyzer.Rule.Id);
		await Assert.That(result.FixedSource).Contains("writer.Else(body => body.Return())");
	}

	[Test]
	public async Task OpenDelimitedBlockScope_WithIfHeaderAndNonBraceDelimiters_DoesNotRegisterFix(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					using (writer.OpenDelimitedBlockScope("if (enabled)", "(", ");"))
						writer.Return();
				}
			}
			""";

		await Assert
			.That(async () =>
				await ApplyCodeFixAsync(
					source,
					new CodeFixTestOptions
					{
						EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
						AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
					},
					cancellationToken
				)
			)
			.Throws<InvalidOperationException>();
	}

	[Test]
	public async Task FixAll_WithMultipleIfScopes_FixesAll(CancellationToken cancellationToken)
	{
		const string source = """
			using Purview.SourceGeneratorFramework;

			class Emitter
			{
				public void Emit()
				{
					var writer = new CodeWriter(new GenerationSettings("G"));
					using (writer.OpenBlockScope("if (first)"))
						writer.Return();
					using (writer.OpenBlockScope("if (second)"))
						writer.Return();
				}
			}
			""";

		var result = await ApplyFixAllAsync(
			source,
			new CodeFixTestOptions
			{
				EquivalenceKey = PreferStructuredCodeWriterIfBlockCodeFixProvider.EquivalenceKey,
				AdditionalAssemblyTypes = [typeof(CodeWriter), typeof(GenerationSettings)],
			},
			cancellationToken
		);

		await Assert.That(result.Diagnostics).IsNotEmpty();
		foreach (var fixedSource in result.FixedSources.Values)
		{
			await Assert.That(fixedSource).Contains("using (writer.IfBlockScope(\"first\"))");
			await Assert.That(fixedSource).Contains("using (writer.IfBlockScope(\"second\"))");
		}
	}
}
