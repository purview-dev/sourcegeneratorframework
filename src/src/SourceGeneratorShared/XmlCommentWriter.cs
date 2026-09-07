using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Provides extension methods for writing XML documentation comments to a <see cref="CodeWriter"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Naming",
	"CA1708:Identifiers should differ by more than case",
	Justification = "Instance vs. Static"
)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XmlCommentWriter
{
	extension(CodeWriter writer)
	{
		/// <summary>
		/// Writes one or more XML documentation lines.
		/// </summary>
		/// <param name="comments">The documentation lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlComment(params string[] comments) => XmlCore(writer, null, comments);

		/// <summary>
		/// Writes an XML documentation block with the specified tag and content.
		/// </summary>
		/// <param name="tag">The XML tag name.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		/// <exception cref="ArgumentException">If the <paramref name="tag"/> is <see langword="null"/> or an empty string.</exception>
		public CodeWriter Xml(string tag, params string[] content) =>
			string.IsNullOrWhiteSpace(tag)
				? throw new ArgumentException("The XML tag name cannot be null or empty.", nameof(tag))
				: XmlCore(writer, tag, content);

		/// <summary>
		/// Writes an XML <c>cref</c> documentation block with the specified type name and content.
		/// </summary>
		/// <param name="typeName">The name of the type.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		/// <exception cref="ArgumentException">If the <paramref name="typeName"/> is <see langword="null"/> or an empty string.</exception>
		public CodeWriter XmlCref(string typeName, string content) =>
			string.IsNullOrWhiteSpace(typeName)
				? throw new ArgumentException("The XML cref cannot be null or empty.", nameof(typeName))
				: XmlCore(writer, BuildXmlTag("cref", ("cref", typeName)), "cref", content);

		/// <summary>
		/// Writes an XML <c>&lt;returns&gt;</c> documentation block with the specified content.
		/// </summary>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlReturn(params string[] content) => XmlCore(writer, "returns", content);

		/// <summary>
		/// Writes an XML <c>&lt;value&gt;</c> documentation block.
		/// </summary>
		public CodeWriter XmlValue(params string[] content) => XmlCore(writer, "value", content);

		/// <summary>
		/// Writes an XML <c>&lt;remarks&gt;</c> documentation block.
		/// </summary>
		public CodeWriter XmlRemarks(params string[] content) => XmlCore(writer, "remarks", content);

		/// <summary>
		/// Writes an XML <c>&lt;permission&gt;</c> documentation block.
		/// </summary>
		public CodeWriter XmlPermission(string cref, params string[] content) =>
			string.IsNullOrWhiteSpace(cref)
				? throw new ArgumentException("The XML cref cannot be null or empty.", nameof(cref))
				: XmlCore(writer, BuildXmlTag("permission", ("cref", cref)), "permission", content);

		/// <summary>
		/// Writes a self-closing XML <c>&lt;inheritdoc /&gt;</c> element.
		/// </summary>
		public CodeWriter XmlInheritDoc() => writer.Write("/// ").Line(BuildSelfClosingXmlTag("inheritdoc"));

		/// <summary>
		/// Writes a self-closing XML <c>&lt;inheritdoc /&gt;</c> element for a member.
		/// </summary>
		public CodeWriter XmlInheritDoc(string cref) =>
			string.IsNullOrWhiteSpace(cref)
				? throw new ArgumentException("The XML cref cannot be null or empty.", nameof(cref))
				: writer.Write("/// ").Line(BuildSelfClosingXmlTag("inheritdoc", ("cref", cref)));

		/// <summary>
		/// Writes an XML <c>&lt;returns&gt;</c> documentation block with the specified content.
		/// </summary>
		/// <param name="exceptionType">The type of the exception.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlException(string exceptionType, params string[] content) =>
			string.IsNullOrWhiteSpace(exceptionType)
				? throw new ArgumentException("The XML exception type cannot be null or empty.", nameof(exceptionType))
				: XmlCore(writer, BuildXmlTag("exception", ("cref", exceptionType)), "exception", content);

		/// <summary>
		/// Writes an XML <c>&lt;exception&gt;</c> documentation block with the specified exception type and content.
		/// </summary>
		/// <param name="exceptionType">The exception type.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlException(TypeIdentity exceptionType, params string[] content) =>
			XmlCore(writer, BuildXmlTag("exception", ("cref", ToXmlCref(exceptionType))), "exception", content);

		/// <summary>
		/// Writes an XML <c>&lt;exception&gt;</c> documentation block with the specified exception type and content.
		/// </summary>
		/// <param name="exceptionType">The exception type reference.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="exceptionType"/> is <see langword="null"/>.</exception>
		public CodeWriter XmlException(TypeReference exceptionType, params string[] content) =>
			exceptionType == null
				? throw new ArgumentNullException(nameof(exceptionType))
				: XmlCore(writer, BuildXmlTag("exception", ("cref", ToXmlCref(exceptionType))), "exception", content);

		/// <summary>
		/// Writes an XML <c>cref</c> documentation block with the specified type and content.
		/// </summary>
		/// <param name="typeName">The type.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlCref(TypeIdentity typeName, params string[] content) =>
			XmlCore(writer, BuildXmlTag("cref", ("cref", ToXmlCref(typeName))), "cref", content);

		/// <summary>
		/// Writes an XML <c>cref</c> documentation block with the specified type and content.
		/// </summary>
		/// <param name="typeName">The type reference.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="typeName"/> is <see langword="null"/>.</exception>
		public CodeWriter XmlCref(TypeReference typeName, params string[] content) =>
			typeName == null
				? throw new ArgumentNullException(nameof(typeName))
				: XmlCore(writer, BuildXmlTag("cref", ("cref", ToXmlCref(typeName))), "cref", content);

		/// <summary>
		/// Writes an XML <c>&lt;c&gt;</c> documentation block with the specified content.
		/// </summary>
		/// <param name="content">The content line.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlCode(string content)
		{
			if (string.IsNullOrWhiteSpace(content))
				throw new ArgumentException("The XML code content cannot be null or empty.", nameof(content));

			// Write an XML <c>&lt;c&gt;</c> documentation block with the specified content.
			return XmlCore(writer, "c", "c", [content], supportsMultiLine: false, compactSingleLine: true);
		}

		/// <summary>
		/// Writes an XML <c>&lt;code&gt;</c> documentation block.
		/// </summary>
		public CodeWriter XmlCodeBlock(params string[] content) =>
			XmlCore(writer, "code", "code", content, supportsMultiLine: true, compactSingleLine: false);

		/// <summary>
		/// Writes an XML <c>&lt;example&gt;</c> documentation block with the specified content.
		/// </summary>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlExample(params string[] content) => XmlCore(writer, "example", content);

		/// <summary>
		/// Writes an XML <c>&lt;seealso&gt;</c> documentation element.
		/// </summary>
		public CodeWriter XmlSeeAlso(string cref, params string[] content)
		{
			if (string.IsNullOrWhiteSpace(cref))
				throw new ArgumentException("The XML cref cannot be null or empty.", nameof(cref));

			// If no content is provided, write a self-closing <seealso /> tag. Otherwise, write a <seealso> block with the provided content.
			return content is null || content.Length == 0
				? writer.Write("/// ").Line(BuildSelfClosingXmlTag("seealso", ("cref", cref)))
				: XmlCore(writer, BuildXmlTag("seealso", ("cref", cref)), "seealso", content);
		}

		/// <summary>
		/// Writes an XML <c>&lt;seealso&gt;</c> documentation element for a type.
		/// </summary>
		public CodeWriter XmlSeeAlso(TypeIdentity type, params string[] content)
		{
			var cref = ToXmlCref(type);

			return content is null || content.Length == 0
				? writer.Write("/// ").Line(BuildSelfClosingXmlTag("seealso", ("cref", cref)))
				: XmlCore(writer, BuildXmlTag("seealso", ("cref", cref)), "seealso", content);
		}

		/// <summary>
		/// Writes an XML <c>&lt;seealso&gt;</c> documentation element for a type reference.
		/// </summary>
		/// <exception cref="ArgumentNullException">If <paramref name="type"/> is <see langword="null"/>.</exception>
		public CodeWriter XmlSeeAlso(TypeReference type, params string[] content)
		{
			if (type is null)
				throw new ArgumentNullException(nameof(type));

			var cref = ToXmlCref(type);

			return content is null || content.Length == 0
				? writer.Write("/// ").Line(BuildSelfClosingXmlTag("seealso", ("cref", cref)))
				: XmlCore(writer, BuildXmlTag("seealso", ("cref", cref)), "seealso", content);
		}

		/// <summary>
		/// Writes a self-closing XML <c>&lt;include /&gt;</c> documentation element.
		/// </summary>
		public CodeWriter XmlInclude(string file, string path)
		{
			if (string.IsNullOrWhiteSpace(file))
				throw new ArgumentException("The include file cannot be null or empty.", nameof(file));
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentException("The include path cannot be null or empty.", nameof(path));

			// Write a self-closing <include /> tag with the specified file and path attributes.
			return writer.Write("/// ").Line(BuildSelfClosingXmlTag("include", ("file", file), ("path", path)));
		}

		/// <summary>
		/// Writes an XML <c>&lt;list&gt;</c> documentation block with the specified content.
		/// Defaults to a bullet list type.
		/// </summary>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlList(params string[] content) => XmlList(writer, "bullet", null, content);

		/// <summary>
		/// Writes an XML <c>&lt;list&gt;</c> documentation block with the specified content.
		/// Defaults to a bullet list type.
		/// </summary>
		/// <param name="description">The description of the list.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlList(string description, params string[] content) =>
			XmlList(writer, "bullet", description, content);

		/// <summary>
		/// Writes an XML <c>&lt;list&gt;</c> documentation block with the specified content.
		/// </summary>
		/// <param name="listType">The type of list, such as "bullet" or "number".</param>
		/// <param name="description">Optional description of the list.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlList(string listType, string? description, params string[] content)
		{
			if (string.IsNullOrWhiteSpace(listType))
				throw new ArgumentException("The list type cannot be null or empty.", nameof(listType));

			var startTag = BuildXmlTag("list", ("type", listType));
			if (content is not null)
				content =
				[
					.. content.Select(line =>
						line.StartsWith("<item", StringComparison.Ordinal)
						|| line.StartsWith("<listheader", StringComparison.Ordinal)
							? line
							: "<item>" + line + "</item>"
					),
				];

			return XmlCore(
				writer,
				startTag,
				"list",
				content,
				insideStart: string.IsNullOrWhiteSpace(description) ? null : w => w.Xml("description", description!),
				null,
				true,
				false
			);
		}

		/// <summary>
		/// Writes an XML <c>&lt;para&gt;</c> documentation block with the specified content.
		/// </summary>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlPara(params string[] content) =>
			XmlCore(writer, startTag: "para", endTag: "para", content: content);

		/// <summary>
		/// Writes an XML <c>&lt;param&gt;</c> documentation block with the specified parameter name and content.
		/// </summary>
		/// <param name="parameterName">The name of the parameter.</param>
		/// <param name="content">The content lines.</param>
		/// <returns>The current writer.</returns>
		/// <exception cref="ArgumentException">If the <paramref name="parameterName"/> is <see langword="null"/> or an empty string.</exception>
		public CodeWriter XmlParam(string parameterName, params string[] content)
		{
			if (string.IsNullOrWhiteSpace(parameterName))
				throw new ArgumentException("The parameter name cannot be null or empty.", nameof(parameterName));

			var tag = "param";
			var startTag = BuildXmlTag(tag, ("name", parameterName));

			return XmlCore(writer, startTag, tag, content, compactSingleLine: true);
		}

		/// <summary>
		/// Writes an XML <c>&lt;typeparam&gt;</c> documentation element.
		/// </summary>
		public CodeWriter XmlTypeParam(string typeParameterName, params string[] content)
		{
			if (string.IsNullOrWhiteSpace(typeParameterName))
				throw new ArgumentException(
					"The type parameter name cannot be null or empty.",
					nameof(typeParameterName)
				);

			// Write an XML <c>&lt;typeparam&gt;</c> documentation block with the specified type parameter name and content.
			return XmlCore(
				writer,
				BuildXmlTag("typeparam", ("name", typeParameterName)),
				"typeparam",
				content,
				compactSingleLine: true
			);
		}

		CodeWriter XmlCore(string? startTag, string? endTag, string content) =>
			string.IsNullOrWhiteSpace(content) ? writer : XmlCore(writer, startTag, endTag, [content], false);

		CodeWriter XmlCore(
			string? startTag,
			string? endTag,
			string[]? content,
			bool supportsMultiLine = true,
			bool compactSingleLine = true
		) => XmlCore(writer, startTag, endTag, content, null, null, supportsMultiLine, compactSingleLine);

		CodeWriter XmlCore(
			string? startTag,
			string? endTag,
			string[]? content,
			Action<CodeWriter>? insideStart,
			Action<CodeWriter>? insideEnd,
			bool supportsMultiLine = true,
			bool compactSingleLine = true
		)
		{
			if (content is null || content.Length == 0)
				return writer;

			if (!supportsMultiLine)
			{
				if (content.Length > 1)
					throw new ArgumentException("Multiple lines are not supported for this XML tag.", nameof(content));
			}

			endTag ??= startTag;
			if (
				compactSingleLine
				&& startTag is not null
				&& content.Length == 1
				&& insideStart is null
				&& insideEnd is null
			)
			{
				var renderedStartTag = startTag.StartsWith("<", StringComparison.Ordinal) ? startTag : $"<{startTag}>";
				return writer
					.Write("/// ")
					.Write(renderedStartTag)
					.Write(content[0])
					.Write("</")
					.Write(endTag)
					.Line(">");
			}

			var isMultiLine = startTag is not null;
			if (startTag is not null)
				writer.Write(
					startTag.StartsWith("<", StringComparison.Ordinal) ? $"/// {startTag}" : $"/// <{startTag}>"
				);
			if (isMultiLine)
				writer.NewLine();

			insideStart?.Invoke(writer);

			foreach (var line in content)
				writer.Write("/// ").Line(line);

			if (endTag is not null)
				writer.Write("/// </").Write(endTag).Line(">");

			insideEnd?.Invoke(writer);

			return writer;
		}

		CodeWriter XmlCore(string? tag, params string[] content) =>
			XmlCore(writer, startTag: tag, endTag: tag, content: content);

		/// <summary>
		/// Writes an XML <c>summary</c> documentation block.
		/// </summary>
		/// <param name="summary">The summary lines.</param>
		/// <returns>The current writer.</returns>
		public CodeWriter XmlSummary(params string[] summary) =>
			XmlCore(writer, startTag: "summary", endTag: "summary", content: summary);
	}

	extension(CodeWriter)
	{
		/// <summary>
		/// Builds an XML tag with optional attributes.
		/// </summary>
		/// <param name="tag">The XML tag name.</param>
		/// <param name="attributes">The XML attributes.</param>
		/// <returns>The constructed XML tag.</returns>
		/// <exception cref="ArgumentException">If the <paramref name="tag"/> is <see langword="null"/> or an empty string.	</exception>
		/// <example>
		/// <c>var seeTag = BuildXmlTag("see", ("cref", PurviewTypeLibrary.System.DateTimeOffset));</c>
		/// Would produce: <c>&lt;see cref="System.DateTimeOffset" /&gt;</c>
		/// </example>
		public static string BuildXmlTag(string tag, params (string Name, object Value)[]? attributes)
		{
			if (string.IsNullOrWhiteSpace(tag))
			{
				throw new ArgumentException("The XML tag name cannot be null or empty.", nameof(tag));
			}

			StringBuilder builder = new(tag.Length + 2);
			builder.Append('<').Append(tag);
			if (attributes is not null && attributes.Length > 0)
			{
				foreach (var (name, value) in attributes)
				{
					var attributeValue = value;
					if (value is bool)
					{
#pragma warning disable CA1308 // Normalize strings to uppercase
						attributeValue = value.ToString().ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
					}

					builder
						.Append(' ')
						.Append(name)
						.Append("=\"")
						.Append(EscapeXml(attributeValue.ToString() ?? string.Empty))
						.Append('"');
				}
			}

			builder.Append('>');

			return builder.ToString();
		}

		/// <summary>
		/// Returns an inline XML reference to a type or member.
		/// </summary>
		public static string XmlSee(string cref, string? description = null)
		{
			if (string.IsNullOrWhiteSpace(cref))
				throw new ArgumentException("The XML cref cannot be null or empty.", nameof(cref));

			//	If no description is provided, return a self-closing <see /> tag. Otherwise, return a <see> block with the provided description.
			return string.IsNullOrWhiteSpace(description)
				? BuildSelfClosingXmlTag("see", ("cref", cref))
				: BuildXmlTag("see", ("cref", cref)) + description + "</see>";
		}

		/// <summary>
		/// Returns an inline XML reference to a type.
		/// </summary>
		public static string XmlSee(TypeIdentity type, string? description = null) =>
			XmlSee(ToXmlCref(type), description);

		/// <summary>
		/// Returns an inline XML reference to a type reference.
		/// </summary>
		/// <exception cref="ArgumentNullException">If <paramref name="type"/> is <see langword="null"/>.</exception>
		public static string XmlSee(TypeReference type, string? description = null) =>
			type == null ? throw new ArgumentNullException(nameof(type)) : XmlSee(ToXmlCref(type), description);

		/// <summary>
		/// Returns the XML-documentation <c>cref</c> name for a type, using the brace form for generics that
		/// XML documentation requires (<c>&lt;see cref="global::System.Collections.Generic.List{global::System.String}" /&gt;</c>).
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>The cref-safe name.</returns>
		public static string ToXmlCref(TypeIdentity type)
		{
			if (type == TypeIdentity.Empty)
				return string.Empty;

			if (type.SpecialType != SpecialType.None)
				return type.Keyword!;

			var name = ToXmlCrefTypeName(type, omitAttributeSuffix: false);
			if (type.IsNested)
				name =
					$"{string.Join(".", type.ContainingTypes.Select(static containing => ToXmlCrefContainingName(containing)))}.{name}";

			return type.IsGlobalNamespace ? name : $"global::{type.Namespace}.{name}";
		}

		/// <summary>
		/// Returns the XML-documentation <c>cref</c> name for a type reference, using the brace form for
		/// generics that XML documentation requires. Nullable value types are rendered as
		/// <c>global::System.Nullable{...}</c>; nullable reference annotations are not representable in a
		/// <c>cref</c> and are rendered as their base type.
		/// </summary>
		/// <param name="reference">The type reference.</param>
		/// <returns>The cref-safe name.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="reference"/> is <see langword="null"/>.</exception>
		public static string ToXmlCref(TypeReference reference)
		{
			if (reference is null)
				throw new ArgumentNullException(nameof(reference));
			if (reference.IsEmpty)
				return string.Empty;

#pragma warning disable IDE0072 // Add missing cases
			var core = reference.Kind switch
			{
				TypeReferenceKind.Named => ToXmlCref(reference.Identity),
				TypeReferenceKind.TypeParameter => "{" + (reference.TypeParameterName ?? string.Empty) + "}",
				TypeReferenceKind.Dynamic => "dynamic",
				_ => string.Empty,
			};
#pragma warning restore IDE0072 // Add missing cases

			if (reference.Modifiers.IsDefaultOrEmpty)
				return core;

			StringBuilder builder = new(core);
			var modifiers = reference.Modifiers;
			var index = 0;
			while (index < modifiers.Length)
			{
				var modifier = modifiers[index];
				switch (modifier.Kind)
				{
					case TypeModifierKind.Array:
#pragma warning disable format
					{
						var start = index;
						while (index < modifiers.Length && modifiers[index].Kind == TypeModifierKind.Array)
							index++;

						for (var reverse = index - 1; reverse >= start; reverse--)
							AppendArraySuffix(builder, modifiers[reverse].Rank);

						continue;
					}
#pragma warning restore format

					case TypeModifierKind.Nullable:
						if (modifier.NullableKind == NullableModifierKind.ValueType)
							WrapInNullable(builder);

						break;

					case TypeModifierKind.PointerModifier:
						builder.Append('*');
						break;

					default:
						break;
				}

				index++;
			}

			return builder.ToString();
		}

		static string ToXmlCrefTypeName(TypeIdentity type, bool omitAttributeSuffix)
		{
			var name =
				omitAttributeSuffix && type.IsAttribute
					? type.Name.Substring(0, type.Name.Length - TypeHelpers.AttributeSuffix.Length)
					: type.Name;

			if (type.GenericArity == 0)
				return name;

			// Constructed generics use their actual arguments; an open definition uses readable placeholders
			// because the type parameter names are not retained by the value object.
			var arguments = type.TypeArguments.IsDefaultOrEmpty
				? Enumerable.Range(0, type.GenericArity).Select(static index => $"T{index}")
				: type.TypeArguments.Select(static argument => ToXmlCref(argument));

			return $"{name}{{{string.Join(",", arguments)}}}";
		}

		static string ToXmlCrefContainingName(ContainingType containing) =>
			containing.GenericArity == 0
				? containing.Name
				: $"{containing.Name}{{{string.Join(",", Enumerable.Range(0, containing.GenericArity).Select(static index => $"T{index}"))}}}";

		static void AppendArraySuffix(StringBuilder builder, int rank)
		{
			builder.Append('[');
			if (rank > 1)
				builder.Append(',', rank - 1);
			builder.Append(']');
		}

		static void WrapInNullable(StringBuilder builder)
		{
			var inner = builder.ToString();
			builder.Clear();
			builder.Append("global::System.Nullable{").Append(inner).Append('}');
		}

		/// <summary>
		/// Returns an inline XML &lt;para&gt; element containing the provided content.
		/// </summary>
		public static string XmlInlinePara(params string[] content) => XmlCore("para", content, false);

		/// <summary>
		/// Returns an inline XML reference to a parameter.
		/// </summary>
		public static string XmlParamRef(string parameterName) =>
			BuildSelfClosingXmlTag("paramref", ("name", parameterName));

		/// <summary>
		/// Returns an inline XML reference to a type parameter.
		/// </summary>
		public static string XmlTypeParamRef(string typeParameterName) =>
			BuildSelfClosingXmlTag("typeparamref", ("name", typeParameterName));

		/// <summary>
		/// Returns inline code suitable for use in the middle of documentation text.
		/// </summary>
		public static string XmlInlineCode(string content) => $"<c>{content}</c>";

		/// <summary>
		/// Returns inline code suitable for use in the middle of documentation text.
		/// </summary>
		public static string XmlInlineCodeBlock(params string[] content) => XmlCore("code", content, false);

		/// <summary>
		/// Escapes plain text for safe composition with XML documentation elements.
		/// </summary>
		public static string XmlText(string content) =>
			EscapeXml(content ?? throw new ArgumentNullException(nameof(content)));

		/// <summary>
		/// Returns an XML line break for use inside documentation text.
		/// </summary>
		public static string XmlLineBreak() => "<br />";

		/// <summary>
		/// Returns an XML list item containing a description.
		/// </summary>
		public static string XmlListItem(string description) => $"<item>{description}</item>";

		/// <summary>
		/// Returns an XML list item containing a term and its description.
		/// </summary>
		public static string XmlListItem(string term, string description) =>
			$"<item><term>{term}</term><description>{description}</description></item>";

		/// <summary>
		/// Returns an XML list header containing a term and its description.
		/// </summary>
		public static string XmlListHeader(string term, string description) =>
			$"<listheader><term>{term}</term><description>{description}</description></listheader>";

		/// <summary>
		/// Returns an XML list header containing a term and its description.
		/// </summary>
		public static string XmlListHeader(string description) => $"<listheader>{description}</listheader>";

		/// <summary>
		/// Returns an XML list term.
		/// </summary>
		public static string XmlTerm(string content) => $"<term>{content}</term>";

		/// <summary>
		/// Returns an XML list description.
		/// </summary>
		public static string XmlDescription(string content) => $"<description>{content}</description>";

		/// <summary>
		/// Returns an arbitrary inline XML element.
		/// </summary>
		public static string XmlInlineElement(string tag, string content) => BuildXmlTag(tag) + content + $"</{tag}>";

		/// <summary>
		/// Returns a self-closing XML element with optional attributes.
		/// </summary>
		public static string BuildSelfClosingXmlTag(string tag, params (string Name, object Value)[]? attributes)
		{
			var openTag = BuildXmlTag(tag, attributes);
			return openTag.Remove(openTag.Length - 1) + " />";
		}

		static string XmlCore(string bareStartTag, string[] content, bool allowSingleLine) =>
			XmlCore($"<{bareStartTag}>", $"</{bareStartTag}>", content, allowSingleLine);

		[DebuggerStepThrough]
		static string XmlCore(string tagStart, string tagEnd, string[] content, bool allowSingleLine)
		{
			if (content is null || content.Length == 0)
				throw new ArgumentException("The XML content cannot be null or empty.", nameof(content));

			// If the content is a single line and single-line formatting is allowed, return a compact representation. Otherwise, return a multi-line representation.
			return allowSingleLine && content.Length == 1
				? $"{tagStart}{content[0]}{tagEnd}"
				// Remember the first line with implicitly have  the `///` prefix from the CodeWriter, so
				// it's only line that doesn't need the prefix. All subsequent lines will have the prefix added.
				: $"{tagStart}\n/// {string.Join("\n/// ", content)}\n/// {tagEnd}";
		}

		static string EscapeXml(string value) =>
			value
				.Replace("&", "&amp;")
				.Replace("<", "&lt;")
				.Replace(">", "&gt;")
				.Replace("\"", "&quot;")
				.Replace("'", "&apos;");
	}
}
