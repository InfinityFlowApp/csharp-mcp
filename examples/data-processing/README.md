# Data Processing Example

This example demonstrates complex data processing with classes, LINQ, and JSON serialization.

## Features
- Custom class definitions  
- Collection manipulation
- LINQ queries and aggregations
- Anonymous types
- GroupBy operations
- JSON serialization (System.Text.Json)

## Running the Example

```bash
# Using the MCP tool directly
dotnet run --project ../../src/InfinityFlow.CSharp.Eval -- eval-csharp --csx-file script.csx

# Or via MCP client
# Provide the full path to script.csx when using the eval_c_sharp tool
```

## Expected Output
The script will output:
- List of people with their hobbies
- Statistical analysis
- Age group distribution
- Return value with processing summary