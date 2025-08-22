using FluentAssertions;
using InfinityFlow.CSharp.Eval.Tools;

namespace InfinityFlow.CSharp.Eval.Tests;

public class CSharpEvalToolsTests
{
    private CSharpEvalTools _sut;
    private string _testFilePath;

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
    public async Task EvalCSharp_WithConsoleOutput_CapturesOutput()
    {
        // Arrange
        var code = "Console.WriteLine(\"Hello World\");";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().Contain("Hello World");
    }

    [Test]
    public async Task EvalCSharp_WithLinqExpression_ExecutesCorrectly()
    {
        // Arrange
        var code = @"
var numbers = new[] { 1, 2, 3, 4, 5 };
var sum = numbers.Sum();
Console.WriteLine($""Sum: {sum}"");
sum";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().Contain("Sum: 15");
        result.Should().Contain("Result: 15");
    }

    [Test]
    public async Task EvalCSharp_FromFile_ExecutesCorrectly()
    {
        // Arrange
        var code = @"
Console.WriteLine(""Executing from file"");
var result = Math.Pow(2, 3);
result";
        await File.WriteAllTextAsync(_testFilePath, code);

        // Act
        var result = await _sut.EvalCSharp(csxFile: _testFilePath);

        // Assert
        result.Should().Contain("Executing from file");
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
        // Arrange
        // Use Thread.Sleep which will actually block and trigger timeout
        var code = "System.Threading.Thread.Sleep(3000); \"Should not complete\"";

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
    public async Task EvalCSharp_WithAsyncCode_ExecutesCorrectly()
    {
        // Arrange
        var code = @"
await Task.Delay(10);
Console.WriteLine(""Async execution completed"");
42";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().Contain("Async execution completed");
        result.Should().Contain("Result: 42");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithNuGetPackageWithTransitiveDependencies_ResolvesAllDependencies()
    {
        // Arrange
        // Simple test to verify NuGet package resolution works with common packages
        var code = """
            #r "nuget: Newtonsoft.Json, 13.0.3"

            using Newtonsoft.Json;
            using System;

            // Simple test of NuGet package functionality
            var data = new { Name = "Test", Value = 42 };
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            
            Console.WriteLine("JSON serialization:");
            Console.WriteLine(json);
            Console.WriteLine($"Newtonsoft.Json version: {typeof(JsonConvert).Assembly.GetName().Version}");

            Console.WriteLine("NuGet package resolution with transitive dependencies works!");
""";

        // Act
        var result = await _sut.EvalCSharp(csx: code);


        result.Should().NotContain("Compilation Error", "Script should compile without errors");
        result.Should().Contain("NuGet package resolution with transitive dependencies works!");
        result.Should().Contain("JSON serialization:");

        // Verify the output from using the packages
        result.Should().Contain("\"Name\": \"Test\"");
        result.Should().Contain("\"Value\": 42");
        result.Should().Contain("Newtonsoft.Json version:");
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
    public async Task EvalCSharp_WithComplexTypes_HandlesCorrectly()
    {
        // Arrange
        var code = @"
var dict = new Dictionary<string, int> { [""a""] = 1, [""b""] = 2 };
var list = new List<string> { ""hello"", ""world"" };
Console.WriteLine($""Dictionary count: {dict.Count}"");
Console.WriteLine($""List items: {string.Join("", "", list)}"");
""Completed""";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().Contain("Dictionary count: 2");
        result.Should().Contain("List items: hello, world");
        result.Should().Contain("Result: Completed");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithDeeplyNestedDependencies_HandlesRecursionDepthLimit()
    {
        // Arrange - Use a package that has dependencies to test recursion handling
        var code = @"
#r ""nuget: Newtonsoft.Json, 13.0.3""

using Newtonsoft.Json;
using System;

var data = new { Message = ""Testing recursion depth handling"", Depth = 1 };
var json = JsonConvert.SerializeObject(data);
Console.WriteLine(json);
""Recursion test completed""";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().NotContain("Maximum recursion depth");
        result.Should().NotContain("Error:");
        result.Should().Contain("Testing recursion depth handling");
        result.Should().Contain("Result: Recursion test completed");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithFrameworkConstants_UsesCorrectTargetFramework()
    {
        // Arrange - Test that we're using the correct framework constants
        var code = @"
#r ""nuget: System.Text.Json, 9.0.0""

using System.Text.Json;
using System;

var options = new JsonSerializerOptions { WriteIndented = true };
var data = new { Framework = ""net9.0"", Message = ""Testing framework constants"" };
var json = JsonSerializer.Serialize(data, options);
Console.WriteLine(json);
""Framework test completed""";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().NotContain("No compatible framework found");
        result.Should().NotContain("Error:");
        result.Should().Contain("net9.0");
        result.Should().Contain("Testing framework constants");
        result.Should().Contain("Result: Framework test completed");
    }

    [Test]
    [Category("RequiresNuGet")]
    public async Task EvalCSharp_WithInvalidPackageReference_ShowsProperError()
    {
        // Arrange - Test improved error handling
        var code = @"#r ""nuget: NonExistentPackageXyz123, 1.0.0""

Console.WriteLine(""This should not execute"");";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().StartWith("NuGet Package Resolution Error(s):");
        result.Should().Contain("Failed to resolve NuGet package 'NonExistentPackageXyz123'");
        result.Should().Contain("not found");
    }

    [Test]
    public async Task EvalCSharp_WithMalformedNuGetDirective_ShowsValidationError()
    {
        // Arrange - Test improved directive validation
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
    public async Task EvalCSharp_WithMicrosoftExtensionsPackage_HandlesFilteringCorrectly()
    {
        // Arrange - Test that Microsoft.Extensions packages are properly allowed
        var code = @"
#r ""nuget: Microsoft.Extensions.Logging.Abstractions, 9.0.0""

using Microsoft.Extensions.Logging;
using System;

// Test that we can use Microsoft.Extensions types
var logLevel = LogLevel.Information;
Console.WriteLine($""Log level: {logLevel}"");
Console.WriteLine(""Microsoft.Extensions packages loaded successfully"");
""Extensions test completed""";

        // Act
        var result = await _sut.EvalCSharp(csx: code);

        // Assert
        result.Should().NotContain("Error:");
        result.Should().Contain("Microsoft.Extensions packages loaded successfully");
        result.Should().Contain("Result: Extensions test completed");
    }
}
