using Purview.SourceGeneratorFramework.Testing;

namespace Purview.SourceGeneratorFramework.ExampleGenerator;

public record TypeLibrarySampleTestOptions : SourceGeneratorTestOptions
{
	public TypeLibrarySampleTestOptions()
	{
		AdditionalNamespaces = AdditionalNamespaces.Add("Purview.SourceGeneratorFramework.Examples");
	}
}
