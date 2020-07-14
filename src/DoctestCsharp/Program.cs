using NotImplementedException = System.NotImplementedException;
using InvalidOperationException = System.InvalidOperationException;
using Console = System.Console;
using Environment = System.Environment;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;
using CSharpSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;
using System.Collections.Generic;
using System.CommandLine;

namespace DoctestCsharp
{
    public class Program
    {
        private static int Handle(string[] inputOutput, string suffix, string[]? excludes, bool check)
        {
            int exitCode = 0;

            var inputOutputOrError = Input.ParseInputOutput(inputOutput, suffix);
            if (inputOutputOrError.Error != null)
            {
                Console.Error.WriteLine($"Failed to parse --input-output: {inputOutputOrError.Error}");
                return 1;
            }

            if (inputOutputOrError.InputOutput == null)
            {
                throw new InvalidOperationException(
                    "Invalid inputOutputOrError: both InputOutput and Error are null.");
            }

            string cwd = Directory.GetCurrentDirectory();

            foreach (var (input, output) in inputOutputOrError.InputOutput)
            {
                string rootedInput =
                    Path.IsPathRooted(input)
                        ? input
                        : Path.Join(cwd, input);

                string rootedOutput =
                    Path.IsPathRooted(output)
                        ? output
                        : Path.Join(cwd, output);

                IEnumerable<string> relativePaths = Input.MatchFiles(
                    rootedInput,
                    new List<string> { "**/*.cs" },
                    new List<string>(excludes ?? new string[0]));

                foreach (string relativePath in relativePaths)
                {
                    if (Path.IsPathRooted(relativePath))
                    {
                        throw new InvalidOperationException(
                            $"Expected path to be relative, but got rooted path: {relativePath}");
                    }

                    string inputPath = Process.InputPath(relativePath, rootedInput);
                    string outputPath = Process.OutputPath(relativePath, rootedOutput);

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
                        bool generated = Process.Generate(doctests, relativePath, outputPath);
                        Console.WriteLine(
                            generated
                                ? $"Generated doctest(s) for: {inputPath} -> {outputPath}"
                                : $"No doctests found in: {inputPath}");
                    }
                    else
                    {
                        var report = Process.Check(doctests, relativePath, outputPath);
                        switch (report)
                        {
                            case Process.Report.Ok:
                                Console.WriteLine(
                                    (doctests.Count > 0)
                                        ? $"OK: {inputPath} -> {outputPath}"
                                        : $"OK, no doctests: {inputPath}");
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
            }

            return exitCode;
        }

        public static int MainWithCode(string[] args)
        {
            var nl = Environment.NewLine;

            var rootCommand = new RootCommand(
                "Generates tests from the embedded code snippets in the code documentation.")
            {
                new Option<string[]>(
                    new[] {"--input-output"},
                    $"Input and output directory pairs containing the *.cs files{nl}{nl}" +
                    "The input is separated from the output by the PATH separator " +
                    "(e.g., ';' on Windows, ':' on POSIX)." +
                    "If no output is specified, the --suffix is appended to the input to automatically obtain " +
                    "the output.")
                {
                    Required = true
                },

                new Option<string>(
                    new[] {"--suffix", "-s"},
                    () => ".Tests",
                    "Suffix to be automatically appended to the input to obtain the output directory " +
                    "in cases where no explicit output directory was given"
                ),

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
                (string[] inputOutput, string suffix, string[]? excludes, bool check) =>
                    Handle(inputOutput, suffix, excludes, check));

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