using System.Collections.Generic;
using NUnit.Framework;

namespace DoctestCsharp.Test
{
    public class Indent
    {
        [TestCase("", 0, "", TestName = "Empty gives empty.")]
        [TestCase("", 1, "", TestName = "Empty gives empty on higher levels.")]
        [TestCase("someText", 0, "someText", TestName = "Single line on level 0")]
        [TestCase("someText", 1, "    someText", TestName = "Single line on higher level")]
        [TestCase(
            @"someText
anotherText",
            0,
            @"someText
anotherText",
            TestName = "Multiple lines on level 0")]
        [TestCase(
            @"someText
anotherText",
            1,
            @"    someText
    anotherText",
            TestName = "Multiple lines on higher level")]
        [TestCase(
            @"someText
anotherText
",
            1,
            @"    someText
    anotherText
",
            TestName = "Multiple lines on higher level with trailing newline")]
        public void Test(string text, int level, string expected)
        {
            string got = Generation.Style.Indent(text, level);
            Assert.AreEqual(expected, got);
        }
    }

    public class Generate
    {
        [Test]
        public void TestRealistic()
        {
            var doctests = new List<Extraction.Doctest>
            {
                new Extraction.Doctest(
                    ns: "SomeNamespace",
                    usings: new List<Extraction.UsingDirective>
                    {
                        new Extraction.UsingDirective("System.IO", null),
                        new Extraction.UsingDirective("Microsoft.CodeAnalysis.SyntaxTree", null),
                    },
                    body: "var a = 1;",
                    line: 10,
                    column: 11),
                new Extraction.Doctest(
                    ns: "SomeNamespace",
                    usings: new List<Extraction.UsingDirective>(),
                    body: "var b = 2;",
                    line: 20,
                    column: 21),
                new Extraction.Doctest(
                    ns: "AnotherNamespace",
                    usings: new List<Extraction.UsingDirective>
                    {
                        new Extraction.UsingDirective("Microsoft.CodeAnalysis.SyntaxTree", null),
                    },
                    body: "var c = 3;",
                    line: 30,
                    column: 31),
                new Extraction.Doctest(
                    ns: "AnotherNamespace",
                    usings: new List<Extraction.UsingDirective>(),
                    body: "var d = 4;",
                    line: 40,
                    column: 41),
                new Extraction.Doctest(
                    ns: "SomeNamespace",
                    usings: new List<Extraction.UsingDirective>
                    {
                        new Extraction.UsingDirective("Microsoft.CodeAnalysis.SyntaxTree", null),
                    },
                    body: "var e = 5;",
                    line: 50,
                    column: 51),
                new Extraction.Doctest(
                    ns: "SomeNamespace",
                    usings: new List<Extraction.UsingDirective>(),
                    body: "var f = 6;",
                    line: 60,
                    column: 61)
            };

            var writer = new System.IO.StringWriter();
            Generation.Generate(doctests, writer);

            string expected = @"// This file was automatically generated by doctest-csharp.
// !!! DO NOT EDIT OR APPEND !!!

using Microsoft.CodeAnalysis.SyntaxTree;
using System.IO;

using NUnit.Framework;

namespace SomeNamespace.Tests
{
    public class DocTests
    {
        [Test]
        public void AtLine10AndColumn11
        {
            var a = 1;
        }

        [Test]
        public void AtLine20AndColumn21
        {
            var b = 2;
        }
    }
}

namespace AnotherNamespace.Tests
{
    public class DocTests
    {
        [Test]
        public void AtLine30AndColumn31
        {
            var c = 3;
        }

        [Test]
        public void AtLine40AndColumn41
        {
            var d = 4;
        }
    }
}

namespace SomeNamespace.Tests
{
    public class DocTests2
    {
        [Test]
        public void AtLine50AndColumn51
        {
            var e = 5;
        }

        [Test]
        public void AtLine60AndColumn61
        {
            var f = 6;
        }
    }
}

// This file was automatically generated by doctest-csharp.
// !!! DO NOT EDIT OR APPEND !!!
";
            Assert.AreEqual(expected, writer.ToString());
        }
    }
}