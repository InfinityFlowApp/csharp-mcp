using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using ModelContextProtocol.Server;

namespace InfinityFlow.CSharp.Eval.Tools;

/// <summary>
/// MCP tool for evaluating and executing C# scripts using Roslyn.
/// </summary>

[McpServerToolType]
public class CSharpEvalTools
{
    [McpServerTool]
    [Description("Evaluates and executes C# script code and returns the output. Can either execute code directly or from a file. Supports NuGet package references using #r \"nuget: PackageName, Version\" directives.")]
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
                    // Only apply this restriction when NOT running in Docker (Docker has volume mounts)
                    var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
                    var allowedPath = Environment.GetEnvironmentVariable("CSX_ALLOWED_PATH");

                    if (!isDocker && !string.IsNullOrEmpty(allowedPath))
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

            // Resolve NuGet packages if any and remove #r directives from script
            var (nugetReferences, errors) = await NuGetPackageResolver.ResolvePackagesAsync(scriptCode);

            // If there were errors resolving packages, return them
            if (errors.Count > 0)
            {
                var errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("NuGet Package Resolution Error(s):");
                errorBuilder.AppendLine();
                foreach (var error in errors)
                {
                    errorBuilder.AppendLine($"  {error}");
                }
                return errorBuilder.ToString().TrimEnd();
            }

            // Remove #r directives from the script since we're handling them separately
            var cleanedScript = System.Text.RegularExpressions.Regex.Replace(
                scriptCode,
                @"^\s*#r\s+""nuget:[^""]*"".*$",
                "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

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
                .AddReferences(nugetReferences)
                .WithImports(
                    "System",
                    "System.IO",
                    "System.Linq",
                    "System.Text",
                    "System.Collections.Generic",
                    "System.Threading.Tasks",
                    "System.Net.Http",
                    "System.Text.Json",
                    "System.Text.RegularExpressions")
                .WithSourceResolver(new SourceFileResolver(ImmutableArray<string>.Empty, baseDirectory: Environment.CurrentDirectory))
                .WithMetadataResolver(ScriptMetadataResolver.Default.WithSearchPaths(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()))
                .WithEmitDebugInformation(true);

            // Capture console output
            var originalOut = Console.Out;
            var outputBuilder = new StringBuilder();

            try
            {
                using var stringWriter = new StringWriter(outputBuilder);
                Console.SetOut(stringWriter);

                // Execute the script with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                // Run script in a task so we can properly handle timeout
                var scriptTask = Task.Run(async () =>
                    await CSharpScript.EvaluateAsync(cleanedScript, scriptOptions, cancellationToken: cts.Token),

                                          var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                var completedTask = await Task.WhenAny(scriptTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    cts.Cancel();
                    throw new OperationCanceledException();
                }

                var result = await scriptTask;

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
            var errorBuilder = new StringBuilder();
            errorBuilder.AppendLine("Compilation Error(s):");
            errorBuilder.AppendLine();

            foreach (var diagnostic in e.Diagnostics)
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                var line = lineSpan.StartLinePosition.Line + 1; // Convert to 1-based
                var column = lineSpan.StartLinePosition.Character + 1;

                errorBuilder.AppendLine($"  Line {line}, Column {column}: {diagnostic.Id} - {diagnostic.GetMessage()}");

                // Try to show the problematic code if available
                if (!diagnostic.Location.IsInSource) continue;

                var sourceText = diagnostic.Location.SourceTree?.GetText();
                if (sourceText != null)
                {
                    var lineText = sourceText.Lines[lineSpan.StartLinePosition.Line].ToString();
                    if (!string.IsNullOrWhiteSpace(lineText))
                    {
                        errorBuilder.AppendLine($"    Code: {lineText.Trim()}");

                        // Add a pointer to the error position
                        if (column > 0 && column <= lineText.Length)
                        {
                            var pointer = new string(' ', column + 9) + "^"; // 9 for "    Code: "
                            errorBuilder.AppendLine(pointer);
                        }
                    }
                }
                errorBuilder.AppendLine();
            }

            return errorBuilder.ToString().TrimEnd();
        }
        catch (OperationCanceledException)
        {
            return $"Error: Script execution timed out after {timeoutSeconds} seconds.";
        }
        catch (Exception e)
        {
            var errorBuilder = new StringBuilder();
            errorBuilder.AppendLine($"Runtime Error: {e.GetType().Name}");
            errorBuilder.AppendLine($"Message: {e.Message}");

            // Try to extract the line number from the stack trace if it's a script error
            if (e.StackTrace != null && e.StackTrace.Contains("Submission#0"))
            {
                var lines = e.StackTrace.Split('\n');
                foreach (var traceLine in lines)
                {
                    if (traceLine.Contains("Submission#0") && traceLine.Contains(":line"))
                    {
                        var lineMatch = System.Text.RegularExpressions.Regex.Match(traceLine, @":line (\d+)");
                        if (lineMatch.Success)
                        {
                            errorBuilder.AppendLine($"Script Line: {lineMatch.Groups[1].Value}");
                            break;
                        }
                    }
                }
            }

            if (e.InnerException != null)
            {
                errorBuilder.AppendLine($"Inner Exception: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
            }

            errorBuilder.AppendLine();
            errorBuilder.AppendLine("Stack Trace:");
            errorBuilder.AppendLine(e.StackTrace);

            return errorBuilder.ToString().TrimEnd();
        }
    }
}
