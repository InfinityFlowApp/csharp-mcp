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
    
    [Test]
    [TestCase("basic-execution")]
    [TestCase("fibonacci-sequence")]
    [TestCase("data-processing")]
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
        if (result.Contains("Failed to resolve NuGet package"))
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
    public async Task AllExamples_HaveRequiredFiles()
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