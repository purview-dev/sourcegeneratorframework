using System.ComponentModel;
using System.Globalization;

namespace Purview.SourceGeneratorFramework.Logging;

[EditorBrowsable(EditorBrowsableState.Never)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible")]
public static class ISourceGenLoggerExtensions
{
	extension(ISourceGenLogger logger)
	{
		/// <summary>
		/// Logs an informational message.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The message arguments.</param>
		public void Info(string message, params object[] args) => logger.Log(SourceGenLogLevel.Info, message, args);

		/// <summary>
		/// Logs an informational message with the specified indentation.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="indentation">The indentation level for the log message.</param>
		/// <param name="args">The message arguments.</param>
		public void Info(string message, int indentation, params object[] args) =>
			logger.Log(SourceGenLogLevel.Info, indentation, message, args);

		/// <summary>
		/// Logs a debug message.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The message arguments.</param>
		public void Debug(string message, params object[] args) => logger.Log(SourceGenLogLevel.Debug, message, args);

		/// <summary>
		/// Logs a debug message with the specified indentation.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="indentation">The indentation level for the log message.</param>
		/// <param name="args">The message arguments.</param>
		public void Debug(string message, int indentation, params object[] args) =>
			logger.Log(SourceGenLogLevel.Debug, indentation, message, args);

		/// <summary>
		/// Logs a diagnostic message.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The message arguments.</param>
		public void Diagnostic(string message, params object[] args) =>
			logger.Log(SourceGenLogLevel.Diagnostic, message, args);

		/// <summary>
		/// Logs a diagnostic message with the specified indentation.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="indentation">The indentation level for the log message.</param>
		/// <param name="args">The message arguments.</param>
		public void Diagnostic(string message, int indentation, params object[] args) =>
			logger.Log(SourceGenLogLevel.Diagnostic, indentation, message, args);

		/// <summary>
		/// Logs a diagnostic message.
		/// </summary>
		/// <param name="diagnostic">The diagnostic information to log.</param>
		/// <param name="args">The message arguments.</param>
		public void Diagnostic(ReportableDiagnostic diagnostic, params object[] args) =>
			Diagnostic(logger, diagnostic, 0, args);

		/// <summary>
		/// Logs a diagnostic message with the specified indentation.
		/// </summary>
		/// <param name="diagnostic">The diagnostic information to log.</param>
		/// <param name="indentation">The indentation level for the log message.</param>
		/// <param name="args">The message arguments.</param>
		/// <exception cref="ArgumentNullException">Thrown if the diagnostic is null.</exception>
		public void Diagnostic(ReportableDiagnostic diagnostic, int indentation, params object[] args)
		{
			if (diagnostic is null)
				throw new ArgumentNullException(nameof(diagnostic));

			// Format the message directly from the descriptor so logging never allocates a Diagnostic.
			logger.Log(
				SourceGenLogLevel.Diagnostic,
				indentation,
				$"{diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}",
				args
			);
		}

		/// <summary>
		///	Logs a warning message.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The message arguments.</param>
		public void Warning(string message, params object[] args) =>
			logger.Log(SourceGenLogLevel.Warning, message, args);

		/// <summary>
		/// Logs a warning message with the specified indentation.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="indentation">The indentation level for the log message.</param>
		/// <param name="args">The message arguments.</param>
		public void Warning(string message, int indentation, params object[] args) =>
			logger.Log(SourceGenLogLevel.Warning, indentation, message, args);

		/// <summary>
		/// Logs an error message.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The message arguments.</param>
		public void Fatal(string message, params object[] args) => logger.Log(SourceGenLogLevel.Fatal, message, args);

		/// <summary>
		/// Logs an error message with the specified indentation.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="indentation">The indentation level for the log message.</param>
		/// <param name="args">The message arguments.</param>
		public void Fatal(string message, int indentation, params object[] args) =>
			logger.Log(SourceGenLogLevel.Fatal, indentation, message, args);

		/// <summary>
		/// Logs an exception as a fatal error.
		/// </summary>
		/// <param name="exception">The exception to log.</param>
		/// <param name="message">An optional message to include with the exception.</param>
		/// <param name="args">The arguments to format the message.</param>
		/// <exception cref="ArgumentNullException">Thrown if the exception is null.</exception>
		public void Fatal(Exception exception, string? message = null, params object[] args) =>
			Fatal(logger, exception, 0, message, args);

		/// <summary>
		/// Logs an exception as a fatal error.
		/// </summary>
		/// <param name="exception">The exception to log.</param>
		/// <param name="indentation">The indentation level for the log message.</param>
		/// <param name="message">An optional message to include with the exception.</param>
		/// <param name="args">The arguments to format the message.</param>
		/// <exception cref="ArgumentNullException">Thrown if the exception is null.</exception>
		public void Fatal(Exception exception, int indentation, string? message = null, params object[] args)
		{
			if (exception is null)
				throw new ArgumentNullException(nameof(exception));

			message ??= "The following exception occurred:";
			message += $"\n\nMessage: {exception.Message}\n\nStack Trace:\n{exception.StackTrace}";

			logger.Log(SourceGenLogLevel.Fatal, indentation, message, args);
		}

		/// <summary>
		/// Logs a message with the specified output type and indentation.
		/// </summary>
		/// <param name="outputType">The type of output.</param>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The message arguments.</param>
		public void Log(SourceGenLogLevel outputType, string message, params object[] args) =>
			logger.Log(outputType, 0, message, args);
	}
}
