namespace Purview.SourceGeneratorFramework;

partial class CodeWriter
{
	// ---------------------------------------------------------------------------------------------
	// Object creation expressions
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes a target-typed object-creation expression with no type or arguments, such as <c>new()</c>.
	/// The expression is written without a trailing semicolon so it composes as the value of an
	/// <see cref="Assignment(string, Action{CodeWriter})"/>, <see cref="Return(Action{CodeWriter})"/>, or
	/// other expression, and is valid only where the target type is known.
	/// </summary>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Assignment("Order", "order", expression =&gt; expression.New());
	/// // Order order = new();</code></example>
	public CodeWriter New() =>
		NewCore(reference: null, type: null, arguments: [], writeArgumentsOnSeparateLines: false);

	/// <summary>
	/// Writes an object-creation expression from a type name and argument expressions, such as
	/// <c>new Type(a, b)</c>. The expression is written without a trailing semicolon so it composes as
	/// the value of an <see cref="Assignment(string, Action{CodeWriter})"/>,
	/// <see cref="Return(Action{CodeWriter})"/>, or other expression callback.
	/// </summary>
	/// <param name="type">The type name written verbatim after <c>new</c>.</param>
	/// <param name="arguments">The constructor argument expressions.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Assignment("var hostKit", expression =&gt; expression.New("HostKit", "onBuilt", "onConfigured"));
	/// // var hostKit = new HostKit(onBuilt, onConfigured);</code></example>
	public CodeWriter New(string type, params string[] arguments)
	{
		ValidateStatementPart(type, nameof(type));
		return NewCore(reference: null, type, arguments, writeArgumentsOnSeparateLines: false);
	}

	/// <summary>
	/// Writes an object-creation expression from a type name and structured argument declarations.
	/// </summary>
	/// <param name="type">The type name written verbatim after <c>new</c>.</param>
	/// <param name="arguments">The structured constructor arguments, preserving <c>ref</c>, <c>out</c>, and <c>in</c> modifiers and named arguments.</param>
	/// <param name="writeArgumentsOnSeparateLines">Whether to force one argument per line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Assignment("var handler", expression =&gt; expression.New(
	/// 	"HostKit",
	/// 	[new("onBuilt"), new("onConfigured") { Name = "configured" }]));
	/// // var handler = new HostKit(onBuilt, configured: onConfigured);</code></example>
	public CodeWriter New(
		string type,
		IEnumerable<MethodCallArgumentOptions> arguments,
		bool writeArgumentsOnSeparateLines = false
	)
	{
		ValidateStatementPart(type, nameof(type));
		if (arguments is null)
			throw new ArgumentNullException(nameof(arguments));

		// The structured CodeWriter API does not yet support preprocessor directives other than #if/#else/#endif and
		return NewCore(
			reference: null,
			type,
			arguments.Select(static argument => RenderCallArgument(argument)),
			writeArgumentsOnSeparateLines
		);
	}

	/// <summary>
	/// Writes an object-creation expression from a structured type reference and argument expressions.
	/// </summary>
	/// <param name="reference">The type to instantiate.</param>
	/// <param name="arguments">The constructor argument expressions.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Assignment("var hostKit", expression =&gt; expression.New(
	/// 	PurviewTypeLibrary.HostKit, "onBuilt", "onConfigured"));</code></example>
	public CodeWriter New(TypeReference reference, params string[] arguments)
	{
		if (reference.IsNullOrEmpty())
			throw new ArgumentException("Object-creation type cannot be empty.", nameof(reference));

		// The structured CodeWriter API does not yet support preprocessor directives other than #if/#else/#endif and
		return NewCore(reference, type: null, arguments, writeArgumentsOnSeparateLines: false);
	}

	/// <summary>
	/// Writes an object-creation expression from a structured type reference and structured argument
	/// declarations.
	/// </summary>
	/// <param name="reference">The type to instantiate.</param>
	/// <param name="arguments">The structured constructor arguments, preserving <c>ref</c>, <c>out</c>, and <c>in</c> modifiers and named arguments.</param>
	/// <param name="writeArgumentsOnSeparateLines">Whether to force one argument per line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Assignment("var handler", expression =&gt; expression.New(
	/// 	PurviewTypeLibrary.HostKit,
	/// 	[new("onBuilt", ParameterModifier.In)]));</code></example>
	public CodeWriter New(
		TypeReference reference,
		IEnumerable<MethodCallArgumentOptions> arguments,
		bool writeArgumentsOnSeparateLines = false
	)
	{
		if (reference.IsNullOrEmpty())
			throw new ArgumentException("Object-creation type cannot be empty.", nameof(reference));
		if (arguments is null)
			throw new ArgumentNullException(nameof(arguments));

		// The structured CodeWriter API does not yet support preprocessor directives other than #if/#else/#endif and
		return NewCore(
			reference,
			type: null,
			arguments.Select(static argument => RenderCallArgument(argument)),
			writeArgumentsOnSeparateLines
		);
	}

	/// <summary>
	/// Writes a target-typed object-creation expression from argument expressions, such as
	/// <c>new(a, b)</c>. The expression is written without a trailing semicolon so it composes as the
	/// value of an <see cref="Assignment(string, Action{CodeWriter})"/>,
	/// <see cref="Return(Action{CodeWriter})"/>, or other expression callback, and is valid only where
	/// the target type is known.
	/// </summary>
	/// <param name="arguments">The constructor argument expressions.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Assignment("HostKit hostKit", expression =&gt; expression.New(["onBuilt", "onConfigured"]));
	/// // HostKit hostKit = new(onBuilt, onConfigured);</code></example>
	public CodeWriter New(IEnumerable<string> arguments) =>
		NewCore(
			reference: null,
			type: null,
			arguments ?? throw new ArgumentNullException(nameof(arguments)),
			writeArgumentsOnSeparateLines: false
		);

	/// <summary>
	/// Writes a target-typed object-creation expression from structured argument declarations.
	/// </summary>
	/// <param name="arguments">The structured constructor arguments, preserving <c>ref</c>, <c>out</c>, and <c>in</c> modifiers and named arguments.</param>
	/// <param name="writeArgumentsOnSeparateLines">Whether to force one argument per line.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Assignment("HostKit hostKit", expression =&gt; expression.New(
	/// 	[new("onBuilt", ParameterModifier.In)]));
	/// // HostKit hostKit = new(in onBuilt);</code></example>
	public CodeWriter New(
		IEnumerable<MethodCallArgumentOptions> arguments,
		bool writeArgumentsOnSeparateLines = false
	) =>
		NewCore(
			reference: null,
			type: null,
			(arguments ?? throw new ArgumentNullException(nameof(arguments))).Select(static argument =>
				RenderCallArgument(argument)
			),
			writeArgumentsOnSeparateLines
		);

	CodeWriter NewCore(
		TypeReference? reference,
		string? type,
		IEnumerable<string?> arguments,
		bool writeArgumentsOnSeparateLines
	)
	{
		Write("new");
		if (reference is not null)
			Write(' ').TypeReference(reference);
		else if (type is not null)
			Write(' ').Write(type);

		Write('(');
		MethodCallArguments([.. arguments], writeArgumentsOnSeparateLines, multilineClosingSuffix: string.Empty);

		return this;
	}
}
