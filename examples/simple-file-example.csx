using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// Function to calculate fibonacci
int Fibonacci(int n)
{
    if (n <= 1) return n;
    return Fibonacci(n - 1) + Fibonacci(n - 2);
}

Console.WriteLine("=== Fibonacci Sequence ===");
for (int i = 0; i < 10; i++)
{
    Console.WriteLine($"F({i}) = {Fibonacci(i)}");
}

// Work with file paths
var currentDir = Environment.CurrentDirectory;
Console.WriteLine($"\nCurrent Directory: {currentDir}");

// Create some test data
var data = new Dictionary<string, int>
{
    ["Apple"] = 5,
    ["Banana"] = 3,
    ["Orange"] = 7,
    ["Grape"] = 12
};

Console.WriteLine("\n=== Fruit Inventory ===");
foreach (var item in data.OrderBy(x => x.Key))
{
    Console.WriteLine($"{item.Key}: {item.Value}");
}

var total = data.Values.Sum();
var average = data.Values.Average();

Console.WriteLine($"\nTotal items: {total}");
Console.WriteLine($"Average per type: {average:F2}");

return $"Script completed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";