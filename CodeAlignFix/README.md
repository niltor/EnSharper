# CodeAlignFix - Roslyn Analyzer for Code Alignment

A Roslyn-based code analyzer and code fix provider that ensures consistent code alignment in C# projects. This analyzer integrates seamlessly with Visual Studio's "Code Cleanup" feature and can be configured via `.editorconfig`.

## Features

This analyzer provides five distinct alignment rules:

### CAF001: Assignment Alignment
Aligns assignment operators in consecutive variable declarations and field members.

**Before:**
```csharp
int x = 5;
string name = "test";
var value = 100;
```

**After:**
```csharp
int    x     = 5;
string name  = "test";
var    value = 100;
```

### CAF002: Object Initializer Alignment
Aligns assignment operators in object initializer expressions.

**Before:**
```csharp
var person = new Person
{
    Name = "John",
    Age = 30,
    Address = "123 Main St"
};
```

**After:**
```csharp
var person = new Person
{
    Name    = "John",
    Age     = 30,
    Address = "123 Main St"
};
```

### CAF003: Parameter Alignment
Ensures method and constructor parameters are placed on separate lines when the count exceeds the configured threshold.

**Before:**
```csharp
public void MyMethod(int a, string b, bool c, double d)
{
}
```

**After:**
```csharp
public void MyMethod(
    int a,
    string b,
    bool c,
    double d
)
{
}
```

### CAF004: Argument Alignment
Ensures method invocation arguments are placed on separate lines when the count exceeds the configured threshold.

**Before:**
```csharp
Console.WriteLine("a", "b", "c", "d");
```

**After:**
```csharp
Console.WriteLine(
    "a",
    "b",
    "c",
    "d"
);
```

### CAF005: Chained Method Alignment
Ensures chained method calls (inside method bodies) are placed on separate lines with proper indentation.

**Before:**
```csharp
var result = items.Where(x => x > 1).Select(x => x * 2).ToList();
```

**After:**
```csharp
var result = items
    .Where(x => x > 1)
    .Select(x => x * 2)
    .ToList();
```

## Installation

### Via NuGet Package
```bash
dotnet add package CodeAlignFix
```

### Via Visual Studio
1. Build the `CodeAlignFix.Package` project to generate the NuGet package
2. Add the generated `.nupkg` file to your project

## Configuration

All rules can be configured via `.editorconfig` file in your project root.

### Example `.editorconfig`

```ini
# CodeAlignFix Configuration

# Enable/Disable specific rules
dotnet_diagnostic.CAF001.severity = suggestion  # Assignment Alignment
dotnet_diagnostic.CAF002.severity = suggestion  # Object Initializer Alignment
dotnet_diagnostic.CAF003.severity = suggestion  # Parameter Alignment
dotnet_diagnostic.CAF004.severity = suggestion  # Argument Alignment
dotnet_diagnostic.CAF005.severity = suggestion  # Chained Method Alignment

# Rule-specific settings
# Maximum gap allowed for type alignment (default: 50)
code_align_max_alignment_gap = 50

# Constructor parameter threshold (default: 3)
# Parameters will be aligned when count >= this value
code_align_constructor_parameter_threshold = 3

# Method parameter threshold (default: 4)
# Parameters will be aligned when count >= this value
code_align_method_parameter_threshold = 4

# Argument threshold (default: 4)
# Arguments will be aligned when count >= this value
code_align_argument_threshold = 4

# Minimum chain length (default: 2)
# Method chains will be aligned when count >= this value
code_align_min_chain_length = 2
```

### Severity Levels

You can control how each rule behaves using severity levels:

- `none` - Rule is disabled
- `silent` - Rule is enabled but doesn't show in the IDE
- `suggestion` - Shows as a code suggestion (three dots)
- `warning` - Shows as a warning
- `error` - Shows as an error

## Integration with Visual Studio Code Cleanup

Once installed, these analyzers automatically integrate with Visual Studio's Code Cleanup feature:

1. **Configure Code Cleanup:**
   - Go to **Tools > Options > Text Editor > C# > Code Cleanup**
   - Select or create a profile
   - The CodeAlignFix rules will appear in the list

