#r "nuget: Newtonsoft.Json, 13.0.3"

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Working with NuGet packages
var data = new JObject
{
    ["name"] = "John Doe",
    ["age"] = 30,
    ["skills"] = new JArray("C#", "ASP.NET", "Azure"),
    ["active"] = true
};

Console.WriteLine("=== NuGet Package Example ===");
Console.WriteLine("Using Newtonsoft.Json for JSON operations");
Console.WriteLine();

// Pretty print JSON
var formatted = JsonConvert.SerializeObject(data, Formatting.Indented);
Console.WriteLine("Formatted JSON:");
Console.WriteLine(formatted);

// Parse and query JSON
var skillCount = data["skills"].Count();
Console.WriteLine($"\nNumber of skills: {skillCount}");

// Create complex object
var person = new 
{
    Name = data["name"].ToString(),
    Age = (int)data["age"],
    IsActive = (bool)data["active"],
    SkillList = data["skills"].Select(s => s.ToString()).ToArray()
};

Console.WriteLine($"\nPerson object created:");
Console.WriteLine($"- Name: {person.Name}");
Console.WriteLine($"- Age: {person.Age}");
Console.WriteLine($"- Active: {person.IsActive}");
Console.WriteLine($"- Skills: {string.Join(", ", person.SkillList)}");

return $"Successfully processed JSON with {skillCount} skills";