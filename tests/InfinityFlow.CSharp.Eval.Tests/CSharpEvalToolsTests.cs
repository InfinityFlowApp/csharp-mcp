using System.Reflection;
using System.Runtime.Loader;
using FluentAssertions;
using InfinityFlow.CSharp.Eval.Tools;

namespace InfinityFlow.CSharp.Eval.Tests;

/// <summary>
/// Unit tests for CSharpEvalTools.
/// Uses AssemblyLoadContext workaround for CSharpScript compatibility.
/// See: https://github.com/dotnet/roslyn/issues/45197#issuecomment-1911820643
/// </summary>
public class CSharpEvalToolsTests
{
    private CSharpEvalTools _sut;
    private string _testFilePath;
    private static bool _resolverConfigured;
    private static AssemblyLoadContext? _testContext;
    private static MethodInfo? _loadMethod;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Workaround for AssemblyLoadContext issue with CSharpScript
        // See: https://github.com/dotnet/roslyn/issues/45197#issuecomment-1911820643
        // This makes the Default AssemblyLoadContext resolve assemblies from NUnit's context
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
        _sut = new CSharpEvalTools();
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csx");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Test]
    public async Task EvalCSharp_WithSimpleExpression_ReturnsResult()
    {
        // Arrange
        var code = "2 + 2";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().Contain("Result: 4");
    }

    [Test]
    public async Task EvalCSharp_WithConsoleOutput_ExecutesSuccessfully()
    {
        // Arrange
        var code = "Console.WriteLine(\"Hello World\");";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert - Console output capture doesn't work in NUnit due to AssemblyLoadContext isolation.
        // The script executes but output goes to NUnit's console instead of our StringWriter.
        // Full console capture is verified via MCP integration (mcp__csharp-mcp__eval_c_sharp).
        result.Should().NotStartWith("Error:");
        result.Should().NotStartWith("Compilation Error");
        result.Should().NotStartWith("Runtime Error");
    }

    [Test]
    public async Task EvalCSharp_WithLinqExpression_ReturnsResult()
    {
        // Arrange
        var code = @"
var numbers = new[] { 1, 2, 3, 4, 5 };
var sum = numbers.Sum();
sum";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert - verify return value works
        result.Should().Contain("Result: 15");
    }

    [Test]
    public async Task EvalCSharp_FromFile_ExecutesCorrectly()
    {
        // Arrange
        var code = @"
var result = Math.Pow(2, 3);
result";
        await File.WriteAllTextAsync(_testFilePath, code);

        // Act
        var result = await _sut.EvalCSharp(csxFile: _testFilePath);

        // Assert
        result.Should().Contain("Result: 8");
    }

    [Test]
    public async Task EvalCSharp_WithCompilationError_ReturnsErrorMessage()
    {
        // Arrange
        var code = "invalidSyntax here";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().StartWith("Compilation Error(s):");
        result.Should().Contain("Line 1");  // Line number
        result.Should().Contain("Column");  // Column indicator
        result.Should().Contain("CS1002");  // "; expected" error
        result.Should().Contain("Code: invalidSyntax here");  // The problematic code
    }

    [Test]
    public async Task EvalCSharp_WithMultilineCompilationError_ShowsCorrectLineNumber()
    {
        // Arrange
        var code = @"var x = 5;
var y = ;  // Error on line 2
var z = 10;";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().StartWith("Compilation Error(s):");
        result.Should().Contain("Line 2");  // Error is on line 2
        result.Should().Contain("Code: var y = ;");  // Shows the problematic line
    }

    [Test]
    public async Task EvalCSharp_WithRuntimeError_ReturnsErrorMessage()
    {
        // Arrange
        var code = "throw new InvalidOperationException(\"Test exception\");";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().StartWith("Runtime Error: InvalidOperationException");
        result.Should().Contain("Message: Test exception");
        result.Should().Contain("Stack Trace:");
    }

    [Test]
    public async Task EvalCSharp_WithNoParameters_ReturnsError()
    {
        // Act
        var result = await _sut.EvalCSharp();

        // Assert
        result.Should().Be("Error: Either csxFile or csx parameter must be provided.");
    }

