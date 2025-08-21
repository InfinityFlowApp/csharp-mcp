#r "nuget: NUnit, 4.2.2"
#r "nuget: NUnit.Engine, 3.18.3"
#r "nuget: System.Xml.XDocument, 4.3.0"

using System;
using System.Reflection;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Engine;

Console.WriteLine("=== NUnit Testing Example with Engine ===");
Console.WriteLine();

// Define test classes
[TestFixture]
public class CalculatorTests
{
    private Calculator _calculator;
    
    [SetUp]
    public void Setup()
    {
        _calculator = new Calculator();
    }
    
    [Test]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = _calculator.Add(2, 3);
        Assert.That(result, Is.EqualTo(5));
    }
    
    [Test]
    public void Subtract_TwoNumbers_ReturnsDifference()
    {
        var result = _calculator.Subtract(10, 4);
        Assert.That(result, Is.EqualTo(6));
    }
    
    [Test]
    public void Multiply_TwoNumbers_ReturnsProduct()
    {
        var result = _calculator.Multiply(3, 4);
        Assert.That(result, Is.EqualTo(12));
    }
    
    [Test]
    public void Divide_ByZero_ThrowsException()
    {
        Assert.Throws<DivideByZeroException>(() => _calculator.Divide(10, 0));
    }
    
    [Test]
    public void Divide_TwoNumbers_ReturnsQuotient()
    {
        var result = _calculator.Divide(10, 2);
        Assert.That(result, Is.EqualTo(5));
    }
}

[TestFixture]
public class StringUtilsTests
{
    [Test]
    public void Reverse_SimpleString_ReturnsReversed()
    {
        var result = StringUtils.Reverse("hello");
        Assert.That(result, Is.EqualTo("olleh"));
    }
    
    [Test]
    public void IsPalindrome_WithPalindrome_ReturnsTrue()
    {
        var result = StringUtils.IsPalindrome("racecar");
        Assert.That(result, Is.True);
    }
    
    [Test]
    public void IsPalindrome_WithNonPalindrome_ReturnsFalse()
    {
        var result = StringUtils.IsPalindrome("hello");
        Assert.That(result, Is.False);
    }
}

// Classes under test
public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
    public int Multiply(int a, int b) => a * b;
    public int Divide(int a, int b)
    {
        if (b == 0) throw new DivideByZeroException("Cannot divide by zero");
        return a / b;
    }
}

public static class StringUtils
{
    public static string Reverse(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var chars = input.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }
    
    public static bool IsPalindrome(string input)
    {
        if (string.IsNullOrEmpty(input)) return true;
        var cleaned = input.ToLower().Replace(" ", "");
        return cleaned == Reverse(cleaned);
    }
}

// Run tests using NUnit Engine
Console.WriteLine("Running tests with NUnit Engine...");
Console.WriteLine();

