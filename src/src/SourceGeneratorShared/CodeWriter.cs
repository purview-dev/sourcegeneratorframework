using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Provides an allocation-conscious writer for constructing generated C# source files.
/// </summary>
/// <remarks>
/// A writer owns its mutable buffer and is intended to be used for one generated source file.
/// Instances are not thread-safe and must not be shared between concurrent generator operations.
/// </remarks>
[SuppressMessage("Design", "CA1034:Nested types should not be visible")]
public sealed partial class CodeWriter
{
	const char NewLineCharacter = '\n';
	const int DefaultCapacity = 4096;
	const int DefaultExpressionCapacity = 128;
	const int DefaultIndentationSize = 4;
	const int DefaultMaximumLineLength = 100;

	int _indentLevel;
	int _nextScopeId;
	int _lastWrittenItemIndent = -1;
	int _lastWrittenItemEnd;
	WrittenItemKind _lastWrittenItem;
	bool _atLineStart = true;

	readonly StringBuilder _builder;
	readonly Dictionary<int, CodeWriterOpenScope>? _openScopes;

	readonly char _indentCharacter;
	readonly int _indentationSize;
	readonly int _maximumLineLength;

	/// <summary>
	/// Gets whether opened scopes are tracked for validation. When <see langword="false"/>, expensive
	/// opening-stack-trace capture is skipped because undisposed scopes will not be reported.
	/// </summary>
	bool TracksOpenScopes => _openScopes is not null;

	/// <summary>
	/// Initializes a new writer with required generator identity.
	/// </summary>
	/// <param name="settings">The generation settings containing the generator name and version used during code writing.</param>
	/// <param name="initialCapacity">
	/// The initial number of characters that the internal buffer can contain without growing.
	/// </param>
	/// <param name="throwOnUnclosedScopes">
	/// Whether materializing the generated source throws while disposable scopes remain open.
	/// The default is <see langword="true"/>; set to <see langword="false"/> only for best-effort
	/// diagnostic output when a scope imbalance is expected and must not terminate generation.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="initialCapacity"/> is less than zero.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="settings"/> is null or contains invalid values.
	/// </exception>
	public CodeWriter(
		GenerationSettings settings,
		int initialCapacity = DefaultCapacity,
		bool throwOnUnclosedScopes = true
	)
	{
		if (settings is null)
			throw new ArgumentNullException(nameof(settings));
		if (initialCapacity < 0)
			throw new ArgumentOutOfRangeException(nameof(initialCapacity));

		_builder = new(initialCapacity);

		GeneratorName = settings.GeneratorName;
		GeneratorVersion = settings.GeneratorVersion;
		NullableDirectiveMode = settings.NullableDirectiveMode;
		IsNullableContextEnabled = settings.IsNullableContextEnabled;
		ThrowOnUnclosedScopes = throwOnUnclosedScopes;

		DefaultTypeAccessibility = settings.DefaultTypeAccessibility;
		DefaultPropertyAccessibility = settings.DefaultPropertyAccessibility;
		DefaultPropertyGetterAccessibility = settings.DefaultPropertyGetterAccessibility;
		DefaultPropertySetterAccessibility = settings.DefaultPropertySetterAccessibility;
		DefaultFieldAccessibility = settings.DefaultFieldAccessibility;
		DefaultMethodAccessibility = settings.DefaultMethodAccessibility;
		DefaultConstructorAccessibility = settings.DefaultConstructorAccessibility;
		DefaultIndexerAccessibility = settings.DefaultIndexerAccessibility;
		DefaultOperatorAccessibility = settings.DefaultOperatorAccessibility;

		_indentationSize = settings.IndentationSize > 0 ? settings.IndentationSize : DefaultIndentationSize;
		_maximumLineLength = settings.MaximumLineLength > 0 ? settings.MaximumLineLength : DefaultMaximumLineLength;
		_indentCharacter = settings.IndentationStyle == IndentationStyle.Spaces ? ' ' : '\t';

		if (throwOnUnclosedScopes)
			_openScopes = [];
	}

	/// <summary>
	/// Initializes an independent scratch writer that inherits this writer's current configuration
	/// without allocating a fresh <see cref="GenerationSettings"/>.
	/// </summary>
	/// <param name="source">The writer whose configuration is inherited.</param>
	/// <param name="initialCapacity">The initial buffer capacity.</param>
	CodeWriter(CodeWriter source, int initialCapacity)
	{
		_builder = new(initialCapacity);
		GeneratorName = source.GeneratorName;
		GeneratorVersion = source.GeneratorVersion;
		NullableDirectiveMode = source.NullableDirectiveMode;
		IsNullableContextEnabled = source.IsNullableContextEnabled;
		ThrowOnUnclosedScopes = source.ThrowOnUnclosedScopes;
		DefaultIncludeGeneratedAttributes = source.DefaultIncludeGeneratedAttributes;
		DefaultTypeAccessibility = source.DefaultTypeAccessibility;
		DefaultPropertyAccessibility = source.DefaultPropertyAccessibility;
		DefaultPropertyGetterAccessibility = source.DefaultPropertyGetterAccessibility;
		DefaultPropertySetterAccessibility = source.DefaultPropertySetterAccessibility;
		DefaultFieldAccessibility = source.DefaultFieldAccessibility;
		DefaultMethodAccessibility = source.DefaultMethodAccessibility;
		DefaultConstructorAccessibility = source.DefaultConstructorAccessibility;
		DefaultIndexerAccessibility = source.DefaultIndexerAccessibility;
		DefaultOperatorAccessibility = source.DefaultOperatorAccessibility;
		_indentCharacter = source._indentCharacter;
		_indentationSize = source._indentationSize;
		_maximumLineLength = source._maximumLineLength;

		if (ThrowOnUnclosedScopes)
			_openScopes = [];
	}

	/// <summary>
	/// Gets the number of characters currently written.
	/// </summary>
	public int Length => _builder.Length;

	/// <summary>
	/// Gets the number of block or indentation scopes that have been opened but not disposed.
	/// </summary>
	public int OpenScopeCount { get; private set; }

	/// <summary>
	/// Gets whether <see cref="ToString"/> throws when disposable scopes remain open.
	/// </summary>
	/// <remarks>
	/// This value is set at construction. When validation is enabled, the writer tracks every
	/// opened scope so that an unclosed scope throws <see cref="CodeWriterScopeValidationException"/>
	/// with the offending scope's stack trace when the generated source is materialized. When
	/// disabled, the writer still tracks scopes for <see cref="OpenScopeCount"/> but does not throw,
	/// allowing best-effort inspection of structurally broken output during debugging.
	/// </remarks>
	public bool ThrowOnUnclosedScopes { get; }

	/// <summary>
	/// Gets the source generator name used by generated headers and attributes.
	/// </summary>
	public string GeneratorName { get; }

	/// <summary>
	/// Gets the source generator version used by generated headers and attributes.
	/// </summary>
	public string GeneratorVersion { get; }

	/// <summary>
	/// Gets or sets how nullable annotations and the <c>#nullable enable</c> directive are emitted by
	/// <see cref="AutoGeneratedHeader"/> and type rendering. The value is seeded from
	/// <see cref="GenerationSettings.NullableDirectiveMode"/> at construction.
	/// </summary>
	public NullableDirectiveMode NullableDirectiveMode { get; set; }

	/// <summary>
	/// Gets or sets whether the target compilation has nullable annotations enabled. The value is
	/// seeded from <see cref="GenerationSettings.IsNullableContextEnabled"/> at construction and is
	/// <see langword="null"/> when the state is unknown.
	/// </summary>
	/// <remarks>
	/// When <see cref="NullableDirectiveMode"/> is <see cref="NullableDirectiveMode.Always"/> or
	/// <see cref="NullableDirectiveMode.Disable"/>, the mode controls both the header directive and
	/// rendered annotations; this value only drives the <see cref="NullableDirectiveMode.Auto"/> mode.
	/// </remarks>
	public bool? IsNullableContextEnabled { get; set; }

	/// <summary>
	/// Gets or sets whether generated attributes are emitted for declarations that do not
	/// explicitly override <see cref="TypeDeclarationOptions.IncludeGeneratedAttributes"/>,
	/// <see cref="MethodDeclarationOptions.IncludeGeneratedAttributes"/>, or other declaration
	/// option equivalents. The default is <see langword="true"/>.
	/// </summary>
	public bool DefaultIncludeGeneratedAttributes { get; set; } = true;

	/// <summary>
	/// Gets or sets the default accessibility emitted for type declarations (classes, structs, records,
	/// interfaces, enums, and delegates) when a declaration does not specify one. The default is
	/// <see cref="TypeDeclarationAccessibility.Public"/>. Set to <see langword="null"/> to omit the
	/// modifier.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultTypeAccessibility { get; set; } = TypeDeclarationAccessibility.Public;

	/// <summary>
	/// Gets or sets the default accessibility emitted for properties and indexers when a declaration does
	/// not specify one. The default is <see cref="TypeDeclarationAccessibility.Public"/>. Set to
	/// <see langword="null"/> to omit the modifier.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultPropertyAccessibility { get; set; } =
		TypeDeclarationAccessibility.Public;

	/// <summary>
	/// Gets or sets the default accessibility emitted for property and indexer getters when a declaration
	/// does not specify one. The default is <see cref="TypeDeclarationAccessibility.Public"/>. The
	/// modifier is emitted only when it is more restrictive than the property's own accessibility;
	/// otherwise the accessor inherits it.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultPropertyGetterAccessibility { get; set; } =
		TypeDeclarationAccessibility.Public;

	/// <summary>
	/// Gets or sets the default accessibility emitted for property and indexer setters when a declaration
	/// does not specify one. The default is <see cref="TypeDeclarationAccessibility.Public"/>. The
	/// modifier is emitted only when it is more restrictive than the property's own accessibility;
	/// otherwise the accessor inherits it.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultPropertySetterAccessibility { get; set; } =
		TypeDeclarationAccessibility.Public;

	/// <summary>
	/// Gets or sets the default accessibility emitted for field declarations when a declaration does not
	/// specify one. The default is <see cref="TypeDeclarationAccessibility.Private"/>. Set to
	/// <see langword="null"/> to omit the modifier.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultFieldAccessibility { get; set; } = TypeDeclarationAccessibility.Private;

	/// <summary>
	/// Gets or sets the default accessibility emitted for method declarations when a declaration does not
	/// specify one. The default is <see cref="TypeDeclarationAccessibility.Public"/>. Set to
	/// <see langword="null"/> to omit the modifier.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultMethodAccessibility { get; set; } = TypeDeclarationAccessibility.Public;

	/// <summary>
	/// Gets or sets the default accessibility emitted for constructor declarations when a declaration does
	/// not specify one. The default is <see cref="TypeDeclarationAccessibility.Public"/>. Set to
	/// <see langword="null"/> to omit the modifier.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultConstructorAccessibility { get; set; } =
		TypeDeclarationAccessibility.Public;

	/// <summary>
	/// Gets or sets the default accessibility emitted for indexer declarations when a declaration does not
	/// specify one. The default is <see cref="TypeDeclarationAccessibility.Public"/>. Set to
	/// <see langword="null"/> to omit the modifier.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultIndexerAccessibility { get; set; } =
		TypeDeclarationAccessibility.Public;

	/// <summary>
	/// Gets or sets the default accessibility emitted for operator declarations when a declaration does
	/// not specify one. The default is <see cref="TypeDeclarationAccessibility.Public"/>. Set to
	/// <see langword="null"/> to omit the modifier.
	/// </summary>
	public TypeDeclarationAccessibility? DefaultOperatorAccessibility { get; set; } =
		TypeDeclarationAccessibility.Public;

	/// <summary>
	/// Increases the current indentation level.
	/// </summary>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Indent().Line("value");</code></example>
	public CodeWriter Indent()
	{
		_indentLevel++;
		return this;
	}

	/// <summary>
	/// Decreases the current indentation level.
	/// </summary>
	/// <returns>The current writer.</returns>
	/// <exception cref="InvalidOperationException">
	/// The current indentation level is zero.
	/// </exception>
	/// <example><code>writer.Indent().Line("value").Unindent();</code></example>
	public CodeWriter Unindent()
	{
		if (_indentLevel == 0)
			throw new InvalidOperationException("Cannot unindent below zero.");

		_indentLevel--;
		return this;
	}

	/// <summary>
	/// Appends a deterministic line-feed character.
	/// </summary>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Write("first").NewLine().Write("second");</code></example>
	public CodeWriter NewLine()
	{
		_builder.Append(NewLineCharacter);
		_atLineStart = true;
		return this;
	}

	/// <summary>
	/// Moves to the start of a new line if content exists on the current line.
	/// </summary>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Write("value").EnsureNewLine();</code></example>
	public CodeWriter EnsureNewLine()
	{
		return _atLineStart ? this : NewLine();
	}

	/// <summary>
	/// Ensures that the next content starts after a blank line.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="EnsureNewLine"/>, this method also adds a separator when the writer is
	/// already at the start of a line. Calling it repeatedly does not add additional blank lines.
	/// </remarks>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MethodCall("Run").EnsureBlankLine().Comment("Explains the next member.");</code></example>
	public CodeWriter EnsureBlankLine()
	{
		if (_builder.Length == 0)
			return this;

		EnsureNewLine();
		if (_builder.Length < 2 || _builder[_builder.Length - 2] != NewLineCharacter)
			NewLine();

		return this;
	}

	/// <summary>
	/// Writes an optional value followed by a line feed, applying the current indentation.
	/// </summary>
	/// <param name="value">The value to write, or <see langword="null"/> to write an empty line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Line("return value;");</code></example>
	public CodeWriter Line(string? value = null)
	{
		if (value is null)
			return NewLine();

		IndentIfRequired();
		_builder.Append(value);
		_builder.Append(NewLineCharacter);
		_atLineStart = true;
		return this;
	}

	/// <summary>
	/// Writes a line comment or a multi-line comment block.
	/// </summary>
	/// <param name="comments">The comment lines.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Comment("Generated member", "Do not edit.");</code></example>
	public CodeWriter Comment(params string[] comments)
	{
		if (comments is null || comments.Length == 0)
			return this;

		if (comments.Length == 1)
			return Write("// ").Line(comments[0]);

		Line("/*");
		for (var index = 0; index < comments.Length; index++)
			Write(" * ").Line(comments[index]);

		return Line(" */");
	}

	/// <summary>
	/// Writes a value without a trailing line feed.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Write("int value = 1;");</code></example>
	public CodeWriter Write(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return this;

		IndentIfRequired();
		_builder.Append(value);
		return this;
	}

	/// <summary>
	/// Writes a character without a trailing line feed.
	/// </summary>
	/// <param name="value">The character to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Write('{');</code></example>
	public CodeWriter Write(char value)
	{
		IndentIfRequired();
		_builder.Append(value);
		return this;
	}

	/// <summary>
	/// Writes a value without a trailing line feed.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Append("int value = 1;");</code></example>
	public CodeWriter Append(string? value) => Write(value);

	/// <summary>
	/// Writes an optional value followed by a line feed.
	/// </summary>
	/// <param name="value">The value to write, or <see langword="null"/> for an empty line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.AppendLine("return value;");</code></example>
	public CodeWriter AppendLine(string? value = null) => Line(value);

	/// <summary>
	/// Writes a value when the supplied condition is true.
	/// </summary>
	/// <param name="condition">Whether to write the value.</param>
	/// <param name="value">The value to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.If(includeValue, "value");</code></example>
	public CodeWriter If(bool condition, string? value) => condition ? Write(value) : this;

	/// <summary>
	/// Writes a line when the supplied condition is true.
	/// </summary>
	/// <param name="condition">Whether to write the line.</param>
	/// <param name="value">The value to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.LineIf(includeValue, "value");</code></example>
	public CodeWriter LineIf(bool condition, string? value) => condition ? Line(value) : this;

	/// <summary>
	/// Writes a value surrounded by double quotes.
	/// </summary>
	/// <param name="value">The value to quote.</param>
	/// <returns>The current writer.</returns>
	/// <remarks>
	/// This method does not escape characters contained in <paramref name="value"/>.
	/// </remarks>
	/// <example><code>writer.Quote("value"); // "value"</code></example>
	public CodeWriter Quote(string? value = null)
	{
		Write('"');
		Write(value);
		return Write('"');
	}

	/// <summary>
	/// Writes a quoted value followed by a line feed.
	/// </summary>
	/// <param name="value">The value to quote.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.QuoteLine("value"); // "value"</code></example>
	public CodeWriter QuoteLine(string? value = null) => Quote(value).NewLine();

	/// <summary>
	/// Opens an indented scope and returns a value that closes it when disposed.
	/// </summary>
	/// <param name="header">Optional content written before the opening brace.</param>
	/// <returns>A scope that restores indentation and writes the closing token.</returns>
	/// <example><code>using (writer.OpenBlockScope("if (enabled)")) writer.Line("Run();");
	/// // if (enabled)
	/// // {
	/// // 	Run();
	/// // }</code></example>
	public BlockScope OpenBlockScope(string? header = null) => OpenDelimitedBlockScope(header, "{", "}");

	/// <summary>
	/// Opens a C# 14 extension-member block for the specified receiver type.
	/// </summary>
	/// <param name="receiver">The plain named type being extended, such as the framework <c>TypeLibrary</c>.</param>
	/// <returns>The extension block body scope.</returns>
	/// <remarks>
	/// The receiver must be a plain named type — composed references (arrays, pointers, nullable
	/// annotations, type parameters and <see langword="dynamic"/>) and the null literal are rejected.
	/// </remarks>
	/// <example><code>using (writer.ExtensionBlockScope(new TypeIdentity("PurviewTypeLibrary", "Purview.SourceGeneratorFramework").AsTypeReference()))
	/// 	writer.Line("public static string Name => \"value\";");</code></example>
	public BlockScope ExtensionBlockScope(TypeReference receiver)
	{
		if (receiver is null)
			throw new ArgumentNullException(nameof(receiver));

		if (receiver.IsEmpty)
			return default;

		if (!receiver.IsPlainNamedType || receiver.IsNullLiteral)
			throw new ArgumentException("An extension block receiver must be a plain named type.", nameof(receiver));

		Write("extension(");
		Type(receiver);
		Write(")");
		EnsureNewLine();
		Line("{");
		Indent();

		return TrackOpenBlockScope("extension(...)", "}");
	}

