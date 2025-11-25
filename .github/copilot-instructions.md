# GitHub Copilot Instructions for EnSharper

This file provides technical context to help GitHub Copilot better understand the EnSharper codebase and provide more accurate suggestions.

## 🛠️ Technology Stack Details

- **Framework**: .NET Framework 4.7.2 (targeting legacy VS versions)
- **Language**: C# 7.3 (limited by framework constraints)
- **IDE Integration**: Visual Studio SDK (VS 2022+ compatible)
- **Code Analysis**: Roslyn APIs (Microsoft.CodeAnalysis.*)
- **Component Model**: MEF (Managed Extensibility Framework) for VS service composition
- **Build System**: MSBuild with VSIX packaging
- **Threading**: STA (Single-Threaded Apartment) for UI operations

## 🏗️ Architecture Deep Dive

### Core Components
- **AlignService**: Main formatting engine using Roslyn syntax trees
- **FormattingCoordinator**: Orchestrates formatting operations with workspace management
- **IAlignmentProcessor**: Interface for pluggable alignment rules
- **Event Listeners**: VS integration points (commands, saves, editor events)

### Key Design Patterns
- **Singleton Listeners**: Global event handlers for VS lifecycle management
- **Processor Pipeline**: Chain of responsibility for different alignment rules
- **Workspace-Aware Formatting**: Respects project .editorconfig and compilation options
- **Thread Safety**: All UI operations on main thread with `ThreadHelper.ThrowIfNotOnUIThread()`

### Formatting Pipeline
```
VS Command/Event → Listener → FormattingCoordinator → AlignService → Processors → TextBuffer Update
     ↓                    ↓              ↓                ↓            ↓              ↓
Format Document    BeforeExecute   TryFormat()    FormatCode()   Apply()     CreateEdit()
Save Event         OnBeforeSave    Workspace       Roslyn Format  Syntax      Apply()
                                      Resolution   + Alignment    Rewriting   Changes
```

### Workspace Management
- **Primary**: `textBuffer.Properties[typeof(Workspace)]` - Most accurate
- **Fallback**: `ComponentModel.GetService<Workspace>()` - Global workspace
- **Purpose**: Ensures .editorconfig and project settings are respected

### Performance Optimizations
- **Single-Pass Editing**: Avoids multiple `TextBuffer.CreateEdit()` calls
- **Incremental Processing**: Only processes changed content regions
- **Early Termination**: Skips formatting when content unchanged
- **Memory Management**: Proper disposal of Roslyn workspaces

## 🔧 Development Guidelines

### Code Style
- Use `ThreadHelper.ThrowIfNotOnUIThread()` for all UI operations
- Implement `IDisposable` for resources requiring cleanup
- Use `Logger.LogDebug()` for diagnostics (appears in VS Output window)
- Follow Roslyn patterns for syntax tree manipulation

### Common Patterns
```csharp
// VS Service Access
var componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
var workspace = componentModel.GetService<Workspace>();

// Roslyn Syntax Processing
var tree = CSharpSyntaxTree.ParseText(code);
var root = tree.GetRoot();
var newRoot = rewriter.Visit(root);

// Text Buffer Operations
using (var edit = textBuffer.CreateEdit())
{
    edit.Replace(0, snapshot.Length, newText);
    edit.Apply();
}
```

### Testing Approach
- Manual testing in VS Experimental Instance
- Verify with various .editorconfig settings
- Test multi-file scenarios and concurrent saves
- Check performance with large codebases

## 📁 Project Structure Reference

```
CodeFormatter/
├── Configuration/           # Options pages and settings
├── Formatting/              # Core formatting engine
├── Processors/              # Roslyn syntax rewriters
├── Listeners/               # VS event handlers
├── CodeFormatterPackage.cs  # VS package entry point
└── Logger.cs                # Diagnostic logging
```

This context helps Copilot understand the VS extension architecture, Roslyn integration patterns, and performance considerations for more accurate code suggestions.