namespace Purview.SourceGeneratorFramework;

public class GeneratorResultTests
{
	[Test]
	public async Task Ok_WithValue_IsSuccess()
	{
		var result = GeneratorResult<string>.Create("value");

		await Assert.That(result.HasValue).IsTrue();
		await Assert.That(result.IsEmpty).IsFalse();
		await Assert.That(result.ShouldProcess).IsTrue();
		await Assert.That(result.HasDiagnostics).IsFalse();
		await Assert.That(result.Value).IsEqualTo("value");
	}

	[Test]
	public async Task Ok_WithValueAndWarningDiagnostic_ShouldProcessIsTrue()
	{
		var diagnostic = CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, isBlocking: false);
		var result = GeneratorResult<string>.Create("value", diagnostic);

		await Assert.That(result.HasValue).IsTrue();
		await Assert.That(result.HasDiagnostics).IsTrue();
		await Assert.That(result.HasBlockingDiagnostics).IsFalse();
		await Assert.That(result.ShouldProcess).IsTrue();
		await Assert.That(result.Value).IsEqualTo("value");
	}

	[Test]
	public async Task Ok_WithValueAndNonBlockingErrorDiagnostic_ShouldProcessIsTrue()
	{
		var diagnostic = CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, isBlocking: false);
		var result = GeneratorResult<string>.Create("value", diagnostic);

		await Assert.That(result.HasValue).IsTrue();
		await Assert.That(result.HasDiagnostics).IsTrue();
		await Assert.That(result.HasErrorDiagnostics).IsTrue();
		await Assert.That(result.HasBlockingDiagnostics).IsFalse();
		await Assert.That(result.ShouldProcess).IsTrue();
	}

	[Test]
	public async Task Ok_WithValueAndBlockingErrorDiagnostic_ShouldProcessIsFalse()
	{
		var diagnostic = CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, isBlocking: true);
		var result = GeneratorResult<string>.Create("value", diagnostic);

		await Assert.That(result.HasValue).IsTrue();
		await Assert.That(result.HasDiagnostics).IsTrue();
		await Assert.That(result.HasBlockingDiagnostics).IsTrue();
		await Assert.That(result.ShouldProcess).IsFalse();
	}

	[Test]
	public async Task Ok_WithValueAndMixedDiagnostics_ShouldProcessIsFalse()
	{
		var blocking = CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, isBlocking: true);
		var nonBlocking = CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, isBlocking: false);
		var result = GeneratorResult<string>.Create("value", blocking, nonBlocking);

		await Assert.That(result.HasBlockingDiagnostics).IsTrue();
		await Assert.That(result.ShouldProcess).IsFalse();
	}

	[Test]
	public async Task Fail_WithBlockingDiagnostics_ShouldProcessIsFalse()
	{
		var diagnostic = CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, isBlocking: true);
		var result = GeneratorResult<string>.Create(diagnostic);

		await Assert.That(result.HasValue).IsFalse();
		await Assert.That(result.ShouldProcess).IsFalse();
		await Assert.That(result.HasBlockingDiagnostics).IsTrue();
		await Assert.That(result.IsEmpty).IsFalse();
		await Assert.That(result.HasDiagnostics).IsTrue();
		await Assert.That(result.Value).IsNull();
	}

	[Test]
	public async Task Fail_WithNonBlockingDiagnostics_ShouldProcessIsFalse()
	{
		var diagnostic = CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, isBlocking: false);
		var result = GeneratorResult<string>.Create(diagnostic);

		await Assert.That(result.HasValue).IsFalse();
		await Assert.That(result.ShouldProcess).IsFalse();
		await Assert.That(result.HasBlockingDiagnostics).IsFalse();
		await Assert.That(result.HasDiagnostics).IsTrue();
	}

	[Test]
	public void Fail_WithoutDiagnostics_Throws()
	{
		Assert.Throws<ArgumentException>(() => GeneratorResult<string>.Create());
	}

	[Test]
	public async Task Empty_IsEmpty()
	{
		var result = GeneratorResult<string>.Empty;

		await Assert.That(result.HasValue).IsFalse();
		await Assert.That(result.IsEmpty).IsTrue();
		await Assert.That(result.ShouldProcess).IsFalse();
		await Assert.That(result.HasDiagnostics).IsFalse();
		await Assert.That(result.Value).IsNull();
	}

	[Test]
	public async Task Empty_WithValueType_IsEmpty()
	{
		var result = GeneratorResult<int>.Empty;

		await Assert.That(result.HasValue).IsFalse();
		await Assert.That(result.IsEmpty).IsTrue();
		await Assert.That(result.ShouldProcess).IsFalse();
	}

	[Test]
	public async Task Fail_WithValueType_ShouldProcessIsFalse()
	{
		var diagnostic = CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, isBlocking: true);

		var result = GeneratorResult<int>.Create(diagnostic);

		await Assert.That(result.HasValue).IsFalse();
		await Assert.That(result.IsEmpty).IsFalse();
		await Assert.That(result.ShouldProcess).IsFalse();
	}

	static ReportableDiagnostic CreateDiagnostic(Microsoft.CodeAnalysis.DiagnosticSeverity severity, bool isBlocking) =>
		ReportableDiagnostic.Create(
			new Microsoft.CodeAnalysis.DiagnosticDescriptor("TEST001", "Test", "Message", "Test", severity, true),
			isBlocking
		);

	[Test]
	public async Task DefaultGeneratorResult_GetHashCode_DoesNotThrow()
	{
		// A default GeneratorResult<T> has a default EquatableArray for Diagnostics; hashing it must not
		// throw (regression: default ImmutableArray enumeration throws NullReferenceException).
		GeneratorResult<int> result = default;

		await Assert.That(() => result.GetHashCode()).ThrowsNothing();
	}
}
