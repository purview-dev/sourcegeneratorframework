#pragma warning disable CS1591
using System.ComponentModel;

namespace Purview.SourceGeneratorFramework;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class CodeWriterExtensions
{
	extension(CodeWriter)
	{
		public static CodeWriter CreateTestWriter(
			GenerationSettings? settings = null,
			bool includeGeneratedAttributes = false
		)
		{
			return new(settings ?? new("TestGenerator", "1"))
			{
				DefaultIncludeGeneratedAttributes = includeGeneratedAttributes,
			};
		}
	}
}
