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
        [Description("C# script code to execute directly")] string? csx = null)
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
                if (!File.Exists(csxFile))
                {
                    return $"Error: File not found: {csxFile}";
                }
                
                scriptCode = await File.ReadAllTextAsync(csxFile);
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
                    typeof(System.Threading.Tasks.Task).Assembly)
                .WithImports(
                    "System",
                    "System.IO",
                    "System.Linq",
                    "System.Text",
                    "System.Collections.Generic",
                    "System.Threading.Tasks");

            // Capture console output
            var originalOut = Console.Out;
            var outputBuilder = new StringBuilder();
            
            try
            {
                using var stringWriter = new StringWriter(outputBuilder);
                Console.SetOut(stringWriter);

                // Execute the script
                var result = await CSharpScript.EvaluateAsync(scriptCode, scriptOptions);

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
        catch (Exception e)
        {
            return $"Runtime Error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
        }
    }
}
