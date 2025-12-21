using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using FluentAssertions;
using InfinityFlow.CSharp.Eval.Tools;

namespace InfinityFlow.CSharp.Eval.Tests;

/// <summary>
/// Tests that verify example scripts execute correctly.
///
/// Note: Due to AssemblyLoadContext differences between NUnit and CSharpScript,
/// Console.SetOut doesn't propagate to CSharpScript's execution context in tests.
/// These tests verify scripts execute without errors and return expected values.
/// Full output validation (including console output) is done via the MCP server.
/// See: https://github.com/dotnet/roslyn/issues/45197#issuecomment-1911820643
/// </summary>
[TestFixture]
public class ExamplesTests
{
    private CSharpEvalTools _evalTools;
    private static bool _resolverConfigured;
    private static AssemblyLoadContext? _testContext;
    private static MethodInfo? _loadMethod;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Workaround for AssemblyLoadContext issue with CSharpScript
        if (!_resolverConfigured)
        {
            _testContext = AssemblyLoadContext.GetLoadContext(GetType().Assembly);
            if (_testContext != null && _testContext != AssemblyLoadContext.Default)
            {
                _loadMethod = typeof(AssemblyLoadContext).GetMethod("Load", BindingFlags.Instance | BindingFlags.NonPublic);
                AssemblyLoadContext.Default.Resolving += ResolveFromTestContext;
            }
            _resolverConfigured = true;
        }
    }

    private static Assembly? ResolveFromTestContext(AssemblyLoadContext context, AssemblyName name)
    {
        if (context == _testContext || _loadMethod == null || _testContext == null)
        {
            return null;
        }
        return _loadMethod.Invoke(_testContext, new object[] { name }) as Assembly;
    }

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
    public async Task Example_ExecutesWithoutErrors(string exampleName)
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

        // Assert - Verify no errors occurred
        result.Should().NotBeNull();
        result.Should().NotStartWith("Error:", $"Example {exampleName} failed with error");
        result.Should().NotStartWith("Compilation Error", $"Example {exampleName} had compilation errors");
        result.Should().NotStartWith("Runtime Error", $"Example {exampleName} had runtime errors");
        result.Should().NotStartWith("NuGet Package Resolution Error", $"Example {exampleName} had NuGet errors");

        // Verify the result contains the expected return value if present in expected output
        var expectedResultLine = expectedOutput
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("Result:"));

        if (expectedResultLine != null)
        {
            // Extract the expected result value (handle wildcards)
            var expectedValue = expectedResultLine.Substring("Result:".Length).Trim();
            if (expectedValue.Contains("*"))
            {
                // Wildcard pattern - verify result contains "Result:" prefix
                result.Should().Contain("Result:", $"Example {exampleName} should return a result");
            }
            else
            {
                // Exact match expected
                result.Should().Contain(expectedResultLine, $"Example {exampleName} should match expected result");
            }
        }
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
}
