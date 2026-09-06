namespace Purview.SourceGeneratorFramework;

public class StringExtensionTests
{
	[Test]
	[Arguments("Hello, World!", "\"Hello, World!\"")]
	[Arguments(@"^[\w\-.]+$", "\"^[\\\\w\\\\-.]+$\"")]
	[Arguments("say \"hi\"", "\"say \\\"hi\\\"\"")]
	[Arguments("", "\"\"")]
	[Arguments("a\nb", "\"a\\nb\"")]
	[Arguments("a\tb", "\"a\\tb\"")]
	[Arguments("a\r\nb", "\"a\\r\\nb\"")]
	public async Task StringLiteral_GivenValue_EscapesItAsCSharpStringLiteral(string value, string expected)
	{
		await Assert.That(value.StringLiteral()).IsEqualTo(expected);
	}

	[Test]
	public async Task StringLiteral_GivenNull_ReturnsNullKeyword()
	{
		await Assert.That(((string?)null).StringLiteral()).IsEqualTo("null");
	}
}
