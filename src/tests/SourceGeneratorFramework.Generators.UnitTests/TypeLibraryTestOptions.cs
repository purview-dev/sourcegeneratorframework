namespace Purview.SourceGeneratorFramework.Generators;

public sealed record TypeLibraryTestOptions : SourceGeneratorTestOptions
{
	public TypeLibraryTestOptions()
	{
		AdditionalAssemblyTypes = AdditionalAssemblyTypes.Add(typeof(TypeIdentity));
	}
}