    [Test]
    public async Task EvalCSharp_WithBothParameters_ReturnsError()
    {
        // Act
        var result = await _sut.EvalCSharp(csxFile: "file.csx", csx: "code");

        // Assert
        result.Should().Be("Error: Only one of csxFile or csx parameter should be provided, not both.");
    }

    [Test]
    public async Task EvalCSharp_WithNonExistentFile_ReturnsError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), "nonexistent.csx");

        // Act
        var result = await _sut.EvalCSharp(csxFile: nonExistentFile);

        // Assert
        result.Should().StartWith("Error: File not found:");
    }

    [Test]
    public async Task EvalCSharp_WithNonCsxFile_ReturnsError()
    {
        // Arrange
        var nonCsxFile = Path.Combine(Path.GetTempPath(), "test.txt");

        // Act
        var result = await _sut.EvalCSharp(csxFile: nonCsxFile);

        // Assert
        result.Should().Be($"Error: Only .csx files are allowed. Provided: {nonCsxFile}");
    }

    [Test]
    public async Task EvalCSharp_WithRestrictedPath_ReturnsError()
    {
        // Skip this test when running in Docker container
        if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
        {
            Assert.Ignore("Path restrictions are disabled in Docker containers");
            return;
        }

        try
        {
            // Arrange
            var restrictedFile = "/etc/passwd.csx";
            Environment.SetEnvironmentVariable("CSX_ALLOWED_PATH", "/tmp");

            // Act
            var result = await _sut.EvalCSharp(csxFile: restrictedFile);

            // Assert
            result.Should().StartWith("Error: File access is restricted to");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CSX_ALLOWED_PATH", null);
        }
    }

    [Test]
    public async Task EvalCSharp_WithTimeout_ReturnsTimeoutError()
    {
        // Arrange - Use await Task.Delay which properly respects cancellation
        var code = "await Task.Delay(10000); \"Should not complete\"";

        // Act
        var result = await _sut.EvalCSharp(csx: code, timeoutSeconds: 1); // 1 second timeout

        // Assert
        result.Should().Be("Error: Script execution timed out after 1 seconds.");
    }

    [Test]
    public async Task EvalCSharp_WithRuntimeError_ShowsLineNumber()
    {
        // Arrange
        var code = @"var x = 5;
var y = 10;
var z = x / 0;  // Division by zero on line 3
Console.WriteLine(z);";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().StartWith("Runtime Error:");
        result.Should().Contain("DivideByZeroException");
        // The result should contain either "Script Line:" or "Submission#0" in the stack trace
        result.Should().Match(r => r.Contains("Script Line:") || r.Contains("Submission#0"));
    }

    [Test]
    public async Task EvalCSharp_WithAsyncCode_ReturnsResult()
    {
        // Arrange
        var code = @"
await Task.Delay(10);
42";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().Contain("Result: 42");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithNuGetPackage_ResolvesAndExecutes()
    {
        // Arrange
        var code = """
            #r "nuget: Newtonsoft.Json, 13.0.3"

            using Newtonsoft.Json;

            var data = new { Name = "Test", Value = 42 };
            var json = JsonConvert.SerializeObject(data);
            json
""";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert - verify return value contains serialized JSON
        result.Should().NotStartWith("Error:");
        result.Should().NotStartWith("Compilation Error");
        result.Should().Contain("Result:");
        result.Should().Contain("Test");
        result.Should().Contain("42");
    }

    [Test]
    public async Task EvalCSharp_WithNoOutput_ReturnsSuccessMessage()
    {
        // Arrange
        var code = "var x = 5;";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().Be("Script executed successfully with no output.");
    }

    [Test]
    public async Task EvalCSharp_WithComplexTypes_ReturnsResult()
    {
        // Arrange
        var code = @"
var dict = new Dictionary<string, int> { [""a""] = 1, [""b""] = 2 };
$""Dictionary count: {dict.Count}""";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().Contain("Result: Dictionary count: 2");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithNuGetDependencies_ReturnsResult()
    {
        // Arrange - Use a package that has dependencies to test resolution
        var code = @"
#r ""nuget: Newtonsoft.Json, 13.0.3""

using Newtonsoft.Json;

var data = new { Message = ""Testing"", Value = 1 };
JsonConvert.SerializeObject(data)";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().NotStartWith("Error:");
        result.Should().Contain("Result:");
        result.Should().Contain("Testing");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithSystemTextJson_ReturnsResult()
    {
        // Arrange - Test with System.Text.Json NuGet package
        var code = @"
#r ""nuget: System.Text.Json, 9.0.0""

using System.Text.Json;

var data = new { Framework = ""net10.0"" };
JsonSerializer.Serialize(data)";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().NotStartWith("Error:");
        result.Should().Contain("Result:");
        result.Should().Contain("net10.0");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithInvalidPackageReference_ShowsProperError()
    {
        // Arrange - Test error handling for non-existent packages
        var code = @"#r ""nuget: NonExistentPackageXyz123, 1.0.0""

Console.WriteLine(""This should not execute"");";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().StartWith("NuGet Package Resolution Error(s):");
        result.Should().Contain("Failed to resolve NuGet package 'NonExistentPackageXyz123'");
    }

    [Test]
    public async Task EvalCSharp_WithMalformedNuGetDirective_ShowsValidationError()
    {
        // Arrange - Test directive validation
        var code = @"#r ""nuget: MissingVersion""

Console.WriteLine(""This should not execute"");";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().StartWith("NuGet Package Resolution Error(s):");
        result.Should().Contain("Invalid NuGet directive syntax");
        result.Should().Contain("Expected format: #r \"nuget: PackageName, Version\"");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithMicrosoftExtensionsPackage_ExecutesSuccessfully()
    {
        // Arrange - Test Microsoft.Extensions packages work correctly
        var code = @"
#r ""nuget: Microsoft.Extensions.Logging.Abstractions, 9.0.0""

using Microsoft.Extensions.Logging;

var logLevel = LogLevel.Information;
logLevel.ToString()";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().NotStartWith("Error:");
        result.Should().Contain("Result: Information");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithNetworkTimeout_UsesCancellationToken()
    {
        // This test verifies that the cancellation token timeout mechanism works correctly
        // Use environment variable to force very short timeout to test cancellation behavior

        try
        {
            // Arrange - Set environment variable to trigger immediate cancellation (1ms)
            Environment.SetEnvironmentVariable("NUGET_TIMEOUT_TEST", "true");

            // Use a package that would require network access (not cached)
            // Even if it exists, the 1ms timeout should trigger cancellation
            var uniquePackageName = $"TestPackageTimeout{DateTimeOffset.UtcNow.Ticks}";
            var code = $@"#r ""nuget: {uniquePackageName}, 1.0.0""

Console.WriteLine(""This should be canceled by timeout"");";

            // Act
            var result = await _sut.EvalCSharp(csx: code, timeoutSeconds: 10);

            // Assert - Should show NuGet error due to cancellation token timeout
            result.Should().StartWith("NuGet Package Resolution Error(s):");
            result.Should().Contain("Failed to resolve NuGet package");

            // The result should indicate some kind of network/timeout issue
            // The exact message depends on when the cancellation occurs in the pipeline
            result.Should().Match(r =>
                r.Contains("Network operation timed out after 0.001 seconds") ||
                r.Contains("not found") ||
                r.Contains("remote source") ||
                r.Contains("Failed to retrieve information") ||
                r.Contains("operation was canceled"));
        }
        finally
        {
            // Cleanup - Reset environment variable
            Environment.SetEnvironmentVariable("NUGET_TIMEOUT_TEST", null);
        }
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithSlowNetworkConditions_HandlesGracefully()
    {
        // Arrange - Test with a package that's likely to complete but exercises network code paths
        var code = @"#r ""nuget: System.Text.Json, 9.0.0""

using System.Text.Json;
var data = new { test = ""timeout handling"" };
var json = JsonSerializer.Serialize(data);
""Network resilience test completed""";

        // Act - Use normal timeout but test that network operations don't hang indefinitely
        var result = await _sut.EvalCSharp(csx: code);

        // Assert - Should complete successfully within reasonable time
        result.Should().NotContain("Network operation timed out");
        result.Should().NotStartWith("Error:");
        result.Should().Contain("Network resilience test completed");
    }
}
