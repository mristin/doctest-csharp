using File = System.IO.File;
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using DirectoryInfo = System.IO.DirectoryInfo;
using Environment = System.Environment;

using NUnit.Framework;

namespace DoctestCsharp.Test
{
    public class ProgramTests
    {
        [Test]
        public void TestNoCommandLineArguments()
        {
            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(new string[0]);

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"Option '--input' is required.{nl}" +
                $"Option '--output' is required.{nl}{nl}",
                consoleCapture.Error());
        }

        [Test]
        public void TestInvalidCommandLineArguments()
        {
            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(new[] { "--invalid-arg" });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"Option '--input' is required.{nl}" +
                $"Option '--output' is required.{nl}" +
                $"Unrecognized command or argument '--invalid-arg'{nl}{nl}",
                consoleCapture.Error());
        }
    }

    public class ProgramGenerateTests
    {
        [Test]
        public void TestNoDoctest()
        {
            using var tmpdir = new TemporaryDirectory();
            DirectoryInfo input = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject"));
            DirectoryInfo output = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject.Test/doctests"));

            string inputPath = Path.Join(input.FullName, "SomeProgram.cs");
            string outputPath = Path.Join(output.FullName, "DocTestSomeProgram.cs");

            File.WriteAllText(inputPath, "no doctests");

            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(new[] { "--input", input.FullName, "--output", output.FullName });

            string nl = Environment.NewLine;

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(
                $"No doctests found in: {inputPath}; not generating {outputPath}{nl}",
                consoleCapture.Output());
        }

        [Test]
        public void TestDoctest()
        {
            using var tmpdir = new TemporaryDirectory();
            DirectoryInfo input = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject"));
            DirectoryInfo output = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject.Test/doctests"));

            string inputPath = Path.Join(input.FullName, "SomeProgram.cs");
            string outputPath = Path.Join(output.FullName, "DocTestSomeProgram.cs");

            File.WriteAllText(
                inputPath,
                @"/// <code doctest=""true"">
/// var x = 1;
/// </code>
");

            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(new[] { "--input", input.FullName, "--output", output.FullName });

            string nl = Environment.NewLine;

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(
                $"Generated doctest(s) for: {inputPath} -> {outputPath}{nl}",
                consoleCapture.Output());
        }

        [Test]
        public void TestExtractionError()
        {
            using var tmpdir = new TemporaryDirectory();
            DirectoryInfo input = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject"));
            DirectoryInfo output = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject.Test/doctests"));

            string inputPath = Path.Join(input.FullName, "SomeProgram.cs");

            File.WriteAllText(
                inputPath,
                @"/// <code doctest=""true"">
/// var a = 0;
/// // ---
/// var x = 1;
/// </code>
");

            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(new[] { "--input", input.FullName, "--output", output.FullName });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"Failed to extract doctest(s) from: {inputPath}{nl}" +
                $"* Line 1, column 1: Expected only using directives in the header, but got: FieldDeclaration{nl}",
                consoleCapture.Output());
        }

        [Test]
        public void TestCheckOk()
        {
            using var tmpdir = new TemporaryDirectory();
            DirectoryInfo input = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject"));
            DirectoryInfo output = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject.Test/doctests"));

            string inputPath = Path.Join(input.FullName, "SomeProgram.cs");
            string outputPath = Path.Join(output.FullName, "DocTestSomeProgram.cs");

            File.WriteAllText(
                inputPath,
                @"/// <code doctest=""true"">
/// var x = 1;
/// </code>
");

            File.WriteAllText(
                outputPath,
                @"// This file was automatically generated by doctest-csharp.
// !!! DO NOT EDIT OR APPEND !!!

using NUnit.Framework;

namespace Tests
{
    public class DocTests
    {
        [Test]
        public void AtLine0AndColumn4
        {
            var x = 1;
        }
    }
}

// This file was automatically generated by doctest-csharp.
// !!! DO NOT EDIT OR APPEND !!!
");

            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(
                new[] { "--input", input.FullName, "--output", output.FullName, "--check" });

            string nl = Environment.NewLine;

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual($"OK: {inputPath} -> {outputPath}{nl}", consoleCapture.Output());
        }

        [Test]
        public void TestCheckDoesntExist()
        {
            using var tmpdir = new TemporaryDirectory();
            DirectoryInfo input = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject"));
            DirectoryInfo output = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject.Test/doctests"));

            string inputPath = Path.Join(input.FullName, "SomeProgram.cs");
            string outputPath = Path.Join(output.FullName, "DocTestSomeProgram.cs");

            File.WriteAllText(
                inputPath,
                @"/// <code doctest=""true"">
/// var x = 1;
/// </code>
");

            // Test pre-condition
            Assert.IsFalse(File.Exists(outputPath));

            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(
                new[] { "--input", input.FullName, "--output", output.FullName, "--check" });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual($"Output file does not exist: {inputPath} -> {outputPath}{nl}", consoleCapture.Output());
        }

        [Test]
        public void TestCheckDifferent()
        {
            using var tmpdir = new TemporaryDirectory();
            DirectoryInfo input = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject"));
            DirectoryInfo output = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject.Test/doctests"));

            string inputPath = Path.Join(input.FullName, "SomeProgram.cs");
            string outputPath = Path.Join(output.FullName, "DocTestSomeProgram.cs");

            File.WriteAllText(
                inputPath,
                @"/// <code doctest=""true"">
/// var x = 1;
/// </code>
");

            File.WriteAllText(outputPath, "different content");

            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(
                new[] { "--input", input.FullName, "--output", output.FullName, "--check" });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"Expected different content: {inputPath} -> {outputPath}{nl}",
                consoleCapture.Output());
        }

        [Test]
        public void TestCheckShouldntExist()
        {
            using var tmpdir = new TemporaryDirectory();
            DirectoryInfo input = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject"));
            DirectoryInfo output = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject.Test/doctests"));

            string inputPath = Path.Join(input.FullName, "SomeProgram.cs");
            string outputPath = Path.Join(output.FullName, "DocTestSomeProgram.cs");

            File.WriteAllText(inputPath, "no code");

            File.WriteAllText(outputPath, "unexpected content");

            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(
                new[] { "--input", input.FullName, "--output", output.FullName, "--check" });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"No doctests found in: {inputPath}; the output should not exist: {outputPath}{nl}",
                consoleCapture.Output());
        }
    }
}