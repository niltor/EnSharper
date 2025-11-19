# Testing Guide for EnSharper Code Alignment Extension

This guide explains how to test the Code Alignment extension both manually and with automated tests.

## Manual Testing

### Prerequisites
- Windows OS
- Visual Studio 2022 or later
- The EnSharper extension installed

### Installation for Testing
1. Open the solution in Visual Studio 2022
2. Build the project (F6)
3. Press F5 to launch the Experimental Instance of Visual Studio
   - This creates a separate VS instance with the extension installed
   - Settings: `/rootsuffix Exp`

### Test Scenarios

#### 1. Testing Variable Assignment Alignment

**Test Case 1.1: Basic Alignment**
```csharp
// Create a C# file with this code:
public void TestMethod()
{
    int x = 5;
    string name = "test";
    var value = 100;
}

// Steps:
// 1. Place cursor in the method
// 2. Press Ctrl+K, Ctrl+D (Format Document)
// Expected: The = signs should align vertically
```

**Expected Result:**
```csharp
public void TestMethod()
{
    int x      = 5;
    string name = "test";
    var value  = 100;
}
```

**Test Case 1.2: Non-Consecutive Assignments**
```csharp
public void TestMethod()
{
    int x = 5;
    Console.WriteLine("break");
    string name = "test";
}

// Expected: No alignment (assignments are not consecutive)
```

**Test Case 1.3: Mixed Declarations and Assignments**
```csharp
public void TestMethod()
{
    int x = 5;
    var name = "test";
    string value = "hello";
    bool isActive = true;
}

// Expected: All = signs aligned
```

#### 2. Testing Parameter Alignment

**Test Case 2.1: Constructor with 3 Parameters (Should Align)**
```csharp
public class MyClass
{
    public MyClass(int a, string b, bool c)
    {
    }
}

// After Format Document:
public class MyClass
{
    public MyClass(
        int a,
        string b,
        bool c
    )
    {
    }
}
```

**Test Case 2.2: Constructor with 2 Parameters (Should NOT Align)**
```csharp
public class MyClass
{
    public MyClass(int a, string b)
    {
    }
}

// Expected: No change (only >2 params trigger alignment)
```

**Test Case 2.3: Method with 4 Parameters (Should Align)**
```csharp
public void MyMethod(int a, string b, bool c, double d)
{
}

// After Format Document:
public void MyMethod(
    int a,
    string b,
    bool c,
    double d
)
{
}
```

**Test Case 2.4: Method with 3 Parameters (Should NOT Align)**
```csharp
public void MyMethod(int a, string b, bool c)
{
}

// Expected: No change (only >3 params trigger alignment)
```

#### 3. Testing Options

**Test Case 3.1: Disable Plugin**
```
Steps:
1. Go to Tools > Options > Code Align > General
2. Uncheck "Enable Plugin"
3. Click OK
4. Format a document with alignment candidates
Expected: No alignment should occur
```

**Test Case 3.2: Disable Align Feature**
```
Steps:
1. Go to Tools > Options > Code Align > General
2. Ensure "Enable Plugin" is checked
3. Uncheck "Enable Align"
4. Click OK
5. Format a document with alignment candidates
Expected: No alignment should occur
```

**Test Case 3.3: Re-enable Features**
```
Steps:
1. Re-check both options
2. Format a document
Expected: Alignment should work again
```

#### 4. Edge Cases

**Test Case 4.1: Syntax Errors**
```csharp
public void TestMethod()
{
    int x = 5
    string name = "test";  // Missing semicolon above
}

// Expected: Extension should not crash, original code preserved
```

**Test Case 4.2: Empty File**
```csharp
// Empty or whitespace only
// Expected: No errors
```

**Test Case 4.3: Large File**
```csharp
// File with 1000+ lines
// Expected: Formatting completes without hanging
```

## Automated Testing

Currently, the project doesn't include automated tests. Here's how to add them:

### Option 1: Unit Tests for AlignService

Create a new test project:

```bash
# In the solution directory
dotnet new xunit -n CodeFormatter.Tests
dotnet add CodeFormatter.Tests/CodeFormatter.Tests.csproj reference CodeFormatter/CodeFormatter.csproj
```

