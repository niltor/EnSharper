# EnSharper - Code Align Extension for Visual Studio

A Visual Studio extension that provides code formatting functionality with alignment features for C# code.

## Features

### Variable Assignment Alignment
When enabled, the extension aligns the `=` signs in consecutive variable declarations and assignments vertically.

**Before:**
```csharp
int x = 5;
string name = "test";
var value = 100;
```

**After:**
```csharp
int x      = 5;
string name = "test";
var value  = 100;
```

### Parameter Alignment

#### Constructor Parameters (>2 parameters)
When a constructor has more than 2 parameters, each parameter is placed on a separate line with proper indentation.

**Before:**
```csharp
public MyClass(int param1, string param2, bool param3)
{
}
```

**After:**
```csharp
public MyClass(
    int param1,
    string param2,
    bool param3
)
{
}
```

#### Method Parameters (>3 parameters)
When a method has more than 3 parameters, each parameter is placed on a separate line with proper indentation.

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

## Configuration

The extension can be configured through Visual Studio's Options dialog:

1. Go to **Tools** > **Options**
2. Navigate to **Code Align** > **General**
3. Configure the following settings:
   - **Enable Plugin**: Controls whether the extension is active (default: `true`)
   - **Enable Align**: Controls whether the alignment features are enabled (default: `true`)
   - **Format On Save**: Automatically apply alignment formatting when saving files (default: `true`)

## Usage

The extension integrates seamlessly with Visual Studio's formatting features:

### Format Document

1. Open a C# file in Visual Studio
2. Press your configured format document shortcut (default: **Ctrl+K, Ctrl+D**) or use **Edit** > **Advanced** > **Format Document**
3. The extension will apply alignment formatting in addition to standard formatting

### Format On Save

When **Format On Save** is enabled in the options:

1. Open a C# file in Visual Studio
2. Make your changes
3. Save the file (Ctrl+S or File > Save)
4. The extension will automatically apply alignment formatting when you save

This feature works non-intrusively - it only applies alignment formatting, without affecting any other save operations or triggering unwanted side effects.

## Requirements

- Visual Studio 2022 or later
- .NET Framework 4.7.2 or later

## How It Works

The extension uses the following technologies:
- **Roslyn**: For syntax analysis and code transformation
- **Visual Studio SDK**: For integration with the editor
- **MEF (Managed Extensibility Framework)**: For component composition

The alignment is applied after the standard Visual Studio formatting, ensuring compatibility with other formatting rules.

## Building from Source

1. Clone the repository
2. Open the solution in Visual Studio 2022
3. Build the solution
4. The VSIX package will be generated in the `bin` folder

## License

See the [LICENSE](LICENSE) file for details.
