using System;
using File = System.IO.File;
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using DirectoryInfo = System.IO.DirectoryInfo;
using Environment = System.Environment;

using NUnit.Framework;
using NUnit.Framework.Interfaces;

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
                $"Option '--input-output' is required.{nl}{nl}",
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
                $"Option '--input-output' is required.{nl}" +
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

            int exitCode = Program.MainWithCode(
                new[]
                {
                    "--input-output", $"{input.FullName}{Path.PathSeparator}{output.FullName}",
                    "--verbose"
                });

            string nl = Environment.NewLine;

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(
                $"No doctests found in: {inputPath}{nl}",
                consoleCapture.Output());

            Assert.IsFalse(File.Exists(outputPath));
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

            int exitCode = Program.MainWithCode(
                new[]
                {
                    "--input-output", $"{input.FullName}{Path.PathSeparator}{output.FullName}",
                    "--verbose"
                });

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

            int exitCode = Program.MainWithCode(
                new[] { "--input-output", $"{input.FullName}{Path.PathSeparator}{output.FullName}" });

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
    public class DocTest_SomeProgram_cs
    {
        [Test]
        public void AtLine0AndColumn4()
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
                new[]
                {
                    "--input-output", $"{input.FullName}{Path.PathSeparator}{output.FullName}",
                    "--check",
                    "--verbose"
                });

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
                new[]
                {
                    "--input-output", $"{input.FullName}{Path.PathSeparator}{output.FullName}",
                    "--check",
                    "--verbose"
                });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"Output file does not exist: {inputPath} -> {outputPath}{nl}",
                consoleCapture.Output());
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
                new[]
                {
                    "--input-output", $"{input.FullName}{Path.PathSeparator}{output.FullName}",
                    "--check",
                    "--verbose"
                });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"Expected different content: {inputPath} -> {outputPath}{nl}" +
                $"Here is the diff between the expected content and the actual content:{nl}" +
                $"- // This file was automatically generated by doctest-csharp.{nl}" +
                $"+ different content{nl}" +
                $"- // !!! DO NOT EDIT OR APPEND !!!{nl}" +
                $"- {nl}" +
                $"- using NUnit.Framework;{nl}" +
                $"- {nl}" +
                $"- namespace Tests{nl}" +
                $"- {{{nl}" +
                $"-     public class DocTest_SomeProgram_cs{nl}" +
                $"-     {{{nl}" +
                $"-         [Test]{nl}" +
                $"-         public void AtLine0AndColumn4(){nl}" +
                $"-         {{{nl}" +
                $"-             var x = 1;{nl}" +
                $"-         }}{nl}" +
                $"-     }}{nl}" +
                $"- }}{nl}" +
                $"- {nl}" +
                $"- // This file was automatically generated by doctest-csharp.{nl}" +
                $"- // !!! DO NOT EDIT OR APPEND !!!{nl}" +
                $"- {nl}",
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
                new[]
                {
                    "--input-output", $"{input.FullName}{Path.PathSeparator}{output.FullName}",
                    "--check",
                    "--verbose"
                });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"No doctests found in: {inputPath}; the output should not exist: {outputPath}{nl}",
                consoleCapture.Output());
        }

        [Test]
        public void TestOutputMayNotExistIfNoDoctests()
        {
            using var tmpdir = new TemporaryDirectory();
            DirectoryInfo input = Directory.CreateDirectory(Path.Join(tmpdir.Path, "SomeProject"));
            string output = Path.Join(tmpdir.Path, "SomeProject.Test/doctests");
            Assert.IsFalse(File.Exists(output));

            string inputPath = Path.Join(input.FullName, "SomeProgram.cs");
            File.WriteAllText(inputPath, "no doctests");

            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(
                new[]
                {
                    "--input-output", $"{input.FullName}{Path.PathSeparator}{output}",
                    "--check",
                    "--verbose"
                });

            string nl = Environment.NewLine;

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(
                $"OK, no doctests: {inputPath}{nl}",
                consoleCapture.Output());
        }
    }
}