**Sample Unit Test:**
```csharp
using Xunit;
using CodeFormatter;

namespace CodeFormatter.Tests
{
    public class AlignServiceTests
    {
        private readonly AlignService _alignService = new AlignService();

        [Fact]
        public void FormatCode_AlignsConsecutiveAssignments()
        {
            // Arrange
            string input = @"
public void TestMethod()
{
    int x = 5;
    string name = ""test"";
    var value = 100;
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            Assert.Contains("int x      = 5;", result);
            Assert.Contains("string name = ""test"";", result);
            Assert.Contains("var value  = 100;", result);
        }

        [Fact]
        public void FormatCode_AlignsConstructorWith3Parameters()
        {
            // Arrange
            string input = @"
public class MyClass
{
    public MyClass(int a, string b, bool c) { }
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            Assert.Contains("public MyClass(\r\n", result);
            Assert.Contains("    int a,", result);
            Assert.Contains("    string b,", result);
            Assert.Contains("    bool c\r\n)", result);
        }

        [Fact]
        public void FormatCode_DoesNotAlignConstructorWith2Parameters()
        {
            // Arrange
            string input = @"public MyClass(int a, string b) { }";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert - Should remain on one line
            Assert.DoesNotContain("\r\n    int a", result);
        }

        [Fact]
        public void FormatCode_HandlesInvalidCodeGracefully()
        {
            // Arrange
            string input = @"public void Test(";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert - Should return original code
            Assert.Equal(input, result);
        }
    }
}
```

### Option 2: Integration Tests with VS Test Framework

For testing the full extension in Visual Studio context, use the VS SDK testing framework.

**Add to CodeFormatter.csproj:**
```xml
<PackageReference Include="Microsoft.VisualStudio.SDK.TestFramework" Version="17.0.31902.203" />
```

**Sample Integration Test:**
```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VSSDK.Tools.VsIdeTesting;

[TestClass]
public class ExtensionIntegrationTests
{
    [TestMethod]
    [HostType("VS IDE")]
    public void TestFormatDocument()
    {
        // This requires VS IDE testing infrastructure
        // Tests the extension in actual VS environment
    }
}
```

### Option 3: Manual Test Checklist

Create a test checklist document (this file) and perform manual testing before each release:

- [ ] Variable alignment with 2 consecutive assignments
- [ ] Variable alignment with 5+ consecutive assignments
- [ ] Constructor with 3 parameters
- [ ] Constructor with 2 parameters (no alignment)
- [ ] Method with 4 parameters
- [ ] Method with 3 parameters (no alignment)
- [ ] Options: Enable/Disable Plugin
- [ ] Options: Enable/Disable Align
- [ ] Syntax errors don't crash extension
- [ ] Empty file handling
- [ ] Large file performance (1000+ lines)

## Running Tests

### Unit Tests
```bash
cd CodeFormatter.Tests
dotnet test
```

### Manual Testing
1. Build the solution in Visual Studio
2. Press F5 to launch Experimental Instance
3. Follow the test scenarios above
4. Check the checklist items

## Debugging

To debug the extension:

1. Set breakpoints in your code
2. Press F5 to launch Experimental Instance
3. In the experimental instance, open a C# file
4. Format the document (Ctrl+K, Ctrl+D)
5. Your breakpoints should hit

**Useful debugging locations:**
- `FormatCommandFilter.Exec` - When format command is triggered
- `AlignService.FormatCode` - When alignment processing starts
- `AlignService.AlignVariableAssignments` - Variable alignment logic
- `AlignService.AlignParameters` - Parameter alignment logic

## Troubleshooting

**Extension not loading:**
- Check VS Output window (View > Output, select "Extensions")
- Check Windows Event Viewer for errors

**Alignment not working:**
- Verify options are enabled (Tools > Options > Code Align)
- Check that you're formatting a C# file
- Verify breakpoints hit when debugging

**Tests failing:**
- Ensure Roslyn packages are referenced correctly
- Check that test project targets correct framework
- Verify Visual Studio SDK test framework is installed

## Continuous Integration

For CI/CD, you can:
1. Run unit tests in CI pipeline
2. Build the VSIX package
3. Optionally use VS Test Agent for integration tests

**GitHub Actions Example:**
```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: '6.0.x'
    - name: Restore dependencies
      run: dotnet restore
    - name: Build
      run: dotnet build --no-restore
    - name: Test
      run: dotnet test --no-build --verbosity normal
```

## Additional Resources

- [Visual Studio SDK Documentation](https://docs.microsoft.com/en-us/visualstudio/extensibility/)
- [Roslyn API Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [VS Extension Testing](https://docs.microsoft.com/en-us/visualstudio/extensibility/testing-extensions)
