# InfinityFlow C# Eval MCP Server

An MCP (Model Context Protocol) server that evaluates and executes C# scripts using Roslyn. This tool allows AI assistants to run C# code dynamically, either from direct input or from .csx script files.

## Features

- Execute C# scripts directly or from files
- Full Roslyn scripting support with common namespaces pre-imported
- Console output capture (safe for MCP stdio protocol)
- Comprehensive error handling for compilation and runtime errors
- Available as both Docker container and dotnet tool

## Tool Description

The server exposes a single tool: `EvalCSharp`

### Parameters

- `csxFile` (optional): Full path to a .csx file to execute
- `csx` (optional): C# script code to execute directly

Either `csxFile` or `csx` must be provided, but not both.

## Developing locally

To test this MCP server from source code (locally) without using a built MCP server package, you can configure your IDE to run the project directly using `dotnet run`.

```json
{
  "servers": {
    "InfinityFlow.CSharp.Eval": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<PATH TO PROJECT DIRECTORY>"
      ]
    }
  }
}
```

## Testing the MCP Server

Once configured, you can ask the AI assistant to execute C# code, for example:
- "Execute this C# code: Console.WriteLine(\"Hello World\");"
- "Run the C# script at /path/to/script.csx"
- "Calculate the factorial of 10 using C#"

## Publishing to NuGet.org

1. Run `dotnet pack -c Release` to create the NuGet package
2. Publish to NuGet.org with `dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json`

## Using the MCP Server from NuGet.org

Once the MCP server package is published to NuGet.org, you can configure it in your preferred IDE. Both VS Code and Visual Studio use the `dnx` command to download and install the MCP server package from NuGet.org.

- **VS Code**: Create a `<WORKSPACE DIRECTORY>/.vscode/mcp.json` file
- **Visual Studio**: Create a `<SOLUTION DIRECTORY>\.mcp.json` file

For both VS Code and Visual Studio, the configuration file uses the following server definition:

```json
{
  "servers": {
    "InfinityFlow.CSharp.Eval": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "<your package ID here>",
        "--version",
        "<your package version here>",
        "--yes"
      ]
    }
  }
}
```

## More information

.NET MCP servers use the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) C# SDK. For more information about MCP:

- [Official Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)

Refer to the VS Code or Visual Studio documentation for more information on configuring and using MCP servers:

- [Use MCP servers in VS Code (Preview)](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Use MCP servers in Visual Studio (Preview)](https://learn.microsoft.com/visualstudio/ide/mcp-servers)
