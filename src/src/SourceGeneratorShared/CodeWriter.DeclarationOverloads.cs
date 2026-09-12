namespace Purview.SourceGeneratorFramework;

partial class CodeWriter
{
	// ---------------------------------------------------------------------------------------------
	// Methods
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes a structured method declaration using the minimal identifying properties and returns its
	/// body scope.
	/// </summary>
	/// <param name="name">The method name.</param>
	/// <param name="returnType">The return type, or <see langword="null"/> for <c>void</c>.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The method body scope.</returns>
	/// <example><code>using (writer.MethodScope("Run")) writer.Line("return;");</code></example>
	public BlockScope MethodScope(
		string name,
		TypeReference? returnType = null,
		TypeDeclarationAccessibility? accessibility = null,
		Func<MethodDeclarationOptions, MethodDeclarationOptions>? configure = null
	)
	{
		MethodDeclarationOptions declaration = new(name, returnType ?? PurviewTypeLibrary.System.Void, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return MethodScope(declaration);
	}

	/// <summary>
	/// Writes a structured method using the minimal identifying properties and invokes a callback for
	/// its body.
	/// </summary>
	/// <param name="name">The method name.</param>
	/// <param name="returnType">The return type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the method body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Method("Run", PurviewTypeLibrary.System.Void, TypeDeclarationAccessibility.Public, null, body =&gt; body.Line("return;"));</code></example>
	public CodeWriter Method(
		string name,
		TypeReference returnType,
		TypeDeclarationAccessibility? accessibility,
		Func<MethodDeclarationOptions, MethodDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		MethodDeclarationOptions declaration = new(name, returnType, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Method(declaration, writeBody);
	}

	/// <summary>
	/// Writes a structured partial method using the minimal identifying properties.
	/// </summary>
	/// <param name="name">The method name.</param>
	/// <param name="returnType">The return type, or <see langword="null"/> for <c>void</c>.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.PartialMethod("OnChanged");</code></example>
	public CodeWriter PartialMethod(
		string name,
		TypeReference? returnType = null,
		TypeDeclarationAccessibility? accessibility = null,
		Func<MethodDeclarationOptions, MethodDeclarationOptions>? configure = null
	)
	{
		MethodDeclarationOptions declaration = new(name, returnType ?? PurviewTypeLibrary.System.Void, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return PartialMethod(declaration);
	}

	/// <summary>
	/// Writes an expression-bodied method using the minimal identifying properties and an expression body.
	/// </summary>
	/// <param name="name">The method name.</param>
	/// <param name="returnType">The return type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="expressionBody">The expression body without the leading <c>=&gt;</c>.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MethodExpression("Count", Type("int"), TypeDeclarationAccessibility.Public, "items.Count");</code></example>
	public CodeWriter MethodExpression(
		string name,
		TypeReference returnType,
		TypeDeclarationAccessibility? accessibility,
		string expressionBody,
		Func<MethodDeclarationOptions, MethodDeclarationOptions>? configure = null
	)
	{
		if (string.IsNullOrWhiteSpace(expressionBody))
		{
			throw new ArgumentException(
				"An expression-bodied method must have a non-empty expression body.",
				nameof(expressionBody)
			);
		}

		MethodDeclarationOptions declaration = new(name, returnType ?? PurviewTypeLibrary.System.Void, accessibility)
		{
			ExpressionBody = expressionBody,
		};
		if (configure is not null)
			declaration = configure(declaration);

		return MethodExpression(declaration);
	}

	/// <summary>
	/// Writes an expression-bodied method using the minimal identifying properties and a callback for
	/// the expression.
	/// </summary>
	/// <param name="name">The method name.</param>
	/// <param name="returnType">The return type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="writeExpression">The action that writes the expression.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.MethodExpression("Count", Type("int"), TypeDeclarationAccessibility.Public, expression =&gt; expression.Write("items.Count"));</code></example>
	public CodeWriter MethodExpression(
		string name,
		TypeReference returnType,
		TypeDeclarationAccessibility? accessibility,
		Action<CodeWriter> writeExpression,
		Func<MethodDeclarationOptions, MethodDeclarationOptions>? configure = null
	)
	{
		if (writeExpression is null)
			throw new ArgumentNullException(nameof(writeExpression));

		MethodDeclarationOptions declaration = new(name, returnType ?? PurviewTypeLibrary.System.Void, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return MethodExpression(declaration, writeExpression);
	}

	// ---------------------------------------------------------------------------------------------
	// Operators
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes a structured operator declaration using the minimal identifying properties and returns its
	/// body scope.
	/// </summary>
	/// <param name="operatorToken">The operator token, such as <c>==</c>; ignored for conversion operators.</param>
	/// <param name="returnType">The operator return type.</param>
	/// <param name="left">The left operand, or the single source parameter for unary and conversion operators.</param>
	/// <param name="right">The right operand, or <see langword="default"/> for unary and conversion operators.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, such as setting <see cref="OperatorDeclarationOptions.Kind"/>.</param>
	/// <returns>The operator body scope.</returns>
	/// <example><code>using (writer.OperatorScope("==", Type("bool"), left, right, TypeDeclarationAccessibility.Public)) { }</code></example>
	public BlockScope OperatorScope(
		string operatorToken,
		TypeReference returnType,
		ParameterDeclarationOptions left,
		ParameterDeclarationOptions right,
		TypeDeclarationAccessibility? accessibility,
		Func<OperatorDeclarationOptions, OperatorDeclarationOptions>? configure = null
	)
	{
		if (returnType is null)
			throw new ArgumentNullException(nameof(returnType));

		OperatorDeclarationOptions declaration = new(operatorToken, returnType, left, right, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return OperatorScope(declaration);
	}

	/// <summary>
	/// Writes a structured operator declaration using the minimal identifying properties and invokes a
	/// callback for its body.
	/// </summary>
	/// <param name="operatorToken">The operator token, such as <c>==</c>; ignored for conversion operators.</param>
	/// <param name="returnType">The operator return type.</param>
	/// <param name="left">The left operand, or the single source parameter for unary and conversion operators.</param>
	/// <param name="right">The right operand, or <see langword="default"/> for unary and conversion operators.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, such as setting <see cref="OperatorDeclarationOptions.Kind"/>, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the operator body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Operator("==", Type("bool"), left, right, TypeDeclarationAccessibility.Public, null, body =&gt; body.Return("left.Equals(right)"));</code></example>
	public CodeWriter Operator(
		string operatorToken,
		TypeReference returnType,
		ParameterDeclarationOptions left,
		ParameterDeclarationOptions right,
		TypeDeclarationAccessibility? accessibility,
		Func<OperatorDeclarationOptions, OperatorDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));
		if (returnType is null)
			throw new ArgumentNullException(nameof(returnType));

		OperatorDeclarationOptions declaration = new(operatorToken, returnType, left, right, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Operator(declaration, writeBody);
	}

	// ---------------------------------------------------------------------------------------------
	// Properties
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes an auto-property or expression-bodied property using the minimal identifying properties.
	/// </summary>
	/// <param name="name">The property name.</param>
	/// <param name="type">The property type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Property("Name", Type("string"));</code></example>
	public CodeWriter Property(
		string name,
		TypeReference type,
		TypeDeclarationAccessibility? accessibility = null,
		Func<PropertyDeclarationOptions, PropertyDeclarationOptions>? configure = null
	)
	{
		PropertyDeclarationOptions declaration = new(name, type, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Property(declaration);
	}

	/// <summary>
	/// Writes a property with callback-generated accessor bodies using the minimal identifying
	/// properties.
	/// </summary>
	/// <param name="name">The property name.</param>
	/// <param name="type">The property type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="writeGetterBody">The action that writes the getter body, or <see langword="null"/> for an auto getter.</param>
	/// <param name="writeSetterBody">The action that writes the setter body, or <see langword="null"/> for an auto setter.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Property("Value", Type("int"), TypeDeclarationAccessibility.Public, getter =&gt; getter.Line("return _value;"), null);</code></example>
	public CodeWriter Property(
		string name,
		TypeReference type,
		TypeDeclarationAccessibility? accessibility,
		Action<CodeWriter>? writeGetterBody,
		Action<CodeWriter>? writeSetterBody,
		Func<PropertyDeclarationOptions, PropertyDeclarationOptions>? configure = null
	)
	{
		PropertyDeclarationOptions declaration = new(name, type, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Property(declaration, writeGetterBody, writeSetterBody);
	}

	/// <summary>
	/// Writes an expression-bodied property using the minimal identifying properties and a callback for
	/// the expression.
	/// </summary>
	/// <param name="name">The property name.</param>
	/// <param name="type">The property type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="writeExpression">The action that writes the expression.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.PropertyExpression("Count", Type("int"), TypeDeclarationAccessibility.Public, expression =&gt; expression.Write("items.Count"));</code></example>
	public CodeWriter PropertyExpression(
		string name,
		TypeReference type,
		TypeDeclarationAccessibility? accessibility,
		Action<CodeWriter> writeExpression,
		Func<PropertyDeclarationOptions, PropertyDeclarationOptions>? configure = null
	)
	{
		if (writeExpression is null)
			throw new ArgumentNullException(nameof(writeExpression));

		PropertyDeclarationOptions declaration = new(name, type, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return PropertyExpression(declaration, writeExpression);
	}

	// ---------------------------------------------------------------------------------------------
	// Indexers
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes an indexer declaration with auto accessors or an expression body using the minimal
	/// identifying properties.
	/// </summary>
	/// <param name="type">The indexer element type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="parameters">The indexer parameters.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Indexer(Type("string"), TypeDeclarationAccessibility.Public, new("index", Type("int")));</code></example>
	public CodeWriter Indexer(
		TypeReference type,
		TypeDeclarationAccessibility? accessibility = null,
		IEnumerable<ParameterDeclarationOptions>? parameters = null,
		Func<IndexerDeclarationOptions, IndexerDeclarationOptions>? configure = null
	)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		IndexerDeclarationOptions declaration = new(type, parameters is null ? [] : [.. parameters]);
		if (accessibility is not null)
			declaration = declaration with { Accessibility = accessibility };
		if (configure is not null)
			declaration = configure(declaration);

		return Indexer(declaration);
	}

	/// <summary>
	/// Writes an indexer with callback-generated accessor bodies using the minimal identifying
	/// properties.
	/// </summary>
	/// <param name="type">The indexer element type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="parameters">The indexer parameters.</param>
	/// <param name="writeGetterBody">The action that writes the getter body, or <see langword="null"/> for an auto getter.</param>
	/// <param name="writeSetterBody">The action that writes the setter body, or <see langword="null"/> for an auto setter.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Indexer(Type("string"), TypeDeclarationAccessibility.Public, [new("index", Type("int"))], getter =&gt; getter.Line("return _items[index];"), null);</code></example>
	public CodeWriter Indexer(
		TypeReference type,
		TypeDeclarationAccessibility? accessibility,
		IEnumerable<ParameterDeclarationOptions>? parameters,
		Action<CodeWriter>? writeGetterBody,
		Action<CodeWriter>? writeSetterBody,
		Func<IndexerDeclarationOptions, IndexerDeclarationOptions>? configure = null
	)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		IndexerDeclarationOptions declaration = new(type, parameters is null ? [] : [.. parameters]);
		if (accessibility is not null)
			declaration = declaration with { Accessibility = accessibility };
		if (configure is not null)
			declaration = configure(declaration);

		return Indexer(declaration, writeGetterBody, writeSetterBody);
	}

	// ---------------------------------------------------------------------------------------------
	// Fields
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes a field declaration using the minimal identifying properties.
	/// </summary>
	/// <param name="name">The field name.</param>
	/// <param name="type">The field type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Field("_value", Type("int"));</code></example>
	public CodeWriter Field(
		string name,
		TypeReference type,
		TypeDeclarationAccessibility? accessibility = null,
		Func<FieldDeclarationOptions, FieldDeclarationOptions>? configure = null
	)
	{
		FieldDeclarationOptions declaration = new(name, type, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Field(declaration);
	}

	// ---------------------------------------------------------------------------------------------
	// Constructors
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes a structured constructor declaration using the minimal identifying properties and returns
	/// its body scope.
	/// </summary>
	/// <param name="name">The name of the containing type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The constructor body scope.</returns>
	/// <example><code>using (writer.ConstructorScope("C")) writer.Line("// body");</code></example>
	public BlockScope ConstructorScope(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<ConstructorDeclarationOptions, ConstructorDeclarationOptions>? configure = null
	)
	{
		ConstructorDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return ConstructorScope(declaration);
	}

	/// <summary>
	/// Writes a structured constructor using the minimal identifying properties and invokes a callback
	/// for its body.
	/// </summary>
	/// <param name="name">The name of the containing type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the constructor body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Constructor("C", TypeDeclarationAccessibility.Public, null, _ =&gt; { });</code></example>
	public CodeWriter Constructor(
		string name,
		TypeDeclarationAccessibility? accessibility,
		Func<ConstructorDeclarationOptions, ConstructorDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		ConstructorDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Constructor(declaration, writeBody);
	}

	// ---------------------------------------------------------------------------------------------
	// Types
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes a class declaration using the minimal identifying properties and returns its body scope.
	/// </summary>
	/// <param name="name">The class name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The class body scope.</returns>
	/// <example><code>using (writer.ClassScope("C")) writer.Line("// body");</code></example>
	public BlockScope ClassScope(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return ClassScope(declaration);
	}

	/// <summary>
	/// Writes an empty class declaration using the minimal identifying properties, terminated with a
	/// semicolon instead of a body.
	/// </summary>
	/// <param name="name">The class name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Class("C"); // public sealed partial class C;</code></example>
	public CodeWriter Class(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Class(declaration);
	}

	/// <summary>
	/// Writes a class declaration using the minimal identifying properties and invokes a callback for
	/// its body.
	/// </summary>
	/// <param name="name">The class name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the class body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Class("C", TypeDeclarationAccessibility.Public, null, _ =&gt; { });</code></example>
	public CodeWriter Class(
		string name,
		TypeDeclarationAccessibility? accessibility,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Class(declaration, writeBody);
	}

	/// <summary>
	/// Writes a struct declaration using the minimal identifying properties and returns its body scope.
	/// </summary>
	/// <param name="name">The struct name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The struct body scope.</returns>
	/// <example><code>using (writer.StructScope("Value")) { }</code></example>
	public BlockScope StructScope(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return StructScope(declaration);
	}

	/// <summary>
	/// Writes an empty struct declaration using the minimal identifying properties, terminated with a
	/// semicolon instead of a body.
	/// </summary>
	/// <param name="name">The struct name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Struct("Value"); // public sealed partial struct Value;</code></example>
	public CodeWriter Struct(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Struct(declaration);
	}

	/// <summary>
	/// Writes a struct declaration using the minimal identifying properties and invokes a callback for
	/// its body.
	/// </summary>
	/// <param name="name">The struct name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the struct body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Struct("Value", TypeDeclarationAccessibility.Public, null, _ =&gt; { });</code></example>
	public CodeWriter Struct(
		string name,
		TypeDeclarationAccessibility? accessibility,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Struct(declaration, writeBody);
	}

	/// <summary>
	/// Writes a record class declaration using the minimal identifying properties and returns its body
	/// scope.
	/// </summary>
	/// <param name="name">The record class name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The record body scope.</returns>
	/// <example><code>using (writer.RecordClassScope("Model")) { }</code></example>
	public BlockScope RecordClassScope(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return RecordClassScope(declaration);
	}

	/// <summary>
	/// Writes an empty record class declaration using the minimal identifying properties, terminated with a
	/// semicolon instead of a body.
	/// </summary>
	/// <param name="name">The record class name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.RecordClass("Model"); // public sealed partial record Model;</code></example>
	public CodeWriter RecordClass(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return RecordClass(declaration);
	}

	/// <summary>
	/// Writes a record class declaration using the minimal identifying properties and invokes a callback
	/// for its body.
	/// </summary>
	/// <param name="name">The record class name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the record body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.RecordClass("Model", TypeDeclarationAccessibility.Public, null, _ =&gt; { });</code></example>
	public CodeWriter RecordClass(
		string name,
		TypeDeclarationAccessibility? accessibility,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return RecordClass(declaration, writeBody);
	}

	/// <summary>
	/// Writes a record struct declaration using the minimal identifying properties and returns its body
	/// scope.
	/// </summary>
	/// <param name="name">The record struct name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The record body scope.</returns>
	/// <example><code>using (writer.RecordStructScope("Value")) { }</code></example>
	public BlockScope RecordStructScope(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return RecordStructScope(declaration);
	}

	/// <summary>
	/// Writes an empty record struct declaration using the minimal identifying properties, terminated with a
	/// semicolon instead of a body.
	/// </summary>
	/// <param name="name">The record struct name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.RecordStruct("Value"); // public readonly partial record struct Value;</code></example>
	public CodeWriter RecordStruct(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return RecordStruct(declaration);
	}

	/// <summary>
	/// Writes a record struct declaration using the minimal identifying properties and invokes a callback
	/// for its body.
	/// </summary>
	/// <param name="name">The record struct name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the record body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.RecordStruct("Value", TypeDeclarationAccessibility.Public, null, _ =&gt; { });</code></example>
	public CodeWriter RecordStruct(
		string name,
		TypeDeclarationAccessibility? accessibility,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return RecordStruct(declaration, writeBody);
	}

	/// <summary>
	/// Writes an interface declaration using the minimal identifying properties and returns its body
	/// scope.
	/// </summary>
	/// <param name="name">The interface name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The interface body scope.</returns>
	/// <example><code>using (writer.InterfaceScope("IService")) { }</code></example>
	public BlockScope InterfaceScope(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return InterfaceScope(declaration);
	}

	/// <summary>
	/// Writes an empty interface declaration using the minimal identifying properties, terminated with a
	/// semicolon instead of a body.
	/// </summary>
	/// <param name="name">The interface name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Interface("IService"); // public partial interface IService;</code></example>
	public CodeWriter Interface(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Interface(declaration);
	}

	/// <summary>
	/// Writes an interface declaration using the minimal identifying properties and invokes a callback
	/// for its body.
	/// </summary>
	/// <param name="name">The interface name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the interface body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Interface("IService", TypeDeclarationAccessibility.Public, null, _ =&gt; { });</code></example>
	public CodeWriter Interface(
		string name,
		TypeDeclarationAccessibility? accessibility,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Interface(declaration, writeBody);
	}

	/// <summary>
	/// Writes an enum declaration using the minimal identifying properties and returns its body scope.
	/// </summary>
	/// <param name="name">The enum name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The enum body scope.</returns>
	/// <example><code>using (writer.EnumScope("Status")) { }</code></example>
	public BlockScope EnumScope(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return EnumScope(declaration);
	}

	/// <summary>
	/// Writes an enum declaration using the minimal identifying properties and invokes a callback for
	/// its body.
	/// </summary>
	/// <param name="name">The enum name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the enum body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Enum("Status", TypeDeclarationAccessibility.Public, null, _ =&gt; { });</code></example>
	public CodeWriter Enum(
		string name,
		TypeDeclarationAccessibility? accessibility,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Enum(declaration, writeBody);
	}

	/// <summary>
	/// Writes an enum declaration using the minimal identifying properties and structured field
	/// declarations.
	/// </summary>
	/// <param name="name">The enum name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="fields">The fields to write in declaration order.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Enum("Status", fields: [new("Ready", 1)]);</code></example>
	public CodeWriter Enum(
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		IEnumerable<EnumFieldDeclarationOptions>? fields = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		if (fields is null)
			return Enum(declaration, static _ => { });

		// The fields are passed as a list to avoid multiple enumerations of the enumerable.
		return Enum(declaration, [.. fields]);
	}

	/// <summary>
	/// Writes a structured type declaration using the minimal identifying properties and returns its
	/// body scope when the declaration has one.
	/// </summary>
	/// <param name="kind">The type declaration kind.</param>
	/// <param name="name">The generated type name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The generated type body scope.</returns>
	/// <example><code>using (writer.TypeScope(TypeDeclarationKind.Interface, "IService")) { }</code></example>
	public BlockScope TypeScope(
		TypeDeclarationKind kind,
		string name,
		TypeDeclarationAccessibility? accessibility = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return TypeScope(declaration with { Kind = kind });
	}

	/// <summary>
	/// Writes a structured type declaration using the minimal identifying properties and invokes a
	/// callback for its body.
	/// </summary>
	/// <param name="kind">The type declaration kind.</param>
	/// <param name="name">The generated type name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="configure">An optional callback that configures the declaration, or <see langword="null"/> for defaults.</param>
	/// <param name="writeBody">The action that writes the type body.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Type(TypeDeclarationKind.Interface, "IService", TypeDeclarationAccessibility.Public, null, _ =&gt; { });</code></example>
	public CodeWriter Type(
		TypeDeclarationKind kind,
		string name,
		TypeDeclarationAccessibility? accessibility,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure,
		Action<CodeWriter> writeBody
	)
	{
		if (writeBody is null)
			throw new ArgumentNullException(nameof(writeBody));

		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return Type(declaration with { Kind = kind }, writeBody);
	}

	/// <summary>
	/// Writes an attribute class with an <see cref="AttributeUsageAttribute"/> declaration using the
	/// minimal identifying properties.
	/// </summary>
	/// <param name="name">The attribute class name.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="targets">The declarations on which the generated attribute may be applied.</param>
	/// <param name="bodyWriter">The action that writes the body of the attribute class.</param>
	/// <param name="inherited">Whether derived classes and overriding members inherit the attribute.</param>
	/// <param name="allowMultiple">Whether more than one instance may be specified on one declaration.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.AttributeClass("MarkerAttribute", TypeDeclarationAccessibility.Public, AttributeTargets.Class, _ =&gt; { });</code></example>
	public CodeWriter AttributeClass(
		string name,
		TypeDeclarationAccessibility? accessibility,
		AttributeTargets targets,
		Action<CodeWriter> bodyWriter,
		bool inherited = false,
		bool allowMultiple = false,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		if (bodyWriter is null)
			throw new ArgumentNullException(nameof(bodyWriter));

		TypeDeclarationOptions declaration = new(name, accessibility);
		if (configure is not null)
			declaration = configure(declaration);

		return AttributeClass(declaration, targets, bodyWriter, inherited, allowMultiple);
	}

	/// <summary>
	/// Writes a complete delegate declaration using the minimal identifying properties.
	/// </summary>
	/// <param name="name">The delegate name.</param>
	/// <param name="delegateReturnType">The delegate return type.</param>
	/// <param name="accessibility">The optional accessibility.</param>
	/// <param name="parameters">The delegate parameters.</param>
	/// <param name="configure">An optional callback that configures the declaration.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.Delegate("Handler", Type("void"), TypeDeclarationAccessibility.Public, [new("value", Type("int"))]);</code></example>
	public CodeWriter Delegate(
		string name,
		TypeReference delegateReturnType,
		TypeDeclarationAccessibility? accessibility = null,
		IEnumerable<ParameterDeclarationOptions>? parameters = null,
		Func<TypeDeclarationOptions, TypeDeclarationOptions>? configure = null
	)
	{
		if (delegateReturnType is null)
			throw new ArgumentNullException(nameof(delegateReturnType));

		TypeDeclarationOptions declaration = new(name, accessibility)
		{
			DelegateReturnType = delegateReturnType,
			DelegateParameters = parameters is null ? [] : [.. parameters],
		};
		if (configure is not null)
			declaration = configure(declaration);

		return Delegate(declaration);
	}

	/// <summary>
	/// Writes a field in an enum declaration using the minimal identifying properties. Consecutive
	/// fields are separated by a blank line, so XML summaries remain readable.
	/// </summary>
	/// <param name="fieldName">The enum field name.</param>
	/// <param name="fieldValue">
	/// The enum field value. Strings are emitted as C# expressions; other values are formatted using the
	/// invariant culture.
	/// </param>
	/// <param name="xmlSummary">The lines written in the field's XML <c>summary</c> block.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.EnumField("Ready", 1);</code></example>
	public CodeWriter EnumField(string fieldName, object fieldValue, params string[] xmlSummary)
	{
		if (fieldValue is null)
			throw new ArgumentNullException(nameof(fieldValue));

		// The field value is passed as an object to allow the caller to pass a string, int, or other type. The
		return EnumField(new(fieldName, fieldValue, xmlSummary));
	}

	// ---------------------------------------------------------------------------------------------
	// Statements
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes a return statement that formats an interpolated string using the invariant culture through
	/// the best API available on the target framework, guarded by a conditional-compilation block.
	/// </summary>
	/// <param name="interpolatedMessage">
	/// The interpolated message written between the <c>$"</c> and closing quote, such as
	/// <c>Argument '{value}' is required</c>.
	/// </param>
	/// <param name="symbol">The preprocessor symbol that selects the modern API, defaulting to <c>NET</c>.</param>
	/// <returns>The current writer.</returns>
	/// <example><code>writer.NetConditionalReturn("Argument '{value}' is required");</code></example>
	public CodeWriter NetConditionalReturn(string interpolatedMessage, string symbol = "NET")
	{
		if (string.IsNullOrWhiteSpace(interpolatedMessage))
		{
			throw new ArgumentException(
				"The interpolated message cannot be null or whitespace.",
				nameof(interpolatedMessage)
			);
		}

		if (string.IsNullOrWhiteSpace(symbol))
			throw new ArgumentException("The preprocessor symbol cannot be null or whitespace.", nameof(symbol));

		Line("#if " + symbol);
		Write("return string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $\"")
			.Write(interpolatedMessage)
			.Line("\");");
		Line("#else");
		Write("return global::System.FormattableString.Invariant($\"").Write(interpolatedMessage).Line("\");");
		Line("#endif");
		return this;
	}
}
