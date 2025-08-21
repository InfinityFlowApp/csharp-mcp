using System;
using System.Collections.Generic;
using System.Linq;
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

Console.WriteLine("=== Data Processing Example ===");
Console.WriteLine($"Processing {people.Count} records...");
Console.WriteLine();

// Display data
Console.WriteLine("People in database:");
foreach (var person in people)
{
    Console.WriteLine($"- {person}");
    Console.WriteLine($"  Hobbies: {string.Join(", ", person.Hobbies)}");
}

// LINQ operations
var statistics = new
{
    TotalPeople = people.Count,
    AverageAge = people.Average(p => p.Age),
    Youngest = people.OrderBy(p => p.Age).First(),
    Oldest = people.OrderByDescending(p => p.Age).First(),
    TotalHobbies = people.Sum(p => p.Hobbies.Count),
    MostHobbies = people.OrderByDescending(p => p.Hobbies.Count).First()
};

Console.WriteLine($"\nStatistics:");
Console.WriteLine($"- Total people: {statistics.TotalPeople}");
Console.WriteLine($"- Average age: {statistics.AverageAge:F1}");
Console.WriteLine($"- Youngest: {statistics.Youngest}");
Console.WriteLine($"- Oldest: {statistics.Oldest}");
Console.WriteLine($"- Total hobbies: {statistics.TotalHobbies}");
Console.WriteLine($"- Most hobbies: {statistics.MostHobbies.Name} ({statistics.MostHobbies.Hobbies.Count})");

// Group by age range
var ageGroups = people.GroupBy(p => p.Age < 30 ? "Under 30" : "30 and over")
                      .Select(g => new { AgeGroup = g.Key, Count = g.Count() })
                      .OrderBy(g => g.AgeGroup);

Console.WriteLine($"\nAge Groups:");
foreach (var group in ageGroups)
{
    Console.WriteLine($"- {group.AgeGroup}: {group.Count} people");
}

// Return summary
return $"Data processing completed. Analyzed {statistics.TotalPeople} people with {statistics.TotalHobbies} total hobbies.";