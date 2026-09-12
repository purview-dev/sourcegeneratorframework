namespace Purview.SourceGeneratorFramework.Logging;

public class SourceGenLoggingTests
{
	[Test]
	public async Task CreateLogger_WithoutRegisteredSink_ReturnsNull()
	{
		var logger = SourceGenLogging.CreateLogger(Guid.NewGuid().ToString("N"));

		await Assert.That(logger).IsNull();
	}

	[Test]
	public async Task Logger_AfterSinkIsRemoved_DropsEntries()
	{
		var sessionId = Guid.NewGuid().ToString("N");
		List<string> entries = [];
		var registration = SourceGenLogging.RegisterSink(sessionId, (message, _) => entries.Add(message));
		var logger = SourceGenLogging.CreateLogger(sessionId);
		var activeLogger = logger!;

		activeLogger.Info("before disposal");
		registration.Dispose();
		activeLogger.Info("after disposal");

		await Assert.That(entries).IsEquivalentTo(["before disposal"]);
	}
}
