using Path = System.IO.Path;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DoctestCsharp.Test
{
    public class InputOutputParseTests
    {
        [Test]
        public void TestNoInputOutput()
        {
            var inputOutputOrError = Input.ParseInputOutput(new string[] { }, ".Tests");
            Assert.AreEqual(null, inputOutputOrError.Error);
            Assert.That(
                inputOutputOrError.InputOutput,
                Is.EquivalentTo(new List<(string, string)>()));
        }

        [Test]
        public void TestSingleInputOutput()
        {
            var inputOutputOrError = Input.ParseInputOutput(
                new[]
                {
                    $"someInput{Path.PathSeparator}someOutput"
                },
                ".Tests");

            Assert.AreEqual(null, inputOutputOrError.Error);
            Assert.That(
                inputOutputOrError.InputOutput,
                Is.EquivalentTo(
                    new List<(string, string)>
                    {
                        ("someInput", "someOutput")
                    }));
        }

        public void TestMultipleInputOutput()
        {
            var inputOutputOrError = Input.ParseInputOutput(
                new[]
                {
                    $"someInput{Path.PathSeparator}someOutput",
                    $"anotherInput{Path.PathSeparator}anotherOutput"
                },
                ".Tests");

            Assert.AreEqual(null, inputOutputOrError.Error);
            Assert.That(
                inputOutputOrError.InputOutput,
                Is.EquivalentTo(
                    new List<(string, string)>
                    {
                        ("someInput", "someOutput"),
                        ("anotherInput", "anotherOutput")
                    }));
        }

        [Test]
        public void TestAutomaticOutputOnEmptyOutput()
        {
            var inputOutputOrError = Input.ParseInputOutput(
                new[]
                {
                    $"someInput{Path.PathSeparator}"
                },
                ".Tests");

            Assert.AreEqual(null, inputOutputOrError.Error);
            Assert.That(
                inputOutputOrError.InputOutput,
                Is.EquivalentTo(
                    new List<(string, string)>
                    {
                        ("someInput", "someInput.Tests")
                    }));
        }

        [Test]
        public void TestAutomaticOutputOnNoOutput()
        {
            var inputOutputOrError = Input.ParseInputOutput(
                new[]
                {
                    "someInput"
                },
                ".Tests");

            Assert.AreEqual(null, inputOutputOrError.Error);
            Assert.That(
                inputOutputOrError.InputOutput,
                Is.EquivalentTo(
                    new List<(string, string)>
                    {
                        ("someInput", "someInput.Tests")
                    }));
        }

        [Test]
        public void TestError()
        {
            string inputOutputRaw = $"someInput{Path.PathSeparator}someOutput{Path.PathSeparator}something wrong";
            var inputOutputOrError = Input.ParseInputOutput(new[] { inputOutputRaw }, ".Tests");

            Assert.AreEqual(
                $"Expected at most a pair, but got 3 parts " +
                $"separated by {Path.PathSeparator} from the input-output: {inputOutputRaw}",
                inputOutputOrError.Error);
        }
    }

    public class MatchFilesTests
    {
        private static void WriteDummyFile(string prefix, string dirName, string subdirName, string name)
        {
            var parent = Path.Join(prefix, dirName, subdirName);
            System.IO.Directory.CreateDirectory(parent);

            var path = Path.Join(parent, name);
            System.IO.File.WriteAllText(path, "some content");
        }

        [Test]
        public void TestThatFilesAreMatchedAsRelativePaths()
        {
            using var tmpdir = new TemporaryDirectory();

            WriteDummyFile(tmpdir.Path, "a", "b", "Program.cs");

            List<string> matchedFiles = Input.MatchFiles(
                    tmpdir.Path,
                    new List<string> { Path.Join("**", "*.cs") },
                    new List<string>())
                .ToList();

            var expectedFiles = new List<string>() { Path.Join("a", "b", "Program.cs") };

            Assert.That(matchedFiles, Is.EquivalentTo(expectedFiles));
        }

        [Test]
        public void TestThatFilesAreMatchedAsRootedPaths()
        {
            using var tmpdir = new TemporaryDirectory();

            WriteDummyFile(tmpdir.Path, "a", "b", "Program.cs");

            List<string> matchedFiles = Input.MatchFiles(
                    tmpdir.Path,
                    new List<string> { Path.Join(tmpdir.Path, "**", "*.cs") },
                    new List<string>())
                .ToList();

            var expectedFiles = new List<string>() { Path.Join(tmpdir.Path, "a", "b", "Program.cs") };

            Assert.That(matchedFiles, Is.EquivalentTo(expectedFiles));
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void TestExclude(bool patternIsRooted, bool excludeIsRooted)
        {
            using var tmpdir = new TemporaryDirectory();

            WriteDummyFile(tmpdir.Path, "a", "b", "Program.cs");
            WriteDummyFile(tmpdir.Path, "a", "obj", "AnotherProgram.cs");

            string pattern = (patternIsRooted)
                ? Path.Join(tmpdir.Path, "**", "*.cs")
                : Path.Join("**", "*.cs");

            string exclude = (excludeIsRooted)
                ? Path.Join(tmpdir.Path, "**", "obj", "**", "*.cs")
                : Path.Join("**", "obj", "**", "*.cs");

            List<string> matchedFiles = Input.MatchFiles(
                    tmpdir.Path,
                    new List<string>() { pattern },
                    new List<string>() { exclude })
                .ToList();

            var expectedFiles = (patternIsRooted)
                ? new List<string>() { Path.Join(tmpdir.Path, "a", "b", "Program.cs") }
                : new List<string>() { Path.Join("a", "b", "Program.cs") };

            Assert.That(matchedFiles, Is.EquivalentTo(expectedFiles));
        }
    }
}