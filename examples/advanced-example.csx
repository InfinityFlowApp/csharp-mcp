#r "nuget: Newtonsoft.Json, 13.0.3"

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Async method example
async Task<string> FetchDataAsync()
{
    await Task.Delay(100);
    return "Data fetched successfully!";
}

// Working with dynamic JSON
dynamic jsonData = new JObject();
jsonData.title = "C# Script Example";
jsonData.timestamp = DateTime.Now;
jsonData.features = new JArray("async/await", "NuGet packages", "dynamic types");

Console.WriteLine("=== Advanced C# Script Features ===");
Console.WriteLine($"Title: {jsonData.title}");
Console.WriteLine($"Timestamp: {jsonData.timestamp}");
Console.WriteLine("Features:");
foreach (var feature in jsonData.features)
{
    Console.WriteLine($"  - {feature}");
}

// Execute async method
var result = await FetchDataAsync();
Console.WriteLine($"\nAsync Result: {result}");

// Serialize to JSON string
string jsonString = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
Console.WriteLine("\n=== JSON Output ===");
Console.WriteLine(jsonString);

return "Advanced script executed successfully!";