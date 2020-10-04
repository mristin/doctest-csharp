# doctest-csharp
![Check](
https://github.com/mristin/doctest-csharp/workflows/Check/badge.svg
) [![Coverage Status](
https://coveralls.io/repos/github/mristin/doctest-csharp/badge.svg)](
https://coveralls.io/github/mristin/doctest-csharp
) [![Nuget](
https://img.shields.io/nuget/v/DoctestCsharp)](
https://www.nuget.org/packages/DoctestCsharp
)

Doctest-csharp extracts the `<code>...</code>` snippets from the structured 
comments and generates the corresponding test files (so called "doctests").

## Motivation

It is important to test your documented code snippets to avoid [the code rot](
https://en.wikipedia.org/wiki/Software_rot
).
Otherwise, future changes to the code base might render the recorded 
instructions in those documentation snippets invalid. This is both confusing 
and misleading since the reader is not sure anymore whether it is a mistake 
in the documentation, a bug in the code or something else.

Such a tool is an established standard in many programming languages (
*e.g.*, Python's [doctest](
https://docs.python.org/3/library/doctest.html
)), but somewhat surprisingly as of this writing in July 2020, C# lacks it.
Therefore we decided to roll out doctest-csharp.

## Related Projects

**Doctest for F#**. There exists [doctest for F#](
https://github.com/moodmosaic/doctest/
) which is almost identical to doctest-csharp, just for F#.

