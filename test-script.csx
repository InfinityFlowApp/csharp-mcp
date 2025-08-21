using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

// Define a class
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Hobbies { get; set; }
    
    public override string ToString() => $"{Name} ({Age} years old)";
}

// Create some data
var people = new List<Person>
{
    new Person { Name = "Alice", Age = 30, Hobbies = new List<string> { "Reading", "Gaming" } },
    new Person { Name = "Bob", Age = 25, Hobbies = new List<string> { "Sports", "Music" } },
    new Person { Name = "Charlie", Age = 35, Hobbies = new List<string> { "Cooking", "Travel" } }
};

Console.WriteLine("=== People Database ===");
foreach (var person in people)
{
    Console.WriteLine($"- {person}");
    Console.WriteLine($"  Hobbies: {string.Join(", ", person.Hobbies)}");
}

// LINQ operations
var averageAge = people.Average(p => p.Age);
var youngest = people.OrderBy(p => p.Age).First();
var oldest = people.OrderByDescending(p => p.Age).First();

Console.WriteLine($"\nStatistics:");
Console.WriteLine($"Average age: {averageAge:F1}");
Console.WriteLine($"Youngest: {youngest}");
Console.WriteLine($"Oldest: {oldest}");

// Find people with specific hobbies
var gamers = people.Where(p => p.Hobbies.Contains("Gaming")).Select(p => p.Name);
Console.WriteLine($"\nGamers: {string.Join(", ", gamers)}");

// JSON serialization
var json = JsonSerializer.Serialize(people, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine("\n=== JSON Output ===");
Console.WriteLine(json);

// Return summary
return $"Processed {people.Count} people with {people.Sum(p => p.Hobbies.Count)} total hobbies";