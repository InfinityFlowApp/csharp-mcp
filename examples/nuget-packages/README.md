# NuGet Packages Example

This example demonstrates how to use NuGet packages in C# scripts using the `#r` directive.

## Features

- NuGet package references
- JSON manipulation with Newtonsoft.Json
- Dynamic object creation
- LINQ operations on JSON arrays

## Requirements

- Internet connection for NuGet package download
- Docker container must use SDK image (not just runtime)

## Running the Example

```bash
# Using the MCP tool directly
dotnet run --project ../../src/InfinityFlow.CSharp.Eval -- eval-csharp --csx-file script.csx

# Or via MCP client
# Provide the full path to script.csx when using the eval_c_sharp tool
```

## Expected Output

The script will output:

- Formatted JSON representation
- JSON query results
- Object property extraction
- Return value with processing summary
