# EnSharper - Code Align Extension for Visual Studio

A Visual Studio extension that provides code formatting functionality with alignment features for C# code.

## Features

### Variable Assignment Alignment
When enabled, the extension aligns both the variable types and names, then the `=` signs in consecutive variable declarations and assignments vertically.

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

The extension also aligns class **fields** (not properties) with initializers:

**Before:**
```csharp
private readonly SystemConfigManager _systemConfig = systemConfig;
private readonly CacheService _cache = cache;
private readonly SystemRoleManager _roleManager = roleManager;
```

**After:**
```csharp
private readonly SystemConfigManager _systemConfig = systemConfig;
private readonly CacheService        _cache        = cache;
private readonly SystemRoleManager   _roleManager  = roleManager;
```

**Note:** Property declarations are not aligned, only field declarations.

And assignment statements to properties (in method bodies), including indexed properties:

**Before:**
```csharp
aesAlg.Key = Encoding.UTF8.GetBytes(Md5Hash(key));
aesAlg.IV = aesAlg.Key[..16];

currentMenus[index].Named = menu.Named;
currentMenus[index].Sort = menu.Sort;
currentMenus[index].Icon = menu.Icon;
```

**After:**
```csharp
aesAlg.Key = Encoding.UTF8.GetBytes(Md5Hash(key));
aesAlg.IV  = aesAlg.Key[..16];

currentMenus[index].Named = menu.Named;
currentMenus[index].Sort  = menu.Sort;
currentMenus[index].Icon  = menu.Icon;
```

### Object Initializer Alignment
The extension aligns the `=` signs in object initializers when properties are on separate lines.

**Before:**
```csharp
var menu = new SystemMenu
{
    Name = item.Name,
    AccessCode = item.AccessCode,
    MenuType = (MenuType)item.MenuType,
    Parent = parent,
    Sort = item.Sort ?? 0,
    Icon = item.Icon,
};
```

**After:**
```csharp
var menu = new SystemMenu
{
    Name       = item.Name,
    AccessCode = item.AccessCode,
    MenuType   = (MenuType)item.MenuType,
    Parent     = parent,
    Sort       = item.Sort ?? 0,
    Icon       = item.Icon,
};
```

### Chained Method Call Alignment
When method calls are chained **inside class methods**, the extension ensures each method is on its own line with proper indentation. This only applies to chained calls within method bodies, not at the class field or property initialization level.

**Before:**
```csharp
menus = await Queryable.AsNoTracking().OrderByDescending(t => t.Sort).ThenByDescending(t => t.CreatedTime).Skip((filter.PageIndex - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
```

**After:**
```csharp
menus = await Queryable
    .AsNoTracking()
    .OrderByDescending(t => t.Sort)
    .ThenByDescending(t => t.CreatedTime)
    .Skip((filter.PageIndex - 1) * filter.PageSize)
    .Take(filter.PageSize)
    .ToListAsync();
```

### Parameter Alignment

#### Primary Constructor Parameters (C# 12+)
When a class, record, or struct has a primary constructor with more than 2 parameters, each parameter is placed on a separate line with proper indentation.

**Before:**
```csharp
public class MyService(ILogger logger, ICache cache, IConfig config)
{
}
```

**After:**
```csharp
public class MyService(
    ILogger logger,
    ICache cache,
    IConfig config
)
{
}
```

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
   - **Format On Save**: Automatically apply alignment formatting when saving files (default: `true`)

## Usage

The extension integrates seamlessly with Visual Studio's formatting features:

### Format Document

1. Open a C# file in Visual Studio
2. Press your configured format document shortcut:
   - **Ctrl+K, Ctrl+D** (default Format Document)
   - **Ctrl+Shift+F** (alternative Format Document)
   - Or use **Edit** > **Advanced** > **Format Document**
3. The extension will apply alignment formatting in addition to standard formatting

The cursor position is preserved during formatting, ensuring a smooth editing experience.

### Logging
Diagnostics now appear in the **Code Align** output pane (created automatically when the package loads) and in the Visual Studio Activity Log; there is no longer any local log file to track.

### Format On Save

When **Format On Save** is enabled in the options:

1. Open a C# file in Visual Studio
2. Make your changes
3. Save the file (Ctrl+S or File > Save)
4. The extension will automatically apply alignment formatting when you save

This feature works non-intrusively:
- It only applies alignment formatting, without affecting other save operations
- The cursor position is preserved after formatting
- It prevents duplicate formatting on repeated saves (idempotent)
- It does not trigger unwanted side effects

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

> **Note:** Building with `dotnet build` outside of Visual Studio is not supported because the project depends on Visual Studio SDK assemblies that are resolved only inside the IDE or a VS developer command prompt. Use the full VS 2022 environment to restore, build, and deploy the extension.

## Extending Alignment Features

### Project Structure

The codebase is organized into logical layers:

- **`Configuration/`** - Options pages (`AlignOptions`), settings models (`AlignmentSettings`), and service factory
- **`Formatting/`** - Core formatting service (`AlignService`) and coordinator (`FormattingCoordinator`)
- **`Processors/`** - Roslyn syntax rewriters for alignment logic (parameter, argument, assignment alignment)
- **`Listeners/`** - VS event handlers (save, format command, keyboard shortcuts, text view creation)

### Adding New Alignment Features

1. Create a new processor class in `Processors/` that implements `IAlignmentProcessor` (see `ArgumentAlignmentProcessor` for reference)
2. Keep the processor focused on Roslyn syntax transformations - no VS service dependencies
3. Register your processor in `AlignService.CreateDefaultProcessors()` (in `Formatting/AlignService.cs`)
4. If configuration is needed:
   - Add properties to `AlignOptions` (in `Configuration/`)
   - Update `AlignmentSettings` to include the new values
   - Pass settings through `AlignServiceFactory`
5. Test by building in Visual Studio and using the Format Document command or saving a `.cs` file

## License

See the [LICENSE](LICENSE) file for details.
