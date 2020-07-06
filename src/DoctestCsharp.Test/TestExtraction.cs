using InvalidOperationException = System.InvalidOperationException;
using CSharpSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;
using Syntax = Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Linq;
using System.Collections.Generic;

using NUnit.Framework;

namespace DoctestCsharp.Test
{
    public class StripMargin
    {
        [TestCase(new string[0], new string[0])]
        [TestCase(new[] { "someText" }, new[] { "someText" })]
        [TestCase(new[] { "noMargin", "againNoMargin" }, new[] { "noMargin", "againNoMargin" })]
        [TestCase(new[] { " X", "  XX" }, new[] { "X", " XX" })]
        [TestCase(new[] { "\tX", "\t\tXX" }, new[] { "X", "\tXX" })]
        [TestCase(new[] { " X", "\t\tXX" }, new[] { "X", "\tXX" })]
        public void Test(string[] lines, string[] expected)
        {
            string[] got = Extraction.Pipeline.StripMargin(lines);
            Assert.That(got, Is.EquivalentTo(expected));
        }
    }

    public class RemoveTopAndBottomPadding
    {
        [TestCase(new string[0], new string[0])]
        [TestCase(new[] { "someText" }, new[] { "someText" })]
        [TestCase(new[] { "", "someText" }, new[] { "someText" })]
        [TestCase(new[] { " ", "someText" }, new[] { "someText" })]
        [TestCase(new[] { "\t", "someText" }, new[] { "someText" })]
        [TestCase(new[] { "someText", "" }, new[] { "someText" })]
        [TestCase(new[] { "someText", " " }, new[] { "someText" })]
        [TestCase(new[] { "someText", "\t" }, new[] { "someText" })]
        [TestCase(new[] { "", "someText", "" }, new[] { "someText" })]
        [TestCase(new[] { "", "", "someText", "", "" }, new[] { "someText" })]
        public void Test(string[] lines, string[] expected)
        {
            string[] got = Extraction.Pipeline.RemoveTopAndBottomPadding(lines);
            Assert.That(got, Is.EquivalentTo(expected));
        }
    }

    public class CodeFromElementText
    {
        [Test]
        public void Test()
        {
            string nl = System.Environment.NewLine;

            var cases = new List<(string, string)>
            {
                ("", ""),
                ("var i = 0;", "var i = 0;"),
                // The following test case corresponds to <code>/// var i = 0;</code>.
                ("/// var i = 0;", "/// var i = 0;"),
                ($"/// var i = 0;{nl}/// ", "var i = 0;"),
                ($"/// {nl}/// var i = 0;", "var i = 0;"),
                ($"/// foreach(var x in lst){nl}///     x.Do();",
                    $"foreach(var x in lst){nl}    x.Do();"),
                ($"/// foreach(var x in lst){nl}/// \tx.Do();",
                    $"foreach(var x in lst){nl}\tx.Do();")
            };

            foreach (var (comment, expected) in cases)
            {
                string got = Extraction.Pipeline.CodeFromElementText(comment);
                Assert.AreEqual(expected, got);
            }
        }
    }