2. **Run Code Cleanup:**
   - Press `Ctrl+K, Ctrl+E` (default shortcut)
   - Or right-click in the editor and select **Remove and Sort Usings** or **Code Cleanup**

3. **Run on Save:**
   - Enable **Run Code Cleanup profile on Save** in Options
   - Select your profile with the alignment rules enabled

## Usage

### Manual Code Fix

When the analyzer detects alignable code:
1. Position your cursor on the highlighted code
2. Press `Ctrl+.` (Quick Actions)
3. Select the appropriate fix (e.g., "Align assignments")

### Batch Fix

Use the "Fix All" command to apply fixes throughout your solution:
1. Click the screwdriver/lightbulb icon
2. Select "Fix all occurrences in..."
3. Choose scope (Document, Project, or Solution)

## Building from Source

### Prerequisites
- .NET SDK 8.0 or later
- Visual Studio 2022 (for development)

### Build Steps

```bash
# Build the analyzer
dotnet build CodeAlignFix/CodeAlignFix/CodeAlignFix.csproj

# Build the code fix provider
dotnet build CodeAlignFix/CodeAlignFix.CodeFixes/CodeAlignFix.CodeFixes.csproj

# Build the NuGet package
dotnet build CodeAlignFix/CodeAlignFix.Package/CodeAlignFix.Package.csproj

# Run tests
dotnet test CodeAlignFix/CodeAlignFix.Test/CodeAlignFix.Test.csproj
```

## Project Structure

```
CodeAlignFix/
├── CodeAlignFix/               # Core analyzer project
│   ├── CodeAlignFixAnalyzer.cs # Main analyzer implementation
│   └── Resources.resx          # Localized diagnostic messages
├── CodeAlignFix.CodeFixes/     # Code fix provider project
│   └── CodeAlignFixCodeFixProvider.cs
├── CodeAlignFix.Package/       # NuGet package project
└── CodeAlignFix.Test/          # Unit tests
    └── CodeAlignFixUnitTests.cs
```

## Diagnostic IDs

| ID     | Description                    | Default Severity |
|--------|--------------------------------|------------------|
| CAF001 | Assignment Alignment           | Info             |
| CAF002 | Object Initializer Alignment   | Info             |
| CAF003 | Parameter Alignment            | Info             |
| CAF004 | Argument Alignment             | Info             |
| CAF005 | Chained Method Alignment       | Info             |

## Migration from VS Extension

If you're migrating from the old `CodeAlign` VS Extension to this Roslyn analyzer:

### Advantages of Roslyn Analyzer

1. **Better IDE Integration:** Works with Code Cleanup, not just custom shortcuts
2. **More Flexible:** Can be configured per-project via `.editorconfig`
3. **Cross-IDE Support:** Works in VS, VS Code, and Rider
4. **Build Integration:** Can be enforced during CI/CD builds
5. **Granular Control:** Enable/disable individual rules

### Configuration Mapping

| VS Extension Setting | .editorconfig Setting |
|---------------------|----------------------|
| MaxAlignmentGap | `code_align_max_alignment_gap` |
| ConstructorParameterThreshold | `code_align_constructor_parameter_threshold` |
| MethodParameterThreshold | `code_align_method_parameter_threshold` |

## Troubleshooting

### Analyzer not showing diagnostics

1. Ensure the package is properly installed
2. Check that the rules are not set to `severity = none` in `.editorconfig`
3. Rebuild the solution
4. Restart Visual Studio

### Code fixes not appearing

1. Check that you have the latest version of the package
2. Ensure the diagnostic is showing (check severity level)
3. Try rebuilding the solution

### Rules applying when you don't want them

1. Add a `.editorconfig` file to your project
2. Set unwanted rules to `severity = none`

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## License

See the [LICENSE](../LICENSE) file for details.

## Related Projects

- [CodeAlign](../CodeAlign/) - The original VS Extension implementation
- Uses this approach for backward compatibility until full migration

## Version History

### 1.0.0
- Initial release with 5 alignment rules
- Support for .editorconfig configuration
- Integration with Visual Studio Code Cleanup
- Batch fix provider support