**[Dotnet-try](https://github.com/dotnet/try)** goes the other way. The code 
examples are written in separate source files and dotnet-try inlines them into
the documentation markdown files. As of July 2020, dotnet-try did not handle 
code examples in the structured comments. Hence it is orthogonal to 
doctest-csharp: use dotnet-try for snippets in the documentation you manually 
write; use doctest-csharp to test your code examples in the structured comments.

## Installation

Doctest-csharp is distributed and run as a dotnet tool.

To install it globally:

```bash
dotnet tool -g DoctestCsharp
```

or locally (if you use tool manifest, see [this Microsoft tutorial](
https://docs.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use
)):

```bash
dotnet tool install DoctestCsharp
```

## Usage

**Overview.** To obtain an overview of the command-line arguments, use `--help`:

```bash
dotnet doctest-csharp --help
```
<!--- Help starts. -->
```
DoctestCsharp:
  Generates tests from the embedded code snippets in the code documentation.

Usage:
  DoctestCsharp [options]

Options:
  --input-output <input-output> (REQUIRED)    Input and output directory pairs containing the *.cs files The input is separated from the output by the PATH separator (e.g., ';' on Windows, ':' on POSIX).If no output is specified, the --suffix is appended to the input to automatically obtain the output.
  -s, --suffix <suffix>                       Suffix to be automatically appended to the input to obtain the output directory in cases where no explicit output directory was given [default: .Tests]
  -e, --excludes <excludes>                   Glob patterns of the files to be excluded from the input. The exclude patterns are either absolute (e.g., rooted with '/') or relative. In case of relative exclude patterns, they are relative to the _input_ directory and NOT to the current working directory.
  -c, --check                                 If set, does not generate any files, but only checks that the content of the test files coincides with what would be generated. This is particularly useful in continuous integration pipelines if you want to check if all the files have been scanned and correctly generated.
  --verbose                                   If set, outputs only important messages to the users such as errors
  --version                                   Show version information
  -?, -h, --help                              Show help and usage information
```
<!--- Help ends. -->


**Mark the code snippets.** You need to explicitly mark which `<code>...</code>`
snippets in your documentation should be tested by adding the `doctest` 
attribute and setting it to `true`.

For example:

```cs
/// <code doctest="true">
/// DoSomething("xyz")
/// </code>
``` 

**Input and output**. You need to specify one or more pairs consisting of 
a project folder ("input") and a test folder ("output"). The pair is given 
as a concatenation `{input}{PATH separator}{output}`. For example, on a Linux 
system the input-output pair might be something like: 
`SomeProject:SomeProject.Tests`. The same input-output pair in Windows is:
`SomeProject;SomeProject.Tests`.

If you omit the output (*e.g.*, `SomeProject:` on Linux), the output is 
automatically inferred by appending the `--suffix` command-line argument.
The default `--suffix` is `.Tests`.

Doctest-csharp will scan all the `**/*.cs` files in the input folder and 
generate the unit tests in the output folder.

The relative paths will be preserved. The resulting doctest files will be 
prefixed with `DocTest`.

For example:

```bash
dotnet doctest-csharp \
    --input-output SomeProject:SomeProject.Tests/doctests
```

Assume there exists `SomeProject/SomeFile.cs`. Doctest-csharp will scan it
and generate the corresponding doctests to 
`SomeProject.Tests/doctests/DocTestSomeFile.cs`. 

In case you have multiple projects all following the same naming convention,
you can specify `--suffix` and pass in multiple input-only pairs. For example:

```bash
dotnet doctest-csharp \
    --input-output \
        SomeProject: \
        AnotherProject: \
    --suffix ".Tests/doctests"
```

The corresponding inferred outputs will be `SomeProject.Tests/doctests` and
`AnotherProject.Tests/doctests`, respectively.

**Exclude**. If you want to exclude certain files from the scan, use `--exclude`
with a Glob pattern.

For example:

```bash
dotnet doctest-csharp \
    --input-output SomeProject:
    --exclude '**/obj/**'
```

**Using directives**. You can specify [using directives](
https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive
) in the "header" of the snippet and separate them with `// ---` from the body
of the doctest. 

Only using directives, and no other statements, are allowed in the header.

For example:

```cs
/// <code doctest="true">
/// using IO = System.IO;
/// using SomeNamespace;
/// // ---
/// DoSomething(
///     new SomeNamespace.SomeClass(
///         new IO.DirectoryInfo("xyz")));
/// </code>
``` 

**Generated code**. We heavily use [NUnit 3](https://nunit.org/) in other
projects, so we decided to base the generated code on that framework.
Please let us know by [submitting a new issue](
https://github.com/mristin/doctest-csharp/issues/new
) if you would like to use it with a different testing framework.

The tests will live in the same namespace as the code snippets suffixed by
`.Test`. The doctests will be identified by line and column in the source
file.

The using directives of the individual code snippets will be de-duplicated
and `NUnit.Framework` will be automatically added.

For example, assume the following code snippet in `SomeProgram.cs`:

```cs
namespace Some.Namespace
{
    // ...

    /// <code doctest="true">
    /// using Another = AnotherNamespace;
    /// // ---
    /// Assert.AreEqual(
    ///     "abc", 
    ///     TransformSomehow(
    ///         Another.Parse("xyz")));
    /// </code>

    // ...
}
``` 

The generated test code will be similar to:

```cs
using Another = AnotherNamespace;

using NUnit.Framework;

namespace Some.Namespace.Tests
{
    // ...

    public class DocTest_SomeProgram_cs
    {
        public void AtLine123AndColumn8() {
            Assert.AreEqual(
                "abc", 
                TransformSomehow(
                    Another.Parse("xyz")));
        }
    }

    // ...
}
```

**Check against a dry run.** You can use doctest-csharp with `--check`
to verify that all the available test files have been generated as in a dry run
(and no obsolete test files remain). 

This is particularly useful in continuous integrations where you check in your 
doctests in the version control and want to make sure that the developer 
re-generated the doctests appropriately.

For example:

```bash
dotnet doctest-csharp \
    --input-output SomeProject:
    --check
```

## Contributing

Feature requests, bug reports *etc.* are highly welcome! Please [submit
a new issue](
https://github.com/mristin/doctest-csharp/issues/new
).

If you want to contribute in code, please see
[CONTRIBUTING.md](CONTRIBUTING.md).

## Versioning

We follow [Semantic Versioning](
http://semver.org/spec/v1.0.0.html
).
The version X.Y.Z indicates:

* X is the major version (backward-incompatible w.r.t. command-line arguments),
* Y is the minor version (backward-compatible), and
* Z is the patch version (backward-compatible bug fix).

Alpha and beta pre-release versions are suffixed (*e.g.*, 1.0.0-beta3) and 
do not follow the semantic versioning (*i.e.*, final release version might be
different).
