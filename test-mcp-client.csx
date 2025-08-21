#!/usr/bin/env dotnet-script

#r "nuget: ModelContextProtocol, 0.3.0-preview.2"
#r "nuget: Microsoft.Extensions.Logging.Console, 10.0.0-preview.7.25380.108"

using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

// Set up logging
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger("TestClient");

// Create process to run the Docker container
var processInfo = new ProcessStartInfo
{
    FileName = "docker",
    Arguments = "run -i --rm -v $HOME:$HOME -w $PWD csharp-mcp-test",
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

logger.LogInformation("Starting Docker container...");
var process = Process.Start(processInfo);

// Create MCP client
var client = new McpClient(
    process.StandardOutput.BaseStream,
    process.StandardInput.BaseStream,
    loggerFactory
);

try
{
    // Initialize the client
    logger.LogInformation("Initializing MCP client...");
    var initResult = await client.InitializeAsync(new ClientInfo
    {
        Name = "TestClient",
        Version = "1.0.0"
    });
    logger.LogInformation($"Server: {initResult.ServerInfo.Name} v{initResult.ServerInfo.Version}");

    // List available tools
    logger.LogInformation("Listing available tools...");
    var tools = await client.ListToolsAsync();
    foreach (var tool in tools.Tools)
    {
        logger.LogInformation($"  Tool: {tool.Name} - {tool.Description}");
    }

    // Test 1: Simple C# expression
    logger.LogInformation("\nTest 1: Simple C# expression");
    var result1 = await client.CallToolAsync("eval_c_sharp", new
    {
        csx = "1 + 2 + 3"
    });
    logger.LogInformation($"Result: {result1.Content.First().Text}");

    // Test 2: Console output
    logger.LogInformation("\nTest 2: Console output");
    var result2 = await client.CallToolAsync("eval_c_sharp", new
    {
        csx = @"
Console.WriteLine(""Hello from MCP!"");
return ""Test completed"";"
    });
    logger.LogInformation($"Result: {result2.Content.First().Text}");

    // Test 3: File execution (with mounted volume)
    logger.LogInformation("\nTest 3: File execution");
    var currentDir = Environment.GetEnvironmentVariable("PWD") ?? Directory.GetCurrentDirectory();
    var result3 = await client.CallToolAsync("eval_c_sharp", new
    {
        csxFile = Path.Combine(currentDir, "test-script.csx")
    });
    logger.LogInformation($"Result: {result3.Content.First().Text}");

    // Test 4: NuGet package reference
    logger.LogInformation("\nTest 4: NuGet package reference");
    var result4 = await client.CallToolAsync("eval_c_sharp", new
    {
        csx = @"
#r ""nuget: Newtonsoft.Json, 13.0.3""
using Newtonsoft.Json;

var obj = new { Name = ""Test"", Value = 42 };
var json = JsonConvert.SerializeObject(obj);
Console.WriteLine($""JSON: {json}"");
return json;"
    });
    logger.LogInformation($"Result: {result4.Content.First().Text}");

    logger.LogInformation("\nAll tests completed successfully!");
}
catch (Exception ex)
{
    logger.LogError(ex, "Error during testing");
}
finally
{
    // Clean up
    process.Kill();
    process.WaitForExit();
    process.Dispose();
}