# Basic Execution Example

This example demonstrates basic C# script execution with console output and return values.

## Features

- Console output
- Variable declarations
- Simple loops
- Return values

## Running the Example

```bash
# Using the MCP tool directly
dotnet run --project ../../src/InfinityFlow.CSharp.Eval -- eval-csharp --csx-file script.csx

# Or via MCP client
# Provide the full path to script.csx when using the eval_c_sharp tool
```

## Expected Output

The script will output:

- A greeting message
- Current timestamp
- Sum calculation result
- Return value with the final sum
