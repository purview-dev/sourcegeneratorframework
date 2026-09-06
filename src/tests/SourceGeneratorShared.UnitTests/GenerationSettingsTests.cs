using System.Reflection;
using Purview.SourceGeneratorFramework.TestGenerators;

namespace Purview.SourceGeneratorFramework;

public class GenerationSettingsTests
{
	[Test]
	public async Task Create_GivenGeneratorType_UsesFullInformationalVersion()
	{
		var assembly = typeof(AlwaysNullableContextTestGenerator).Assembly;
		var informationalVersion = assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;

		await Assert.That(informationalVersion).IsNotNull();

		var settings = GenerationSettings.Create<AlwaysNullableContextTestGenerator>();

		await Assert.That(settings.GeneratorVersion).IsEqualTo(informationalVersion);
		await Assert.That(settings.GeneratorVersion).IsNotEqualTo(assembly.GetName().Version?.ToString());
	}
}
