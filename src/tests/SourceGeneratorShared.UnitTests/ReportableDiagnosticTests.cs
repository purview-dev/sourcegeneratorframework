using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Purview.SourceGeneratorFramework;

public class ReportableDiagnosticTests
{
	static DiagnosticDescriptor TestDescriptor { get; } =
		new("TEST001", "Test Title", "Test message: {0}", "Test", DiagnosticSeverity.Warning, true);

	[Test]
	public async Task Create_WithNullLocation_ReturnsDiagnosticWithEmptyPath()
	{
		var info = ReportableDiagnostic.Create(TestDescriptor, isBlocking: true, (Location?)null, "arg");

		await Assert.That(info.FilePath).IsEqualTo(string.Empty);
		await Assert.That(info.TextSpan).IsEqualTo(default);
		await Assert.That(info.MessageArgs.Count).IsEqualTo(1);
		await Assert.That(info.MessageArgs[0]).IsEqualTo("arg");
		await Assert.That(info.Descriptor).IsEqualTo(TestDescriptor);
	}

	[Test]
	public async Task Create_WithLocation_CapturesPathAndSpan()
	{
		var source = "class C { }";
		var tree = CSharpSyntaxTree.ParseText(source, path: "Test.cs");
		var location = Location.Create(tree, TextSpan.FromBounds(6, 7));

		var info = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false, location);

		await Assert.That(info.FilePath).IsEqualTo("Test.cs");
		await Assert.That(info.TextSpan.Start).IsEqualTo(6);
		await Assert.That(info.TextSpan.End).IsEqualTo(7);
	}

	[Test]
	public async Task Create_WithIsBlocking_PreservesValue()
	{
		var blocking = ReportableDiagnostic.Create(TestDescriptor, isBlocking: true, (Location?)null, "arg");
		var nonBlocking = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false, (Location?)null, "arg");

		await Assert.That(blocking.IsBlocking).IsTrue();
		await Assert.That(nonBlocking.IsBlocking).IsFalse();
	}

	[Test]
	public async Task ToDiagnostic_WithNullLocation_CreatesDiagnostic()
	{
		var info = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false, (Location?)null, "arg");
		var diagnostic = info.ToDiagnostic();

		await Assert.That(diagnostic.Descriptor).IsEqualTo(TestDescriptor);
		await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("Test message: arg");
		await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(default);
	}

	[Test]
	public async Task ToDiagnostic_WithLocation_CreatesDiagnosticWithLocation()
	{
		var source = "class C { }";
		var tree = CSharpSyntaxTree.ParseText(source, path: "Test.cs");
		var location = Location.Create(tree, TextSpan.FromBounds(6, 7));
		var info = ReportableDiagnostic.Create(TestDescriptor, isBlocking: true, location, "arg");

		var diagnostic = info.ToDiagnostic();

		await Assert.That(diagnostic.Location.SourceSpan.Start).IsEqualTo(6);
		await Assert.That(diagnostic.Location.SourceSpan.End).IsEqualTo(7);
		await Assert.That(diagnostic.Location.GetLineSpan().Path).IsEqualTo("Test.cs");
	}

	[Test]
	public async Task GetMessage_FormatsUsingDescriptorAndArgs()
	{
		var info = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false, (Location?)null, "value");

		await Assert.That(info.GetMessage(CultureInfo.InvariantCulture)).IsEqualTo("Test message: value");
	}

	[Test]
	public async Task IdAndSeverity_ReflectDescriptor()
	{
		var info = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false);

		await Assert.That(info.Id).IsEqualTo("TEST001");
		await Assert.That(info.Severity).IsEqualTo(DiagnosticSeverity.Warning);
	}

	[Test]
	public async Task EqualDiagnostics_WithFreshArgumentArrays_CompareEqual()
	{
		var first = ReportableDiagnostic.Create(TestDescriptor, isBlocking: true, (Location?)null, "first");
		var second = ReportableDiagnostic.Create(TestDescriptor, isBlocking: true, (Location?)null, "first");

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task EqualDiagnostics_WithRecreatedDescriptor_CompareEqual()
	{
		// DiagnosticDescriptor equality is structural, so a recreated descriptor with the same rule
		// identity keeps the pipeline cache stable.
		DiagnosticDescriptor recreated = new(
			"TEST001",
			"Test Title",
			"Test message: {0}",
			"Test",
			DiagnosticSeverity.Warning,
			true
		);

		var first = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false, (Location?)null, "value");
		var second = ReportableDiagnostic.Create(recreated, isBlocking: false, (Location?)null, "value");

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task EqualDiagnostics_WithAdditionalLocations_CompareEqual()
	{
		var locations = new[]
		{
			Location.Create(
				"File.cs",
				new TextSpan(0, 5),
				new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 5))
			),
			Location.Create(
				"File.cs",
				new TextSpan(8, 3),
				new LinePositionSpan(new LinePosition(1, 0), new LinePosition(1, 3))
			),
		};
		var first = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false, locations, "value");
		var second = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false, locations, "value");

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task DifferentMessageArgs_AreNotEqual()
	{
		var first = ReportableDiagnostic.Create(TestDescriptor, isBlocking: true, (Location?)null, "first");
		var second = ReportableDiagnostic.Create(TestDescriptor, isBlocking: true, (Location?)null, "second");

		await Assert.That(first).IsNotEqualTo(second);
	}

	[Test]
	public async Task DifferentIsBlocking_AreNotEqual()
	{
		var blocking = ReportableDiagnostic.Create(TestDescriptor, isBlocking: true, (Location?)null, "value");
		var nonBlocking = ReportableDiagnostic.Create(TestDescriptor, isBlocking: false, (Location?)null, "value");

		await Assert.That(blocking).IsNotEqualTo(nonBlocking);
		await Assert.That(blocking.GetHashCode()).IsNotEqualTo(nonBlocking.GetHashCode());
	}
}
