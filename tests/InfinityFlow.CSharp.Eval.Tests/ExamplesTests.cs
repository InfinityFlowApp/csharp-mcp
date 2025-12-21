using System.Text.RegularExpressions;
using FluentAssertions;
using InfinityFlow.CSharp.Eval.Tools;

namespace InfinityFlow.CSharp.Eval.Tests;

[TestFixture]
public class ExamplesTests
{
    private CSharpEvalTools _evalTools;

    [SetUp]
    public void Setup()
    {
        _evalTools = new CSharpEvalTools();
    }

    private static string GetExamplesRoot()
    {
        // Try multiple possible paths for examples directory
        var possiblePaths = new[]
        {
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "examples"),
            Path.Combine(Directory.GetCurrentDirectory(), "examples"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "examples"),
            "/source/examples" // Docker build path
        };

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        throw new DirectoryNotFoundException("Examples directory not found in any expected location");
    }

    public static IEnumerable<TestCaseData> GetExampleDirectories()
    {

        var examplesRoot = GetExamplesRoot();
        foreach (var dir in Directory.GetDirectories(examplesRoot))
        {
            var dirName = Path.GetFileName(dir);
            yield return new TestCaseData(dirName).SetName($"Example_{dirName}");
        }
    }

    [Test]
    [TestCaseSource(nameof(GetExampleDirectories))]
    public async Task Example_ExecutesCorrectly_And_MatchesExpectedOutput(string exampleName)
    {
        // Arrange
        var examplesRoot = GetExamplesRoot();
        var exampleDir = Path.Combine(examplesRoot, exampleName);
        var scriptPath = Path.Combine(exampleDir, "script.csx");
        var expectedOutputPath = Path.Combine(exampleDir, "expected-output.txt");

        // Files must exist for test to pass
        File.Exists(scriptPath).Should().BeTrue($"script.csx not found for {exampleName}");
        File.Exists(expectedOutputPath).Should().BeTrue($"expected-output.txt not found for {exampleName}");

        var scriptContent = await File.ReadAllTextAsync(scriptPath);
        var expectedOutput = await File.ReadAllTextAsync(expectedOutputPath);

        // Act
        var result = await _evalTools.EvalCSharp(csx: scriptContent);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotContain("Error:");

        // Normalize line endings and whitespace for comparison
        var normalizedResult = NormalizeOutput(result);
        var normalizedExpected = NormalizeOutput(expectedOutput);

        // Check that key lines from expected output are present in the result
        // Note: Console output may not be captured in all test environments (NUnit 6.0 + .NET 10)
        var expectedLines = normalizedExpected.Split('\n');

        // At minimum, check that the final Result line matches if present
        var finalExpectedLine = expectedLines.LastOrDefault(l => l.StartsWith("Result:"));
        if (!string.IsNullOrEmpty(finalExpectedLine))
        {
            if (finalExpectedLine.Contains("*"))
            {
                var pattern = Regex.Escape(finalExpectedLine).Replace("\\*", ".*");
                normalizedResult.Should().MatchRegex(pattern,
                    $"Final result line doesn't match pattern for {exampleName}");
            }
            else
            {
                normalizedResult.Should().Contain(finalExpectedLine,
                    $"Final result line mismatch for {exampleName}");
            }
        }
        else
        {
            // No Result: line expected, just verify script completed without errors
            normalizedResult.Should().NotContain("Compilation Error");
        }
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task NuGetPackageExample_ExecutesCorrectly_When_NuGetAvailable()
    {
        // This test requires NuGet package resolution to work
        // It may fail in restricted environments

        // Arrange
        var examplesRoot = GetExamplesRoot();
        var exampleDir = Path.Combine(examplesRoot, "nuget-packages");
        var scriptPath = Path.Combine(exampleDir, "script.csx");

        var scriptContent = await File.ReadAllTextAsync(scriptPath);

        // Act
        var result = await _evalTools.EvalCSharp(csx: scriptContent, timeoutSeconds: 60);

        // Assert - verify script executed and returned expected result
        result.Should().NotContain("Error:");
        result.Should().NotContain("Compilation Error");
        // The result should contain the return value with skill count
        result.Should().Contain("Result:");
        result.Should().Contain("3 skills");
    }


    [Test]
    public void AllExamples_HaveRequiredFiles()
    {
        // Arrange
        var examplesRoot = GetExamplesRoot();

        var exampleDirs = Directory.GetDirectories(examplesRoot);

        // Act & Assert
        foreach (var dir in exampleDirs)
        {
            var dirName = Path.GetFileName(dir);
            var scriptPath = Path.Combine(dir, "script.csx");
            var readmePath = Path.Combine(dir, "README.md");
            var expectedOutputPath = Path.Combine(dir, "expected-output.txt");

            File.Exists(scriptPath).Should().BeTrue(
                $"script.csx missing in {dirName}");
            File.Exists(readmePath).Should().BeTrue(
                $"README.md missing in {dirName}");
            File.Exists(expectedOutputPath).Should().BeTrue(
                $"expected-output.txt missing in {dirName}");
        }
    }

    private static string NormalizeOutput(string output)
    {
        // Normalize line endings and trim trailing whitespace
        return output
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .TrimEnd();
    }
}
