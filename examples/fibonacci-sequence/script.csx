using System;
using System.Linq;

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

// Calculate some statistics
var fibNumbers = Enumerable.Range(0, 10).Select(Fibonacci).ToList();
var sum = fibNumbers.Sum();
var max = fibNumbers.Max();

Console.WriteLine($"\nSum of first 10 Fibonacci numbers: {sum}");
Console.WriteLine($"Largest Fibonacci number: {max}");

return $"Fibonacci calculation completed. Sum = {sum}";