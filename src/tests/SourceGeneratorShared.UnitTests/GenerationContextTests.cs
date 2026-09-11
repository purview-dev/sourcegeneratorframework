using Purview.SourceGeneratorFramework.Logging;

namespace Purview.SourceGeneratorFramework;

public class GenerationContextTests
{
	[Test]
	public async Task CreateCodeWriter_GivenScopeValidationEnabled_ConfiguresEveryWriter()
	{
		// Arrange
		var context = CreateGenerationContext(
			new GenerationSettings("TestGenerator", "1.0.0") { ValidateCodeWriterScopes = true }
		);

		// Act
		var writer = context.CreateCodeWriter();

		// Assert
		await Assert.That(writer.ThrowOnUnclosedScopes).IsTrue();
		await Assert.That(ReferenceEquals(writer, context.CreateCodeWriter())).IsFalse();
	}

	[Test]
	public async Task CreateCodeWriter_GivenGeneratorIdentity_PropagatesIdentity()
	{
		var context = CreateGenerationContext(new GenerationSettings("HostKitGenerator", "2.3.4"));

		var writer = context.CreateCodeWriter();

		await Assert.That(writer.GeneratorName).IsEqualTo("HostKitGenerator");
		await Assert.That(writer.GeneratorVersion).IsEqualTo("2.3.4");
	}

	[Test]
	public async Task CreateCodeWriter_GivenNullableDirectiveSettings_PropagatesToWriter()
	{
		var context = CreateGenerationContext(
			new GenerationSettings("TestGenerator", "1.0.0")
			{
				NullableDirectiveMode = NullableDirectiveMode.Disable,
				IsNullableContextEnabled = true,
			}
		);

		var writer = context.CreateCodeWriter();

		await Assert.That(writer.NullableDirectiveMode).IsEqualTo(NullableDirectiveMode.Disable);
		await Assert.That(writer.IsNullableContextEnabled).IsTrue();
	}

	[Test]
	public async Task Constructor_GivenLogger_ExposesLogger()
	{
		// Arrange
		TestLogger logger = new();
		var context = CreateGenerationContext(new GenerationSettings("TestGenerator", "1.0.0"), logger);

		// Act / Assert
		await Assert.That(context.Logger).IsSameReferenceAs(logger);
	}

	[Test]
	public async Task GenerationSettings_UsesValueEquality()
	{
		GenerationSettings settingsA = new("A", "1.0.0");
		GenerationSettings settingsB = new("A", "1.0.0");
		var settingsC = settingsA with { ValidateCodeWriterScopes = true };

		await Assert.That(settingsA).IsEqualTo(settingsB);
		await Assert.That(settingsA).IsNotEqualTo(settingsC);
	}

	static GenerationContext<EmptyCapabilities> CreateGenerationContext(
		GenerationSettings? settings = null,
		ISourceGenLogger? logger = null
	)
	{
		settings ??= new GenerationSettings("TestGenerator", "1.0.0");
		return new(EmptyCapabilities.Instance, settings, logger);
	}

	sealed class TestLogger : ISourceGenLogger
	{
		public void Log(SourceGenLogLevel level, int indentation, string message, params object[] args) { }
	}
}
