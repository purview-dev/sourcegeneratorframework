#pragma warning disable CS1591
using System.ComponentModel;

namespace Microsoft.CodeAnalysis;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class SourceProductionContextExtensions
{
	extension(SourceProductionContext context)
	{
		/// <summary>
		/// Reports a single <see cref="ReportableDiagnostic"/> to the source production context.
		/// </summary>
		public void ReportDiagnostic(ReportableDiagnostic diagnostic)
		{
			if (diagnostic is null)
				throw new ArgumentNullException(nameof(diagnostic));

			context.ReportDiagnostic(diagnostic.ToDiagnostic());
		}

		/// <summary>
		/// Reports a sequence of <see cref="ReportableDiagnostic"/> diagnostics to the source production context.
		/// </summary>
		public void ReportDiagnostics(IEnumerable<ReportableDiagnostic> diagnostics)
		{
			if (diagnostics is null)
				throw new ArgumentNullException(nameof(diagnostics));

			foreach (var diagnostic in diagnostics)
				context.ReportDiagnostic(diagnostic);
		}
	}
}