try
{
    using (var engine = TestEngineActivator.CreateInstance())
    {
        // Create a test package for the current assembly
        var package = new TestPackage(Assembly.GetExecutingAssembly().Location);
        
        using (var runner = engine.GetRunner(package))
        {
            // Run the tests
            var xmlResult = runner.Run(null, TestFilter.Empty);
            
            // Parse XML results
            var xmlText = xmlResult.OuterXml;
            var doc = XDocument.Parse(xmlText);
            
            var testRun = doc.Descendants("test-run").FirstOrDefault();
            if (testRun != null)
            {
                var testCount = int.Parse(testRun.Attribute("testcasecount")?.Value ?? "0");
                var passCount = int.Parse(testRun.Attribute("passed")?.Value ?? "0");
                var failCount = int.Parse(testRun.Attribute("failed")?.Value ?? "0");
                var skipCount = int.Parse(testRun.Attribute("skipped")?.Value ?? "0");
                
                // Display individual test results
                Console.WriteLine("Test Results:");
                Console.WriteLine("-------------");
                
                var testCases = doc.Descendants("test-case");
                foreach (var testCase in testCases)
                {
                    var name = testCase.Attribute("name")?.Value ?? "Unknown";
                    var outcome = testCase.Attribute("result")?.Value ?? "Unknown";
                    var symbol = outcome == "Passed" ? "✓" : outcome == "Failed" ? "✗" : "○";
                    var shortName = name.Split('.').Last();
                    Console.WriteLine($"  {symbol} {shortName}");
                }
                
                Console.WriteLine();
                Console.WriteLine("=== Test Summary ===");
                Console.WriteLine($"Total Tests: {testCount}");
                Console.WriteLine($"Passed: {passCount}");
                Console.WriteLine($"Failed: {failCount}");
                Console.WriteLine($"Skipped: {skipCount}");
                Console.WriteLine($"Success Rate: {(testCount > 0 ? (passCount * 100.0 / testCount) : 0):F1}%");
            }
            else
            {
                Console.WriteLine("No test results found in XML output.");
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error running tests with NUnit Engine: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Falling back to manual test execution...");
    Console.WriteLine();
    
    // Fallback: Run tests manually
    var passed = 0;
    var failed = 0;
    
    Console.WriteLine("Running Calculator Tests:");
    Console.WriteLine("-------------------------");
    
    var calc = new Calculator();
    
    // Test Add
    try { 
        Assert.That(calc.Add(2, 3), Is.EqualTo(5)); 
        Console.WriteLine("  ✓ Add_TwoNumbers_ReturnsSum");
        passed++;
    } catch { 
        Console.WriteLine("  ✗ Add_TwoNumbers_ReturnsSum");
        failed++;
    }
    
    // Test Subtract
    try { 
        Assert.That(calc.Subtract(10, 4), Is.EqualTo(6)); 
        Console.WriteLine("  ✓ Subtract_TwoNumbers_ReturnsDifference");
        passed++;
    } catch { 
        Console.WriteLine("  ✗ Subtract_TwoNumbers_ReturnsDifference");
        failed++;
    }
    
    // Test Multiply
    try { 
        Assert.That(calc.Multiply(3, 4), Is.EqualTo(12)); 
        Console.WriteLine("  ✓ Multiply_TwoNumbers_ReturnsProduct");
        passed++;
    } catch { 
        Console.WriteLine("  ✗ Multiply_TwoNumbers_ReturnsProduct");
        failed++;
    }
    
    // Test Divide by Zero
    try { 
        Assert.Throws<DivideByZeroException>(() => calc.Divide(10, 0)); 
        Console.WriteLine("  ✓ Divide_ByZero_ThrowsException");
        passed++;
    } catch { 
        Console.WriteLine("  ✗ Divide_ByZero_ThrowsException");
        failed++;
    }
    
    // Test Divide
    try { 
        Assert.That(calc.Divide(10, 2), Is.EqualTo(5)); 
        Console.WriteLine("  ✓ Divide_TwoNumbers_ReturnsQuotient");
        passed++;
    } catch { 
        Console.WriteLine("  ✗ Divide_TwoNumbers_ReturnsQuotient");
        failed++;
    }
    
    Console.WriteLine();
    Console.WriteLine("Running String Utils Tests:");
    Console.WriteLine("---------------------------");
    
    // Test Reverse
    try { 
        Assert.That(StringUtils.Reverse("hello"), Is.EqualTo("olleh")); 
        Console.WriteLine("  ✓ Reverse_SimpleString_ReturnsReversed");
        passed++;
    } catch { 
        Console.WriteLine("  ✗ Reverse_SimpleString_ReturnsReversed");
        failed++;
    }
    
    // Test IsPalindrome true
    try { 
        Assert.That(StringUtils.IsPalindrome("racecar"), Is.True); 
        Console.WriteLine("  ✓ IsPalindrome_WithPalindrome_ReturnsTrue");
        passed++;
    } catch { 
        Console.WriteLine("  ✗ IsPalindrome_WithPalindrome_ReturnsTrue");
        failed++;
    }
    
    // Test IsPalindrome false
    try { 
        Assert.That(StringUtils.IsPalindrome("hello"), Is.False); 
        Console.WriteLine("  ✓ IsPalindrome_WithNonPalindrome_ReturnsFalse");
        passed++;
    } catch { 
        Console.WriteLine("  ✗ IsPalindrome_WithNonPalindrome_ReturnsFalse");
        failed++;
    }
    
    Console.WriteLine();
    Console.WriteLine("=== Test Summary ===");
    Console.WriteLine($"Total Tests: {passed + failed}");
    Console.WriteLine($"Passed: {passed}");
    Console.WriteLine($"Failed: {failed}");
    Console.WriteLine($"Success Rate: {(passed * 100.0 / (passed + failed)):F1}%");
}

"NUnit Engine test execution completed!"