# Extension Verification Quick Test

This document provides quick test cases to verify the extension is working.

## Quick Test Checklist

After building and installing the extension in the experimental instance, follow these steps:

### 1. ✅ Verify Extension is Loaded

1. Open Visual Studio (Experimental Instance)
2. Go to **Extensions** > **Manage Extensions**
3. Look for "CodeFormatter" in the "Installed" section
4. It should be enabled (not grayed out)

### 2. ✅ Check Debug Output

1. Open a C# file
2. Open **View** > **Output** window
3. Select **Debug** from the dropdown
4. You should see messages like:
   ```
   [CodeFormatter] TextViewCreated - Starting initialization
   [CodeFormatter] Adding format command filter
   [CodeFormatter] Creating DocumentSaveListener
   ```

If you see these messages, the extension is loading correctly!

### 3. ✅ Test Variable Alignment

Create a new C# file with this content:

```csharp
public class Test
{
    public void Method()
    {
        int x = 5;
        string name = "test";
        var value = 100;
    }
}
```

**Test 1: Format Document**
- Press `Ctrl+K, Ctrl+D`
- Check the Debug output for:
  ```
  [CodeFormatter] Format Document command detected
  [CodeFormatter] ApplyAlignment - Starting alignment
  ```
- The equals signs should align

**Test 2: Save File**
- Make a change (add a space)
- Press `Ctrl+S`
- Check the Debug output for:
  ```
  [CodeFormatter] OnBeforeSave - Document: <filepath>
  [CodeFormatter] ApplyAlignment - checkFormatOnSave=True
  ```
- If FormatOnSave is enabled, alignment should be applied

### 4. ✅ Test Constructor Parameter Alignment

Add this to your C# file:

```csharp
public class MyClass
{
    public MyClass(int param1, string param2, bool param3)
    {
    }
}
```

Format with `Ctrl+K, Ctrl+D`. Parameters should split to separate lines:

```csharp
public class MyClass
{
    public MyClass(
        int param1,
        string param2,
        bool param3
    )
    {
    }
}
```

### 5. ✅ Check Extension Options

1. Go to **Tools** > **Options**
2. Navigate to **Code Align** > **General**
3. You should see:
   - Enable Plugin
   - Enable Align
   - Format On Save

Toggle these options and verify in Debug output that they're being read correctly:
```
[CodeFormatter] ApplyAlignment - Options: EnablePlugin=True, EnableAlign=True, FormatOnSave=True
```

## Troubleshooting Quick Tips

### No Debug Output?
- Ensure you're looking at the **Debug** output (not Build or other)
- Try closing and reopening a C# file
- Check Activity Log: `%AppData%\Microsoft\VisualStudio\17.0_*Exp\ActivityLog.xml`

### Extension Not in Extensions Manager?
- Close experimental instance
- Rebuild the solution
- Double-click `CodeFormatter.vsix` to reinstall
- Restart experimental instance

### Breakpoints Not Hitting?
- Ensure you're attached to the experimental instance process
- Verify Debug configuration is being used
- Clean and rebuild the solution

### No Formatting Happening?
- Check the Debug output for "Plugin or alignment is disabled"
- Verify all options are enabled in **Tools** > **Options** > **Code Align**
- Ensure the code meets criteria (consecutive assignments, >2 constructor params, >3 method params)

## Success Criteria

The extension is working correctly if:

- ✅ Debug output shows initialization messages when opening C# files
- ✅ Debug output shows format/save events when triggered
- ✅ Variable assignments align their equals signs
- ✅ Constructor parameters (>2) split to multiple lines
- ✅ Method parameters (>3) split to multiple lines
- ✅ Options page is accessible and functional
- ✅ No errors in Debug output or Activity Log

## For More Details

See the comprehensive debugging guides:
- [DEBUGGING.md](DEBUGGING.md) - Chinese version
- [DEBUGGING_EN.md](DEBUGGING_EN.md) - English version
