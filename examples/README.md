# C# MCP Examples

This directory contains example C# scripts demonstrating various features of the C# MCP server.

## Examples

### 1. [Basic Execution](./basic-execution)
Simple script demonstrating console output, variables, loops, and return values.

### 2. [Fibonacci Sequence](./fibonacci-sequence)
Recursive functions and LINQ operations for calculating Fibonacci numbers.

### 3. [Data Processing](./data-processing)
Complex data manipulation with custom classes, LINQ queries, and JSON serialization.

### 4. [NuGet Packages](./nuget-packages)
Using external NuGet packages with the `#r` directive (requires SDK runtime).

## Running Examples

Each example can be run in several ways:

### Using the MCP Tool Directly
```bash
cd examples/basic-execution
dotnet run --project ../../src/InfinityFlow.CSharp.Eval -- eval-csharp --csx-file script.csx
```

### Using Docker
```bash
docker run -i --rm -v $(pwd):/scripts ghcr.io/infinityflowapp/csharp-mcp:latest \
  eval-csharp --csx-file /scripts/examples/basic-execution/script.csx
```

### Via MCP Client
When using an MCP client like Claude or Cursor, provide the full path to the script file.

## Testing Examples

All examples are automatically tested to ensure they produce the expected output:

```bash
dotnet test --filter "FullyQualifiedName~ExamplesTests"
```

## Structure

Each example directory contains:
- `script.csx` - The C# script to execute
- `README.md` - Documentation for the example
- `expected-output.txt` - Expected output for validation

The test suite validates that each example:
1. Executes without errors
2. Produces output matching the expected output
3. Contains all required files