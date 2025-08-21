using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using ModelContextProtocol.Server;

namespace InfinityFlow.CSharp.Eval.Tools;

/// <summary>
/// MCP tool for evaluating and executing C# scripts using Roslyn.
/// </summary>
internal class CSharpEvalTools
{
    [McpServerTool]
    [Description("Evaluates and executes C# script code and returns the output. Can either execute code directly or from a file.")]
    public async Task<string> EvalCSharp(
        [Description("Full path to a .csx file to execute")] string? csxFile = null,
        [Description("C# script code to execute directly")] string? csx = null,
        [Description("Maximum execution time in seconds (default: 30)")] int timeoutSeconds = 30)
    {
        if (string.IsNullOrWhiteSpace(csxFile) && string.IsNullOrWhiteSpace(csx))
        {
            return "Error: Either csxFile or csx parameter must be provided.";
        }

        if (!string.IsNullOrWhiteSpace(csxFile) && !string.IsNullOrWhiteSpace(csx))
        {
            return "Error: Only one of csxFile or csx parameter should be provided, not both.";
        }

        string scriptCode;
        
        try
        {
            if (!string.IsNullOrWhiteSpace(csxFile))
            {
                // Validate and normalize the file path to prevent directory traversal
                try
                {
                    var fullPath = Path.GetFullPath(csxFile);
                    
                    // Ensure the file has .csx extension
                    if (!fullPath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Error: Only .csx files are allowed. Provided: {csxFile}";
                    }
                    
                    // Optional: Restrict to specific directories for additional security
                    // This can be configured via environment variable
                    var allowedPath = Environment.GetEnvironmentVariable("CSX_ALLOWED_PATH");
                    if (!string.IsNullOrEmpty(allowedPath))
                    {
                        var normalizedAllowedPath = Path.GetFullPath(allowedPath);
                        if (!fullPath.StartsWith(normalizedAllowedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            return $"Error: File access is restricted to {normalizedAllowedPath}";
                        }
                    }
                    
                    if (!File.Exists(fullPath))
                    {
                        return $"Error: File not found: {fullPath}";
                    }
                    
                    scriptCode = await File.ReadAllTextAsync(fullPath);
                }
                catch (Exception ex)
                {
                    return $"Error: Invalid file path: {ex.Message}";
                }
            }
            else
            {
                scriptCode = csx!;
            }

            // Create script options with common assemblies and imports
            var scriptOptions = ScriptOptions.Default
                .WithReferences(
                    typeof(object).Assembly,
                    typeof(Console).Assembly,
                    typeof(System.Linq.Enumerable).Assembly,
                    typeof(System.Text.StringBuilder).Assembly,
                    typeof(System.IO.File).Assembly,
                    typeof(System.Collections.Generic.List<>).Assembly,
                    typeof(System.Threading.Tasks.Task).Assembly,
                    typeof(System.Net.Http.HttpClient).Assembly,
                    typeof(System.Text.Json.JsonSerializer).Assembly,
                    typeof(System.Text.RegularExpressions.Regex).Assembly)
                .WithImports(
                    "System",
                    "System.IO",
                    "System.Linq",
                    "System.Text",
                    "System.Collections.Generic",
                    "System.Threading.Tasks",
                    "System.Net.Http",
                    "System.Text.Json",
                    "System.Text.RegularExpressions");

            // Capture console output
            var originalOut = Console.Out;
            var outputBuilder = new StringBuilder();
            
            try
            {
                using var stringWriter = new StringWriter(outputBuilder);
                Console.SetOut(stringWriter);

                // Execute the script with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var result = await CSharpScript.EvaluateAsync(scriptCode, scriptOptions, cancellationToken: cts.Token);

                // Add the result value if it's not null
                if (result != null)
                {
                    if (outputBuilder.Length > 0)
                    {
                        outputBuilder.AppendLine();
                    }
                    outputBuilder.AppendLine($"Result: {result}");
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            var output = outputBuilder.ToString();
            return string.IsNullOrWhiteSpace(output) ? "Script executed successfully with no output." : output;
        }
        catch (CompilationErrorException e)
        {
            return $"Compilation Error:\n{string.Join("\n", e.Diagnostics)}";
        }
        catch (OperationCanceledException)
        {
            return $"Error: Script execution timed out after {timeoutSeconds} seconds.";
        }
        catch (Exception e)
        {
            return $"Runtime Error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
        }
    }
}
