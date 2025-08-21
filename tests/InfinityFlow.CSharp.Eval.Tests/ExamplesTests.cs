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

    public static IEnumerable<TestCaseData> GetExampleDirectories()
    {
        var examplesRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "examples");
        if (Directory.Exists(examplesRoot))
        {
            foreach (var dir in Directory.GetDirectories(examplesRoot))
            {
                var dirName = Path.GetFileName(dir);
                // Skip nuget-packages as it's tested separately with RequiresNuGet category
                if (dirName != "nuget-packages")
                {
                    yield return new TestCaseData(dirName).SetName($"Example_{dirName}");
                }
            }
        }
    }

    [Test]
    [TestCaseSource(nameof(GetExampleDirectories))]
    public async Task Example_ExecutesCorrectly_And_MatchesExpectedOutput(string exampleName)
    {
        // Arrange
        var examplesRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "examples");
        var exampleDir = Path.Combine(examplesRoot, exampleName);
        var scriptPath = Path.Combine(exampleDir, "script.csx");
        var expectedOutputPath = Path.Combine(exampleDir, "expected-output.txt");

        // Skip if files don't exist (running in CI or different environment)
        if (!File.Exists(scriptPath) || !File.Exists(expectedOutputPath))
        {
            Assert.Ignore($"Example files not found for {exampleName}");
            return;
        }

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

        // Check each line, allowing wildcards (*) in expected output
        var resultLines = normalizedResult.Split('\n');
        var expectedLines = normalizedExpected.Split('\n');

        resultLines.Should().HaveCount(expectedLines.Length,
            $"Output line count mismatch for {exampleName}");

        for (int i = 0; i < expectedLines.Length; i++)
        {
            if (expectedLines[i].Contains("*"))
            {
                // Convert wildcard pattern to regex
                var pattern = Regex.Escape(expectedLines[i]).Replace("\\*", ".*");
                resultLines[i].Should().MatchRegex($"^{pattern}$",
                    $"Line {i + 1} doesn't match pattern for {exampleName}");
            }
            else
            {
                resultLines[i].Should().Be(expectedLines[i],
                    $"Line {i + 1} mismatch for {exampleName}");
            }
        }
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task NuGetPackageExample_ExecutesCorrectly_When_NuGetAvailable()
    {
        // This test requires NuGet package resolution to work
        // It may fail in restricted environments

        // Arrange
        var examplesRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "examples");
        var exampleDir = Path.Combine(examplesRoot, "nuget-packages");
        var scriptPath = Path.Combine(exampleDir, "script.csx");
        var expectedOutputPath = Path.Combine(exampleDir, "expected-output.txt");

        // Skip if files don't exist
        if (!File.Exists(scriptPath) || !File.Exists(expectedOutputPath))
        {
            Assert.Ignore("NuGet example files not found");
            return;
        }

        var scriptContent = await File.ReadAllTextAsync(scriptPath);

        // Act
        var result = await _evalTools.EvalCSharp(csx: scriptContent, timeoutSeconds: 60);

        // Assert
        if (result.Contains("Failed to resolve NuGet package") || result.Contains("CS0006"))
        {
            Assert.Ignore("NuGet package resolution not available in this environment");
            return;
        }

        result.Should().NotContain("Error:");
        result.Should().Contain("NuGet Package Example");
        result.Should().Contain("Newtonsoft.Json");
        result.Should().Contain("John Doe");
        result.Should().Contain("Successfully processed JSON");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithMultipleNuGetPackages_ExecutesCorrectly()
    {
        // Arrange
        var script = @"
#r ""nuget: Humanizer, 2.14.1""
#r ""nuget: Newtonsoft.Json, 13.0.3""

using Humanizer;
using Newtonsoft.Json;

var data = new { 
    Message = ""Hello World"",
    Count = 5,
    Timestamp = DateTime.Now
};

var json = JsonConvert.SerializeObject(data, Formatting.Indented);
Console.WriteLine(""Serialized JSON:"");
Console.WriteLine(json);

Console.WriteLine($""\n'{data.Count} items' humanized: {data.Count.ToWords()} items"");
Console.WriteLine($""'2 hours' humanized: {""{0:hh\\:mm\\:ss}"".FormatWith(TimeSpan.FromHours(2))}"");

""Multiple NuGet packages loaded successfully!""
";

        // Act
        var result = await _evalTools.EvalCSharp(csx: script);

        // Assert
        if (result.Contains("Failed to resolve NuGet package"))
        {
            Assert.Ignore("NuGet package resolution not available in this environment");
            return;
        }

        result.Should().NotContain("Error:", "Script should execute without errors");
        result.Should().Contain("Serialized JSON:");
        result.Should().Contain("Hello World");
        result.Should().Contain("'5 items' humanized: five items");
        result.Should().Contain("'2 hours' humanized: 02:00:00");
        result.Should().Contain("Result: Multiple NuGet packages loaded successfully!");
    }

    [Test]
    public void AllExamples_HaveRequiredFiles()
    {
        // Arrange
        var examplesRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "examples");

        if (!Directory.Exists(examplesRoot))
        {
            Assert.Ignore("Examples directory not found");
            return;
        }

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