    public class CodesFromDocumentation
    {
        [TestCase(
            @"/// <code>var x = 1;</code>",
            new string[0],
            TestName = "no doctest attribute")]
        [TestCase(
            @"/// <code doctest=""false"">var x = 1;</code>",
            new string[0],
            TestName = "doctest attribute is set to false.")]
        [TestCase(
            @"/// <code doctest=""true"">var x = 1;</code>",
            new[] { "var x = 1;" },
            TestName = "single line"
        )]
        [TestCase(
            @"/// <CODE DOCTEST=""TRUE"">var x = 1;</CODE>",
            new[] { "var x = 1;" },
            TestName = "Extraction is case-insensitive."
        )]
        [TestCase(
            @"
/// <code doctest=""true"">
///     var x = 1;
/// </code>",
            new[] { "var x = 1;" },
            TestName = "Comment of multiple lines"
        )]
        [TestCase(
            @"
/// <code doctest=""true"">
///     foreach(var x in lst)
///         print(x);
/// </code>",
            new[]
            {
                @"foreach(var x in lst)
    print(x);"
            },
            TestName = "Extraction works with multi-line code."
        )]
        [TestCase(
            @"
/// <code doctest=""true"">var x = 1;</code>
/// <code doctest=""true"">var y = 2;</code>
",
            new[] { "var x = 1;", "var y = 2;" },
            TestName = "multiple code blocks"
        )]
        public void Test(string programText, string[] expected)
        {
            var tree = CSharpSyntaxTree.ParseText(programText);
            var root = (Syntax.CompilationUnitSyntax)tree.GetRoot();
            var first = root.DescendantNodes(descendIntoTrivia: true).First();

            List<Extraction.Pipeline.Code>? got;

            switch (first)
            {
                case Syntax.DocumentationCommentTriviaSyntax documentation:
                    got = Extraction.Pipeline.CodesFromDocumentation(documentation);
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected type of tree: {tree.GetType()}");
            }

            Assert.NotNull(got);
            Assert.AreEqual(expected.Length, got.Count);

            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], got[i].Text);
            }
        }
    }


    public class SplitHeaderBody
    {
        [Test]
        public void TestNoHeader()
        {
            string text = "var x = 1;";
            var doctestOrError = Extraction.Pipeline.SplitHeaderBody(
                new Extraction.Pipeline.Code(text, 0, 0));

            if (doctestOrError.Error != null)
            {
                Assert.Fail($"Expected no error, but got: {doctestOrError.Error.Message}");
            }

            var doctest = doctestOrError.DoctestWithoutNamespace!;

            Assert.AreEqual(0, doctest.Usings.Count);
            Assert.AreEqual("var x = 1;", doctest.Body);
        }

        [TestCase(@"using System;
// ---
var x = 1;", TestName = "tight whitespace")]
        [TestCase(
            @"using System;
//   ---   
var x = 1;",
            TestName = "Leading and trailing whitespace in separator")]
        [TestCase(
            @"using System;
//   -------   
var x = 1;",
            TestName = "Many dots in separator")]
        [TestCase(
            @"using System;

//   -------   

var x = 1;

",
            TestName = "Header and body trimmed")]
        public void TestHeaderWithoutAlias(string text)
        {
            var doctestOrError = Extraction.Pipeline.SplitHeaderBody(
                new Extraction.Pipeline.Code(text, 0, 0));

            if (doctestOrError.Error != null)
            {
                Assert.Fail($"Expected no error, but got: {doctestOrError.Error.Message}");
            }

            var doctest = doctestOrError.DoctestWithoutNamespace!;

            var expectedUsings = new List<Extraction.UsingDirective>
            {
                new Extraction.UsingDirective("System", null)
            };

            Assert.That(doctest.Usings, Is.EquivalentTo(expectedUsings));
            Assert.AreEqual("var x = 1;", doctest.Body);
        }

        [Test]
        public void TestHeaderWithAlias()
        {
            string text = @"using Sys = System;
// ---
var x = 1;";

            var doctestOrError = Extraction.Pipeline.SplitHeaderBody(
                new Extraction.Pipeline.Code(text, 0, 0));

            if (doctestOrError.Error != null)
            {
                Assert.Fail($"Expected no error, but got: {doctestOrError.Error.Message}");
            }

            var doctest = doctestOrError.DoctestWithoutNamespace!;

            var expectedUsings = new List<Extraction.UsingDirective>
            {
                new Extraction.UsingDirective("System", "Sys")
            };

            Assert.That(doctest.Usings, Is.EquivalentTo(expectedUsings));
            Assert.AreEqual("var x = 1;", doctest.Body);
        }

        [Test]
        public void TestError()
        {
            string text = @"    // some comment
    var a = 0;
    // ---
    var x = 1;";

            var doctestOrError = Extraction.Pipeline.SplitHeaderBody(
                new Extraction.Pipeline.Code(text, 0, 0));

            Assert.IsNull(doctestOrError.DoctestWithoutNamespace);

            var error = doctestOrError.Error!;
            Assert.AreEqual(
                "Expected only using directives in the header, but got: FieldDeclaration",
                error.Message);
            Assert.AreEqual(1, error.Line);
            Assert.AreEqual(4, error.Column);
        }
    }

    public class NamespaceDetectionTests
    {
        [TestCase(
            @"
    /// <code doctest=""true"">var x = 1;</code>
", new[] { "" },
            TestName = "No namespace")]
        [TestCase(
            @"
namespace A {
    /// <code doctest=""true"">var x = 1;</code>
}
", new[] { "A" },
            TestName = "Single namespace")]
        [TestCase(
            @"
namespace A.X {
    /// <code doctest=""true"">var x = 1;</code>
}

namespace B.Y {
    /// <code doctest=""true"">var x = 1;</code>
}

namespace A.X {
    /// <code doctest=""true"">var x = 1;</code>
}
", new[] { "A.X", "B.Y", "A.X" },
            TestName = "Multiple recurring namespaces")]
        [TestCase(
            @"
/// <code doctest=""true"">var x = 1;</code>

namespace A {
    /// <code doctest=""true"">var x = 1;</code>

    namespace X {
        /// <code doctest=""true"">var x = 1;</code>
    }

    namespace Y {
        /// <code doctest=""true"">var x = 1;</code>
    }

    namespace Z {
    }

    /// <code doctest=""true"">var x = 1;</code>
}

namespace A.X {
    /// <code doctest=""true"">var x = 1;</code>
}

/// <code doctest=""true"">var x = 1;</code>
",
            new[] { "", "A", "A.X", "A.Y", "A", "A.X", "" },
            TestName = "Nested namespaces")]
        public void Test(string text, string[] expectedNamespaces)
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            var doctestsAndErrors = Extraction.Extract(tree);

            Assert.That(doctestsAndErrors.Errors, Is.EquivalentTo(new List<Extraction.Error>()));

            var doctests = doctestsAndErrors.Doctests;

            var gotNamespaces = doctests.Select((doctest) => doctest.Namespace).ToList();

            Assert.That(gotNamespaces, Is.EquivalentTo(expectedNamespaces));
        }
    }

    public class RealisticExamples
    {
        [Test]
        public void TestMethodDoctest()
        {
            string text = @"
namespace SomeNamespace
{
    public static class SomeClass
    {
        /// <summary>
        /// Does something.
        /// </summary>
        /// <code doctest=""true"">
        /// using Microsoft.CodeAnalysis.SyntaxTree;
        /// // ---
        /// var x = 1;
        /// </code>
        public void SomeMethod() 
        {
            // some implementation
            var y = 2;
        }

        /// <summary>
        /// Does something else.
        /// </summary>
        /// <code doctest=""true"">
        /// var y = 2;
        /// </code>
        public void AnotherMethod() 
        {
            // another implementation
            var z = 3;
        }
    } 
}
";
            var expected = new List<Extraction.Doctest>
            {
                new Extraction.Doctest(
                    ns: "SomeNamespace",
                    usings: new List<Extraction.UsingDirective>
                    {
                        new Extraction.UsingDirective("Microsoft.CodeAnalysis.SyntaxTree", null)
                    },
                    body: "var x = 1;",
                    line: 8,
                    column: 12),
                new Extraction.Doctest(
                    ns: "SomeNamespace",
                    usings: new List<Extraction.UsingDirective>(),
                    body: "var y = 2;",
                    line: 22,
                    column: 12)
            };

            var tree = CSharpSyntaxTree.ParseText(text);
            var doctestsAndErrors = Extraction.Extract(tree);

            Assert.That(doctestsAndErrors.Errors, Is.EquivalentTo(new List<Extraction.Error>()));

            var doctests = doctestsAndErrors.Doctests;

            Assert.AreEqual(expected.Count, doctests.Count);
            for (var i = 0; i < doctests.Count; i++)
            {
                Assert.AreEqual(expected[i].Namespace, doctests[i].Namespace);
                Assert.That(doctests[i].Usings, Is.EquivalentTo(expected[i].Usings));
                Assert.AreEqual(expected[i].Body, doctests[i].Body);
                Assert.AreEqual(expected[i].Line, doctests[i].Line);
                Assert.AreEqual(expected[i].Column, doctests[i].Column);
            }
        }
    }
}