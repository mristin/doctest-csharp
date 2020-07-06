using NotImplementedException = System.NotImplementedException;
using InvalidOperationException = System.InvalidOperationException;
using Console = System.Console;
using Environment = System.Environment;

using CSharpSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;

using System.Collections.Generic;
using System.CommandLine;
using System.IO;


namespace DoctestCsharp
{
    public class Program
    {
        private static int Handle(DirectoryInfo input, DirectoryInfo output, string[]? excludes, bool check)
        {
            int exitCode = 0;

            IEnumerable<string> relativePaths = Input.MatchFiles(
                input.FullName,
                new List<string> { "**/*.cs" },
                new List<string>(excludes ?? new string[0]));

            foreach (string relativePath in relativePaths)
            {
                if (Path.IsPathRooted(relativePath))
                {
                    throw new InvalidOperationException(
                        $"Expected path to be relative, but got rooted path: {relativePath}");
                }

                string inputPath = Process.InputPath(relativePath, input);
                string outputPath = Process.OutputPath(relativePath, output);

                var doctestsAndErrors = Extraction.Extract(
                    CSharpSyntaxTree.ParseText(
                        File.ReadAllText(inputPath)));

                if (doctestsAndErrors.Errors.Count > 0)
                {
                    Console.WriteLine($"Failed to extract doctest(s) from: {inputPath}");
                    foreach (var error in doctestsAndErrors.Errors)
                    {
                        Console.WriteLine($"* Line {error.Line + 1}, column {error.Column + 1}: {error.Message}");
                    }

                    exitCode = 1;

                    continue;
                }

                var doctests = doctestsAndErrors.Doctests;

                if (!check)
                {
                    bool generated = Process.Generate(doctests, outputPath);
                    Console.WriteLine(
                        generated
                        ? $"Generated doctest(s) for: {inputPath} -> {outputPath}"
                        : $"No doctests found in: {inputPath}; not generating {outputPath}");
                }
                else
                {
                    var report = Process.Check(doctests, outputPath);
                    switch (report)
                    {
                        case Process.Report.Ok:
                            Console.WriteLine($"OK: {inputPath} -> {outputPath}");
                            break;
                        case Process.Report.Different:
                            Console.WriteLine($"Expected different content: {inputPath} -> {outputPath}");
                            exitCode = 1;
                            break;

                        case Process.Report.DoesntExist:
                            Console.WriteLine($"Output file does not exist: {inputPath} -> {outputPath}");
                            exitCode = 1;
                            break;

                        case Process.Report.ShouldNotExist:
                            Console.WriteLine(
                                $"No doctests found in: {inputPath}; the output should not exist: {outputPath}");
                            exitCode = 1;
                            break;

                        default:
                            throw new NotImplementedException($"Uncovered report: {report}");
                    }
                }
            }

            return exitCode;
        }

        public static int MainWithCode(string[] args)
        {
            var nl = Environment.NewLine;

            var rootCommand = new RootCommand(
                "Generates tests from the embedded code snippets in the code documentation.")
            {
                new Option<DirectoryInfo>(
                    new[] {"--input", "-i"},
                    "Input directory containing the *.cs files")
                {
                    Required = true,
                    Argument = new Argument<DirectoryInfo>().ExistingOnly()
                },

                new Option<DirectoryInfo>(
                    new[] {"--output", "-o"},
                    "Output directory where the test source code will be generated.")
                {
                    Required = true,
                    Argument = new Argument<DirectoryInfo>().ExistingOnly()
                },

                new Option<string[]>(
                    new[] {"--excludes", "-e"},
                    $"Glob patterns of the files to be excluded from the input.{nl}{nl}" +
                    "The exclude patterns are either absolute (e.g., rooted with '/') or relative. " +
                    "In case of relative exclude patterns, they are relative to the _input_ directory " +
                    "and NOT to the current working directory."),

                new Option<bool>(
                    new[] {"--check", "-c"},
                    "If set, does not generate any files, but only checks that " +
                    $"the content of the test files coincides with what would be generated.{nl}{nl}" +
                    "This is particularly useful " +
                    "in continuous integration pipelines if you want to check if all the files " +
                    "have been scanned and correctly generated."
                )
            };

            rootCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create(
                (DirectoryInfo input, DirectoryInfo output, string[]? excludes, bool check) =>
                    Handle(input, output, excludes, check));

            int exitCode = rootCommand.InvokeAsync(args).Result;
            return exitCode;
        }

        public static void Main(string[] args)
        {
            int exitCode = MainWithCode(args);
            Environment.ExitCode = exitCode;
        }
    }
}