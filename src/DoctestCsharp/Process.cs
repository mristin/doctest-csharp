using ArgumentException = System.ArgumentException;
using DirectoryInfo = System.IO.DirectoryInfo;
using Path = System.IO.Path;
using File = System.IO.File;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DoctestCsharp
{
    public static class Process
    {
        public static string InputPath(string relativePath, string input)
        {
            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException($"Expected a relative path, but got a rooted one: {relativePath}");
            }

            return Path.Join(input, relativePath);
        }

        public static string OutputPath(string relativePath, string output)
        {
            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException($"Expected a relative path, but got a rooted one: {relativePath}");
            }

            string doctestRelativePath = Path.Join(
                Path.GetDirectoryName(relativePath),
                "DocTest" + Path.GetFileName(relativePath));

            return Path.Join(output, doctestRelativePath);
        }

        private static readonly Regex nonidentifierCharRe = new Regex(@"[^a-zA-Z0-9_]");

        /// <summary>
        /// Infers the identifier for the test suite based on the relative path to the input file. 
        /// </summary>
        /// <param name="relativeInputPath">path to the input file, relative</param>
        /// <returns>Valid C# class identifier</returns>
        public static string Identifier(string relativeInputPath)
        {
            if (relativeInputPath.Length == 0)
            {
                throw new ArgumentException($"Unexpected empty {nameof(relativeInputPath)}");
            }

            if (Path.IsPathRooted(relativeInputPath))
            {
                throw new ArgumentException(
                    $"Unexpected rooted {nameof(relativeInputPath)}: {relativeInputPath}");
            }

            return $"DocTest_{nonidentifierCharRe.Replace(relativeInputPath, "_")}";
        }

        /// <summary>
        /// Generates the doctests given the extracted doctests from the input file.
        /// </summary>
        /// <param name="doctests">Extracted doctests from the input file</param>
        /// <param name="relativeInputPath">Relative path to the input file</param>
        /// <param name="outputPath">Absolute path to the output doctest file</param>
        /// <returns>true if there is at least one generated doctest</returns>
        /// <exception cref="ArgumentException"></exception>
        public static bool Generate(
            List<Extraction.Doctest> doctests,
            string relativeInputPath,
            string outputPath)
        {
            // Pre-condition(s)
            if (!Path.IsPathRooted(outputPath))
            {
                throw new ArgumentException($"Expected a rooted outputPath, but got: {outputPath}");
            }

            // Implementation

            if (doctests.Count == 0)
            {
                return false;
            }

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            string identifier = Identifier(relativeInputPath);

            using var streamWriter = new System.IO.StreamWriter(outputPath);

            Generation.Generate(doctests, identifier, streamWriter);
            return true;
        }

        public enum Report
        {
            Ok,
            Different,
            DoesntExist,
            ShouldNotExist
        }

        /// <summary>
        /// Checks that the generated output actually matches the stored output.
        /// </summary>
        /// <param name="doctests">Extracted doctests</param>
        /// <param name="relativeInputPath">Relative path to the input file</param>
        /// <param name="outputPath">Absolute path to the output doctest file</param>
        /// <returns>Outcome of the check</returns>
        public static Report Check(
            List<Extraction.Doctest> doctests,
            string relativeInputPath,
            string outputPath)
        {
            // Pre-condition(s)

            if (!Path.IsPathRooted(outputPath))
            {
                throw new ArgumentException($"Expected a rooted outputPath, but got: {outputPath}");
            }

            // Implementation

            if (doctests.Count == 0)
            {
                if (File.Exists(outputPath))
                {
                    return Report.ShouldNotExist;
                }

                return Report.Ok;
            }

            if (doctests.Count > 0 && !File.Exists(outputPath))
            {
                return Report.DoesntExist;
            }

            string identifier = Identifier(relativeInputPath);

            using var stringWriter = new System.IO.StringWriter();

            Generation.Generate(doctests, identifier, stringWriter);

            string expected = stringWriter.ToString();

            string got = File.ReadAllText(outputPath);

            return (got == expected)
                ? Report.Ok
                : Report.Different;
        }
    }
}