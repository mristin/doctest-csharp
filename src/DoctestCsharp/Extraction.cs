using ArgumentException = System.ArgumentException;
using InvalidOperationException = System.InvalidOperationException;
using StringSplitOptions = System.StringSplitOptions;
using String = System.String;
using HashCode = System.HashCode;
using Int32 = System.Int32;
using Regex = System.Text.RegularExpressions.Regex;
using Environment = System.Environment;

using CSharpSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;
using Syntax = Microsoft.CodeAnalysis.CSharp.Syntax;
using TextSpan = Microsoft.CodeAnalysis.Text.TextSpan;
using SyntaxTree = Microsoft.CodeAnalysis.SyntaxTree;
using SyntaxNode = Microsoft.CodeAnalysis.SyntaxNode;
using CompilationUnitSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace DoctestCsharp
{
    public static class Extraction
    {
        public class Error
        {
            public readonly string Message;
            public readonly int Line; // indexed from 0
            public readonly int Column; // indexed from 0

            public Error(string message, int line, int column)
            {
                Message = message;
                Line = line;
                Column = column;
            }

            private bool Equals(Error other)
            {
                return Message == other.Message && Line == other.Line && Column == other.Column;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Error)obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Message, Line, Column);
            }

            public override string ToString()
            {
                return $"{nameof(Message)}: {Message}, {nameof(Line)}: {Line}, {nameof(Column)}: {Column}";
            }
        }

        public static class Pipeline
        {
            public static string[] StripMargin(string[] lines)
            {
                string[] result = new string[lines.Length];

                int minWhitespaceCount = Int32.MaxValue;
                foreach (string line in lines)
                {
                    int whitespaceCount = 0;
                    foreach (char c in line)
                    {
                        if (c == ' ' || c == '\t')
                        {
                            whitespaceCount++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (whitespaceCount < minWhitespaceCount)
                    {
                        minWhitespaceCount = whitespaceCount;
                    }
                }

                for (var i = 0; i < lines.Length; i++)
                {
                    result[i] = lines[i].Substring(minWhitespaceCount);
                }

                // Post-condition
                if (result.Length != lines.Length)
                {
                    throw new InvalidOperationException(
                        $"Expected result.Length == lines.Length (== {lines.Length}, but got: {result.Length}");
                }

                return result;
            }

            private static readonly Regex EmptyLineRe = new Regex(@"^\s*$");

            public static string[] RemoveTopAndBottomPadding(string[] lines)
            {
                if (lines.Length == 0)
                {
                    return new string[0];
                }

                int start = 0;
                for (; start < lines.Length; start++)
                {
                    if (!EmptyLineRe.IsMatch(lines[start])) break;
                }

                int endInclusive = lines.Length - 1;
                for (; endInclusive >= 0; endInclusive--)
                {
                    if (!EmptyLineRe.IsMatch(lines[endInclusive])) break;
                }

                string[] linesWithoutPadding = new string[endInclusive - start + 1];
                System.Array.Copy(
                    lines, start,
                    linesWithoutPadding, 0, linesWithoutPadding.Length);

                // Post-condition
                if (linesWithoutPadding.Length > lines.Length)
                {
                    throw new InvalidOperationException(
                        $"Expected linesWithoutPadding.Length <= lines.length (== {lines.Length}), " +
                        $"but got: {linesWithoutPadding.Length}");
                }

                return linesWithoutPadding;
            }

            private static readonly Regex LeadingSlashesRe = new Regex(@"^\s*//+");

            public static string CodeFromElementText(string elementText)
            {
                if (elementText.Length == 0)
                {
                    return "";
                }

                string[] lines = elementText.Split(
                    new[] { "\r\n", "\r", "\n" },
                    StringSplitOptions.None
                );

                if (lines.Length == 0)
                {
                    return "";
                }

                if (lines.Length == 1)
                {
                    StripMargin(lines);
                    return lines[0];
                }

                for (var i = 0; i < lines.Length; i++)
                {
                    lines[i] = LeadingSlashesRe.Replace(lines[i], "");
                }

                lines = RemoveTopAndBottomPadding(lines);
                lines = StripMargin(lines);

                return String.Join(Environment.NewLine, lines);
            }

            public class Code
            {
                public readonly string Text;
                public readonly int Line; // starts at 0
                public readonly int Column; // starts at 0

                public Code(string text, int line, int column)
                {
                    Text = text;
                    Line = line;
                    Column = column;
                }
            }

            public static List<Code> CodesFromDocumentation(Syntax.DocumentationCommentTriviaSyntax documentation)
            {
                var result = new List<Code>();

                foreach (var element in documentation.DescendantNodes().OfType<Syntax.XmlElementSyntax>())
                {
                    bool isDoctest = false;
                    foreach (Syntax.XmlAttributeSyntax attr in element.StartTag.Attributes)
                    {
                        if (attr.Name.ToString().ToLowerInvariant() == "doctest")
                        {
                            int valueStart = attr.StartQuoteToken.Span.End;
                            int valueEnd = attr.EndQuoteToken.Span.Start;

                            TextSpan valueSpan = new TextSpan(valueStart, valueEnd - valueStart);

                            string value = attr.SyntaxTree
                                .GetText()
                                .GetSubText(valueSpan)
                                .ToString();

                            isDoctest = value.ToLowerInvariant() == "true";
                        }
                    }

                    if (!isDoctest) continue;

                    string name = element.StartTag.Name.ToString().ToLowerInvariant();
                    if (name != "code")
                    {
                        continue;
                    }

                    string text = CodeFromElementText(element.Content.ToString());
                    var lineSpan = element.SyntaxTree.GetLineSpan(element.Span);

                    int line = lineSpan.StartLinePosition.Line;
                    int column = lineSpan.StartLinePosition.Character;
                    result.Add(new Code(text, line, column));
                }

                return result;
            }

            public class DoctestWithoutNamespace
            {
                public readonly List<UsingDirective> Usings;
                public readonly string Body;
                public readonly int Line; // starts at 0
                public readonly int Column; // starts at 0

                public DoctestWithoutNamespace(List<UsingDirective> usings, string body, int line, int column)
                {
                    Usings = usings;
                    Body = body;
                    Line = line;
                    Column = column;
                }
            }

            public class DoctestWithoutNamespaceOrError
            {
                public readonly DoctestWithoutNamespace? DoctestWithoutNamespace;
                public readonly Error? Error;

                public DoctestWithoutNamespaceOrError(DoctestWithoutNamespace? doctestWithoutNamespace, Error? error)
                {
                    if (doctestWithoutNamespace == null && error == null)
                    {
                        throw new ArgumentException("Both doctest and error are null.");
                    }

                    if (doctestWithoutNamespace != null && error != null)
                    {
                        throw new ArgumentException("Both doctest and error are given.");
                    }

                    DoctestWithoutNamespace = doctestWithoutNamespace;
                    Error = error;
                }
            }

            private static readonly Regex SplitLineRe = new Regex(@"^//\s*----*\s*$");

            public static DoctestWithoutNamespaceOrError SplitHeaderBody(Code code)
            {
                var tree = CSharpSyntaxTree.ParseText(code.Text);
                var root = (CompilationUnitSyntax)tree.GetRoot();

                int headerEnd = 0; // exclusive
                string body = code.Text;

                foreach (var trivia in root.DescendantTrivia())
                {
                    if (SplitLineRe.IsMatch(trivia.ToString()))
                    {
                        headerEnd = trivia.SpanStart;
                        body = code.Text.Substring(trivia.Span.End).Trim();
                        break;
                    }
                }

                // A work-around to skip descending into children if the parent node has been accepted
                int acceptedEnd = 0;

                var usings = new List<UsingDirective>();
                foreach (var node in root.DescendantNodes())
                {
                    if (node.SpanStart < acceptedEnd || node.SpanStart >= headerEnd)
                    {
                        break;
                    }

                    switch (node)
                    {
                        case Syntax.UsingDirectiveSyntax usingDirectiveSyntax:
                            usings.Add(new UsingDirective(
                                usingDirectiveSyntax.Name.ToString(),
                                usingDirectiveSyntax.Alias?.Name.ToString()));
                            acceptedEnd = usingDirectiveSyntax.Span.End;
                            break;
                        default:
                            var location = node.SyntaxTree.GetLineSpan(node.Span);
                            return new DoctestWithoutNamespaceOrError(
                                null,
                                new Error(
                                    $"Expected only using directives in the header, but got: {node.Kind()}",
                                    location.StartLinePosition.Line,
                                    location.StartLinePosition.Character));
                    }
                }

                return new DoctestWithoutNamespaceOrError(
                    new DoctestWithoutNamespace(usings, body, code.Line, code.Column),
                    null);
            }
        }

        public class UsingDirective
        {
            public readonly string Name;
            public readonly string? Alias;

            public UsingDirective(string name, string? alias)
            {
                Name = name;
                Alias = alias;
            }

            private bool Equals(UsingDirective other)
            {
                return Name == other.Name && Alias == other.Alias;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((UsingDirective)obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Name, Alias);
            }

            public override string ToString()
            {
                return $"{nameof(Name)}: {Name}, {nameof(Alias)}: {Alias}";
            }
        }

        public class Doctest
        {
            public readonly string Namespace;
            public readonly List<UsingDirective> Usings;
            public readonly string Body;
            public readonly int Line; // starts at 0
            public readonly int Column; // starts at 0

            public Doctest(string ns, List<UsingDirective> usings, string body, int line, int column)
            {
                Namespace = ns;
                Usings = usings;
                Body = body;
                Line = line;
                Column = column;
            }
        }

        public class DoctestsAndErrors
        {
            public readonly List<Doctest> Doctests;
            public readonly List<Error> Errors;

            public DoctestsAndErrors(List<Doctest> doctests, List<Error> errors)
            {
                Doctests = doctests;
                Errors = errors;
            }
        }

        private class NamespaceWip
        {
            public string Namespace = "";

            public readonly List<Doctest> Doctests = new List<Doctest>();

            // position when the namespace declaration ends
            public int SpanEnd = Int32.MaxValue;
        }

        /// <summary>
        /// Applies merge sort on Span.Start to obtain ordered stream of namespace decls and documentation
        /// syntax nodes.
        /// </summary>
        /// <param name="root">root of the syntax tree</param>
        /// <returns>stream ordered by span.start</returns>
        private static IEnumerable<SyntaxNode> NamespaceDeclsAndDocumentations(CompilationUnitSyntax root)
        {
            using var namespaceDecls =
                root
                    .DescendantNodes()
                    .OfType<Syntax.NamespaceDeclarationSyntax>()
                    .GetEnumerator();

            using var documentations =
                root
                    .DescendantNodes(descendIntoTrivia: true)
                    .OfType<Syntax.DocumentationCommentTriviaSyntax>()
                    .GetEnumerator();

            bool doneWithNamespaceDecls = !namespaceDecls.MoveNext();
            bool doneWithDocumentations = !documentations.MoveNext();

            SyntaxNode? prevWhat = null;  // previous yield return

            while (!doneWithNamespaceDecls || !doneWithDocumentations)
            {
                SyntaxNode? what;  // what to yield return

                if (doneWithNamespaceDecls && !doneWithDocumentations)
                {
                    what = documentations.Current;
                    doneWithDocumentations = !documentations.MoveNext();
                }
                else if (!doneWithNamespaceDecls && doneWithDocumentations)
                {
                    what = namespaceDecls.Current;
                    doneWithNamespaceDecls = !namespaceDecls.MoveNext();
                }
                else
                {
                    if (namespaceDecls.Current.SpanStart < documentations.Current.SpanStart)
                    {
                        what = namespaceDecls.Current;
                        doneWithNamespaceDecls = !namespaceDecls.MoveNext();
                    }
                    else
                    {
                        what = documentations.Current;
                        doneWithDocumentations = !documentations.MoveNext();
                    }
                }

                // Loop invariant
                if (what == null)
                {
                    throw new InvalidOperationException("Unexpected null what");
                }

                if (prevWhat != null && what.SpanStart <= prevWhat.SpanStart)
                {
                    throw new InvalidOperationException(
                        $"Unexpected {nameof(what)}.SpanStart (== {what.SpanStart}) " +
                        $"before {nameof(prevWhat)}.SpanStart (== {prevWhat.SpanStart})");
                }
                yield return what;
            }
        }

        public static DoctestsAndErrors Extract(SyntaxTree tree)
        {
            var doctests = new List<Doctest>();
            var errors = new List<Error>();

            // stack
            var stack = new Stack<NamespaceWip>();

            // Push the Work-in-progress for the global namespace
            stack.Push(new NamespaceWip { Namespace = "", SpanEnd = tree.Length });

            var root = (CompilationUnitSyntax)tree.GetRoot();

            foreach (var node in NamespaceDeclsAndDocumentations(root))
            {
                if (node.SpanStart >= stack.Peek().SpanEnd)
                {
                    var wip = stack.Pop();
                    if (wip.Doctests.Count > 0)
                    {
                        doctests.AddRange(wip.Doctests);
                    }
                }

                switch (node)
                {
                    case Syntax.DocumentationCommentTriviaSyntax documentation:
                        List<Pipeline.Code> codes = Pipeline.CodesFromDocumentation(documentation);

                        List<Pipeline.DoctestWithoutNamespaceOrError> dtWoNsOrErrorList =
                            codes.Select(Pipeline.SplitHeaderBody).ToList();

                        stack.Peek().Doctests.AddRange(
                            dtWoNsOrErrorList
                                .Where((dtOrErr) => dtOrErr.DoctestWithoutNamespace != null)
                                .Select((dtOrErr) => new Doctest(
                                    stack.Peek().Namespace,
                                    dtOrErr.DoctestWithoutNamespace!.Usings,
                                    dtOrErr.DoctestWithoutNamespace!.Body,
                                    dtOrErr.DoctestWithoutNamespace!.Line,
                                    dtOrErr.DoctestWithoutNamespace!.Column)));

                        errors.AddRange(
                            dtWoNsOrErrorList
                                .Where((dtOrErr) => dtOrErr.Error != null)
                                .Select((dtOrErr) => dtOrErr.Error!));

                        break;

                    case Syntax.NamespaceDeclarationSyntax namespaceDecl:
                        string ns = (stack.Peek().Namespace == "")
                            ? namespaceDecl.Name.ToString()
                            : $"{stack.Peek().Namespace}.{namespaceDecl.Name.ToString()}";

                        stack.Push(new NamespaceWip
                        {
                            Namespace = ns,
                            SpanEnd = namespaceDecl.Span.End
                        });

                        break;

                    default:
                        continue;
                }
            }

            while (stack.Count != 0)
            {
                var wip = stack.Pop();
                if (wip.Doctests.Count > 0)
                {
                    doctests.AddRange(wip.Doctests);
                }
            }

            // Sort doctests by line and column
            doctests.Sort((doctest, otherDoctest) =>
            {
                int ret = doctest.Line.CompareTo(otherDoctest.Line);
                if (ret == 0) ret = doctest.Column.CompareTo(otherDoctest.Column);

                return ret;
            });

            return new DoctestsAndErrors(doctests, errors);
        }
    }
}