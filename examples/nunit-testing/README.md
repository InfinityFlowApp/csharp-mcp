# NUnit Testing Example

This example demonstrates creating and running NUnit tests programmatically within a C# script.

## Features

- **Test Fixtures**: Two test classes (`CalculatorTests` and `StringUtilsTests`)
- **Test Methods**: Multiple test methods with assertions
- **Setup Methods**: Demonstrates test setup/initialization
- **Exception Testing**: Shows how to test for expected exceptions
- **Programmatic Execution**: Runs tests and displays results without external test runner
- **NuGet Integration**: Uses `#r "nuget:..."` directives to load NUnit packages

## Code Overview

The script includes:

1. **Calculator Class**: Basic arithmetic operations with division by zero handling
2. **StringUtils Class**: String manipulation utilities (reverse, palindrome check)
3. **Test Classes**: Comprehensive tests for both utility classes
4. **Test Runner**: Custom code to execute tests and display results

## Test Coverage

### Calculator Tests
- Addition
- Subtraction
- Multiplication
- Division (including divide by zero exception)

### String Utils Tests
- String reversal
- Palindrome detection (positive and negative cases)

## Output

The script outputs:
- Individual test results with pass/fail indicators
- Test summary with total passed/failed counts
- Success rate percentage

## NuGet Packages Used

- `NUnit 4.2.2`: Core testing framework with assertions and attributes
- `NUnit.Engine 3.18.3`: Test execution engine for running tests programmatically
- `System.Xml.XDocument 4.3.0`: XML parsing for test results

## Implementation Note

The script attempts to use NUnit Engine to run tests properly, but in the scripting context, the engine cannot locate the assembly. The script includes a fallback mechanism that manually executes each test method using the NUnit assertions, demonstrating that:
1. NuGet packages are successfully loaded and available
2. NUnit assertions and attributes work correctly
3. Tests can be executed programmatically even without the full engine