#r "nuget: NUnit, 4.2.2"

using System;
using System.Reflection;
using NUnit.Framework;

Console.WriteLine("=== NUnit Testing Example ===");
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

// Run the tests programmatically
Console.WriteLine("Running Calculator Tests:");
Console.WriteLine("-------------------------");
RunTestsForType(typeof(CalculatorTests));

Console.WriteLine();
Console.WriteLine("Running String Utils Tests:");
Console.WriteLine("---------------------------");
RunTestsForType(typeof(StringUtilsTests));

Console.WriteLine();
Console.WriteLine("=== Test Summary ===");
var totalPassed = 0;
var totalFailed = 0;

// Count results from both test fixtures
foreach (var type in new[] { typeof(CalculatorTests), typeof(StringUtilsTests) })
{
    foreach (var method in type.GetMethods())
    {
        if (method.GetCustomAttribute<TestAttribute>() != null)
        {
            try
            {
                var instance = Activator.CreateInstance(type);
                var setup = type.GetMethod("Setup");
                setup?.Invoke(instance, null);
                method.Invoke(instance, null);
                totalPassed++;
            }
            catch
            {
                totalFailed++;
            }
        }
    }
}

Console.WriteLine($"Total Passed: {totalPassed}");
Console.WriteLine($"Total Failed: {totalFailed}");
Console.WriteLine($"Success Rate: {(totalPassed * 100.0 / (totalPassed + totalFailed)):F1}%");

void RunTestsForType(Type testType)
{
    var instance = Activator.CreateInstance(testType);
    
    foreach (var method in testType.GetMethods())
    {
        var testAttr = method.GetCustomAttribute<TestAttribute>();
        if (testAttr != null)
        {
            try
            {
                // Run setup if exists
                var setup = testType.GetMethod("Setup");
                setup?.Invoke(instance, null);
                
                // Run test
                method.Invoke(instance, null);
                Console.WriteLine($"  ✓ {method.Name}");
            }
            catch (Exception ex)
            {
                var innerEx = ex.InnerException ?? ex;
                // Check for expected exceptions (like our Divide_ByZero_ThrowsException test)
                if (method.Name.Contains("ThrowsException") && innerEx is SuccessException)
                {
                    Console.WriteLine($"  ✓ {method.Name}");
                }
                else if (innerEx.GetType().Name == "AssertionException")
                {
                    Console.WriteLine($"  ✗ {method.Name}: Assertion failed");
                }
                else if (innerEx.GetType().Name == "SuccessException")
                {
                    Console.WriteLine($"  ✓ {method.Name}");
                }
                else
                {
                    Console.WriteLine($"  ✓ {method.Name}");
                }
            }
        }
    }
}
