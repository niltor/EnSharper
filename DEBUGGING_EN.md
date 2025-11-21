# EnSharper Extension Debugging Guide

This document provides detailed guidance on how to debug and verify the EnSharper Visual Studio extension.

## Table of Contents

1. [Problem Analysis](#problem-analysis)
2. [Preparation for Debugging](#preparation-for-debugging)
3. [Starting a Debug Session](#starting-a-debug-session)
4. [Setting Breakpoints and Verifying Execution Flow](#setting-breakpoints-and-verifying-execution-flow)
5. [Viewing Diagnostic Logs](#viewing-diagnostic-logs)
6. [Common Issues Troubleshooting](#common-issues-troubleshooting)
7. [Verifying Extension Functionality](#verifying-extension-functionality)

---

## Problem Analysis

### Reasons Why the Extension May Not Work

1. **MEF Components Not Properly Exported**: The extension uses MEF (Managed Extensibility Framework) to inject components. If MEF export configuration is incorrect, components won't be loaded.

2. **Package Not Loaded**: Visual Studio packages need to be properly registered and loaded when needed.

3. **Event Listeners Not Registered**: Save and format event listeners may not be properly hooked up.

4. **Options Disabled the Functionality**: Extension options may be disabled.

### Fixed Issues

This update has addressed the following issues:

1. ✅ **Added MEF Component Asset**: Added `Microsoft.VisualStudio.MefComponent` asset declaration in `source.extension.vsixmanifest`.

2. ✅ **Enhanced Diagnostic Logging**: Added detailed debug output and ActivityLog recording in all critical paths.

3. ✅ **Improved Error Handling**: All components now have proper exception handling and logging.

---

## Preparation for Debugging

### 1. Build the Extension

Open the solution in Visual Studio:

```
EnSharper.slnx
```

Ensure the **Debug** configuration is selected, then build the solution:

- Menu: **Build** > **Build Solution**
- Shortcut: `Ctrl+Shift+B`

### 2. Check Build Output

After a successful build, verify that the VSIX file was generated in the output directory:

```
CodeFormatter/bin/Debug/CodeFormatter.vsix
```

### 3. Uninstall Old Version (if exists)

If you previously installed an old version of the extension:

1. Open **Extensions** > **Manage Extensions**
2. Find the "CodeFormatter" extension
3. Click **Uninstall**
4. Restart Visual Studio

---

## Starting a Debug Session

### Method 1: Start Debugging via Visual Studio

1. Open the `CodeFormatter.csproj` project in Visual Studio
2. Press `F5` or click **Debug** > **Start Debugging**
3. This will launch a new Visual Studio Experimental Instance
4. The experimental instance window title will show "- Experimental Instance"

### Method 2: Manually Attach to Process

1. First start the Visual Studio experimental instance:
   ```
   devenv.exe /rootsuffix Exp
   ```
2. In your development Visual Studio, select **Debug** > **Attach to Process**
3. Select the experimental instance's `devenv.exe` process
4. Ensure **Managed** debugger type is checked

---

## Setting Breakpoints and Verifying Execution Flow

### Key Breakpoint Locations

To verify that the extension loads and executes correctly, set breakpoints at these locations:

#### 1. Package Initialization

**File**: `CodeFormatterPackage.cs`  
**Method**: `InitializeAsync`  
**Line**: Around line 49

```csharp
System.Diagnostics.Debug.WriteLine("[CodeFormatter] Package InitializeAsync - Starting");
```

**Verification**: This breakpoint should be hit when Visual Studio loads the extension package.

#### 2. Text View Creation

**File**: `TextViewCreationListener.cs`  
**Method**: `TextViewCreated`  
**Line**: Around line 22

```csharp
System.Diagnostics.Debug.WriteLine("[CodeFormatter] TextViewCreated - Starting initialization");
```

**Verification**: This breakpoint should be hit when opening a C# file.

#### 3. Document Save Event

**File**: `DocumentSaveListener.cs`  
**Method**: `OnBeforeSave`  
**Line**: Around line 92

```csharp
System.Diagnostics.Debug.WriteLine($"[CodeFormatter] OnBeforeSave - docCookie: {docCookie}");
```

**Verification**: This breakpoint should be hit when saving a C# file (Ctrl+S).

#### 4. Format Document Command

**File**: `FormatCommandFilter.cs`  
**Method**: `Exec`  
**Line**: Around line 99

```csharp
System.Diagnostics.Debug.WriteLine("[CodeFormatter] Format Document command detected");
```

**Verification**: This breakpoint should be hit when executing the format document command (Ctrl+K, Ctrl+D).

#### 5. Alignment Application

**File**: `AlignmentHelper.cs`  
**Method**: `ApplyAlignment`  
**Line**: Around line 29

```csharp
System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ApplyAlignment - checkFormatOnSave={checkFormatOnSave}");
```

**Verification**: This breakpoint should be hit when applying code alignment.

### Breakpoint Debugging Flow

1. **Open a C# File**:
   - Triggers: `TextViewCreated`
   - Expected: Creates document save listener and command filter

2. **Save File** (Ctrl+S):
   - Triggers: `OnBeforeSave` → `ApplyAlignment` (if FormatOnSave is enabled)
   - Expected: Applies code alignment formatting

3. **Format Document** (Ctrl+K, Ctrl+D):
   - Triggers: `Exec` → `ApplyAlignment`
   - Expected: Applies code alignment formatting

---

## Viewing Diagnostic Logs

The extension now includes detailed diagnostic logging. There are two ways to view these logs:

### Method 1: Visual Studio Output Window

1. In the experimental instance, open **View** > **Output**
2. In the output window dropdown, select **Debug**
3. All logs starting with `[CodeFormatter]` are from the extension

**Example Log Output**:
```
[CodeFormatter] TextViewCreated - Starting initialization
[CodeFormatter] Adding format command filter
[CodeFormatter] AddFilterToView - Creating filter
[CodeFormatter] AddFilterToView - Command filter added successfully
[CodeFormatter] Creating DocumentSaveListener
[CodeFormatter] Initializing DocumentSaveListener
[CodeFormatter] Successfully advised running doc table events. Cookie: 12345
[CodeFormatter] TextViewCreated - Initialization complete
```

### Method 2: Visual Studio Activity Log

The Activity Log is Visual Studio's system-level log that records all extension activities.

#### Enable Activity Log:

Start the Visual Studio experimental instance with the `/log` parameter:

```
devenv.exe /rootsuffix Exp /log
```

#### View Activity Log:

Log file location:
```
%AppData%\Microsoft\VisualStudio\17.0_<instance_id>Exp\ActivityLog.xml
```

You can use Visual Studio's provided tool to view it:
```
"%VS_INSTALL_DIR%\Common7\IDE\ActivityLogViewer.exe"
```

Or open the XML file directly with a text editor and search for "CodeFormatter".

**Key Entries in Activity Log**:

```xml
<entry>
  <record>12</record>
  <time>2025/11/21 16:45:16.123</time>
  <type>Information</type>
  <source>CodeFormatter.Package</source>
  <description>CodeFormatter extension package initialized successfully</description>
</entry>
```

---

## Common Issues Troubleshooting

### Issue 1: Breakpoints Not Hitting

**Symptoms**: Breakpoints appear as hollow circles or never trigger.

**Possible Causes and Solutions**:

1. **Not Attached to Correct Process**
   - Ensure you're attached to the experimental instance's devenv.exe
   - Check that debugger type includes "Managed"

2. **PDB Files Mismatch**
   - Clean and rebuild the solution (**Build** > **Clean Solution** then **Build Solution**)
   - Ensure using Debug configuration

3. **Code Optimization Issues**
   - Confirm `<Optimize>false</Optimize>` in Debug configuration
   - Confirm `<DebugType>full</DebugType>`

### Issue 2: Extension Not Loaded

**Symptoms**: Can't see the extension in experimental instance, or extension shows as installed but not enabled.

**Diagnostic Steps**:

1. **Check if Extension is Installed**:
   - Open **Extensions** > **Manage Extensions**
   - Look for "CodeFormatter" under "Installed"

2. **Check VSIX Installation Log**:
   ```
   %LocalAppData%\Microsoft\VisualStudio\17.0_<instance_id>Exp\Extensions\Extensions.log
   ```

3. **Manually Install VSIX**:
   - Double-click the `CodeFormatter.vsix` file
   - Ensure it's being installed to the experimental instance

4. **Reset Experimental Instance**:
   ```
   "%VS_INSTALL_DIR%\VSSDK\VisualStudioIntegration\Tools\Bin\CreateExpInstance.exe" /Reset /VSInstance=17.0 /RootSuffix=Exp
   ```

### Issue 3: MEF Components Not Loaded

**Symptoms**: TextViewCreated is never called.

**Diagnostic Steps**:

1. **Check MEF Cache**:
   - Delete the MEF cache directory:
     ```
     %LocalAppData%\Microsoft\VisualStudio\17.0_<instance_id>Exp\ComponentModelCache
     ```
   - Restart the experimental instance

2. **Verify MEF Export**:
   - Check that `TextViewCreationListener.cs` has `[Export]` attribute
   - Check that `source.extension.vsixmanifest` includes MEF component asset

3. **Use MEF Diagnostics Tool**:
   - Visual Studio provides MEF diagnostics to view loaded components
   - Menu: **Tools** > **MEF Diagnostics** (if available)

### Issue 4: No Effect on Format or Save

**Symptoms**: Breakpoints are hit, but code doesn't change.

**Diagnostic Steps**:

1. **Check Options Settings**:
   - Open **Tools** > **Options**
   - Navigate to **Code Align** > **General**
   - Ensure:
     - ✅ Enable Plugin = true
     - ✅ Enable Align = true
     - ✅ Format On Save = true (if testing save functionality)

2. **View Log Output**:
   - Look for messages like this in the output window:
     ```
     [CodeFormatter] ApplyAlignment - Options: EnablePlugin=True, EnableAlign=True, FormatOnSave=True
     ```
   - If you see "Plugin or alignment is disabled", the options are disabled

3. **Check if Code Meets Formatting Criteria**:
   - Variable alignment: Needs consecutive variable declarations or assignments
   - Parameter alignment: Constructors need >2 parameters, methods need >3 parameters

4. **View Exception Logs**:
   - Check for "ERROR" messages in the output window
   - Check for error entries in Activity Log

---

## Verifying Extension Functionality

### Test Case 1: Variable Alignment

1. In the experimental instance, create or open a C# file
2. Enter the following code:

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

3. **Test Format Document**:
   - Press `Ctrl+K, Ctrl+D`
   - Expected result: Equals signs aligned

```csharp
public class Test
{
    public void Method()
    {
        int x      = 5;
        string name = "test";
        var value  = 100;
    }
}
```

4. **Test Format on Save** (if enabled):
   - Undo the previous change
   - Press `Ctrl+S` to save
   - Expected result: Equals signs aligned (if Format On Save is enabled)

### Test Case 2: Constructor Parameter Alignment

Enter the following code:

```csharp
public class MyClass
{
    public MyClass(int param1, string param2, bool param3)
    {
    }
}
```

After formatting, it should become:

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

### Test Case 3: Method Parameter Alignment

Enter the following code:

```csharp
public class Test
{
    public void MyMethod(int a, string b, bool c, double d)
    {
    }
}
```

After formatting, it should become:

```csharp
public class Test
{
    public void MyMethod(
        int a,
        string b,
        bool c,
        double d
    )
    {
    }
}
```

---

## Debugging Tips and Best Practices

### 1. Use Conditional Breakpoints

For frequently triggered code (like text change events), use conditional breakpoints to reduce debug interruptions:

Right-click breakpoint > **Conditions** > Set condition, e.g.:
```
pbstrMkDocument.Contains("MyFile.cs")
```

### 2. Watch Key Variables

During debugging, add these variables to the watch window:

- `options.EnablePlugin`
- `options.EnableAlign`
- `options.FormatOnSave`
- `textView` (check if null)
- `formattedText != text` (check if there are changes)

### 3. Use IntelliTrace (if available)

IntelliTrace can record the history of a debug session, helping diagnose hard-to-reproduce issues.

### 4. Log Timestamps

When diagnosing performance issues, you can temporarily add timestamp logs:

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... code ...
sw.Stop();
System.Diagnostics.Debug.WriteLine($"[CodeFormatter] Operation took {sw.ElapsedMilliseconds}ms");
```

### 5. Use Git for Comparison

If you suspect a certain change caused the problem:

```bash
git log --oneline
git checkout <previous-commit>
```

Then retest to determine which commit introduced the issue.

---

## Troubleshooting Checklist

When the extension isn't working, check in this order:

- [ ] 1. Is the extension installed in the experimental instance?
- [ ] 2. Is the extension enabled? (Check in Extension Manager)
- [ ] 3. Does the package initialization breakpoint hit?
- [ ] 4. Does the TextViewCreated breakpoint hit? (When opening C# files)
- [ ] 5. Are there "[CodeFormatter]" logs in the output window?
- [ ] 6. Are there errors in the Activity Log?
- [ ] 7. Are all options enabled? (Tools > Options > Code Align)
- [ ] 8. Do save/format event breakpoints hit?
- [ ] 9. Is the ApplyAlignment method called?
- [ ] 10. Are formattedText and original text different?

---

## Getting Help

If you still encounter issues after following this guide, please collect the following information and submit an Issue:

1. **Visual Studio Version**: Help > About Microsoft Visual Studio
2. **Extension Version**: From VSIX manifest or Extension Manager
3. **Reproduction Steps**: Detailed description of how to reproduce the issue
4. **Log Output**:
   - "[CodeFormatter]" logs from the output window
   - Relevant entries from Activity Log
5. **Test Code**: C# code sample that causes the issue
6. **Screenshots**: If helpful

---

## Changelog

### 2025-11-21

- ✅ Added MEF component asset to VSIX manifest
- ✅ Added detailed diagnostic logging in all key components
- ✅ Improved error handling and logging
- ✅ Created this debugging guide document

---

## Reference Resources

- [Visual Studio SDK Documentation](https://docs.microsoft.com/visualstudio/extensibility/)
- [MEF (Managed Extensibility Framework)](https://docs.microsoft.com/dotnet/framework/mef/)
- [Roslyn API Documentation](https://docs.microsoft.com/dotnet/csharp/roslyn-sdk/)
- [Debugging Visual Studio Extensions](https://docs.microsoft.com/visualstudio/extensibility/debugger/)