	/// <summary>
	/// Writes a complete C# 14 extension-member block and invokes a callback for its body.
	/// </summary>
	/// <param name="receiver">The plain named type being extended.</param>
	/// <param name="bodyWriter">The action that writes the extension block body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.ExtensionBlock(new TypeIdentity("PurviewTypeLibrary", "Purview.SourceGeneratorFramework").AsTypeReference(), body =&gt; body.Line("public static string Name => \"value\";"));</code></example>
	public CodeWriter ExtensionBlock(TypeReference receiver, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (ExtensionBlockScope(receiver))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes a complete block and invokes a callback for its body.
	/// </summary>
	/// <example><code>writer.OpenBlock("if (enabled)", body =&gt; body.Line("Run();"));</code></example>
	public CodeWriter OpenBlock(string? header, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (OpenBlockScope(header))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Opens an indented scope using explicit opening and closing tokens.
	/// </summary>
	/// <param name="header">Optional content written before the opening token.</param>
	/// <param name="openingToken">The opening token, or <see langword="null"/> for none.</param>
	/// <param name="closingToken">The closing token, or <see langword="null"/> for none.</param>
	/// <returns>A scope that restores indentation and writes the closing token.</returns>
	/// <example><code>using (writer.OpenDelimitedBlockScope("items", "(", ");")) writer.Line("value");
	/// // items
	/// // (
	/// // 	value
	/// // );</code></example>
	public BlockScope OpenDelimitedBlockScope(string? header, string? openingToken, string? closingToken)
	{
		if (header is not null)
		{
			Write(header);
			EnsureNewLine();
		}

		if (openingToken is not null)
			Line(openingToken);

		Indent();
		return TrackOpenBlockScope(header, closingToken);
	}

	/// <summary>
	/// Writes a complete explicitly delimited block and invokes a callback for its body.
	/// </summary>
	/// <example><code>writer.OpenDelimitedBlock("items", "(", ");", body =&gt; body.Line("value"));</code></example>
	public CodeWriter OpenDelimitedBlock(
		string? header,
		string? openingToken,
		string? closingToken,
		Action<CodeWriter> bodyWriter
	)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));
		using (OpenDelimitedBlockScope(header, openingToken, closingToken))
			bodyWriter(this);
		return this;
	}

	/// <summary>
	/// Opens an indented scope whose header is completed by a callback before the opening token.
	/// </summary>
	/// <param name="header">Optional content written before the additional header parts.</param>
	/// <param name="writeRemainingHeader">Writes content appended to the header.</param>
	/// <param name="openingToken">The opening token, or <see langword="null"/> for none.</param>
	/// <param name="closingToken">The closing token, or <see langword="null"/> for none.</param>
	/// <returns>A scope that restores indentation and writes the closing token.</returns>
	/// <example><code>using (writer.OpenDelimitedBlockWithHeaderScope("Call", w =&gt; w.Write("(value)"), "{", "}"))
	/// 	writer.Line("Run();");</code></example>
	public BlockScope OpenDelimitedBlockWithHeaderScope(
		string? header,
		Action<CodeWriter> writeRemainingHeader,
		string? openingToken,
		string? closingToken
	)
	{
		if (writeRemainingHeader is null)
			throw new ArgumentNullException(nameof(writeRemainingHeader));

		if (header is not null)
			Write(header);

		writeRemainingHeader(this);
		EnsureNewLine();

		if (openingToken is not null)
			Line(openingToken);

		Indent();
		return TrackOpenBlockScope(header, closingToken);
	}

	/// <summary>
	/// Writes a complete delimited block with a callback-completed header and body.
	/// </summary>
	/// <example><code>writer.OpenDelimitedBlockWithHeader("Call", w =&gt; w.Write("(value)"), "{", "}", body =&gt; body.Line("Run();"));</code></example>
	public CodeWriter OpenDelimitedBlockWithHeader(
		string? header,
		Action<CodeWriter> writeRemainingHeader,
		string? openingToken,
		string? closingToken,
		Action<CodeWriter> bodyWriter
	)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));
		using (OpenDelimitedBlockWithHeaderScope(header, writeRemainingHeader, openingToken, closingToken))
			bodyWriter(this);
		return this;
	}

	/// <summary>
	/// Writes a complete scoped block by invoking the supplied body.
	/// </summary>
	/// <param name="header">Optional content written before the opening token.</param>
	/// <param name="body">The block body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Block("if (enabled)", body =&gt; body.Line("Run();"));</code></example>
	public CodeWriter Block(string? header, Action<CodeWriter> body) => DelimitedBlock(header, "{", "}", body);

	/// <summary>
	/// Writes a complete scope using explicit opening and closing tokens.
	/// </summary>
	/// <param name="header">Optional content written before the opening token.</param>
	/// <param name="openingToken">The opening token, or <see langword="null"/> for none.</param>
	/// <param name="closingToken">The closing token, or <see langword="null"/> for none.</param>
	/// <param name="body">The block body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.DelimitedBlock("items", "(", ");", body =&gt; body.Line("value"));</code></example>
	public CodeWriter DelimitedBlock(
		string? header,
		string? openingToken,
		string? closingToken,
		Action<CodeWriter> body
	)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));

		using (OpenDelimitedBlockScope(header, openingToken, closingToken))
			body(this);

		return this;
	}

	/// <summary>
	/// Writes a structured method declaration and returns its body scope.
	/// </summary>
	/// <param name="declaration">The method declaration.</param>
	/// <returns>
	/// The method body scope, or an empty scope when an abstract or expression-bodied method was
	/// emitted.
	/// </returns>
	/// <example><code>using (writer.MethodScope(new MethodDeclarationOptions("Run"))) writer.Line("return;");</code></example>
	public BlockScope MethodScope(MethodDeclarationOptions declaration) =>
		MethodScope(declaration, expressionWriter: null);

	BlockScope MethodScope(MethodDeclarationOptions declaration, Action<CodeWriter>? expressionWriter)
	{
		if (declaration.ReturnType.IsEmpty)
			return default;

		MethodHeader(declaration);

		if (declaration.IsPartial)
		{
			Write(';').NewLine();

			CompleteWrittenItem(WrittenItemKind.Method, _indentLevel);
			return default;
		}

		if (declaration.ExpressionBody is not null)
		{
			Write(" => ");
			Expression(declaration.ExpressionBody, expressionWriter);
			Line(";");
			CompleteWrittenItem(WrittenItemKind.Method, _indentLevel);
			return default;
		}

		if (declaration.IsAbstract)
		{
			Line(";");
			CompleteWrittenItem(WrittenItemKind.Method, _indentLevel);
			return default;
		}

		NewLine();
		return OpenBlockScope(WrittenItemKind.Method);
	}

	void MethodHeader(MethodDeclarationOptions declaration)
	{
		ValidateMethodDeclaration(declaration);
		BeginWrittenItem(WrittenItemKind.Method);

		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: true, includeEmbeddedAttribute: false);

		Attributes(declaration.Attributes);
		Attributes(declaration.ReturnAttributes, defaultTarget: "return");

		MemberModifiers(
			ResolveAccessibility(declaration.Accessibility, DefaultMethodAccessibility),
			declaration.IsStatic,
			declaration.IsAbstract,
			declaration.IsVirtual,
			declaration.IsOverride,
			declaration.IsSealed,
			isReadOnly: declaration.IsReadOnly
		);

		If(declaration.IsAsync, "async ").If(declaration.IsUnsafe, "unsafe ");
		If(declaration.IsPartial, "partial ").TypeReference(declaration.ReturnType).Write(' ').Write(declaration.Name);

		GenericTypeParameters(declaration.GenericTypes);
		ParametersWithHeuristic(declaration.Parameters);
		if (HasGenericConstraints(declaration.GenericTypes))
			NewLine();

		MethodGenericConstraints(declaration.GenericTypes);
	}

	/// <summary>
	/// Writes a structured partial method declaration.
	/// </summary>
	/// <example><code>writer.PartialMethod(new MethodDeclarationOptions("OnChanged"));</code></example>
	public CodeWriter PartialMethod(MethodDeclarationOptions declaration)
	{
		MethodScope(declaration with { IsPartial = true });
		return this;
	}

	/// <summary>
	/// Writes an expression-bodied method.
	/// </summary>
	/// <example><code>writer.MethodExpression(new MethodDeclarationOptions("Count", "int") { ExpressionBody = "items.Count" });</code></example>
	public CodeWriter MethodExpression(MethodDeclarationOptions declaration)
	{
		if (string.IsNullOrWhiteSpace(declaration.ExpressionBody))
		{
			throw new ArgumentException(
				"An expression-bodied method must have a non-empty expression body.",
				nameof(declaration)
			);
		}

		using (MethodScope(declaration))
		{
			//
		}

		return this;
	}

	/// <summary>
	/// Writes an expression-bodied method using a callback for the expression.
	/// </summary>
	/// <example><code>writer.MethodExpression(new MethodDeclarationOptions("Count", "int"), expression =&gt; expression.Write("items.Count"));</code></example>
	public CodeWriter MethodExpression(MethodDeclarationOptions declaration, Action<CodeWriter> writeExpression)
	{
		if (writeExpression is null)
			throw new ArgumentNullException(nameof(writeExpression));
		if (declaration.ExpressionBody is not null)
			throw new ArgumentException(
				"A callback expression cannot be supplied when ExpressionBody is already set.",
				nameof(declaration)
			);

		using (MethodScope(declaration with { ExpressionBody = string.Empty }, writeExpression))
		{
			//
		}

		return this;
	}

	/// <summary>
	/// Writes a structured method and invokes a callback for its body.
	/// </summary>
	/// <example><code>writer.Method(new MethodDeclarationOptions("Run"), body =&gt; body.Line("return;"));</code></example>
	public CodeWriter Method(MethodDeclarationOptions declaration, Action<CodeWriter> writeBody)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		if (declaration.IsAbstract || declaration.ExpressionBody is not null)
		{
			throw new ArgumentException(
				"A callback body cannot be supplied for an abstract or expression-bodied method.",
				nameof(declaration)
			);
		}

		if (declaration.IsPartial)
		{
			if (declaration.ReturnType.IsEmpty)
				return this;

			MethodHeader(declaration);
			NewLine();
			using (OpenBlockScope(WrittenItemKind.Method))
				writeBody(this);

			return this;
		}

		using (MethodScope(declaration))
			writeBody(this);

		return this;
	}

	/// <summary>
	/// Writes a structured operator declaration and returns its body scope.
	/// </summary>
	/// <param name="declaration">The operator declaration.</param>
	/// <returns>
	/// The operator body scope, or an empty scope when an expression-bodied operator was emitted.
	/// </returns>
	/// <example><code>using (writer.OperatorScope(new OperatorDeclarationOptions("==", PurviewTypeLibrary.System.Boolean, left, right))) writer.Line("return left.Equals(right);");</code></example>
	public BlockScope OperatorScope(OperatorDeclarationOptions declaration)
	{
		if (declaration.ReturnType.IsEmpty)
			return default;

		ValidateOperatorDeclaration(declaration);
		BeginWrittenItem(WrittenItemKind.Method);

		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: true, includeEmbeddedAttribute: false);

		Attributes(declaration.Attributes);

		if (ResolveAccessibility(declaration.Accessibility, DefaultOperatorAccessibility) is { } accessibility)
			Accessibility(accessibility).Write(' ');

		If(declaration.IsStatic, "static ");
		switch (declaration.Kind)
		{
			case OperatorDeclarationKind.ImplicitConversion:
			case OperatorDeclarationKind.ExplicitConversion:
				Write(declaration.Kind == OperatorDeclarationKind.ImplicitConversion ? "implicit " : "explicit ");
				Write("operator ").TypeReference(declaration.ReturnType);
				ParametersWithHeuristic([declaration.Left]);
				break;

			case OperatorDeclarationKind.Unary:
				TypeReference(declaration.ReturnType).Write(" operator ").Write(declaration.OperatorToken);
				ParametersWithHeuristic([declaration.Left]);
				break;

			case OperatorDeclarationKind.Binary:
				TypeReference(declaration.ReturnType).Write(" operator ").Write(declaration.OperatorToken);
				ParametersWithHeuristic([declaration.Left, declaration.Right]);
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(declaration));
		}

		if (declaration.ExpressionBody is not null)
		{
			Write(" => ");
			Expression(declaration.ExpressionBody, expressionWriter: null);
			Line(";");
			CompleteWrittenItem(WrittenItemKind.Method, _indentLevel);
			return default;
		}

		NewLine();
		return OpenBlockScope(WrittenItemKind.Method);
	}

	/// <summary>
	/// Writes a structured operator declaration and invokes a callback for its body.
	/// </summary>
	/// <param name="declaration">The operator declaration.</param>
	/// <param name="writeBody">The action that writes the operator body.</param>
	/// <returns>The current writer.</returns>
	/// <exception cref="ArgumentException">The operator has an expression body.</exception>
	/// <example><code>writer.Operator(new OperatorDeclarationOptions("==", PurviewTypeLibrary.System.Boolean, left, right), body =&gt; body.Line("return left.Equals(right);"));</code></example>
	public CodeWriter Operator(OperatorDeclarationOptions declaration, Action<CodeWriter> writeBody)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));
		if (declaration.ExpressionBody is not null)
			throw new ArgumentException(
				"A callback body cannot be supplied for an expression-bodied operator.",
				nameof(declaration)
			);

		using (OperatorScope(declaration))
			writeBody(this);

		return this;
	}

	/// <summary>
	/// Writes an auto-property or expression-bodied property.
	/// </summary>
	/// <example><code>writer.Property(new PropertyDeclarationOptions("Name", "string"));</code></example>
	public CodeWriter Property(PropertyDeclarationOptions declaration)
	{
		if (declaration.Type.IsEmpty)
			return this;
		ValidatePropertyDeclaration(declaration);
		var propertyAccessibility = ResolveAccessibility(declaration.Accessibility, DefaultPropertyAccessibility);
		BeginWrittenItem(WrittenItemKind.Property);
		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: true, includeEmbeddedAttribute: false);
		Attributes(declaration.Attributes);
		PropertyHeader(declaration);
		if (declaration.ExpressionBody is not null)
		{
			Write(" => ");
			Expression(declaration.ExpressionBody, expressionWriter: null);
			Line(";");
			CompleteWrittenItem(WrittenItemKind.Property, _indentLevel);
			return this;
		}

		Write(" { ");
		if (declaration.IsFieldBacked)
		{
			// C# 14 field-keyword semi-auto property: accessors reference the implicit backing field.
			if (declaration.HasGetter)
				Write("get => field; ");
			if (declaration.HasSetter || declaration.IsInitOnly)
				Write(declaration.IsInitOnly ? "init => field = value; " : "set => field = value; ");
		}
		else
		{
			if (declaration.HasGetter)
			{
				Accessor(
					ResolveAccessorAccessibility(
						declaration.GetterAccessibility,
						DefaultPropertyGetterAccessibility,
						propertyAccessibility
					),
					"get;"
				);
			}

			if (declaration.HasSetter || declaration.IsInitOnly)
			{
				Accessor(
					ResolveAccessorAccessibility(
						declaration.SetterAccessibility,
						DefaultPropertySetterAccessibility,
						propertyAccessibility
					),
					declaration.IsInitOnly ? "init;" : "set;"
				);
			}
		}

		Write("}");
		if (declaration.Initializer is not null)
		{
			Write(" = ");
			Expression(declaration.Initializer, expressionWriter: null);
			Write(';');
		}
		NewLine();
		CompleteWrittenItem(WrittenItemKind.Property, _indentLevel);
		return this;
	}

	/// <summary>
	/// Writes an expression-bodied property using a callback for the expression.
	/// </summary>
	/// <example><code>writer.PropertyExpression(new PropertyDeclarationOptions("Count", "int"), expression =&gt; expression.Write("items.Count"));</code></example>
	public CodeWriter PropertyExpression(PropertyDeclarationOptions declaration, Action<CodeWriter> writeExpression)
	{
		if (writeExpression is null)
			throw new ArgumentNullException(nameof(writeExpression));
		if (declaration.ExpressionBody is not null)
			throw new ArgumentException(
				"A callback expression cannot be supplied when ExpressionBody is already set.",
				nameof(declaration)
			);
		if (declaration.Initializer is not null)
			throw new ArgumentException(
				"An expression-bodied property cannot specify an initializer.",
				nameof(declaration)
			);

		ValidatePropertyDeclaration(declaration with { ExpressionBody = "callback" });
		BeginWrittenItem(WrittenItemKind.Property);
		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: true, includeEmbeddedAttribute: false);
		Attributes(declaration.Attributes);
		PropertyHeader(declaration).Write(" => ");
		Expression(null, writeExpression);
		Line(";");
		CompleteWrittenItem(WrittenItemKind.Property, _indentLevel);
		return this;
	}

	/// <summary>
	/// Writes a property with callback-generated accessor bodies.
	/// </summary>
	/// <example><code>writer.Property(new PropertyDeclarationOptions("Value", "int"), get =&gt; get.Line("return _value;"), null);</code></example>
	public CodeWriter Property(
		PropertyDeclarationOptions declaration,
		Action<CodeWriter>? writeGetterBody,
		Action<CodeWriter>? writeSetterBody
	)
	{
		ValidatePropertyDeclaration(declaration);
		if (declaration.ExpressionBody is not null || declaration.Initializer is not null || declaration.IsFieldBacked)
			throw new ArgumentException(
				"A property with accessor bodies cannot specify an expression body, initializer, or the field keyword.",
				nameof(declaration)
			);
		if (declaration.IsAbstract)
			throw new ArgumentException(
				"Accessor bodies cannot be supplied for an abstract property.",
				nameof(declaration)
			);

		var propertyAccessibility = ResolveAccessibility(declaration.Accessibility, DefaultPropertyAccessibility);
		BeginWrittenItem(WrittenItemKind.Property);
		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: true, includeEmbeddedAttribute: false);
		Attributes(declaration.Attributes);
		PropertyHeader(declaration).NewLine();
		using (OpenBlockScope())
		{
			if (declaration.HasGetter)
			{
				AccessorBody(
					ResolveAccessorAccessibility(
						declaration.GetterAccessibility,
						DefaultPropertyGetterAccessibility,
						propertyAccessibility
					),
					"get",
					writeGetterBody
				);
			}

			if (declaration.HasSetter || declaration.IsInitOnly)
			{
				AccessorBody(
					ResolveAccessorAccessibility(
						declaration.SetterAccessibility,
						DefaultPropertySetterAccessibility,
						propertyAccessibility
					),
					declaration.IsInitOnly ? "init" : "set",
					writeSetterBody
				);
			}
		}
		CompleteWrittenItem(WrittenItemKind.Property, _indentLevel);
		return this;
	}

	/// <summary>
	/// Writes an indexer declaration with auto accessors or an expression body.
	/// </summary>
	/// <param name="declaration">The indexer declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Indexer(new IndexerDeclarationOptions(Type("string"), new("index", Type("int"))));</code></example>
	public CodeWriter Indexer(IndexerDeclarationOptions declaration)
	{
		if (declaration.Type.IsEmpty)
			return this;
		ValidateIndexerDeclaration(declaration);
		var indexerAccessibility = ResolveAccessibility(declaration.Accessibility, DefaultIndexerAccessibility);
		BeginWrittenItem(WrittenItemKind.Property);
		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: true, includeEmbeddedAttribute: false);
		Attributes(declaration.Attributes);
		IndexerHeader(declaration);
		if (declaration.ExpressionBody is not null)
		{
			Write(" => ");
			Expression(declaration.ExpressionBody, expressionWriter: null);
			Line(";");
			CompleteWrittenItem(WrittenItemKind.Property, _indentLevel);
			return this;
		}

		Write(" { ");
		if (declaration.HasGetter)
		{
			Accessor(
				ResolveAccessorAccessibility(
					declaration.GetterAccessibility,
					DefaultPropertyGetterAccessibility,
					indexerAccessibility
				),
				"get;"
			);
		}

		if (declaration.HasSetter || declaration.IsInitOnly)
		{
			Accessor(
				ResolveAccessorAccessibility(
					declaration.SetterAccessibility,
					DefaultPropertySetterAccessibility,
					indexerAccessibility
				),
				declaration.IsInitOnly ? "init;" : "set;"
			);
		}

		Write("}");
		NewLine();
		CompleteWrittenItem(WrittenItemKind.Property, _indentLevel);
		return this;
	}

	/// <summary>
	/// Writes an indexer with callback-generated accessor bodies.
	/// </summary>
	/// <param name="declaration">The indexer declaration.</param>
	/// <param name="writeGetterBody">The action that writes the getter body, or <see langword="null"/> for an auto getter.</param>
	/// <param name="writeSetterBody">The action that writes the setter body, or <see langword="null"/> for an auto setter.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Indexer(new IndexerDeclarationOptions(Type("string"), new("index", Type("int"))), get =&gt; get.Line("return _items[index];"), null);</code></example>
	public CodeWriter Indexer(
		IndexerDeclarationOptions declaration,
		Action<CodeWriter>? writeGetterBody,
		Action<CodeWriter>? writeSetterBody
	)
	{
		ValidateIndexerDeclaration(declaration);
		if (declaration.ExpressionBody is not null)
			throw new ArgumentException(
				"An indexer with accessor bodies cannot specify an expression body.",
				nameof(declaration)
			);
		if (declaration.IsAbstract)
			throw new ArgumentException(
				"Accessor bodies cannot be supplied for an abstract indexer.",
				nameof(declaration)
			);

		var indexerAccessibility = ResolveAccessibility(declaration.Accessibility, DefaultIndexerAccessibility);
		BeginWrittenItem(WrittenItemKind.Property);
		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: true, includeEmbeddedAttribute: false);
		Attributes(declaration.Attributes);
		IndexerHeader(declaration).NewLine();
		using (OpenBlockScope())
		{
			if (declaration.HasGetter)
			{
				AccessorBody(
					ResolveAccessorAccessibility(
						declaration.GetterAccessibility,
						DefaultPropertyGetterAccessibility,
						indexerAccessibility
					),
					"get",
					writeGetterBody
				);
			}

			if (declaration.HasSetter || declaration.IsInitOnly)
			{
				AccessorBody(
					ResolveAccessorAccessibility(
						declaration.SetterAccessibility,
						DefaultPropertySetterAccessibility,
						indexerAccessibility
					),
					declaration.IsInitOnly ? "init" : "set",
					writeSetterBody
				);
			}
		}
		CompleteWrittenItem(WrittenItemKind.Property, _indentLevel);
		return this;
	}

	CodeWriter IndexerHeader(IndexerDeclarationOptions declaration)
	{
		MemberModifiers(
			ResolveAccessibility(declaration.Accessibility, DefaultIndexerAccessibility),
			declaration.IsStatic,
			declaration.IsAbstract,
			declaration.IsVirtual,
			declaration.IsOverride,
			declaration.IsSealed
		);
		TypeReference(declaration.Type).Write(" this[");
		for (var index = 0; index < declaration.Parameters.Length; index++)
		{
			if (index != 0)
				Write(", ");
			Parameter(declaration.Parameters[index]);
		}
		return Write(']');
	}

	/// <summary>
	/// Writes a field declaration.
	/// </summary>
	/// <example><code>writer.Field(new FieldDeclarationOptions("_value", "int"));</code></example>
	public CodeWriter Field(FieldDeclarationOptions declaration)
	{
		if (declaration.Type.IsEmpty)
			return this;
		ValidateFieldDeclaration(declaration);
		BeginWrittenItem(WrittenItemKind.Field);
		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: false, includeEmbeddedAttribute: false);
		Attributes(declaration.Attributes);
		if (ResolveAccessibility(declaration.Accessibility, DefaultFieldAccessibility) is { } accessibility)
			Accessibility(accessibility).Write(' ');
		If(declaration.IsRequired, "required ")
			.If(declaration.IsConst, "const ")
			.If(declaration.IsStatic && !declaration.IsConst, "static ")
			.If(declaration.IsReadOnly, "readonly ")
			.If(declaration.IsVolatile, "volatile ")
			.If(declaration.IsRefField, "ref ")
			.TypeReference(declaration.Type)
			.Write(' ')
			.Write(declaration.Name);
		if (declaration.Initializer is not null)
		{
			Write(" = ");
			Expression(declaration.Initializer, expressionWriter: null);
		}
		Line(";");
		CompleteWrittenItem(WrittenItemKind.Field, _indentLevel);
		return this;
	}

	/// <summary>
	/// Writes a C# using directive.
	/// </summary>
	/// <param name="namespaceName">The namespace to import.</param>
	/// <param name="isGlobal">Whether the directive is emitted as a <c>global using</c>.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Using("System"); // using System;</code></example>
	public CodeWriter Using(string namespaceName, bool isGlobal = false)
	{
		return string.IsNullOrWhiteSpace(namespaceName)
			? throw new ArgumentException("Namespace cannot be null or whitespace.", nameof(namespaceName))
			: Write(isGlobal ? "global using " : "using ").Write(namespaceName).Line(";");
	}

	/// <summary>
	/// Writes a C# using alias directive.
	/// </summary>
	/// <param name="alias">The alias name.</param>
	/// <param name="target">The aliased namespace or type.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.UsingAlias("Events", "global::Purview.Events"); // using Events = global::Purview.Events;</code></example>
	public CodeWriter UsingAlias(string alias, string target)
	{
		if (string.IsNullOrWhiteSpace(alias))
			throw new ArgumentException("Alias cannot be null or whitespace.", nameof(alias));
		if (string.IsNullOrWhiteSpace(target))
			throw new ArgumentException("Alias target cannot be null or whitespace.", nameof(target));

		// The alias directive is not indented, so we don't call IndentIfRequired().
		return Write("using ").Write(alias).Write(" = ").Write(target).Line(";");
	}

	/// <summary>
	/// Writes a <c>#region</c> directive and returns a scope that restores indentation and emits
	/// <c>#endregion</c> when disposed.
	/// </summary>
	/// <param name="name">The region name.</param>
	/// <returns>The region scope.</returns>
	/// <example><code>using (writer.OpenRegionScope("Generated members")) writer.Line("public int Value { get; }");</code></example>
	public BlockScope OpenRegionScope(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Region name cannot be null or whitespace.", nameof(name));

		EnsureBlankLine();
		Write("#region ").Line(name);
		Indent();
		return TrackOpenBlockScope(header: null, closingSeparator: "#endregion");
	}

	/// <summary>
	/// Writes a <c>#region</c> and invokes a callback for its body.
	/// </summary>
	/// <param name="name">The region name.</param>
	/// <param name="body">The action that writes the region body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.OpenRegion("Generated members", body =&gt; body.Property(new PropertyDeclarationOptions("Value", "int")));</code></example>
	public CodeWriter OpenRegion(string name, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (OpenRegionScope(name))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>#if</c> directive at column zero and returns a scope that writes <c>#endif</c> at column
	/// zero when disposed. Content written inside the scope is indented normally.
	/// </summary>
	/// <param name="expression">The preprocessor condition written after <c>#if</c>.</param>
	/// <returns>The conditional-compilation scope.</returns>
	/// <example><code>using (writer.HashDefinesScope("!EXCLUDE_PURVIEW_TELEMETRY_LOGGING")) writer.FileScopedNamespace("Example");</code></example>
	public BlockScope HashDefinesScope(string expression)
	{
		ValidateStatementPart(expression, nameof(expression));
		if (_indentLevel == 0)
			EnsureBlankLine();
		DirectiveLine("#if " + expression);
		return TrackOpenBlockScope(
			header: null,
			closingSeparator: "#endif",
			closingAtColumnZero: true,
			changesIndentation: false
		);
	}

	/// <summary>
	/// Writes a <c>#if</c>/<c>#endif</c> block, with both directives at column zero, and invokes a callback
	/// for its body.
	/// </summary>
	/// <param name="expression">The preprocessor condition written after <c>#if</c>.</param>
	/// <param name="body">The action that writes the conditional block body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.HashDefines("NET", body =&gt; body.Line("// NET only"));</code></example>
	public CodeWriter HashDefines(string expression, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (HashDefinesScope(expression))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes an <c>#else</c> directive at column zero, typically between the two bodies of a
	/// <c>HashDefinesScope</c> block. The else body is written at the same indentation as the if body.
	/// </summary>
	/// <returns>The current writer.</returns>
	/// <example><code>using (writer.HashDefinesScope("NET")) { writer.Line("// NET"); writer.HashElse(); writer.Line("// other"); }</code></example>
	public CodeWriter HashElse()
	{
		DirectiveLine("#else");

		// A directive between two declarations must not participate in member blank-line spacing, so
		// reset the tracker to prevent BeginWrittenItem from inserting a blank line across the #else.
		_lastWrittenItem = WrittenItemKind.None;
		return this;
	}

	/// <summary>
	/// Returns a no-op scope that writes nothing and performs no indentation changes when disposed, so a
	/// conditional block can be wrapped only when a guard requires it.
	/// </summary>
	/// <returns>A scope that does nothing when disposed.</returns>
	/// <example><code>using var scope = wrapped ? writer.EmptyScope() : writer.HashDefinesScope("EXCLUDE_PURVIEW_TELEMETRY_LOGGING");</code></example>
	[SuppressMessage("Performance", "CA1822:Mark members as static")]
	public BlockScope EmptyScope() => default;

	/// <summary>
	/// Invokes a callback without opening any scope, mirroring the action form of the scope-returning
	/// methods so conditional wrapping can choose between <c>Empty</c> and a real scope.
	/// </summary>
	/// <param name="body">The action to invoke.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Empty(body =&gt; body.Line("value"));</code></example>
	public CodeWriter Empty(Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		body(this);
		return this;
	}

	/// <summary>
	/// Writes a block-scoped namespace and returns its body scope.
	/// </summary>
	/// <param name="namespaceName">The namespace, or <see langword="null"/> to write nothing.</param>
	/// <returns>The namespace body scope, or an empty scope when no namespace is supplied.</returns>
	/// <example><code>using (writer.BlockNamespaceScope("Example")) writer.Line("class C { }");</code></example>
	public BlockScope BlockNamespaceScope(string? namespaceName)
	{
		if (string.IsNullOrWhiteSpace(namespaceName))
			return default;

		BeginWrittenItem(WrittenItemKind.Namespace);
		Write("namespace ").Line(namespaceName);
		return OpenBlockScope(WrittenItemKind.Namespace);
	}

	/// <summary>
	/// Writes a block-scoped namespace and invokes a callback for its body.
	/// </summary>
	/// <param name="typeReference">The type reference whose namespace will be used, or a value with no namespace to omit the wrapper.</param>
	/// <param name="bodyWriter">The action that writes the namespace body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.BlockNamespace(new TypeValueObject("C", "Example").AsTypeReference(), body =&gt; body.Line("class C { }"));</code></example>
	public CodeWriter BlockNamespace(TypeReference typeReference, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (BlockNamespaceScope(typeReference))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes a block-scoped namespace and returns its body scope.
	/// </summary>
	/// <param name="typeReference">The type reference whose namespace will be used, or a value with no namespace to return an empty scope.</param>
	/// <returns>The namespace body scope, or an empty scope when no namespace is supplied.</returns>
	/// <example><code>using (writer.BlockNamespaceScope(new TypeValueObject("C", "Example").AsTypeReference())) writer.Line("class C { }");</code></example>
	public IDisposable BlockNamespaceScope(TypeReference? typeReference) =>
		typeReference is null ? NoOpScope.Instance : BlockNamespaceScope(typeReference.Identity.Namespace);

	/// <summary>
	/// Writes a block-scoped namespace and invokes a callback for its body.
	/// </summary>
	/// <param name="namespaceName">The namespace, or <see langword="null"/> to omit the wrapper.</param>
	/// <param name="bodyWriter">The action that writes the namespace body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.BlockNamespace("Example", body =&gt; body.Line("class C { }"));</code></example>
	public CodeWriter BlockNamespace(string? namespaceName, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));
		using (BlockNamespaceScope(namespaceName))
			bodyWriter(this);
		return this;
	}

	/// <summary>
	/// Writes a file-scoped namespace followed by an empty line.
	/// </summary>
	/// <param name="namespaceName">The namespace, or <see langword="null"/> to write nothing.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.FileScopedNamespace("Example"); // namespace Example;</code></example>
	public CodeWriter FileScopedNamespace(string? namespaceName)
	{
		return string.IsNullOrWhiteSpace(namespaceName)
			? this
			: Write("namespace ").Write(namespaceName).Line(";").NewLine();
	}

	/// <summary>
	/// Writes a file-scoped namespace followed by an empty line.
	/// </summary>
	/// <param name="typeReference">The type reference whose namespace will be used, or a value with no namespace to write nothing.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.FileScopedNamespace(new TypeValueObject("C", "Example").AsTypeReference());</code></example>
	public CodeWriter FileScopedNamespace(TypeReference? typeReference) =>
		typeReference is null ? this : FileScopedNamespace(typeReference.Identity.Namespace);

	/// <summary>
	/// Writes a class declaration from structured options and returns its body scope.
	/// </summary>
	/// <param name="declaration">The class declaration options.</param>
	/// <returns>The class body scope.</returns>
	/// <example><code>using (writer.ClassScope(new TypeDeclarationOptions("C"))) writer.Line("// body");</code></example>
	public BlockScope ClassScope(TypeDeclarationOptions declaration)
	{
		return declaration is null
			? throw new ArgumentNullException(nameof(declaration))
			: TypeScope(declaration with { Kind = TypeDeclarationKind.Class });
	}

	/// <summary>
	/// Writes a class declaration from structured options and returns its body scope.
	/// </summary>
	/// <param name="declaration">The class declaration options.</param>
	/// <param name="bodyWriter">The action that writes the body of the class.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Class(new TypeDeclarationOptions("C"), body =&gt; body.Line("// body"));</code></example>
	public CodeWriter Class(TypeDeclarationOptions declaration, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (ClassScope(declaration))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes an attribute class with an <see cref="AttributeUsageAttribute"/> declaration.
	/// </summary>
	/// <param name="declaration">
	/// The class declaration options. When no base type is specified, <see cref="Attribute"/> is
	/// used. Set <see cref="TypeDeclarationOptions.BaseType"/> to derive from a custom attribute
	/// base class.
	/// </param>
	/// <param name="targets">The declarations on which the generated attribute may be applied.</param>
	/// <param name="bodyWriter">The action that writes the body of the attribute class.</param>
	/// <param name="inherited">Whether derived classes and overriding members inherit the attribute.</param>
	/// <param name="allowMultiple">Whether more than one instance may be specified on one declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.AttributeClass(new TypeDeclarationOptions("MarkerAttribute"), AttributeTargets.Class, _ =&gt; { });</code></example>
	public CodeWriter AttributeClass(
		TypeDeclarationOptions declaration,
		AttributeTargets targets,
		Action<CodeWriter> bodyWriter,
		bool inherited = false,
		bool allowMultiple = false
	)
	{
		if (declaration is null)
			throw new ArgumentNullException(nameof(declaration));
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));
		if (targets == 0 || (targets & ~AttributeTargets.All) != 0)
			throw new ArgumentOutOfRangeException(nameof(targets), targets, "Invalid attribute targets.");

		AttributeDeclarationOptions attributeUsage = new(new TypeIdentity("AttributeUsageAttribute", "System"))
		{
			Arguments =
			[
				new(RenderAttributeTargets(targets)),
				new(inherited, "Inherited", isPropertyAssignment: true),
				new(allowMultiple, "AllowMultiple", isPropertyAssignment: true),
			],
		};

		return Class(
			declaration with
			{
				BaseType = declaration.BaseType ?? new TypeIdentity("Attribute", "System"),
				Attributes = declaration.Attributes.Insert(0, attributeUsage),
				IncludeEmbeddedAttribute = declaration.IncludeEmbeddedAttribute ?? true,
			},
			bodyWriter
		);
	}

	static string RenderAttributeTargets(AttributeTargets targets)
	{
		if (targets == AttributeTargets.All)
			return "global::System.AttributeTargets.All";

		var names = targets.ToString().Split(',');
		var builder = new StringBuilder();
		for (var index = 0; index < names.Length; index++)
		{
			if (index != 0)
				builder.Append(" | ");
			builder.Append("global::System.AttributeTargets.").Append(names[index].Trim());
		}
		return builder.ToString();
	}

	/// <summary>
	/// Writes a struct declaration from structured options and returns its body scope.
	/// </summary>
	/// <param name="declaration">The struct declaration options.</param>
	/// <returns>The struct body scope.</returns>
	/// <example><code>using (writer.StructScope(new TypeDeclarationOptions("Value"))) { }</code></example>
	public BlockScope StructScope(TypeDeclarationOptions declaration)
	{
		return declaration is null
			? throw new ArgumentNullException(nameof(declaration))
			: TypeScope(declaration with { Kind = TypeDeclarationKind.Struct });
	}

	/// <summary>
	/// Writes a struct declaration from structured options and returns its body scope.
	/// </summary>
	/// <param name="declaration">The struct declaration options.</param>
	/// <param name="bodyWriter">The action that writes the body of the struct.</param>
	/// <returns>The struct body scope.</returns>
	/// <example><code>writer.Struct(new TypeDeclarationOptions("Value"), _ =&gt; { });</code></example>
	public CodeWriter Struct(TypeDeclarationOptions declaration, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (StructScope(declaration))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes a record class declaration from structured options and returns its body scope.
	/// </summary>
	/// <param name="declaration">The record class declaration options.</param>
	/// <returns>The record body scope.</returns>
	/// <example><code>using (writer.RecordClassScope(new TypeDeclarationOptions("Model"))) { }</code></example>
	public BlockScope RecordClassScope(TypeDeclarationOptions declaration)
	{
		return declaration is null
			? throw new ArgumentNullException(nameof(declaration))
			: TypeScope(declaration with { Kind = TypeDeclarationKind.RecordClass });
	}

	/// <summary>
	/// Writes a record class declaration from structured options and returns its body scope.
	/// </summary>
	/// <param name="declaration">The record class declaration options.</param>
	/// <param name="bodyWriter">The action that writes the body of the record class.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.RecordClass(new TypeDeclarationOptions("Model"), _ =&gt; { });</code></example>
	public CodeWriter RecordClass(TypeDeclarationOptions declaration, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (RecordClassScope(declaration))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes a record struct declaration from structured options and returns its body scope.
	/// </summary>
	/// <param name="declaration">The record struct declaration options.</param>
	/// <returns>The record body scope.</returns>
	/// <example><code>using (writer.RecordStructScope(new TypeDeclarationOptions("Value"))) { }</code></example>
	public BlockScope RecordStructScope(TypeDeclarationOptions declaration)
	{
		return declaration is null
			? throw new ArgumentNullException(nameof(declaration))
			: TypeScope(declaration with { Kind = TypeDeclarationKind.RecordStruct });
	}

	/// <summary>
	/// Writes a record struct declaration from structured options and returns its body scope.
	/// </summary>
	/// <param name="declaration">The record struct declaration options.</param>
	/// <param name="bodyWriter">The action that writes the body of the record struct.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.RecordStruct(new TypeDeclarationOptions("Value"), _ =&gt; { });</code></example>
	public CodeWriter RecordStruct(TypeDeclarationOptions declaration, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (RecordStructScope(declaration))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes an interface declaration and returns its body scope.
	/// </summary>
	/// <example><code>using (writer.InterfaceScope(new TypeDeclarationOptions("IService"))) { }</code></example>
	public BlockScope InterfaceScope(TypeDeclarationOptions declaration) =>
		declaration is null
			? throw new ArgumentNullException(nameof(declaration))
			: TypeScope(declaration with { Kind = TypeDeclarationKind.Interface });

	/// <summary>
	/// Writes an interface declaration and invokes a callback for its body.
	/// </summary>
	/// <example><code>writer.Interface(new TypeDeclarationOptions("IService"), _ =&gt; { });</code></example>
	public CodeWriter Interface(TypeDeclarationOptions declaration, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));
		using (InterfaceScope(declaration))
			bodyWriter(this);
		return this;
	}

	/// <summary>
	/// Writes an enum declaration and returns its body scope.
	/// </summary>
	/// <example><code>using (writer.EnumScope(new TypeDeclarationOptions("Status"))) { }</code></example>
	public BlockScope EnumScope(TypeDeclarationOptions declaration) =>
		declaration is null
			? throw new ArgumentNullException(nameof(declaration))
			: TypeScope(declaration with { Kind = TypeDeclarationKind.Enum });

	/// <summary>
	/// Writes an enum declaration and invokes a callback for its body.
	/// </summary>
	/// <example><code>writer.Enum(new TypeDeclarationOptions("Status"), _ =&gt; { });</code></example>
	public CodeWriter Enum(TypeDeclarationOptions declaration, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (EnumScope(declaration))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes an enum declaration with structured field declarations.
	/// </summary>
	/// <param name="declaration">The enum declaration options.</param>
	/// <param name="fields">The fields to write in declaration order.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Enum(new TypeDeclarationOptions("Status"), new EnumFieldDeclarationOptions("Ready", 1));</code></example>
	public CodeWriter Enum(TypeDeclarationOptions declaration, params EnumFieldDeclarationOptions[] fields)
	{
		if (fields is null)
			throw new ArgumentNullException(nameof(fields));

		// Validate each field before writing the enum to avoid partial output on error.
		for (var index = 0; index < fields.Length; index++)
			ValidateEnumFieldDeclaration(fields[index]);

		return Enum(
			declaration,
			body =>
			{
				for (var index = 0; index < fields.Length; index++)
					body.EnumField(fields[index]);
			}
		);
	}

	/// <summary>
	/// Writes a field in an enum declaration.
	/// </summary>
	/// <param name="declaration">The enum field declaration options.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.EnumField(new EnumFieldDeclarationOptions("Ready", 1));</code></example>
	public CodeWriter EnumField(EnumFieldDeclarationOptions declaration)
	{
		ValidateEnumFieldDeclaration(declaration);
		if (!declaration.XmlSummary.IsDefaultOrEmpty)
			XmlSummary(declaration.XmlSummary);
		Attributes(declaration.Attributes);
		Write(declaration.FieldName);
		if (declaration.FieldValue is not null)
		{
			Write(" = ");
			Write(
				declaration.FieldValue as string
					?? Convert.ToString(declaration.FieldValue, CultureInfo.InvariantCulture)
			);
		}
		return Line(",");
	}

	void XmlSummary(ImmutableArray<string> summary)
	{
		if (summary.Length == 1)
		{
			Write("/// <summary>").Write(summary[0]).Line("</summary>");
			return;
		}

		Line("/// <summary>");
		for (var index = 0; index < summary.Length; index++)
			Write("/// ").Line(summary[index]);
		Line("/// </summary>");
	}

	/// <summary>
	/// Writes a complete delegate declaration.
	/// </summary>
	/// <example><code>writer.Delegate(new TypeDeclarationOptions("Handler") { DelegateReturnType = "void" });</code></example>
	public CodeWriter Delegate(TypeDeclarationOptions declaration)
	{
		if (declaration is null)
			throw new ArgumentNullException(nameof(declaration));
		TypeScope(declaration with { Kind = TypeDeclarationKind.Delegate });
		return this;
	}

	/// <summary>
	/// Writes a structured type declaration and returns its body scope when the declaration has one.
	/// </summary>
	/// <param name="declaration">The structured type declaration options.</param>
	/// <returns>The generated type body scope.</returns>
	/// <example><code>using (writer.TypeScope(new TypeDeclarationOptions("C"))) { }</code></example>
	public BlockScope TypeScope(TypeDeclarationOptions declaration)
	{
		if (declaration is null)
			throw new ArgumentNullException(nameof(declaration));

		ValidateTypeDeclaration(declaration);
		BeginWrittenItem(WrittenItemKind.Type);

		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
		{
			GeneratedAttributes(
				includeCoverageExclusion: declaration.Kind
					is TypeDeclarationKind.Class
						or TypeDeclarationKind.Struct
						or TypeDeclarationKind.RecordClass
						or TypeDeclarationKind.RecordStruct,
				includeEmbeddedAttribute: declaration.IncludeEmbeddedAttribute == true
			);
		}

		Attributes(declaration.Attributes);

		if (ResolveAccessibility(declaration.Accessibility, DefaultTypeAccessibility) is { } accessibility)
			Accessibility(accessibility).Write(' ');

		var isStruct = declaration.Kind is TypeDeclarationKind.Struct or TypeDeclarationKind.RecordStruct;
		var isClass = declaration.Kind is TypeDeclarationKind.Class or TypeDeclarationKind.RecordClass;

		if (declaration.IsStatic)
			Write("static ");
		else if (isStruct && declaration.IsReadOnly)
			Write("readonly ");
		else if (isClass && declaration.IsAbstract)
			Write("abstract ");
		else if (isClass && declaration.IsSealed)
			Write("sealed ");

		if (isStruct && declaration.IsRefStruct)
			Write("ref ");

		if (
			declaration.IsPartial
			&& declaration.Kind is not TypeDeclarationKind.Enum and not TypeDeclarationKind.Delegate
		)
			Write("partial ");

		if (declaration.Kind == TypeDeclarationKind.Delegate)
			Write("delegate ").TypeReference(declaration.DelegateReturnType!).Write(' ');

		Write(
				declaration.Kind switch
				{
					TypeDeclarationKind.Class => "class ",
					TypeDeclarationKind.Struct => "struct ",
					TypeDeclarationKind.RecordClass => "record class ",
					TypeDeclarationKind.RecordStruct => "record struct ",
					TypeDeclarationKind.Interface => "interface ",
					TypeDeclarationKind.Enum => "enum ",
					TypeDeclarationKind.Delegate => string.Empty,
					_ => throw new ArgumentOutOfRangeException(nameof(declaration)),
				}
			)
			.Write(declaration.Name);

		GenericTypeParameters(declaration.GenericTypes);
		if (declaration.Kind == TypeDeclarationKind.Delegate)
			ParametersWithHeuristic(declaration.DelegateParameters);
		else
			ParameterList(declaration.PrimaryConstructorParameters, declaration.ConstructorParametersOnSeparateLines);
		BaseTypes(declaration);
		if (declaration.Kind == TypeDeclarationKind.Enum && declaration.EnumUnderlyingType is { IsEmpty: false })
			Write(" : ").TypeReference(declaration.EnumUnderlyingType!);

		if (declaration.Kind == TypeDeclarationKind.Delegate)
		{
			if (HasGenericConstraints(declaration.GenericTypes))
				NewLine();
			MethodGenericConstraints(declaration.GenericTypes);
			Line(";");
			CompleteWrittenItem(WrittenItemKind.Type, _indentLevel);
			return default;
		}

		NewLine();
		GenericConstraints(declaration.GenericTypes);

		return OpenBlockScope(WrittenItemKind.Type);
	}

	/// <summary>
	/// Writes a structured type declaration and invokes a callback for its body.
	/// </summary>
	/// <param name="declaration">The structured type declaration options.</param>
	/// <param name="bodyWriter">The action that writes the type body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Type(new TypeDeclarationOptions("C"), _ =&gt; { });</code></example>
	public CodeWriter Type(TypeDeclarationOptions declaration, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (TypeScope(declaration))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes an ordinary instance or static constructor and returns its body scope.
	/// </summary>
	/// <param name="declaration">The constructor declaration options.</param>
	/// <returns>The constructor body scope.</returns>
	/// <example><code>using (writer.ConstructorScope(new ConstructorDeclarationOptions("C"))) writer.Line("// body");</code></example>
	public BlockScope ConstructorScope(ConstructorDeclarationOptions declaration)
	{
		ValidateConstructorDeclaration(declaration);
		BeginWrittenItem(WrittenItemKind.Constructor);
		if (declaration.IncludeGeneratedAttributes ?? DefaultIncludeGeneratedAttributes)
			GeneratedAttributes(includeCoverageExclusion: true, includeEmbeddedAttribute: false);
		Attributes(declaration.Attributes);

		if (declaration.IsStatic)
			Write("static ");
		else if (ResolveAccessibility(declaration.Accessibility, DefaultConstructorAccessibility) is { } accessibility)
			Accessibility(accessibility).Write(' ');

		Write(declaration.Reference.Identity.Name);
		if (declaration.WriteParametersOnSeparateLines)
			ParameterList(declaration.Parameters, writeOnSeparateLines: true, writeWhenEmpty: true);
		else
			ParametersWithHeuristic(declaration.Parameters);

		if (!string.IsNullOrWhiteSpace(declaration.Initializer))
		{
			EnsureNewLine();
			Indent();
			Write(": ").Line(declaration.Initializer);
			Unindent();
		}

		EnsureNewLine();
		return OpenBlockScope(WrittenItemKind.Constructor);
	}

	/// <summary>
	/// Writes a structured constructor and invokes a callback for its body.
	/// </summary>
	/// <example><code>writer.Constructor(new ConstructorDeclarationOptions("C"), _ =&gt; { });</code></example>
	public CodeWriter Constructor(ConstructorDeclarationOptions declaration, Action<CodeWriter> writeBody)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));
		using (ConstructorScope(declaration))
			writeBody(this);
		return this;
	}

	/// <summary>
	/// Writes the standard header for an automatically generated source file.
	/// </summary>
	/// <param name="generatorName">The generator name; defaults to <see cref="GeneratorName"/>.</param>
	/// <param name="version">The generator version; defaults to <see cref="GeneratorVersion"/>.</param>
	/// <param name="nullableDirective">
	/// Controls whether the <c>#nullable enable</c> directive is emitted and whether nullable
	/// reference annotations are rendered. When <see langword="null"/>, <see cref="NullableDirectiveMode"/>
	/// is used, which defaults to <see cref="NullableDirectiveMode.Auto"/>.
	/// </param>
	/// <param name="pragmas">The pragmas to include in the header.</param>
	/// <returns>The current writer.</returns>
	/// <example>
	/// <code>writer.AutoGeneratedHeader(pragmas: ["CS0618"]);</code>
	/// <code>writer.AutoGeneratedHeader(nullableDirective: NullableDirectiveMode.Disable);</code>
	/// </example>
	public CodeWriter AutoGeneratedHeader(
		string? generatorName = null,
		string? version = null,
		NullableDirectiveMode? nullableDirective = null,
		params string[] pragmas
	)
	{
		generatorName ??= GeneratorName;
		version ??= GeneratorVersion;

		var mode = nullableDirective ?? NullableDirectiveMode;

		Line("// <auto-generated />");
		if (!string.IsNullOrEmpty(generatorName))
		{
			Write("// This code was generated by ").Write(generatorName);
			if (!string.IsNullOrEmpty(version))
				Write(" (version ").Write(version).Write(')');

			Line(".");
		}

		Line("// Changes to this file will be lost when the source generator runs again.");

		if (ShouldWriteNullableDirective(mode, IsNullableContextEnabled))
			NewLine().Line("#nullable enable");

		if (pragmas is not null && pragmas.Length > 0)
		{
			NewLine();
			foreach (var pragma in pragmas)
			{
				Write("#pragma warning disable ").Line(pragma);
			}
		}

		return NewLine();
	}

	static bool ShouldWriteNullableDirective(NullableDirectiveMode mode, bool? isNullableContextEnabled) =>
		mode switch
		{
			NullableDirectiveMode.Always => true,
			NullableDirectiveMode.Disable => false,
			NullableDirectiveMode.Auto => isNullableContextEnabled is null or true,
			_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown nullable directive mode."),
		};

	/// <summary>
	/// Resolves whether nullable reference annotations are emitted by this writer, reconciling
	/// <see cref="NullableDirectiveMode"/> and <see cref="IsNullableContextEnabled"/> so that the
	/// header directive and type rendering always agree.
	/// </summary>
	bool ShouldUseNullableAnnotations => ShouldWriteNullableDirective(NullableDirectiveMode, IsNullableContextEnabled);

	/// <summary>
	/// Writes a <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> declaration.
	/// </summary>
	/// <param name="generatorName">The generator name.</param>
	/// <param name="version">The generator version, defaulting to <c>1.0.0.0</c>.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.GeneratedCodeAttribute("MyGenerator", "1.0.0");</code></example>
	public CodeWriter GeneratedCodeAttribute(string generatorName, string? version = null)
	{
		if (string.IsNullOrWhiteSpace(generatorName))
		{
			throw new ArgumentException("Generator name cannot be null or whitespace.", nameof(generatorName));
		}

		// The GeneratedCodeAttribute constructor requires a non-null version, so we default to "1.0.0.0".
		return Attribute(
				new(PurviewTypeLibrary.System.CodeDom.Compiler.GeneratedCodeAttribute)
				{
					Arguments = [new($"\"{generatorName}\""), new($"\"{version ?? "1.0.0.0"}\"")],
				},
				defaultTarget: null
			)
			.NewLine();
	}

	/// <summary>
	/// Writes the standard marker attributes for a generated declaration.
	/// </summary>
	/// <param name="includeCoverageExclusion">
	/// Whether to emit <see cref="ExcludeFromCodeCoverageAttribute"/>.
	/// This must be enabled only for declaration targets supported by that attribute.
	/// </param>
	/// <param name="includeEmbeddedAttribute">
	/// Whether to emit <see cref="Microsoft.CodeAnalysis.EmbeddedAttribute"/>.
	/// This is intended only for generator-emitted marker attribute types (R11) and must be
	/// <see langword="false"/> for ordinary generated members.
	/// </param>
	/// <param name="includeGeneratedCodeAttribute">Whether to emit <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> and <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/>.</param>
	/// <example><code>writer.GeneratedAttributes(includeCoverageExclusion: true);</code></example>
	public CodeWriter GeneratedAttributes(
		bool includeCoverageExclusion = false,
		bool includeEmbeddedAttribute = false,
		bool includeGeneratedCodeAttribute = true
	)
	{
		if (includeEmbeddedAttribute)
			Attribute(new(PurviewTypeLibrary.Microsoft.CodeAnalysis.EmbeddedAttribute), defaultTarget: null).NewLine();

		if (includeCoverageExclusion)
		{
			Attribute(
					new(PurviewTypeLibrary.System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute),
					defaultTarget: null
				)
				.NewLine();
		}

		if (includeGeneratedCodeAttribute)
		{
			Attribute(
					new(PurviewTypeLibrary.System.Runtime.CompilerServices.CompilerGeneratedAttribute),
					defaultTarget: null
				)
				.NewLine();
			GeneratedCodeAttribute(GeneratorName, GeneratorVersion);
		}

		return this;
	}

	/// <summary>
	/// Disables the specified warning pragmas and returns a scope that restores them on dispose.
	/// </summary>
	/// <param name="pragmas">The warning codes to disable.</param>
	/// <returns>A scope that writes the corresponding restore pragmas once.</returns>
	/// <example><code>using (writer.OpenPragmasScope("CS0618")) writer.Line("ObsoleteCall();");</code></example>
	public PragmaScope OpenPragmasScope(params string[] pragmas)
	{
		if (pragmas is null || pragmas.Length == 0)
			return new(this, []);

		NewLine();
		foreach (var pragma in pragmas)
			Write("#pragma warning disable ").Line(pragma);

		return new(this, pragmas);
	}

	/// <summary>
	/// Writes a <c>#pragma warning disable</c> directive at column zero for one or more warning codes,
	/// as a single directive. Use <see cref="OpenPragmasScope"/> for a scoped disable that restores the
	/// warnings when disposed.
	/// </summary>
	/// <param name="codes">The warning codes to disable, such as <c>CS8625</c>.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.PragmaDisable("CS8625", "CS0618");</code></example>
	public CodeWriter PragmaDisable(params string[] codes)
	{
		if (codes is null || codes.Length == 0)
			throw new ArgumentException("At least one warning code is required.", nameof(codes));

		for (var index = 0; index < codes.Length; index++)
			ValidateStatementPart(codes[index], nameof(codes));

		if (_indentLevel == 0)
			EnsureBlankLine();
		DirectiveLine("#pragma warning disable " + string.Join(" ", codes));
		if (_indentLevel == 0)
			EnsureBlankLine();

		return this;
	}

	/// <summary>
	/// Writes each supplied part on its own line.
	/// </summary>
	/// <param name="parts">The lines to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MultiLine("first", "second");</code></example>
	public CodeWriter MultiLine(params string[] parts)
	{
		if (parts is null)
			throw new ArgumentNullException(nameof(parts));

		for (var index = 0; index < parts.Length; index++)
			Line(parts[index]);

		return this;
	}

	/// <summary>
	/// Writes a multi-line parameter list ending with a closing parenthesis.
	/// </summary>
	/// <param name="parameters">The parameters to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Write("Call(").MultiLineParameters("string name", "int count");</code></example>
	public CodeWriter MultiLineParameters(params string[] parameters)
	{
		if (parameters is null)
			throw new ArgumentNullException(nameof(parameters));

		if (parameters.Length == 0)
			return Write(')');

		NewLine();
		Indent();
		for (var index = 0; index < parameters.Length; index++)
		{
			Write(parameters[index]);
			Line(index == parameters.Length - 1 ? ")" : ",");
		}

		return Unindent();
	}

	/// <summary>
	/// Writes a method invocation statement.
	/// </summary>
	/// <param name="methodName">The method name, optionally including a receiver.</param>
	/// <param name="arguments">The argument expressions.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MethodCall("Run", "value", "cancellationToken"); // Run(value, cancellationToken);</code></example>
	public CodeWriter MethodCall(string methodName, params string[] arguments) =>
		MethodCallCore(methodName, arguments, receiver: null, genericArguments: null, false, false, false);

	/// <summary>
	/// Writes an awaited method invocation statement.
	/// </summary>
	/// <param name="methodName">The method name, optionally including a receiver.</param>
	/// <param name="arguments">The argument expressions.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.AwaitedMethodCall("LoadAsync", "cancellationToken"); // await LoadAsync(cancellationToken);</code></example>
	public CodeWriter AwaitedMethodCall(string methodName, params string[] arguments) =>
		MethodCallCore(methodName, arguments, receiver: null, genericArguments: null, false, true, false);

	/// <summary>
	/// Writes a method invocation on a receiver, such as <c>variable.Method(arg)</c>.
	/// </summary>
	/// <param name="receiver">The receiver expression written before the method name.</param>
	/// <param name="methodName">The method name.</param>
	/// <param name="arguments">The argument expressions.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MethodCallOn("service", "Add", "value"); // service.Add(value);</code></example>
	public CodeWriter MethodCallOn(string receiver, string methodName, params string[] arguments) =>
		MethodCallCore(methodName, arguments, receiver, genericArguments: null, false, false, false);

	/// <summary>
	/// Writes a method invocation on a receiver from structured argument declarations.
	/// </summary>
	/// <param name="receiver">The receiver expression written before the method name.</param>
	/// <param name="methodName">The method name.</param>
	/// <param name="arguments">The structured arguments to invoke the method with.</param>
	/// <param name="genericArguments">Optional generic type arguments written after the method name.</param>
	/// <param name="nullConditional">
	/// Whether the receiver is invoked with the null-conditional operator (<c>?.</c>) so the call is
	/// skipped when the receiver is <see langword="null"/>.
	/// </param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MethodCallOn("builder.Services", "AddOptions", genericArguments: [optionsType]); // builder.Services.AddOptions&lt;Options&gt;();</code></example>
	public CodeWriter MethodCallOn(
		string receiver,
		string methodName,
		IEnumerable<MethodCallArgumentOptions>? arguments = null,
		IEnumerable<TypeReference>? genericArguments = null,
		bool nullConditional = false
	) =>
		MethodCallCore(
			methodName,
			(arguments ?? []).Select(RenderCallArgument),
			receiver,
			genericArguments,
			false,
			false,
			nullConditional
		);

	/// <summary>
	/// Writes an awaited method invocation on a receiver, such as <c>await variable.MethodAsync(arg)</c>.
	/// </summary>
	/// <param name="receiver">The receiver expression written before the method name.</param>
	/// <param name="methodName">The method name.</param>
	/// <param name="arguments">The argument expressions.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.AwaitedMethodCallOn("service", "LoadAsync", "token"); // await service.LoadAsync(token);</code></example>
	public CodeWriter AwaitedMethodCallOn(string receiver, string methodName, params string[] arguments) =>
		MethodCallCore(methodName, arguments, receiver, genericArguments: null, false, true, false);

	/// <summary>
	/// Writes an awaited method invocation on a receiver from structured argument declarations.
	/// </summary>
	/// <param name="receiver">The receiver expression written before the method name.</param>
	/// <param name="methodName">The method name.</param>
	/// <param name="arguments">The structured arguments to invoke the method with.</param>
	/// <param name="genericArguments">Optional generic type arguments written after the method name.</param>
	/// <param name="nullConditional">
	/// Whether the receiver is invoked with the null-conditional operator (<c>?.</c>) so the call is
	/// skipped when the receiver is <see langword="null"/>.
	/// </param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.AwaitedMethodCallOn("service", "LoadAsync", ["token"], nullConditional: true); // await service?.LoadAsync(token);</code></example>
	public CodeWriter AwaitedMethodCallOn(
		string receiver,
		string methodName,
		IEnumerable<MethodCallArgumentOptions>? arguments = null,
		IEnumerable<TypeReference>? genericArguments = null,
		bool nullConditional = false
	) =>
		MethodCallCore(
			methodName,
			(arguments ?? []).Select(RenderCallArgument),
			receiver,
			genericArguments,
			false,
			true,
			nullConditional
		);

	/// <summary>
	/// Writes a method invocation from structured argument declarations.
	/// </summary>
	/// <remarks>
	/// The argument type is not emitted; each declaration contributes its name and argument
	/// modifier. This allows call sites to reuse parameter declarations while preserving
	/// <c>ref</c>, <c>out</c>, and <c>in</c> arguments.
	/// </remarks>
	/// <param name="methodName">The method name, optionally including a receiver.</param>
	/// <param name="arguments">The structured arguments to invoke the method with.</param>
	/// <param name="receiver">An optional receiver such as <c>service</c>.</param>
	/// <param name="genericArguments">Optional generic type arguments.</param>
	/// <param name="writeArgumentsOnSeparateLines">Whether to force one argument per line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MethodCall("Copy", [
	/// 	new("source"),
	/// 	new("destination") { Modifier = ParameterModifier.Out }]);</code></example>
	public CodeWriter MethodCall(
		string methodName,
		IEnumerable<MethodCallArgumentOptions> arguments,
		string? receiver = null,
		IEnumerable<TypeReference>? genericArguments = null,
		bool writeArgumentsOnSeparateLines = false
	) =>
		MethodCall(
			methodName,
			(arguments ?? throw new ArgumentNullException(nameof(arguments))).Select(RenderCallArgument),
			receiver,
			genericArguments,
			writeArgumentsOnSeparateLines
		);

	/// <summary>
	/// Writes an awaited method invocation from structured argument declarations.
	/// </summary>
	/// <param name="methodName">The method name without a receiver or generic argument list.</param>
	/// <param name="arguments">The structured arguments to invoke the method with.</param>
	/// <param name="receiver">An optional receiver such as <c>service</c>.</param>
	/// <param name="genericArguments">Optional generic type arguments.</param>
	/// <param name="writeArgumentsOnSeparateLines">Whether to force one argument per line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.AwaitedMethodCall("LoadAsync", [new("token")], "service");</code></example>
	public CodeWriter AwaitedMethodCall(
		string methodName,
		IEnumerable<MethodCallArgumentOptions> arguments,
		string? receiver = null,
		IEnumerable<TypeReference>? genericArguments = null,
		bool writeArgumentsOnSeparateLines = false
	) =>
		MethodCallCore(
			methodName,
			(arguments ?? throw new ArgumentNullException(nameof(arguments))).Select(RenderCallArgument),
			receiver,
			genericArguments,
			writeArgumentsOnSeparateLines,
			true,
			false
		);

	/// <summary>
	/// Writes a method invocation statement with optional receiver and generic type arguments.
	/// </summary>
	/// <param name="methodName">The method name without a receiver or generic argument list.</param>
	/// <param name="arguments">The argument expressions.</param>
	/// <param name="receiver">An optional receiver such as <c>service</c> or <c>global::Api.Service</c>.</param>
	/// <param name="genericArguments">Optional generic type arguments.</param>
	/// <param name="writeArgumentsOnSeparateLines">Whether to force one argument per line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MethodCall("Create", ["value"], "factory", [PurviewTypeLibrary.System.String.AsTypeReference()]);</code></example>
	public CodeWriter MethodCall(
		string methodName,
		IEnumerable<string?> arguments,
		string? receiver = null,
		IEnumerable<TypeReference>? genericArguments = null,
		bool writeArgumentsOnSeparateLines = false
	) => MethodCallCore(methodName, arguments, receiver, genericArguments, writeArgumentsOnSeparateLines, false, false);

	CodeWriter MethodCallCore(
		string methodName,
		IEnumerable<string?> arguments,
		string? receiver,
		IEnumerable<TypeReference>? genericArguments,
		bool writeArgumentsOnSeparateLines,
		bool isAwaited,
		bool nullConditional
	)
	{
		ValidateStatementPart(methodName, nameof(methodName));
		if (arguments is null)
			throw new ArgumentNullException(nameof(arguments));
		if (receiver is not null && string.IsNullOrWhiteSpace(receiver))
			throw new ArgumentException("Receiver cannot be whitespace.", nameof(receiver));

		var argumentList = arguments.ToArray();
		var genericArgumentList = genericArguments?.ToArray() ?? [];
		for (var index = 0; index < genericArgumentList.Length; index++)
			if (genericArgumentList[index].IsEmpty)
				throw new ArgumentException("Generic arguments cannot be empty.", nameof(genericArguments));

		if (isAwaited)
			Write("await ");
		if (receiver is not null)
			Write(receiver).Write(nullConditional ? "?." : ".");
		Write(methodName);
		if (genericArgumentList.Length > 0)
		{
			Write('<');
			for (var index = 0; index < genericArgumentList.Length; index++)
			{
				if (index != 0)
					Write(", ");
				TypeReference(genericArgumentList[index]);
			}
			Write('>');
		}

		Write('(');
		if (MethodCallArguments(argumentList, writeArgumentsOnSeparateLines))
			return this;

		// If the arguments were written inline, we can write the closing parenthesis and semicolon on the same line.
		return Line(";");
	}

	bool MethodCallArguments(string?[] arguments, bool writeOnSeparateLines, string multilineClosingSuffix = ";")
	{
		var inlineLength = CurrentLineLength + 2;
		for (var index = 0; index < arguments.Length; index++)
			inlineLength += (arguments[index]?.Length ?? 0) + (index == 0 ? 0 : 2);

		var canWriteInline =
			!writeOnSeparateLines
			&& inlineLength <= _maximumLineLength
			&& arguments.All(static argument => argument is not null && !argument.Contains('\n'));
		if (arguments.Length == 0)
		{
			Write(')');
			return false;
		}

		if (canWriteInline)
		{
			for (var index = 0; index < arguments.Length; index++)
			{
				if (index != 0)
					Write(", ");
				Write(arguments[index]);
			}
			Write(')');
			return false;
		}

		NewLine().Indent();
		for (var index = 0; index < arguments.Length; index++)
		{
			Expression(arguments[index], expressionWriter: null);
			if (index != arguments.Length - 1)
				Line(",");
			else
				NewLine();
		}
		Unindent();
		Write(')');
		if (multilineClosingSuffix.Length > 0)
		{
			Write(multilineClosingSuffix);
			NewLine();
		}

		return true;
	}

	/// <summary>
	/// Writes a chained method-call expression in which the result of each call is the receiver of the
	/// next. The chain is written without a trailing semicolon so it composes as the value of an
	/// <see cref="Assignment(string, Action{CodeWriter})"/>, <see cref="Return(Action{CodeWriter})"/>, or
	/// other expression. A standalone chain statement is terminated by appending <c>.Line(";")</c>.
	/// </summary>
	/// <param name="rootMethod">The root invocation, optionally including a receiver, such as <c>builder.Configuration.GetSection</c>.</param>
	/// <param name="arguments">The root argument expressions.</param>
	/// <param name="configure">The callback that appends chained invocations and the postfix.</param>
	/// <param name="genericArguments">Optional generic type arguments for the root invocation.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Assignment("var value", value =&gt; value.MethodCallChain(
	/// 	"builder.Configuration.GetSection", [$"{name}.SectionName"],
	/// 	chain =&gt; chain.Method("Get", genericArguments: [optionsType]).Postfix("?? new()")));
	/// // var value = builder.Configuration.GetSection("x.SectionName").Get&lt;Options&gt;() ?? new();</code></example>
	public CodeWriter MethodCallChain(
		string rootMethod,
		IEnumerable<string?> arguments,
		Action<MethodChainBuilder> configure,
		IEnumerable<TypeReference>? genericArguments = null
	) => MethodCallChainCore(rootMethod, arguments, configure, genericArguments, isAwaited: false);

	/// <summary>
	/// Writes an awaited chained method-call expression in which the result of each call is the receiver
	/// of the next. The chain is written without a trailing semicolon so it composes as an expression.
	/// </summary>
	/// <param name="rootMethod">The root invocation, optionally including a receiver.</param>
	/// <param name="arguments">The root argument expressions.</param>
	/// <param name="configure">The callback that appends chained invocations and the postfix.</param>
	/// <param name="genericArguments">Optional generic type arguments for the root invocation.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Return(value =&gt; value.AwaitedMethodCallChain(
	/// 	"service.LoadAsync", ["token"], chain =&gt; chain.Method("Configure")));</code></example>
	public CodeWriter AwaitedMethodCallChain(
		string rootMethod,
		IEnumerable<string?> arguments,
		Action<MethodChainBuilder> configure,
		IEnumerable<TypeReference>? genericArguments = null
	) => MethodCallChainCore(rootMethod, arguments, configure, genericArguments, isAwaited: true);

	CodeWriter MethodCallChainCore(
		string rootMethod,
		IEnumerable<string?> arguments,
		Action<MethodChainBuilder> configure,
		IEnumerable<TypeReference>? genericArguments,
		bool isAwaited
	)
	{
		ValidateStatementPart(rootMethod, nameof(rootMethod));
		if (arguments is null)
			throw new ArgumentNullException(nameof(arguments));
		if (configure is null)
			throw new ArgumentNullException(nameof(configure));

		var rootArguments = arguments.ToArray();
		for (var index = 0; index < rootArguments.Length; index++)
			ValidateStatementPart(rootArguments[index], nameof(arguments));

		var builder = new MethodChainBuilder(rootMethod, rootArguments, genericArguments);
		configure(builder);

		return RenderMethodChain(builder, isAwaited);
	}

	CodeWriter RenderMethodChain(MethodChainBuilder builder, bool isAwaited)
	{
		if (isAwaited)
			Write("await ");

		Write(builder.RootMethod);
		RenderGenericArgumentList(builder.RootGenericArguments);
		RenderInvocationArguments(builder.RootArguments);

		for (var index = 0; index < builder.Segments.Count; index++)
		{
			var segment = builder.Segments[index];
			Write('.');
			Write(segment.MethodName);
			RenderGenericArgumentList(segment.GenericArguments);
			RenderInvocationArguments(segment.Arguments);
		}

		if (builder.PostfixExpression is not null)
			Write(builder.PostfixExpression);

		return this;
	}

	void RenderGenericArgumentList(ImmutableArray<TypeReference> genericArguments)
	{
		if (genericArguments.IsDefaultOrEmpty)
			return;

		Write('<');
		for (var index = 0; index < genericArguments.Length; index++)
		{
			if (index != 0)
				Write(", ");
			if (genericArguments[index].IsEmpty)
				throw new ArgumentException("Generic arguments cannot be empty.");
			TypeReference(genericArguments[index]);
		}
		Write('>');
	}

	void RenderInvocationArguments(ImmutableArray<string?> arguments)
	{
		Write('(');
		for (var index = 0; index < arguments.Length; index++)
		{
			if (index != 0)
				Write(", ");
			Write(arguments[index]);
		}
		Write(')');
	}

	/// <summary>
	/// Writes an assignment statement.
	/// </summary>
	/// <param name="target">The target, such as <c>value</c> or <c>var result</c>.</param>
	/// <param name="value">The assigned expression.</param>
	/// <param name="forceNotNull">Whether to force the value to be not null, by appending the null-forgiving operator (<c>!</c>).</param>
	/// <example><code>writer.Assignment("value", "CreateValue()"); // value = CreateValue();</code></example>
	public CodeWriter Assignment(string target, string value, bool forceNotNull = false)
	{
		ValidateStatementPart(target, nameof(target));
		ValidateStatementPart(value, nameof(value));
		Write(target).Write(" = ");
		Expression(value, expressionWriter: null);
		if (forceNotNull)
			Write("!");

		return Line(";");
	}

	/// <summary>
	/// Writes an assignment statement using a callback for a multiline expression.
	/// </summary>
	/// <example><code>writer.Assignment("value", expression =&gt; expression.Write("new Value()"));</code></example>
	public CodeWriter Assignment(string target, Action<CodeWriter> writeValue)
	{
		ValidateStatementPart(target, nameof(target));
		if (writeValue is null)
			throw new ArgumentNullException(nameof(writeValue));
		Write(target).Write(" = ");
		Expression(null, writeValue);
		return Line(";");
	}

	/// <summary>
	/// Writes an assignment whose value is a structured object-creation expression.
	/// </summary>
	/// <example><code>writer.Assignment("@event", new ObjectCreationOptions(eventType, "propVal1", "propVal2"));</code></example>
	public CodeWriter Assignment(string target, ObjectCreationOptions value, bool forceNotNull = false)
	{
		ValidateStatementPart(target, nameof(target));
		Write(target).Write(" = ");

		if (ObjectCreationExpression(value, forceNotNull))
			return this;

		// If the object creation was written inline, we can write the closing semicolon on the same line.
		return Line(";");
	}

	/// <summary>
	/// Writes a typed local or declaration assignment.
	/// </summary>
	/// <example><code>writer.Assignment("var", "value", "CreateValue()");</code></example>
	public CodeWriter Assignment(string type, string name, string value, bool forceNotNull = false)
	{
		ValidateStatementPart(type, nameof(type));
		ValidateStatementPart(name, nameof(name));
		return Assignment($"{type} {name}", value, forceNotNull);
	}

	/// <summary>
	/// Writes a typed local or declaration assignment with a multiline expression.
	/// </summary>
	/// <example><code>writer.Assignment("Value", "value", expression =&gt; expression.Write("CreateValue()"));</code></example>
	public CodeWriter Assignment(string type, string name, Action<CodeWriter> writeValue)
	{
		ValidateStatementPart(type, nameof(type));
		ValidateStatementPart(name, nameof(name));
		return Assignment($"{type} {name}", writeValue);
	}

	/// <summary>
	/// Writes a typed local assignment whose value is a structured object creation.
	/// </summary>
	/// <example><code>writer.Assignment("var", "@event", new ObjectCreationOptions(eventType, "propVal1", "propVal2"));</code></example>
	public CodeWriter Assignment(string type, string name, ObjectCreationOptions value, bool forceNotNull = false)
	{
		ValidateStatementPart(type, nameof(type));
		ValidateStatementPart(name, nameof(name));
		return Assignment($"{type} {name}", value, forceNotNull);
	}

	/// <summary>
	/// Writes the <c>new</c> keyword followed by the type name, or the bare <c>new</c> keyword for a
	/// target-typed object creation whose type is inferred from the surrounding context.
	/// </summary>
	CodeWriter WriteNewPrefix(TypeReference reference)
	{
		Write("new");
		if (!reference.IsNullOrEmpty())
			Write(' ').TypeReference(reference);

		return this;
	}

	bool ObjectCreationExpression(ObjectCreationOptions value, bool forceNotNull)
	{
		ValidateInitializerMembers(value);
		WriteNewPrefix(value.Reference);

		var hasInitializer = !value.InitializerMembers.IsDefaultOrEmpty;
		string[] arguments = value.Arguments.IsDefault ? [] : [.. value.Arguments.Select(RenderCallArgument)];
		if (arguments.Length > 0 || !hasInitializer)
		{
			// MethodCallArguments writes the closing parenthesis itself: inline or for empty
			// arguments it emits ')' and returns false, while a multiline layout emits the closing
			// token and returns true.
			Write('(');
			if (
				MethodCallArguments(
					arguments,
					value.WriteArgumentsOnSeparateLines,
					hasInitializer ? string.Empty
						: forceNotNull ? "!;"
						: ";"
				)
			)
			{
				// Multiline arguments: the closing token was already written. When an initializer
				// follows, the initializer supplies the terminating semicolon via the caller.
				if (hasInitializer)
				{
					ObjectInitializer(value, forceNotNull);
					return false;
				}

				return true;
			}
		}

		if (hasInitializer)
		{
			ObjectInitializer(value, forceNotNull);
			return false;
		}

		if (forceNotNull)
			Write('!');

		return false;
	}

	bool ObjectInitializer(ObjectCreationOptions value, bool forceNotNull)
	{
		if (value.WriteInitializerMembersOnSeparateLines)
		{
			EnsureNewLine();
			Line("{");
			Indent();
			for (var index = 0; index < value.InitializerMembers.Length; index++)
			{
				var member = value.InitializerMembers[index];
				Write(member.Name).Write(" = ").Write(member.Value).Line(",");
			}

			Unindent();
			Write("}");
		}
		else
		{
			Write(" { ");
			for (var index = 0; index < value.InitializerMembers.Length; index++)
			{
				if (index != 0)
					Write(" ");

				var member = value.InitializerMembers[index];
				Write(member.Name).Write(" = ").Write(member.Value).Write(",");
			}

			Write(" }");
		}

		if (forceNotNull)
			Write('!');

		return false;
	}

	static void ValidateInitializerMembers(ObjectCreationOptions value)
	{
		for (
			var index = 0;
			!value.InitializerMembers.IsDefaultOrEmpty && index < value.InitializerMembers.Length;
			index++
		)
		{
			ValidateRequired(value.InitializerMembers[index].Name, "Initializer member name", nameof(value));
			ValidateRequired(value.InitializerMembers[index].Value, "Initializer member value", nameof(value));
		}
	}

	/// <summary>
	/// Writes a return statement.
	/// </summary>
	/// <example><code>writer.Return("value"); // return value;</code></example>
	public CodeWriter Return(string? expression = null)
	{
		if (string.IsNullOrWhiteSpace(expression))
			return Line("return;");
		Write("return ");
		Expression(expression, expressionWriter: null);
		return Line(";");
	}

	/// <summary>
	/// Writes a return statement using a callback for a multiline expression.
	/// </summary>
	/// <example><code>writer.Return(expression =&gt; expression.Write("value"));</code></example>
	public CodeWriter Return(Action<CodeWriter> writeExpression)
	{
		if (writeExpression is null)
			throw new ArgumentNullException(nameof(writeExpression));
		Write("return ");
		Expression(null, writeExpression);
		return Line(";");
	}

	/// <summary>
	/// Writes a return statement whose value is a structured object-creation expression.
	/// </summary>
	/// <param name="value">The object-creation expression to return.</param>
	/// <param name="forceNotNull">Whether to force the value to be not null, by appending the null-forgiving operator (<c>!</c>).</param>
	/// <example><code>writer.Return(new ObjectCreationOptions(Type("Order"), "customerId"));</code></example>
	public CodeWriter Return(ObjectCreationOptions value, bool forceNotNull = false)
	{
		Write("return ");

		if (ObjectCreationExpression(value, forceNotNull))
			return this;

		// If the object creation was written inline, we can write the closing semicolon on the same line.
		return Line(";");
	}

	/// <summary>
	/// Writes a throw statement.
	/// </summary>
	/// <example><code>writer.Throw("new InvalidOperationException()");</code></example>
	public CodeWriter Throw(string expression)
	{
		ValidateStatementPart(expression, nameof(expression));
		Write("throw ");
		Expression(expression, expressionWriter: null);
		return Line(";");
	}

	/// <summary>
	/// Writes a throw statement using a structured exception type, an optional message, and optional raw
	/// constructor arguments.
	/// </summary>
	/// <param name="exceptionType">The exception type to throw.</param>
	/// <param name="message">
	/// The exception message written as a string literal, or <see langword="null"/> to throw the exception
	/// without a message. Backslashes, double quotes, carriage returns, line feeds, and tabs are escaped so
	/// raw literal text can be supplied.
	/// </param>
	/// <param name="constructorArguments">
	/// Raw constructor argument expressions written verbatim after the message; never escaped. When
	/// <paramref name="message"/> is <see langword="null"/>, they are written as the sole constructor arguments.
	/// </param>
	/// <example><code>writer.Throw(PurviewTypeLibrary.System.ArgumentNullException, null, "nameof(value)");</code></example>
	public CodeWriter Throw(TypeReference exceptionType, string? message = null, params string[] constructorArguments)
	{
		if (exceptionType.IsNullOrEmpty())
			throw new ArgumentException("Exception type cannot be null or empty.", nameof(exceptionType));

		Write("throw new ");
		var arguments = constructorArguments ?? [];
		if (message is null && arguments.Length == 0)
		{
			Expression($"{exceptionType}()", expressionWriter: null);
		}
		else
		{
			var rendered = new string[arguments.Length + (message is null ? 0 : 1)];
			var index = 0;
			if (message is not null)
				rendered[index++] = $"\"{EscapeStringLiteral(message)}\"";

			foreach (var argument in arguments)
				rendered[index++] = argument;

			Expression($"{exceptionType}({string.Join(", ", rendered)})", expressionWriter: null);
		}

		return Line(";");
	}

	/// <summary>
	/// Writes a throw statement using a callback for a multiline expression.
	/// </summary>
	/// <example><code>writer.Throw(expression =&gt; expression.Write("new InvalidOperationException()"));</code></example>
	public CodeWriter Throw(Action<CodeWriter> writeExpression)
	{
		if (writeExpression is null)
			throw new ArgumentNullException(nameof(writeExpression));
		Write("throw ");
		Expression(null, writeExpression);
		return Line(";");
	}

	/// <summary>
	/// Writes an if statement with a block body and invokes a callback for the body.
	/// </summary>
	/// <param name="condition">The condition of the if statement.</param>
	/// <param name="bodyWriter">The action to invoke for the body of the if statement.</param>
	/// <returns>The current writer.</returns>
	/// <exception cref="ArgumentException">Thrown if the condition is null or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the bodyWriter is null.</exception>
	/// <example><code>writer.IfBlock("enabled", body =&gt; body.Return());</code></example>
	public CodeWriter IfBlock(string condition, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (IfBlockScope(condition))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes an if statement and returns its body scope.
	/// </summary>
	/// <example><code>using (writer.IfBlockScope("enabled")) writer.Return();</code></example>
	public BlockScope IfBlockScope(string condition)
	{
		ValidateStatementPart(condition, nameof(condition));
		Write("if (");
		Expression(condition, expressionWriter: null);
		Line(")");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes an <c>if</c> statement and an optional <c>else</c> block.
	/// </summary>
	/// <param name="condition">The if condition.</param>
	/// <param name="ifBody">The action that writes the if body.</param>
	/// <param name="elseBody">The action that writes the else body, or <see langword="null"/> to omit the else.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.IfElse("enabled", body =&gt; body.Return("value"), null);</code></example>
	public CodeWriter IfElse(string condition, Action<CodeWriter> ifBody, Action<CodeWriter>? elseBody)
	{
		if (ifBody is null)
			throw new ArgumentNullException(nameof(ifBody));
		using (IfBlockScope(condition))
			ifBody(this);
		if (elseBody is not null)
			Else(elseBody);
		return this;
	}

	/// <summary>
	/// Writes an <c>else</c> block following an <c>if</c> and invokes a callback for its body.
	/// </summary>
	/// <param name="body">The action that writes the else body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.IfBlock("enabled", body =&gt; body.Return("value")).Else(body =&gt; body.Return("null"));</code></example>
	public CodeWriter Else(Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (ElseScope())
			body(this);
		return this;
	}

	/// <summary>
	/// Writes an <c>else</c> block and returns its body scope.
	/// </summary>
	/// <returns>The else body scope.</returns>
	/// <example><code>using (writer.IfBlockScope("enabled")) writer.Return("value"); using (writer.ElseScope()) writer.Return("null");</code></example>
	public BlockScope ElseScope()
	{
		EnsureNewLine();
		Line("else");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes an <c>else if</c> block following an <c>if</c> or another <c>else if</c> and invokes a
	/// callback for its body.
	/// </summary>
	/// <param name="condition">The else-if condition.</param>
	/// <param name="bodyWriter">The action to invoke for the body of the else-if block.</param>
	/// <returns>The current writer.</returns>
	/// <exception cref="ArgumentException">Thrown if the condition is null or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the bodyWriter is null.</exception>
	/// <example><code>writer.IfBlock("enabled", body =&gt; body.Return("value")).ElseIf("retry", body =&gt; body.Return("retry"));</code></example>
	public CodeWriter ElseIf(string condition, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		using (ElseIfScope(condition))
			bodyWriter(this);

		return this;
	}

	/// <summary>
	/// Writes an <c>else if</c> block and returns its body scope.
	/// </summary>
	/// <param name="condition">The else-if condition.</param>
	/// <returns>The else-if body scope.</returns>
	/// <example><code>using (writer.IfBlockScope("enabled")) writer.Return("value"); using (writer.ElseIfScope("retry")) writer.Return("retry");</code></example>
	public BlockScope ElseIfScope(string condition)
	{
		ValidateStatementPart(condition, nameof(condition));
		EnsureNewLine();
		Write("else if (");
		Expression(condition, expressionWriter: null);
		Line(")");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes a <c>foreach</c> statement and invokes a callback for its body.
	/// </summary>
	/// <param name="iterator">The iterator declaration, such as <c>var item in items</c>.</param>
	/// <param name="body">The action that writes the loop body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Foreach("var item in items", body =&gt; body.MethodCall("Process", "item"));</code></example>
	public CodeWriter Foreach(string iterator, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (ForeachScope(iterator))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>foreach</c> statement and returns its body scope.
	/// </summary>
	/// <param name="iterator">The iterator declaration, such as <c>var item in items</c>.</param>
	/// <returns>The loop body scope.</returns>
	/// <example><code>using (writer.ForeachScope("var item in items")) writer.MethodCall("Process", "item");</code></example>
	public BlockScope ForeachScope(string iterator)
	{
		ValidateStatementPart(iterator, nameof(iterator));
		Write("foreach (").Write(iterator).Line(")");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes a <c>for</c> statement and invokes a callback for its body.
	/// </summary>
	/// <param name="initializer">The initializer expression, or <see langword="null"/> for none.</param>
	/// <param name="condition">The condition expression, or <see langword="null"/> for none.</param>
	/// <param name="iterator">The iterator expression, or <see langword="null"/> for none.</param>
	/// <param name="body">The action that writes the loop body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.For("int i = 0", "i &lt; count", "i++", body =&gt; body.MethodCall("Process", "items[i]"));</code></example>
	public CodeWriter For(string? initializer, string? condition, string? iterator, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (ForScope(initializer, condition, iterator))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>for</c> statement and returns its body scope.
	/// </summary>
	/// <param name="initializer">The initializer expression, or <see langword="null"/> for none.</param>
	/// <param name="condition">The condition expression, or <see langword="null"/> for none.</param>
	/// <param name="iterator">The iterator expression, or <see langword="null"/> for none.</param>
	/// <returns>The loop body scope.</returns>
	/// <example><code>using (writer.ForScope("int i = 0", "i &lt; count", "i++")) writer.MethodCall("Process", "items[i]");</code></example>
	public BlockScope ForScope(string? initializer, string? condition, string? iterator)
	{
		Write("for (");
		Write(initializer).Write("; ");
		Write(condition).Write("; ");
		Write(iterator).Line(")");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes a <c>while</c> statement and invokes a callback for its body.
	/// </summary>
	/// <param name="condition">The loop condition.</param>
	/// <param name="body">The action that writes the loop body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.While("queue.Count &gt; 0", body =&gt; body.MethodCall("Process", "queue.Dequeue()"));</code></example>
	public CodeWriter While(string condition, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (WhileScope(condition))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>while</c> statement and returns its body scope.
	/// </summary>
	/// <param name="condition">The loop condition.</param>
	/// <returns>The loop body scope.</returns>
	/// <example><code>using (writer.WhileScope("queue.Count &gt; 0")) writer.MethodCall("Process", "queue.Dequeue()");</code></example>
	public BlockScope WhileScope(string condition)
	{
		ValidateStatementPart(condition, nameof(condition));
		Write("while (").Write(condition).Line(")");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes a <c>do</c>-<c>while</c> statement and invokes a callback for its body.
	/// </summary>
	/// <param name="condition">The trailing loop condition.</param>
	/// <param name="body">The action that writes the loop body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.DoWhile("!finished", body =&gt; body.MethodCall("Advance"));</code></example>
	public CodeWriter DoWhile(string condition, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (DoWhileScope(condition))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>do</c>-<c>while</c> statement and returns its body scope.
	/// </summary>
	/// <param name="condition">The trailing loop condition.</param>
	/// <returns>The loop body scope, which writes <c>} while (condition);</c> when disposed.</returns>
	/// <example><code>using (writer.DoWhileScope("!finished")) writer.MethodCall("Advance");</code></example>
	public BlockScope DoWhileScope(string condition)
	{
		ValidateStatementPart(condition, nameof(condition));
		return OpenDelimitedBlockScope("do", "{", "} while (" + condition + ");");
	}

	/// <summary>
	/// Writes a <c>try</c> block and invokes a callback for its body.
	/// </summary>
	/// <param name="body">The action that writes the try body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Try(body =&gt; body.MethodCall("Run"));</code></example>
	public CodeWriter Try(Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (TryScope())
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>try</c> block and returns its body scope.
	/// </summary>
	/// <returns>The try body scope.</returns>
	/// <example><code>using (writer.TryScope()) writer.MethodCall("Run");</code></example>
	public BlockScope TryScope() => OpenDelimitedBlockScope("try", "{", "}");

	/// <summary>
	/// Writes a <c>catch</c> block and invokes a callback for its body.
	/// </summary>
	/// <param name="body">The action that writes the catch body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Catch(body =&gt; body.Throw(PurviewTypeLibrary.System.InvalidOperationException, "Failed"));</code></example>
	public CodeWriter Catch(Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (CatchScope())
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a typed <c>catch</c> block and invokes a callback for its body.
	/// </summary>
	/// <param name="exceptionType">The caught exception type, or <see langword="null"/> for a bare catch.</param>
	/// <param name="name">The exception variable name, or <see langword="null"/> to omit it.</param>
	/// <param name="body">The action that writes the catch body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Catch(PurviewTypeLibrary.System.Exception, "ex", body =&gt; body.MethodCall("Log", "ex"));</code></example>
	public CodeWriter Catch(TypeReference? exceptionType, string? name, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (CatchScope(exceptionType, name))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>catch</c> block and returns its body scope.
	/// </summary>
	/// <param name="exceptionType">The caught exception type, or <see langword="null"/> for a bare catch.</param>
	/// <param name="name">The exception variable name, or <see langword="null"/> to omit it.</param>
	/// <returns>The catch body scope.</returns>
	/// <example><code>using (writer.CatchScope(PurviewTypeLibrary.System.Exception, "ex")) writer.MethodCall("Log", "ex");</code></example>
	public BlockScope CatchScope(TypeReference? exceptionType = null, string? name = null)
	{
		Write("catch");
		if (exceptionType is not null)
		{
			Write(" (").TypeReference(exceptionType);
			if (!string.IsNullOrWhiteSpace(name))
				Write(' ').Write(name);
			Write(')');
		}
		Line();
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes a <c>finally</c> block and invokes a callback for its body.
	/// </summary>
	/// <param name="body">The action that writes the finally body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Finally(body =&gt; body.MethodCall("Dispose"));</code></example>
	public CodeWriter Finally(Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (FinallyScope())
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>finally</c> block and returns its body scope.
	/// </summary>
	/// <returns>The finally body scope.</returns>
	/// <example><code>using (writer.FinallyScope()) writer.MethodCall("Dispose");</code></example>
	public BlockScope FinallyScope()
	{
		Line("finally");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes a <c>using</c> statement and invokes a callback for its body.
	/// </summary>
	/// <param name="declaration">The resource declaration, such as <c>var stream = Open()</c>.</param>
	/// <param name="body">The action that writes the using body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.UsingStatement("var stream = Open()", body =&gt; body.MethodCall("Read", "stream"));</code></example>
	public CodeWriter UsingStatement(string declaration, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (UsingStatementScope(declaration))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>using</c> statement and returns its body scope.
	/// </summary>
	/// <param name="declaration">The resource declaration, such as <c>var stream = Open()</c>.</param>
	/// <returns>The using body scope.</returns>
	/// <example><code>using (writer.UsingStatementScope("var stream = Open()")) writer.MethodCall("Read", "stream");</code></example>
	public BlockScope UsingStatementScope(string declaration)
	{
		ValidateStatementPart(declaration, nameof(declaration));
		Write("using (").Write(declaration).Line(")");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes a <c>lock</c> statement and invokes a callback for its body.
	/// </summary>
	/// <param name="expression">The lock expression.</param>
	/// <param name="body">The action that writes the lock body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.LockStatement("_gate", body =&gt; body.MethodCall("Run"));</code></example>
	public CodeWriter LockStatement(string expression, Action<CodeWriter> body)
	{
		if (body is null)
			throw new ArgumentNullException(nameof(body));
		using (LockStatementScope(expression))
			body(this);
		return this;
	}

	/// <summary>
	/// Writes a <c>lock</c> statement and returns its body scope.
	/// </summary>
	/// <param name="expression">The lock expression.</param>
	/// <returns>The lock body scope.</returns>
	/// <example><code>using (writer.LockStatementScope("_gate")) writer.MethodCall("Run");</code></example>
	public BlockScope LockStatementScope(string expression)
	{
		ValidateStatementPart(expression, nameof(expression));
		Write("lock (").Write(expression).Line(")");
		return OpenBlockScope();
	}

	/// <summary>
	/// Writes a comma-separated collection with one item per line.
	/// </summary>
	/// <param name="items">The items to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MultiLineItems("first", "second");</code></example>
	public CodeWriter MultiLineItems(params string[] items)
	{
		if (items is null)
			throw new ArgumentNullException(nameof(items));

		for (var index = 0; index < items.Length; index++)
		{
			Write(items[index]);
			if (index != items.Length - 1)
				Write(',');

			NewLine();
		}

		return this;
	}

	/// <summary>
	/// Writes each non-null value on its own line.
	/// </summary>
	/// <param name="lines">The lines to write.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Lines(["first", null, "second"]);</code></example>
	public CodeWriter Lines(IEnumerable<string?> lines)
	{
		if (lines is null)
			throw new ArgumentNullException(nameof(lines));

		foreach (var line in lines)
		{
			if (line is not null)
				Line(line);
		}

		return this;
	}

	/// <summary>
	/// Writes values separated by the specified delimiter.
	/// </summary>
	/// <param name="items">The values to write.</param>
	/// <param name="delimiter">The delimiter written between values.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Delimited(["a", "b"], " | "); // a | b</code></example>
	public CodeWriter Delimited(IEnumerable<string?> items, string delimiter = ", ")
	{
		if (items is null)
			throw new ArgumentNullException(nameof(items));
		if (delimiter is null)
			throw new ArgumentNullException(nameof(delimiter));

		var first = true;
		foreach (var item in items)
		{
			if (!first)
				Write(delimiter);

			Write(item);
			first = false;
		}

		return this;
	}

	/// <summary>
	/// Writes a C# collection expression such as <c>[a, b, ..c]</c>, optionally one element per line with
	/// the closing bracket on its own unindented line.
	/// </summary>
	/// <param name="items">The element expressions; spread elements such as <c>..source</c> are passed verbatim.</param>
	/// <param name="writeOnSeparateLines">Whether to write one element per line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.CollectionExpression(["first", "second", "..rest"]); // [first, second, ..rest]</code></example>
	public CodeWriter CollectionExpression(IEnumerable<string?> items, bool writeOnSeparateLines = false)
	{
		if (items is null)
			throw new ArgumentNullException(nameof(items));

		var elements = items.ToArray();
		Write('[');
		if (elements.Length == 0)
			return Write(']');

		if (!writeOnSeparateLines)
		{
			for (var index = 0; index < elements.Length; index++)
			{
				if (index != 0)
					Write(", ");
				Write(elements[index]);
			}

			return Write(']');
		}

		NewLine().Indent();
		for (var index = 0; index < elements.Length; index++)
		{
			Write(elements[index]);
			if (index != elements.Length - 1)
				Line(",");
			else
				NewLine();
		}
		Unindent();
		return Write(']');
	}

	/// <summary>
	/// Increases indentation until the returned scope is disposed.
	/// </summary>
	/// <returns>A scope that restores the indentation level.</returns>
	/// <example><code>using (writer.IndentedScope()) writer.Line("value");</code></example>
	public IndentScope IndentedScope()
	{
		Indent();
		return new(
			this,
			OpenScope(
				"indentation",
				header: null,
				TracksOpenScopes ? new StackTrace(1, fNeedFileInfo: true).ToString() : string.Empty
			)
		);
	}

	/// <summary>
	/// Invokes a callback at one additional indentation level.
	/// </summary>
	/// <summary>
	/// Invokes a callback at one additional indentation level.
	/// </summary>
	/// <param name="bodyWriter">The action to invoke while indented.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Indented(body =&gt; body.Line("value"));</code></example>
	public CodeWriter Indented(Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));
		using (IndentedScope())
			bodyWriter(this);
		return this;
	}

	/// <summary>
	/// Writes a line and increases indentation until the returned scope is disposed.
	/// </summary>
	/// <param name="line">The line to write before indenting.</param>
	/// <returns>A scope that restores the indentation level.</returns>
	/// <example><code>using (writer.IndentedScope("if (enabled)")) writer.Line("Run();");</code></example>
	public IndentScope IndentedScope(string line)
	{
		Line(line);
		return IndentedScope();
	}

	/// <summary>
	/// Writes a line and invokes a callback at one additional indentation level.
	/// </summary>
	/// <summary>
	/// Writes a line and invokes a callback at one additional indentation level.
	/// </summary>
	/// <param name="line">The line to write before indenting.</param>
	/// <param name="bodyWriter">The action to invoke while indented.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Indented("if (enabled)", body =&gt; body.Line("Run();"));</code></example>
	public CodeWriter Indented(string line, Action<CodeWriter> bodyWriter)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));
		using (IndentedScope(line))
			bodyWriter(this);
		return this;
	}

	/// <summary>
	/// Creates the generated source string.
	/// </summary>
	/// <returns>The complete contents of the writer.</returns>
	/// <example><code>writer.Line("class C { }");
	/// var source = writer.ToString(); // class C { }</code></example>
	[SuppressMessage(
		"Design",
		"CA1065:Do not raise exceptions in unexpected locations",
		Justification = "Throwing is explicit opt-in validation for detecting unclosed generated-code scopes during development and tests."
	)]
	public override string ToString()
	{
		if (ThrowOnUnclosedScopes && OpenScopeCount != 0)
			throw new CodeWriterScopeValidationException(_openScopes!.Values);

		// Note: StringBuilder.ToString() is optimized to avoid copying the buffer when possible, so this is a low-cost operation.
		return _builder.ToString();
	}

	/// <summary>
	/// Creates a <see cref="Microsoft.CodeAnalysis.Text.SourceText"/> from the writer's contents.
	/// </summary>
	/// <example><code>Microsoft.CodeAnalysis.Text.SourceText sourceText = writer; // class C { }</code></example>
	[SuppressMessage("Design", "CA1062:Validate arguments of public methods")]
	public static implicit operator Microsoft.CodeAnalysis.Text.SourceText(CodeWriter writer) =>
		Microsoft.CodeAnalysis.Text.SourceText.From(writer.ToString(), Encoding.UTF8);

	void IndentIfRequired()
	{
		if (!_atLineStart)
			return;

		if (_indentLevel != 0)
			AppendIndentation();

		_atLineStart = false;
	}

	void AppendIndentation()
	{
		if (_indentCharacter == '\t')
			_builder.Append('\t', _indentLevel);
		else
			_builder.Append(' ', _indentLevel * _indentationSize);
	}

	/// <summary>
	/// Writes a preprocessor directive such as <c>#if NET</c> or <c>#endif</c> at column zero, without any
	/// indentation, followed by a line feed.
	/// </summary>
	void DirectiveLine(string value)
	{
		EnsureNewLine();
		_builder.Append(value);
		_builder.Append(NewLineCharacter);
		_atLineStart = true;
	}

	void Expression(string? expression, Action<CodeWriter>? expressionWriter)
	{
		var callback = expressionWriter;
		if (callback is not null)
		{
			// Expressions are typically short, so use a small buffer and copy this writer's current
			// settings directly rather than allocating a fresh GenerationSettings.
			CodeWriter expressionWriterBuffer = new(this, DefaultExpressionCapacity);
			callback!(expressionWriterBuffer);
			expression = expressionWriterBuffer.ToString().TrimEnd(NewLineCharacter);
		}

		if (string.IsNullOrEmpty(expression))
			return;

		if (callback is not null)
		{
			// Callback-produced expressions carry their own internal layout and indentation relative to
			// the expression's start. Splice each line in place: the writer's current indentation is
			// applied at the start of every continuation line, and the callback's own indentation is
			// preserved, so multi-line expressions are not double-indented.
			var callbackLines = expression!.Split(NewLineCharacter);
			Write(callbackLines[0].TrimEnd('\r'));
			for (var index = 1; index < callbackLines.Length; index++)
			{
				NewLine();
				Write(callbackLines[index].TrimEnd('\r'));
			}

			return;
		}

		var lines = expression!.Split(NewLineCharacter);
		for (var index = 0; index < lines.Length; index++)
		{
			if (index != 0)
			{
				NewLine();
				Indent();
			}
			Write(lines[index].TrimEnd('\r'));
			if (index != 0)
				Unindent();
		}
	}

	static void ValidateStatementPart(string? value, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException("Statement text cannot be null or whitespace.", parameterName);
	}

	static string EscapeStringLiteral(string value) =>
		value
			.Replace("\\", "\\\\")
			.Replace("\"", "\\\"")
			.Replace("\r", "\\r")
			.Replace("\n", "\\n")
			.Replace("\t", "\\t");

	void ParametersWithHeuristic(ImmutableArray<ParameterDeclarationOptions> parameters)
	{
		if (parameters.IsDefault)
			parameters = [];

		var inlineLength = CurrentLineLength + 2;
		for (var index = 0; index < parameters.Length; index++)
			inlineLength += GetParameterLength(parameters[index]) + (index == 0 ? 0 : 2);

		Write('(');
		if (inlineLength <= _maximumLineLength)
		{
			for (var index = 0; index < parameters.Length; index++)
			{
				if (index != 0)
					Write(", ");
				Parameter(parameters[index]);
			}
			Write(')');
			return;
		}

		NewLine().Indent();
		for (var index = 0; index < parameters.Length; index++)
		{
			Parameter(parameters[index]);
			if (index != parameters.Length - 1)
				Line(",");
			else
				NewLine();
		}
		Unindent();
		Write(')');
	}

	CodeWriter Parameter(ParameterDeclarationOptions parameter)
	{
		for (var index = 0; !parameter.Attributes.IsDefaultOrEmpty && index < parameter.Attributes.Length; index++)
			Attribute(parameter.Attributes[index], defaultTarget: null).Write(' ');
		If(parameter.IsThis, "this ")
			.If(parameter.IsScoped, "scoped ")
			.If(parameter.IsParams, "params ")
			.Write(
				parameter.Modifier switch
				{
					ParameterModifier.None => string.Empty,
					ParameterModifier.Ref => "ref ",
					ParameterModifier.Out => "out ",
					ParameterModifier.In => "in ",
					ParameterModifier.RefReadOnly => "ref readonly ",
					_ => throw new ArgumentOutOfRangeException(nameof(parameter)),
				}
			)
			.TypeReference(GetParameterType(parameter))
			.Write(' ')
			.Write(parameter.Name);
		if (parameter.DefaultValue is not null)
			Write(" = ").Write(parameter.DefaultValue);
		return this;
	}

	int GetParameterLength(ParameterDeclarationOptions parameter)
	{
		var length = GetTypeReferenceLength(GetParameterType(parameter)) + parameter.Name.Length + 1;
		if (parameter.IsThis)
			length += 5;
		if (parameter.IsScoped)
			length += 7;
		if (parameter.IsParams)
			length += 7;
		length += parameter.Modifier switch
		{
			ParameterModifier.None => 0,
			ParameterModifier.Ref or ParameterModifier.Out => 4,
			ParameterModifier.In => 3,
			ParameterModifier.RefReadOnly => 13,
			_ => 0,
		};
		if (parameter.DefaultValue is not null)
			length += parameter.DefaultValue.Length + 3;
		for (var index = 0; !parameter.Attributes.IsDefaultOrEmpty && index < parameter.Attributes.Length; index++)
			length += GetAttributeLength(parameter.Attributes[index]) + 1;
		return length;
	}

	static TypeReference GetParameterType(ParameterDeclarationOptions parameter) => parameter.Reference;

	static string RenderCallArgument(MethodCallArgumentOptions argument)
	{
		ValidateRequired(argument.Value, "Argument value", nameof(argument));
		if (argument.Name is not null)
			ValidateRequired(argument.Name, "Argument name", nameof(argument));
		return (
				argument.Modifier switch
				{
					ParameterModifier.None => string.Empty,
					ParameterModifier.Ref => "ref ",
					ParameterModifier.Out => "out ",
					ParameterModifier.In => "in ",
					ParameterModifier.RefReadOnly => "ref readonly ",
					_ => throw new ArgumentOutOfRangeException(nameof(argument)),
				}
			)
			+ (argument.Name is null ? string.Empty : argument.Name + ": ")
			+ argument.Value;
	}

	void Attributes(ImmutableArray<AttributeDeclarationOptions> attributes, string? defaultTarget = null)
	{
		ValidateAttributes(attributes, nameof(attributes));
		for (var index = 0; !attributes.IsDefaultOrEmpty && index < attributes.Length; index++)
			Attribute(attributes[index], defaultTarget).NewLine();
	}

	CodeWriter Attribute(AttributeDeclarationOptions attribute, string? defaultTarget)
	{
		Write('[');
		var target = attribute.Target ?? defaultTarget;
		if (target is not null)
			Write(target).Write(": ");
		Write(attribute.Reference.RenderAttributeName);
		if (!attribute.Arguments.IsDefaultOrEmpty)
		{
			Write('(');
			for (var index = 0; index < attribute.Arguments.Length; index++)
			{
				if (index != 0)
					Write(", ");
				var argument = attribute.Arguments[index];
				if (argument.Name is not null)
					Write(argument.Name).Write(argument.IsPropertyAssignment ? " = " : ": ");
				Write(argument.Value);
			}
			Write(')');
		}
		return Write(']');
	}

	static int GetAttributeLength(AttributeDeclarationOptions attribute)
	{
		var length = attribute.Reference.RenderAttributeName.Length + 2;
		if (attribute.Target is not null)
			length += attribute.Target.Length + 2;
		if (attribute.Arguments.IsDefaultOrEmpty)
			return length;
		length += 2;
		for (var index = 0; index < attribute.Arguments.Length; index++)
		{
			if (index != 0)
				length += 2;
			var argument = attribute.Arguments[index];
			length += argument.Value.Length;
			if (argument.Name is not null)
				length += argument.Name.Length + 3;
		}
		return length;
	}

	void MemberModifiers(
		TypeDeclarationAccessibility? accessibility,
		bool isStatic,
		bool isAbstract,
		bool isVirtual,
		bool isOverride,
		bool isSealed,
		bool isReadOnly = false,
		bool isRequired = false
	)
	{
		if (accessibility is { } value)
			Accessibility(value).Write(' ');
		If(isRequired, "required ")
			.If(isReadOnly, "readonly ")
			.If(isStatic, "static ")
			.If(isSealed, "sealed ")
			.If(isAbstract, "abstract ")
			.If(isVirtual, "virtual ")
			.If(isOverride, "override ");
	}

	CodeWriter PropertyHeader(PropertyDeclarationOptions declaration)
	{
		MemberModifiers(
			ResolveAccessibility(declaration.Accessibility, DefaultPropertyAccessibility),
			declaration.IsStatic,
			declaration.IsAbstract,
			declaration.IsVirtual,
			declaration.IsOverride,
			declaration.IsSealed,
			isRequired: declaration.IsRequired
		);
		return TypeReference(declaration.Type).Write(' ').Write(declaration.Name);
	}

	void Accessor(TypeDeclarationAccessibility? accessibility, string accessor)
	{
		if (accessibility is { } value)
			Accessibility(value).Write(' ');
		Write(accessor).Write(' ');
	}

	void AccessorBody(TypeDeclarationAccessibility? accessibility, string accessor, Action<CodeWriter>? writeBody)
	{
		if (accessibility is { } value)
			Accessibility(value).Write(' ');
		if (writeBody is null)
		{
			Write(accessor).Line(";");
			return;
		}
		using (OpenBlockScope(accessor))
			writeBody(this);
	}

	void BeginWrittenItem(WrittenItemKind nextItem)
	{
		if (_lastWrittenItem == WrittenItemKind.None || _lastWrittenItemIndent != _indentLevel)
			return;

		var requiresBlankLine = _lastWrittenItem != WrittenItemKind.Field || nextItem != WrittenItemKind.Field;
		if (
			requiresBlankLine
			&& (_builder.Length == _lastWrittenItemEnd || _builder[_lastWrittenItemEnd] != NewLineCharacter)
		)
		{
			_builder.Insert(_lastWrittenItemEnd, NewLineCharacter);
		}

		// A declaration can only consume the preceding item's separator once. Its own completion
		// establishes the state used by the following declaration.
		_lastWrittenItem = WrittenItemKind.None;
	}

	void CompleteWrittenItem(WrittenItemKind item, int indent)
	{
		_lastWrittenItem = item;
		_lastWrittenItemIndent = indent;
		_lastWrittenItemEnd = _builder.Length;
	}

	BlockScope OpenBlockScope(WrittenItemKind completedItem)
	{
		var itemIndent = _indentLevel;
		Line("{").Indent();
		return TrackOpenBlockScope(header: null, closingSeparator: "}", completedItem, itemIndent);
	}

	int CurrentLineLength
	{
		get
		{
			var length = 0;
			for (var index = _builder.Length - 1; index >= 0; index--)
			{
				if (_builder[index] == NewLineCharacter)
					break;
				length += _builder[index] == '\t' ? _indentationSize : 1;
			}
			return length + (_atLineStart ? _indentLevel * _indentationSize : 0);
		}
	}

	BlockScope TrackOpenBlockScope(
		string? header,
		string? closingSeparator,
		WrittenItemKind completedItem = WrittenItemKind.None,
		int itemIndent = -1,
		bool closingAtColumnZero = false,
		bool changesIndentation = true
	)
	{
		return new BlockScope(
			this,
			closingSeparator,
			OpenScope(
				"block",
				header,
				TracksOpenScopes ? new StackTrace(1, fNeedFileInfo: true).ToString() : string.Empty
			),
			(int)completedItem,
			itemIndent,
			closingAtColumnZero,
			changesIndentation
		);
	}

	int OpenScope(string kind, string? header, string capturedStackTrace)
	{
		OpenScopeCount++;
		if (_openScopes is null)
			return 0;

		var scopeId = ++_nextScopeId;
		_openScopes.Add(scopeId, new CodeWriterOpenScope(kind, header, capturedStackTrace));
		return scopeId;
	}

	void CloseBlock(
		string? closingSeparator,
		int scopeId,
		int completedItem,
		int itemIndent,
		bool closingAtColumnZero,
		bool changesIndentation
	)
	{
		CloseScope(scopeId, "block");

		if (changesIndentation)
			Unindent();

		if (closingSeparator is not null)
		{
			if (closingAtColumnZero)
			{
				DirectiveLine(closingSeparator);

				// A directive between declarations must not participate in member blank-line spacing:
				// advance the tracker so the next member's separator lands after the directive, and at
				// file level ensure a trailing blank line so directive sections remain separated.
				_lastWrittenItemEnd = _builder.Length;
				if (_indentLevel == 0)
					EnsureBlankLine();
			}
			else
				Line(closingSeparator);
		}

		if (completedItem != (int)WrittenItemKind.None)
			CompleteWrittenItem((WrittenItemKind)completedItem, itemIndent);
	}

	void CloseIndentScope(int scopeId)
	{
		CloseScope(scopeId, "indentation");
		Unindent();
	}

	void CloseScope(int scopeId, string kind)
	{
		if (OpenScopeCount <= 0)
			throw new InvalidOperationException($"A {kind} scope was closed without a matching open scope.");

		if (_openScopes is not null && !_openScopes.Remove(scopeId))
			throw new InvalidOperationException($"The {kind} scope has already been closed.");

		OpenScopeCount--;
	}

	CodeWriter Accessibility(TypeDeclarationAccessibility accessibility)
	{
		return Write(
			accessibility switch
			{
				TypeDeclarationAccessibility.Public => "public",
				TypeDeclarationAccessibility.Internal => "internal",
				TypeDeclarationAccessibility.Protected => "protected",
				TypeDeclarationAccessibility.Private => "private",
				TypeDeclarationAccessibility.ProtectedInternal => "protected internal",
				TypeDeclarationAccessibility.PrivateProtected => "private protected",
				TypeDeclarationAccessibility.File => "file",
				_ => throw new ArgumentOutOfRangeException(nameof(accessibility)),
			}
		);
	}

	static TypeDeclarationAccessibility? ResolveAccessibility(
		TypeDeclarationAccessibility? explicitAccessibility,
		TypeDeclarationAccessibility? defaultAccessibility
	) => explicitAccessibility ?? defaultAccessibility;

	static TypeDeclarationAccessibility? ResolveAccessorAccessibility(
		TypeDeclarationAccessibility? explicitAccessibility,
		TypeDeclarationAccessibility? defaultAccessibility,
		TypeDeclarationAccessibility? propertyAccessibility
	)
	{
		var resolved = explicitAccessibility ?? defaultAccessibility;
		if (resolved is null || propertyAccessibility is null)
			return null;

		// C# forbids an accessor modifier that is equal to or more permissive than the property's own
		// accessibility (CS0273). When the resolved accessor accessibility is not strictly more
		// restrictive, the accessor inherits the property's accessibility instead.
		return IsValidAccessorAccessibility(resolved.Value, propertyAccessibility.Value) ? resolved : null;
	}

	[SuppressMessage("Style", "IDE0072:Add missing cases")]
	static bool IsValidAccessorAccessibility(
		TypeDeclarationAccessibility accessor,
		TypeDeclarationAccessibility property
	) =>
		accessor switch
		{
			TypeDeclarationAccessibility.Private => true,
			TypeDeclarationAccessibility.PrivateProtected => property
				is not (TypeDeclarationAccessibility.Private or TypeDeclarationAccessibility.PrivateProtected),
			TypeDeclarationAccessibility.Protected or TypeDeclarationAccessibility.Internal => property
				is TypeDeclarationAccessibility.Public
					or TypeDeclarationAccessibility.ProtectedInternal,
			TypeDeclarationAccessibility.ProtectedInternal => property is TypeDeclarationAccessibility.Public,
			_ => false,
		};

	void GenericTypeParameters(ImmutableArray<GenericTypeParameterOptions> genericTypes)
	{
		if (genericTypes.IsDefaultOrEmpty)
			return;

		Write('<');
		for (var index = 0; index < genericTypes.Length; index++)
		{
			if (index != 0)
				Write(", ");

			Write(genericTypes[index].Name);
		}

		Write('>');
	}

	void ParameterList(
		ImmutableArray<ParameterDeclarationOptions> parameters,
		bool writeOnSeparateLines,
		bool writeWhenEmpty = false
	)
	{
		if (parameters.IsDefaultOrEmpty && !writeWhenEmpty)
			return;

		Write('(');
		if (!parameters.IsDefaultOrEmpty)
		{
			if (writeOnSeparateLines)
				Indent();

			for (var index = 0; index < parameters.Length; index++)
			{
				if (index != 0 && !writeOnSeparateLines)
					Write(", ");

				if (writeOnSeparateLines)
					NewLine();

				Parameter(parameters[index]);
				if (writeOnSeparateLines && index != parameters.Length - 1)
					Write(',');
			}

			if (writeOnSeparateLines)
				Unindent().NewLine();
		}

		Write(')');
	}

	void BaseTypes(TypeDeclarationOptions declaration)
	{
		var hasBaseType = declaration.BaseType is { IsEmpty: false };
		var hasInterfaces = HasNonEmptyTypeReferences(declaration.Interfaces);
		if (!hasBaseType && !hasInterfaces)
			return;

		Write(" : ");
		if (hasBaseType)
			TypeReference(declaration.BaseType!);

		if (!hasInterfaces)
			return;

		var wroteType = hasBaseType;
		for (var index = 0; index < declaration.Interfaces.Length; index++)
		{
			if (declaration.Interfaces[index].IsEmpty)
				continue;
			if (wroteType)
				Write(", ");

			TypeReference(declaration.Interfaces[index]);
			wroteType = true;
		}
	}

	static bool HasNonEmptyTypeReferences(ImmutableArray<TypeReference> types)
	{
		for (var index = 0; !types.IsDefaultOrEmpty && index < types.Length; index++)
		{
			if (!types[index].IsEmpty)
				return true;
		}
		return false;
	}

	void GenericConstraints(ImmutableArray<GenericTypeParameterOptions> genericTypes)
	{
		if (genericTypes.IsDefaultOrEmpty)
			return;

		for (var typeIndex = 0; typeIndex < genericTypes.Length; typeIndex++)
		{
			var genericType = genericTypes[typeIndex];
			if (genericType.Constraints.IsDefaultOrEmpty)
				continue;

			Write("where ").Write(genericType.Name).Write(" : ");
			for (var constraintIndex = 0; constraintIndex < genericType.Constraints.Length; constraintIndex++)
			{
				if (constraintIndex != 0)
					Write(", ");

				Write(genericType.Constraints[constraintIndex]);
			}

			NewLine();
		}
	}

	static void ValidateTypeDeclaration(TypeDeclarationOptions declaration)
	{
		var isStruct = declaration.Kind is TypeDeclarationKind.Struct or TypeDeclarationKind.RecordStruct;
		var supportsPrimaryConstructor =
			declaration.Kind
			is TypeDeclarationKind.Class
				or TypeDeclarationKind.Struct
				or TypeDeclarationKind.RecordClass
				or TypeDeclarationKind.RecordStruct;
		ValidateTypeDeclarationModifiers(declaration, isStruct);
		ValidateAdditionalTypeKindOptions(declaration, supportsPrimaryConstructor);

		ValidateParameters(
			declaration.PrimaryConstructorParameters,
			"Primary-constructor parameters cannot contain null or whitespace values.",
			nameof(declaration)
		);

		for (var index = 0; !declaration.Interfaces.IsDefaultOrEmpty && index < declaration.Interfaces.Length; index++)
		{
			ValidateTypeReference(declaration.Interfaces[index], nameof(declaration));
		}

		if (declaration.GenericTypes.IsDefaultOrEmpty)
			return;

		for (var typeIndex = 0; typeIndex < declaration.GenericTypes.Length; typeIndex++)
		{
			var genericType =
				declaration.GenericTypes[typeIndex]
				?? throw new ArgumentException(
					"Generic type parameters cannot contain null values.",
					nameof(declaration)
				);
			if (genericType.Constraints.IsDefaultOrEmpty)
				continue;

			for (var constraintIndex = 0; constraintIndex < genericType.Constraints.Length; constraintIndex++)
			{
				if (string.IsNullOrWhiteSpace(genericType.Constraints[constraintIndex]))
					throw new ArgumentException(
						"Generic constraints cannot be null or whitespace.",
						nameof(declaration)
					);
			}
		}
	}

	static void ValidateTypeDeclarationModifiers(TypeDeclarationOptions declaration, bool isStruct)
	{
		if (declaration.IsStatic && declaration.Kind != TypeDeclarationKind.Class)
			throw new ArgumentException("Only class declarations can be static.", nameof(declaration));

		if (
			declaration.IsAbstract
			&& declaration.Kind is not TypeDeclarationKind.Class and not TypeDeclarationKind.RecordClass
		)
			throw new ArgumentException(
				"Only class and record class declarations can be abstract.",
				nameof(declaration)
			);

		if (declaration.IsAbstract && declaration.IsStatic)
			throw new ArgumentException("A class cannot be both abstract and static.", nameof(declaration));

		if (declaration.IsStatic && declaration.BaseType is { IsEmpty: false })
			throw new ArgumentException("A static class cannot specify a base type.", nameof(declaration));

		if (declaration.IsStatic && HasNonEmptyTypeReferences(declaration.Interfaces))
			throw new ArgumentException("A static class cannot implement interfaces.", nameof(declaration));

		if (declaration.IsStatic && !declaration.PrimaryConstructorParameters.IsDefaultOrEmpty)
			throw new ArgumentException(
				"A static class cannot declare primary-constructor parameters.",
				nameof(declaration)
			);

		if (isStruct && declaration.BaseType is { IsEmpty: false })
			throw new ArgumentException(
				"Struct and record struct declarations cannot specify a base type.",
				nameof(declaration)
			);

		if (!isStruct && declaration.IsReadOnly)
			throw new ArgumentException(
				"Only struct and record struct declarations can be readonly.",
				nameof(declaration)
			);

		if (declaration.IsRefStruct && declaration.Kind != TypeDeclarationKind.Struct)
			throw new ArgumentException("Only struct declarations can be ref structs.", nameof(declaration));

		if (declaration.IsRefStruct && declaration.BaseType is { IsEmpty: false })
			throw new ArgumentException("A ref struct cannot specify a base type.", nameof(declaration));
	}

	static void ValidateAdditionalTypeKindOptions(TypeDeclarationOptions declaration, bool supportsPrimaryConstructor)
	{
		if (
			declaration.Kind
				is TypeDeclarationKind.Interface
					or TypeDeclarationKind.Enum
					or TypeDeclarationKind.Delegate
			&& declaration.BaseType is { IsEmpty: false }
		)
			throw new ArgumentException(
				"Interfaces, enums, and delegates cannot specify BaseType. Use Interfaces for interface inheritance and EnumUnderlyingType for enums.",
				nameof(declaration)
			);
		if (!supportsPrimaryConstructor && !declaration.PrimaryConstructorParameters.IsDefaultOrEmpty)
			throw new ArgumentException(
				"This type declaration does not support primary-constructor parameters.",
				nameof(declaration)
			);
		if (
			declaration.Kind is TypeDeclarationKind.Enum or TypeDeclarationKind.Delegate
			&& HasNonEmptyTypeReferences(declaration.Interfaces)
		)
			throw new ArgumentException("Enums and delegates cannot declare interfaces.", nameof(declaration));
		if (declaration.Kind == TypeDeclarationKind.Enum && !declaration.GenericTypes.IsDefaultOrEmpty)
			throw new ArgumentException("Enums cannot be generic.", nameof(declaration));
		if (declaration.EnumUnderlyingType is not null && declaration.Kind != TypeDeclarationKind.Enum)
			throw new ArgumentException("EnumUnderlyingType is only valid for enum declarations.", nameof(declaration));
		if (
			declaration.Kind == TypeDeclarationKind.Enum
			&& declaration.EnumUnderlyingType is not null
			&& string.IsNullOrWhiteSpace(declaration.EnumUnderlyingType.Identity.Name)
		)
			throw new ArgumentException("Enum underlying type cannot be whitespace.", nameof(declaration));
		if (declaration.Kind == TypeDeclarationKind.Delegate)
		{
			if (declaration.DelegateReturnType is null)
				throw new ArgumentException("Delegate return type is required.", nameof(declaration));
			ValidateTypeReference(declaration.DelegateReturnType, nameof(declaration));
			ValidateParameters(
				declaration.DelegateParameters,
				"Delegate parameters cannot contain null or whitespace values.",
				nameof(declaration)
			);
		}
		else if (declaration.DelegateReturnType is not null || !declaration.DelegateParameters.IsDefaultOrEmpty)
			throw new ArgumentException(
				"Delegate return type and parameters are only valid for delegate declarations.",
				nameof(declaration)
			);
	}

	static void ValidateConstructorDeclaration(ConstructorDeclarationOptions declaration)
	{
		if (declaration.Accessibility == TypeDeclarationAccessibility.File)
			throw new ArgumentException(
				"The file accessibility modifier is only valid for types.",
				nameof(declaration)
			);
		ValidateParameters(
			declaration.Parameters,
			"Constructor parameters cannot contain null or whitespace values.",
			nameof(declaration)
		);

		if (declaration.IsStatic && !declaration.Parameters.IsDefaultOrEmpty)
			throw new ArgumentException("A static constructor cannot declare parameters.", nameof(declaration));

		if (declaration.IsStatic && !string.IsNullOrWhiteSpace(declaration.Initializer))
			throw new ArgumentException("A static constructor cannot specify an initializer.", nameof(declaration));

		if (declaration.IsStatic && declaration.Accessibility is not null)
			throw new ArgumentException("A static constructor cannot specify accessibility.", nameof(declaration));
	}

	void MethodGenericConstraints(ImmutableArray<GenericTypeParameterOptions> genericTypes)
	{
		if (genericTypes.IsDefaultOrEmpty)
			return;

		var wroteConstraint = false;
		for (var typeIndex = 0; typeIndex < genericTypes.Length; typeIndex++)
		{
			var genericType = genericTypes[typeIndex];
			if (genericType.Constraints.IsDefaultOrEmpty)
				continue;
			if (wroteConstraint)
				NewLine();
			Write("where ").Write(genericType.Name).Write(" : ");
			for (var index = 0; index < genericType.Constraints.Length; index++)
			{
				if (index != 0)
					Write(", ");
				Write(genericType.Constraints[index]);
			}
			wroteConstraint = true;
		}
	}

	static void ValidateMethodDeclaration(MethodDeclarationOptions declaration)
	{
		ValidateRequired(declaration.Name, "Method name", nameof(declaration));
		ValidateTypeReference(declaration.ReturnType, nameof(declaration));
		ValidateMemberModifiers(
			declaration.Accessibility,
			declaration.IsAbstract,
			declaration.IsVirtual,
			declaration.IsOverride,
			declaration.IsSealed,
			nameof(declaration)
		);
		ValidateParameters(
			declaration.Parameters,
			"Method parameters cannot contain null or whitespace values.",
			nameof(declaration)
		);
		if (declaration.IsReadOnly && declaration.IsStatic)
			throw new ArgumentException("A readonly method cannot also be static.", nameof(declaration));
		if (declaration.IsAbstract && declaration.ExpressionBody is not null)
			throw new ArgumentException("An abstract method cannot have an expression body.", nameof(declaration));
		if (declaration.IsPartial && declaration.ExpressionBody is not null)
			throw new ArgumentException("A partial method cannot have an expression body.", nameof(declaration));
	}

	static void ValidateOperatorDeclaration(OperatorDeclarationOptions declaration)
	{
		ValidateRequired(declaration.OperatorToken, "Operator token", nameof(declaration));
		ValidateTypeReference(declaration.ReturnType, nameof(declaration));
		ValidateMemberModifiers(
			declaration.Accessibility,
			isAbstract: false,
			isVirtual: false,
			isOverride: false,
			isSealed: false,
			nameof(declaration)
		);
		switch (declaration.Kind)
		{
			case OperatorDeclarationKind.Binary:
				ValidateParameters(
					[declaration.Left, declaration.Right],
					"Operator parameters cannot contain null or whitespace values.",
					nameof(declaration)
				);
				break;

			case OperatorDeclarationKind.Unary:
			case OperatorDeclarationKind.ImplicitConversion:
			case OperatorDeclarationKind.ExplicitConversion:
				ValidateParameters(
					[declaration.Left],
					"Operator parameters cannot contain null or whitespace values.",
					nameof(declaration)
				);
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(declaration));
		}

		ValidateAttributes(declaration.Attributes, nameof(declaration));
	}

	static void ValidatePropertyDeclaration(PropertyDeclarationOptions declaration)
	{
		ValidateRequired(declaration.Name, "Property name", nameof(declaration));
		ValidateTypeReference(declaration.Type, nameof(declaration));
		ValidateMemberModifiers(
			declaration.Accessibility,
			declaration.IsAbstract,
			declaration.IsVirtual,
			declaration.IsOverride,
			declaration.IsSealed,
			nameof(declaration)
		);
		if (!declaration.HasGetter && !declaration.HasSetter && !declaration.IsInitOnly)
			throw new ArgumentException(
				"A property must have a getter, setter, or init accessor.",
				nameof(declaration)
			);
		if (declaration.ExpressionBody is not null && declaration.HasSetter)
			throw new ArgumentException("An expression-bodied property cannot have a setter.", nameof(declaration));
		if (declaration.ExpressionBody is not null && declaration.Initializer is not null)
			throw new ArgumentException(
				"A property cannot specify both an expression body and an initializer.",
				nameof(declaration)
			);
		if (declaration.IsAbstract && declaration.ExpressionBody is not null)
			throw new ArgumentException("An abstract property cannot have an expression body.", nameof(declaration));
		if (declaration.IsFieldBacked && declaration.ExpressionBody is not null)
			throw new ArgumentException(
				"A field-keyword property cannot have an expression body.",
				nameof(declaration)
			);
		if (declaration.IsFieldBacked && declaration.Initializer is not null)
			throw new ArgumentException("A field-keyword property cannot have an initializer.", nameof(declaration));
	}

	static void ValidateIndexerDeclaration(IndexerDeclarationOptions declaration)
	{
		ValidateTypeReference(declaration.Type, nameof(declaration));
		ValidateMemberModifiers(
			declaration.Accessibility,
			declaration.IsAbstract,
			declaration.IsVirtual,
			declaration.IsOverride,
			declaration.IsSealed,
			nameof(declaration)
		);
		ValidateParameters(
			declaration.Parameters,
			"Indexer parameters cannot contain null or whitespace values.",
			nameof(declaration)
		);
		if (declaration.ExpressionBody is not null && (declaration.HasSetter || declaration.IsInitOnly))
			throw new ArgumentException("An expression-bodied indexer cannot have a setter.", nameof(declaration));
		if (declaration.IsAbstract && declaration.ExpressionBody is not null)
			throw new ArgumentException("An abstract indexer cannot have an expression body.", nameof(declaration));
	}

	static void ValidateFieldDeclaration(FieldDeclarationOptions declaration)
	{
		ValidateRequired(declaration.Name, "Field name", nameof(declaration));
		ValidateTypeReference(declaration.Type, nameof(declaration));
		if (declaration.Accessibility == TypeDeclarationAccessibility.File)
			throw new ArgumentException(
				"The file accessibility modifier is only valid for types.",
				nameof(declaration)
			);
		if (declaration.IsConst && (declaration.IsStatic || declaration.IsReadOnly || declaration.IsVolatile))
			throw new ArgumentException(
				"A const field cannot also be static, readonly, or volatile.",
				nameof(declaration)
			);
		if (declaration.IsReadOnly && declaration.IsVolatile)
			throw new ArgumentException("A field cannot be both readonly and volatile.", nameof(declaration));
		if (declaration.IsConst && declaration.Initializer is null)
			throw new ArgumentException("A const field requires an initializer.", nameof(declaration));
		if (declaration.IsRefField && declaration.IsConst)
			throw new ArgumentException("A ref field cannot be const.", nameof(declaration));
		if (declaration.IsRefField && declaration.Initializer is not null)
			throw new ArgumentException("A ref field cannot have an initializer.", nameof(declaration));
		if (declaration.IsRefField && declaration.IsStatic)
			throw new ArgumentException("A ref field cannot be static.", nameof(declaration));
	}

	static void ValidateEnumFieldDeclaration(EnumFieldDeclarationOptions declaration)
	{
		ValidateRequired(declaration.FieldName, "Enum field name", nameof(declaration));
		ValidateAttributes(declaration.Attributes, nameof(declaration));
	}

	static void ValidateMemberModifiers(
		TypeDeclarationAccessibility? accessibility,
		bool isAbstract,
		bool isVirtual,
		bool isOverride,
		bool isSealed,
		string parameterName
	)
	{
		if (accessibility == TypeDeclarationAccessibility.File)
			throw new ArgumentException("The file accessibility modifier is only valid for types.", parameterName);
		var count = (isAbstract ? 1 : 0) + (isVirtual ? 1 : 0) + (isOverride ? 1 : 0);
		if (count > 1)
			throw new ArgumentException(
				"A member cannot be abstract, virtual, and override at the same time.",
				parameterName
			);
		if (isSealed && !isOverride)
			throw new ArgumentException("Only an override member can be sealed.", parameterName);
	}

	static void ValidateRequired(string? value, string description, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException($"{description} cannot be null or whitespace.", parameterName);
	}

	static void ValidateParameters(
		ImmutableArray<ParameterDeclarationOptions> parameters,
		string message,
		string parameterName
	)
	{
		if (parameters.IsDefaultOrEmpty)
			return;

		for (var index = 0; index < parameters.Length; index++)
		{
			if (
				string.IsNullOrWhiteSpace(parameters[index].Name)
				|| string.IsNullOrWhiteSpace(parameters[index].Reference.Identity.Name)
			)
				throw new ArgumentException(message, parameterName);
			else
			{
				ValidateTypeReference(parameters[index].Reference, parameterName);
				ValidateAttributes(parameters[index].Attributes, parameterName);
			}
		}
	}

	/// <summary>
	/// Writes the given type reference using the writer's nullable context.
	/// </summary>
	/// <param name="reference">The type reference to write.</param>
	/// <returns>The current writer.</returns>
	/// <remarks>
	/// When the writer's nullable context resolves to disabled (see <see cref="NullableDirectiveMode"/>
	/// and <see cref="IsNullableContextEnabled"/>), nullable reference annotations such as
	/// <c>string?</c> are elided because they are invalid outside a nullable context. Nullable value
	/// types such as <c>int?</c> are always written.
	/// </remarks>
	public CodeWriter Type(TypeReference reference)
	{
		if (reference is null)
			throw new ArgumentNullException(nameof(reference));
		if (reference.IsEmpty)
			return this;

		ValidateTypeReference(reference, nameof(reference));

		return Write(reference.RenderFullNameForNullable(ShouldUseNullableAnnotations));
	}

	CodeWriter TypeReference(TypeReference reference)
	{
		if (reference.IsEmpty)
			return this;

		ValidateTypeReference(reference, nameof(reference));

		Write(reference.RenderFullNameForNullable(ShouldUseNullableAnnotations));

		return this;
	}

	int GetTypeReferenceLength(TypeReference type) =>
		type.IsEmpty ? 0 : type.RenderFullNameForNullable(ShouldUseNullableAnnotations).Length;

	static void ValidateTypeReference(TypeReference reference, string parameterName)
	{
		if (reference.IsEmpty)
			return;

		var type = reference.Identity;
		if (type == TypeIdentity.Null)
			throw new ArgumentException("The null literal is not a valid type.", parameterName);
		if (string.IsNullOrWhiteSpace(type.Name))
			throw new ArgumentException("Type name cannot be null or whitespace.", parameterName);
		if (type.GenericArity < 0)
			throw new ArgumentException("Generic arity cannot be negative.", parameterName);
		if (
			!type.TypeArguments.IsDefaultOrEmpty
			&& (type.GenericArity == 0 || type.TypeArguments.Length != type.GenericArity)
		)
			throw new ArgumentException(
				"A constructed generic type must have one type argument for each declared generic parameter.",
				parameterName
			);
		for (var index = 0; !type.TypeArguments.IsDefaultOrEmpty && index < type.TypeArguments.Length; index++)
			ValidateTypeReference(type.TypeArguments[index], parameterName);
	}

	static void ValidateAttributes(ImmutableArray<AttributeDeclarationOptions> attributes, string parameterName)
	{
		for (var index = 0; !attributes.IsDefaultOrEmpty && index < attributes.Length; index++)
		{
			var attribute = attributes[index];
			if (attribute.Reference.IsEmpty)
				throw new ArgumentException("Attribute type names cannot be null or whitespace.", parameterName);
			if (attribute.Target is not null && string.IsNullOrWhiteSpace(attribute.Target))
				throw new ArgumentException("Attribute targets cannot be whitespace.", parameterName);
			for (
				var argumentIndex = 0;
				!attribute.Arguments.IsDefaultOrEmpty && argumentIndex < attribute.Arguments.Length;
				argumentIndex++
			)
			{
				var argument = attribute.Arguments[argumentIndex];
				if (string.IsNullOrWhiteSpace(argument.Value))
					throw new ArgumentException(
						"Attribute argument values cannot be null or whitespace.",
						parameterName
					);
				if (argument.Name is not null && string.IsNullOrWhiteSpace(argument.Name))
					throw new ArgumentException("Attribute argument names cannot be whitespace.", parameterName);
			}
		}
	}

	static bool HasGenericConstraints(ImmutableArray<GenericTypeParameterOptions> genericTypes)
	{
		if (genericTypes.IsDefaultOrEmpty)
			return false;

		for (var index = 0; index < genericTypes.Length; index++)
			if (!genericTypes[index].Constraints.IsDefaultOrEmpty)
				return true;
		return false;
	}
}
