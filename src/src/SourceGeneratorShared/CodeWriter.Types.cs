using System.Diagnostics.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

partial class CodeWriter
{
	enum WrittenItemKind
	{
		None,
		Field,
		Property,
		Constructor,
		Method,
		Type,
		Namespace,
		EnumField,
	}

	/// <summary>
	/// Restores warning pragmas when disposed.
	/// </summary>
	[SuppressMessage(
		"Performance",
		"CA1815:Override equals and operator equals on value types",
		Justification = "This type is a mutable lifetime token and has no meaningful value equality."
	)]
	public struct PragmaScope(CodeWriter writer, string[] pragmas) : IDisposable
	{
		CodeWriter? _writer = writer;
		readonly string[] _pragmas = pragmas;

		/// <summary>
		/// Restores the disabled pragmas once.
		/// </summary>
		public void Dispose()
		{
			var writer = _writer;
			if (writer is null)
				return;

			_writer = null;
			writer.RestorePragmas(_pragmas);
		}
	}

	void RestorePragmas(string[] pragmas)
	{
		if (pragmas.Length == 0)
			return;

		NewLine();
		foreach (var pragma in pragmas)
			Write("#pragma warning restore ").Line(pragma);
	}

	/// <summary>
	/// Restores a writer's indentation when disposed.
	/// </summary>
	[SuppressMessage(
		"Performance",
		"CA1815:Override equals and operator equals on value types",
		Justification = "This type is a mutable lifetime token and has no meaningful value equality."
	)]
	public struct IndentScope(CodeWriter writer, int scopeId) : IDisposable
	{
		CodeWriter? _writer = writer;

		/// <summary>
		/// Restores the indentation level once.
		/// </summary>
		public void Dispose()
		{
			var writer = _writer;
			if (writer is null)
				return;

			_writer = null;
			writer.CloseIndentScope(scopeId);
		}
	}

	/// <summary>
	/// Restores indentation and writes a block's closing token when disposed.
	/// </summary>
	[SuppressMessage(
		"Performance",
		"CA1815:Override equals and operator equals on value types",
		Justification = "This type is a mutable lifetime token and has no meaningful value equality."
	)]
	public struct BlockScope : IDisposable
	{
		CodeWriter? _writer;
		readonly string? _closingSeparator;
		readonly int _scopeId;
		readonly int _completedItem;
		readonly int _itemIndent;
		readonly bool _closingAtColumnZero;
		readonly bool _changesIndentation;

		internal BlockScope(
			CodeWriter writer,
			string? closingSeparator,
			int scopeId,
			int completedItem,
			int itemIndent,
			bool closingAtColumnZero = false,
			bool changesIndentation = true
		)
		{
			_writer = writer;
			_closingSeparator = closingSeparator;
			_scopeId = scopeId;
			_completedItem = completedItem;
			_itemIndent = itemIndent;
			_closingAtColumnZero = closingAtColumnZero;
			_changesIndentation = changesIndentation;
		}

		/// <summary>
		/// Closes the block once.
		/// </summary>
		public void Dispose()
		{
			var writer = _writer;
			if (writer is null)
				return;

			_writer = null;
			writer.CloseBlock(
				_closingSeparator,
				_scopeId,
				_completedItem,
				_itemIndent,
				_closingAtColumnZero,
				_changesIndentation
			);
		}
	}

	sealed record class NoOpScope : IDisposable
	{
		public static NoOpScope Instance { get; } = new();

		public void Dispose() { }
	}
}
