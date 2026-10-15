using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

using JetBrains.Annotations;

using SpringExpressions.Parser;
using SpringExpressions.Parser.antlr;

namespace SpringExpressions
{
    /// <summary>
    /// Prints a parsed tree, for a person (text) or a program (XML) - what <c>Expression.DescribeTree</c>
    /// and <c>Expression.DescribeTreeAsXml</c> return.
    /// </summary>
    /// <remarks>
    /// <p>
    /// A walker of its own rather than antlr's <c>BaseAST.ToTree</c> and <c>xmlSerialize</c>, for three
    /// measured reasons. <c>ToTree</c> prints <see cref="BaseNode.ToString"/>, which is the class name and
    /// a hash code: no member names, no literal values, no cast target, and an output that differs per
    /// parse. <c>xmlSerialize</c> writes the generated parser's token number, which shifts whenever the
    /// grammar does. And both print the node's following siblings too, which for a node taken out of a
    /// larger tree and handed to <c>Expression.Wrap</c> means nodes that are not part of it - and, in XML,
    /// several root elements.
    /// </p>
    /// <p>
    /// The tree is printed as it is. The single-child <see cref="Expression"/> the grammar wraps round
    /// nearly every operand is a real node that evaluates and emits, and hiding it would print a tree
    /// other than the one that runs.
    /// </p>
    /// </remarks>
    internal static class TreeDescription
    {
        /// <summary>
        /// The texts <c>Expression.g</c> gives the imaginary nodes it builds with
        /// <c>#([EXPR, "&lt;text&gt;", …])</c>, keyed on the node type each one builds.
        /// </summary>
        /// <remarks>
        /// Made up by the grammar rather than written by the caller, so printing them in quotes would pass
        /// them off as source. Keyed on the node type, not the text alone: an imaginary node that carries a
        /// source text - a cast's target type, a declared local's name, <c>T(…)</c>'s type name - keeps it,
        /// even if a caller names a local <c>$expression</c>.
        /// </remarks>
        private static readonly Dictionary<Type, string> GrammarMadeTexts = new Dictionary<Type, string>
        {
            { typeof(Expression), "expression" },
            { typeof(ExpressionListNode), "expressionList" },
            { typeof(LambdaExpressionNode), "lambda" },
            { typeof(SpringAST), "args" },
            { typeof(MapEntryNode), "entry" },
            { typeof(SpringContext.Support.ReferenceNode), "ref" },
        };

        [NotNull]
        internal static string ToText([NotNull] BaseNode root)
        {
            var text = new StringBuilder();
            AppendLine(text, root);
            AppendChildren(text, root, string.Empty);

            return text.ToString();
        }

        [NotNull]
        internal static string ToXml([NotNull] BaseNode root)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
                NewLineChars = Environment.NewLine,
            };

            using (var stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                {
                    WriteElement(xmlWriter, root);
                }

                return stringWriter.ToString();
            }
        }

        private static void AppendChildren([NotNull] StringBuilder text, [NotNull] BaseAST parent, [NotNull] string indent)
        {
            for (var child = (BaseAST) parent.getFirstChild(); child != null; child = (BaseAST) child.getNextSibling())
            {
                var isLast = child.getNextSibling() == null;

                text.Append(Environment.NewLine).Append(indent).Append(isLast ? "+--" : "|--");
                AppendLine(text, child);
                AppendChildren(text, child, indent + (isLast ? "   " : "|  "));
            }
        }

        private static void AppendLine([NotNull] StringBuilder text, [NotNull] BaseAST node)
        {
            text.Append(node.GetType().Name);

            var sourceText = SourceTextOf(node);
            if (sourceText != null)
                text.Append(" '").Append(EscapeForText(sourceText)).Append('\'');

            if (IsNullConditional(node))
                text.Append(node is IndexerNode ? " ?[" : " ?.");
        }

        private static void WriteElement([NotNull] XmlWriter xmlWriter, [NotNull] BaseAST node)
        {
            xmlWriter.WriteStartElement(XmlConvert.EncodeLocalName(node.GetType().Name));

            var sourceText = SourceTextOf(node);
            if (sourceText != null)
                xmlWriter.WriteAttributeString("text", EscapeForXml(sourceText));

            if (IsNullConditional(node))
                xmlWriter.WriteAttributeString("nullConditional", "true");

            for (var child = (BaseAST) node.getFirstChild(); child != null; child = (BaseAST) child.getNextSibling())
                WriteElement(xmlWriter, child);

            xmlWriter.WriteEndElement();
        }

        /// <summary>
        /// The node's text where the caller wrote it; null for a node the grammar made up, and for a node
        /// built by hand, which has none.
        /// </summary>
        [CanBeNull]
        private static string SourceTextOf([NotNull] BaseAST node)
        {
            var text = node.getText();

            if (string.IsNullOrEmpty(text))
                return null;

            if (node.Type == ExpressionParser.EXPR
                && GrammarMadeTexts.TryGetValue(node.GetType(), out var madeUp)
                && madeUp == text)
                return null;

            return text;
        }

        private static bool IsNullConditional([NotNull] BaseAST node)
        {
            return node is SpringAST springAst && springAst.IsNullConditional;
        }

        /// <summary>One node per line, so a control character in a literal must not break the line.</summary>
        [NotNull]
        private static string EscapeForText([NotNull] string text)
        {
            var escaped = new StringBuilder(text.Length);

            foreach (var c in text)
            {
                switch (c)
                {
                    case '\\': escaped.Append("\\\\"); break;
                    case '\'': escaped.Append("\\'"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (char.IsControl(c))
                            escaped.Append("\\u").Append(((int) c).ToString("X4", CultureInfo.InvariantCulture));
                        else
                            escaped.Append(c);
                        break;
                }
            }

            return escaped.ToString();
        }

        /// <summary>
        /// The text as it is, except for what XML cannot carry at all - a character such as U+0001, or an
        /// unpaired surrogate - which is written as <c>\uXXXX</c> rather than making the writer throw.
        /// </summary>
        [NotNull]
        private static string EscapeForXml([NotNull] string text)
        {
            var escaped = new StringBuilder(text.Length);

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    escaped.Append(c).Append(text[++i]);
                }
                else if (XmlConvert.IsXmlChar(c))
                {
                    escaped.Append(c);
                }
                else
                {
                    escaped.Append("\\u").Append(((int) c).ToString("X4", CultureInfo.InvariantCulture));
                }
            }

            return escaped.ToString();
        }
    }
}
