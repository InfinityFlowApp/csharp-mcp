using System;

// Basic C# script execution example
Console.WriteLine("Hello from C# MCP!");
Console.WriteLine($"Script is running...");

// Simple calculation
var sum = 0;
for (int i = 1; i <= 10; i++)
{
    sum += i;
}

Console.WriteLine($"Sum of 1 to 10: {sum}");

// Return a value
return $"Script completed successfully with sum = {sum}";
