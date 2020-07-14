using System;
using ArgumentException = System.ArgumentException;
using StringComparer = System.StringComparer;
using Path = System.IO.Path;

using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DoctestCsharp
{
    public static class Input
    {
        public class InputOutputOrError
        {
            public readonly List<(string, string)>? InputOutput;
            public readonly string? Error;

            public InputOutputOrError(List<(string, string)>? inputOutput, string? error)
            {
                if (inputOutput == null && error == null)
                {
                    throw new ArgumentException("Both inputOutput and error null");
                }

                if (inputOutput != null && error != null)
                {
                    throw new ArgumentException("Both inputOutput and error given");
                }

                InputOutput = inputOutput;
                Error = error;
            }
        }
        // TODO: document this behavior in the readme

        /// <summary>
        /// Parses the command-line arguments given as --input-output. 
        /// </summary>
        /// <param name="inputOutput">command-line arguments for --input-output</param>
        /// <param name="suffix">command-line argument --suffix to be appended if no output given</param>
        /// <returns>List of pairs (input, output)</returns>
        public static InputOutputOrError ParseInputOutput(string[] inputOutput, string suffix)
        {
            if (inputOutput.Length == 0)
            {
                return new InputOutputOrError(new List<(string, string)>(), null);
            }

            var result = new List<(string, string)>(inputOutput.Length);

            foreach (string pairStr in inputOutput)
            {
                string[] parts = pairStr.Split(Path.PathSeparator);

                switch (parts.Length)
                {
                    case 0:
                        return new InputOutputOrError(
                            null, "Expected at least an input, but got an empty string");
                    case 1:
                        result.Add((parts[0], parts[0] + suffix));
                        break;
                    case 2:

                        result.Add(
                            (
                                parts[0],
                                // Empty output implies automatic output.
                                (parts[1].Length == 0) ? parts[0] + suffix : parts[1]));
                        break;
                    default:
                        return new InputOutputOrError(
                            null,
                            $"Expected at most a pair, but got {parts.Length} parts " +
                            $"separated by {Path.PathSeparator} from the input-output: {pairStr}");
                }
            }

            // Post-condition
            if (result.Count != inputOutput.Length)
            {
                throw new InvalidOperationException(
                    $"Unexpected result.Count (== {result.Count}) != " +
                    $"inputOutput.Length (== {inputOutput.Length})");
            }

            return new InputOutputOrError(result, null);
        }

        /// <summary>
        /// Matches all the files defined by the patterns, includes and excludes.
        /// If any of the patterns is given as a relative directory,
        /// current working directory is prepended.
        /// </summary>
        /// <param name="cwd">current working directory</param>
        /// <param name="patterns">GLOB patterns to match files for inspection</param>
        /// <param name="excludes">GLOB patterns to exclude files matching patterns</param>
        /// <returns>Paths of the matched files</returns>
        public static IEnumerable<string> MatchFiles(
            string cwd,
            List<string> patterns,
            List<string> excludes)
        {
            ////
            // Pre-condition(s)
            ////

            if (cwd.Length == 0)
            {
                throw new ArgumentException("Expected a non-empty cwd");
            }

            if (!Path.IsPathRooted(cwd))
            {
                throw new ArgumentException("Expected cwd to be rooted");
            }

            ////
            // Implementation
            ////

            if (patterns.Count == 0)
            {
                yield break;
            }

            var globExcludes = excludes.Select(
                (pattern) =>
                {
                    string rootedPattern = (Path.IsPathRooted(pattern))
                        ? pattern
                        : Path.Join(cwd, pattern);

                    return new GlobExpressions.Glob(rootedPattern);
                }).ToList();

            foreach (var pattern in patterns)
            {
                IEnumerable<string>? files;

                if (Path.IsPathRooted(pattern))
                {
                    var root = Path.GetPathRoot(pattern);
                    if (root == null)
                    {
                        throw new ArgumentException(
                            $"Root could not be retrieved from rooted pattern: {pattern}");
                    }

                    var relPattern = Path.GetRelativePath(root, pattern);

                    files = GlobExpressions.Glob.Files(root, relPattern)
                        .Select((path) => Path.Join(root, path));
                }
                else
                {
                    files = GlobExpressions.Glob.Files(cwd, pattern);
                }

                List<string> accepted =
                    files
                        .Where((path) =>
                        {
                            string rootedPath = (Path.IsPathRooted(path))
                                ? path
                                : Path.Join(cwd, path);

                            return globExcludes.TrueForAll((glob) => !glob.IsMatch(rootedPath));
                        })
                        .ToList();

                accepted.Sort(StringComparer.InvariantCulture);

                foreach (string path in accepted)
                {
                    yield return path;
                }
            }
        }
    }
}