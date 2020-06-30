using InvalidOperationException = System.InvalidOperationException;
using Environment = System.Environment;

using System.Collections.Generic;
using System.Linq;

namespace DoctestCsharp
{
    public static class Generation
    {
        public static class Style
        {
            public static string Indent(string text, int level)
            {
                if (text == "") return "";

                string margin = new string(' ', level * 4);
                string[] lines = text.Split(
                    new[] { "\r\n", "\r", "\n" },
                    System.StringSplitOptions.None
                );

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Length > 0)
                    {
                        lines[i] = margin + lines[i];
                    }
                }

                return string.Join(Environment.NewLine, lines);
            }

            public class NamespacedDoctests
            {
                public readonly string Namespace;
                public readonly List<Extraction.Doctest> Doctests;

                public NamespacedDoctests(string ns, List<Extraction.Doctest> doctests)
                {
                    Namespace = ns;
                    Doctests = doctests;
                }
            }

            public static List<NamespacedDoctests> GroupConsecutiveDoctestsByNamespace(
                List<Extraction.Doctest> doctests)
            {
                var result = new List<NamespacedDoctests>();
                if (doctests.Count == 0)
                {
                    return result;
                }

                string? accumulatorNs = null;
                List<Extraction.Doctest>? accumulator = null;

                foreach (var doctest in doctests)
                {
                    if (accumulatorNs == null)
                    {
                        accumulator = new List<Extraction.Doctest>();
                        accumulatorNs = doctest.Namespace;
                    }
                    else if (accumulatorNs != doctest.Namespace)
                    {
                        if (accumulator == null)
                            throw new InvalidOperationException("Unexpected null accumulator");

                        if (accumulatorNs == null)
                            throw new InvalidOperationException("Unexpected null accumulatorNs");

                        result.Add(new NamespacedDoctests(accumulatorNs, accumulator));

                        accumulator = new List<Extraction.Doctest>();
                        accumulatorNs = doctest.Namespace;
                    }

                    if (accumulator == null)
                        throw new InvalidOperationException("Unexpected null accumulator");

                    if (accumulatorNs == null)
                        throw new InvalidOperationException("Unexpected null accumulatorNs");

                    accumulator.Add(doctest);
                }

                if (accumulator == null)
                    throw new InvalidOperationException("Unexpected null accumulator");

                if (accumulatorNs == null)
                    throw new InvalidOperationException("Unexpected null accumulatorNs");

                result.Add(new NamespacedDoctests(accumulatorNs, accumulator));

                return result;
            }

            /// <summary>
            /// Merges all the headers by removing the duplicate using directives.
            /// </summary>
            /// <param name="doctests">Extracted doctests</param>
            /// <returns>Single header</returns>
            public static string MergeHeaders(List<Extraction.Doctest> doctests)
            {
                if (doctests.Count == 0)
                {
                    return "";
                }

                bool noHeaders = doctests.All((doctest) => doctest.Usings.Count == 0);
                if (noHeaders)
                {
                    return "";
                }

                var usings = new HashSet<Extraction.UsingDirective>(
                        doctests.SelectMany((doctest) => doctest.Usings));

                var lines = new List<string>(usings.Count);
                foreach (Extraction.UsingDirective aUsing in usings)
                {
                    lines.Add(
                        aUsing.Alias != null
                            ? $"using {aUsing.Alias} = {aUsing.Name};"
                            : $"using {aUsing.Name};");
                }

                lines.Sort(System.StringComparer.InvariantCulture);

                return System.String.Join(Environment.NewLine, lines) + Environment.NewLine;
            }
        }

        public static void Generate(List<Extraction.Doctest> doctests, System.IO.TextWriter writer)
        {
            var blocks = new List<string>();

            string nl = Environment.NewLine;

            blocks.Add(
                $"// This file was automatically generated by doctest-csharp.{nl}" +
                $"// !!! DO NOT EDIT OR APPEND !!!{nl}");

            // Header blocks

            string header = Style.MergeHeaders(doctests);
            if (header.Length > 0)
            {
                blocks.Add(header);
            }

            blocks.Add($"using NUnit.Framework;{nl}");

            // Doctests

            List<Style.NamespacedDoctests> groupedDoctests = Style.GroupConsecutiveDoctestsByNamespace(doctests);

            var namespaceCount = new Dictionary<string, int>();

            foreach (var namespacedDoctests in groupedDoctests)
            {
                if (!namespaceCount.ContainsKey(namespacedDoctests.Namespace))
                {
                    namespaceCount[namespacedDoctests.Namespace] = 0;
                }

                namespaceCount[namespacedDoctests.Namespace]++;

                var block = new System.IO.StringWriter();

                block.WriteLine(
                    namespacedDoctests.Namespace == ""
                        ? "namespace Tests"
                        : $"namespace {namespacedDoctests.Namespace}.Tests");

                block.WriteLine("{"); // namespace opening

                block.WriteLine(
                    namespaceCount[namespacedDoctests.Namespace] > 1
                        ? $"    public class DocTests{namespaceCount[namespacedDoctests.Namespace]}"
                        : "    public class DocTests");

                block.WriteLine("    {"); // class opening

                for (var i = 0; i < namespacedDoctests.Doctests.Count; i++)
                {
                    if (i > 0)
                    {
                        block.WriteLine();
                    }

                    var doctest = namespacedDoctests.Doctests[i];
                    block.WriteLine("        [Test]");
                    block.WriteLine($"        public void AtLine{doctest.Line}AndColumn{doctest.Column}");
                    block.WriteLine("        {"); // method opening

                    block.WriteLine(
                        doctest.Body == ""
                            ? Style.Indent("// Empty doctest", 3)
                            : Style.Indent(doctest.Body, 3));

                    block.WriteLine("        }"); // method closing
                }

                block.WriteLine("    }"); // class closing
                block.WriteLine("}"); // namespace closing

                blocks.Add(block.ToString());
            }

            blocks.Add(
                $"// This file was automatically generated by doctest-csharp.{nl}" +
                $"// !!! DO NOT EDIT OR APPEND !!!{nl}");

            // Join blocks

            foreach (string block in blocks)
            {
                if (!block.EndsWith(Environment.NewLine))
                {
                    throw new InvalidOperationException(
                        $"Expected block to end with a new line, but got: {block}");
                }
            }

            for (var i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                {
                    writer.WriteLine();
                }

                // All block must end with a new-line character, so do not add a new line here.
                writer.Write(blocks[i]);
            }
        }
    }
}