# EnSharper 扩展调试指南

本文档提供了如何调试和验证 EnSharper Visual Studio 扩展的详细指导。

## 目录

1. [问题分析](#问题分析)
2. [调试前的准备](#调试前的准备)
3. [启动调试会话](#启动调试会话)
4. [设置断点和验证执行流程](#设置断点和验证执行流程)
5. [查看诊断日志](#查看诊断日志)
6. [常见问题排查](#常见问题排查)
7. [验证扩展是否生效](#验证扩展是否生效)

---

## 问题分析

### 扩展可能不工作的原因

1. **MEF 组件未正确导出**：扩展使用 MEF（Managed Extensibility Framework）来注入组件，如果 MEF 导出配置不正确，组件不会被加载。

2. **包（Package）未加载**：Visual Studio 包需要正确注册并在需要时加载。

3. **事件监听器未正确注册**：保存和格式化事件的监听器可能未正确挂接。

4. **选项设置禁用了功能**：扩展选项可能被禁用。

### 已修复的问题

本次更新已经修复了以下问题：

1. ✅ **添加了 MEF 组件资产**：在 `source.extension.vsixmanifest` 中添加了 `Microsoft.VisualStudio.MefComponent` 资产声明。

2. ✅ **增强了诊断日志**：在所有关键路径添加了详细的调试输出和 ActivityLog 记录。

3. ✅ **改进了错误处理**：所有组件都有适当的异常处理和日志记录。

---

## 调试前的准备

### 1. 构建扩展

在 Visual Studio 中打开解决方案：

```
EnSharper.slnx
```

确保选择 **Debug** 配置，然后构建解决方案：

- 菜单：**生成** > **生成解决方案**
- 快捷键：`Ctrl+Shift+B`

### 2. 检查构建输出

构建成功后，检查输出目录中是否生成了 VSIX 文件：

```
CodeFormatter/bin/Debug/CodeFormatter.vsix
```

### 3. 卸载旧版本（如果存在）

如果之前安装过扩展的旧版本：

1. 打开 **扩展** > **管理扩展**
2. 找到 "CodeFormatter" 扩展
3. 点击 **卸载**
4. 重启 Visual Studio

---

## 启动调试会话

### 方法 1：通过 Visual Studio 启动调试

1. 在 Visual Studio 中打开 `CodeFormatter.csproj` 项目
2. 按 `F5` 或点击 **调试** > **启动调试**
3. 这将启动一个新的 Visual Studio 实验实例（Experimental Instance）
4. 实验实例的窗口标题会显示 "- Experimental Instance"

### 方法 2：手动附加到进程

1. 先启动 Visual Studio 实验实例：
   ```
   devenv.exe /rootsuffix Exp
   ```
2. 在开发 Visual Studio 中，选择 **调试** > **附加到进程**
3. 选择实验实例的 `devenv.exe` 进程
4. 确保选中 **托管代码** 调试器类型

---

## 设置断点和验证执行流程

### 关键断点位置

为了验证扩展是否正确加载和执行，在以下位置设置断点：

#### 1. 包初始化（Package Initialization）

**文件**：`CodeFormatterPackage.cs`  
**方法**：`InitializeAsync`  
**行号**：约第 49 行

```csharp
System.Diagnostics.Debug.WriteLine("[CodeFormatter] Package InitializeAsync - Starting");
```

**验证**：当 Visual Studio 加载扩展包时，应该触发此断点。

#### 2. 文本视图创建（Text View Creation）

**文件**：`TextViewCreationListener.cs`  
**方法**：`TextViewCreated`  
**行号**：约第 22 行

```csharp
System.Diagnostics.Debug.WriteLine("[CodeFormatter] TextViewCreated - Starting initialization");
```

**验证**：当打开 C# 文件时，应该触发此断点。

#### 3. 文档保存事件（Document Save）

**文件**：`DocumentSaveListener.cs`  
**方法**：`OnBeforeSave`  
**行号**：约第 92 行

```csharp
System.Diagnostics.Debug.WriteLine($"[CodeFormatter] OnBeforeSave - docCookie: {docCookie}");
```

**验证**：当保存 C# 文件时（Ctrl+S），应该触发此断点。

#### 4. 格式化文档命令（Format Document）

**文件**：`FormatCommandFilter.cs`  
**方法**：`Exec`  
**行号**：约第 99 行

```csharp
System.Diagnostics.Debug.WriteLine("[CodeFormatter] Format Document command detected");
```

**验证**：当执行格式化文档命令（Ctrl+K, Ctrl+D）时，应该触发此断点。

#### 5. 对齐应用（Alignment Application）

**文件**：`AlignmentHelper.cs`  
**方法**：`ApplyAlignment`  
**行号**：约第 29 行

```csharp
System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ApplyAlignment - checkFormatOnSave={checkFormatOnSave}");
```

**验证**：当应用代码对齐时，应该触发此断点。

### 断点调试流程

1. **打开 C# 文件**：
   - 触发：`TextViewCreated`
   - 预期：创建文档保存监听器和命令过滤器

2. **保存文件**（Ctrl+S）：
   - 触发：`OnBeforeSave` → `ApplyAlignment`（如果启用了 FormatOnSave）
   - 预期：应用代码对齐格式化

3. **格式化文档**（Ctrl+K, Ctrl+D）：
   - 触发：`Exec` → `ApplyAlignment`
   - 预期：应用代码对齐格式化

---

## 查看诊断日志

扩展现在包含详细的诊断日志输出。有两种方法查看这些日志：

### 方法 1：Visual Studio 输出窗口

1. 在实验实例中，打开 **视图** > **输出**
2. 在输出窗口的下拉菜单中选择 **调试**
3. 所有以 `[CodeFormatter]` 开头的日志都是扩展输出的

**示例日志输出**：
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

### 方法 2：Visual Studio Activity Log

Activity Log 是 Visual Studio 的系统级日志，记录所有扩展的活动。

#### 启用 Activity Log：

启动 Visual Studio 实验实例时使用 `/log` 参数：

```
devenv.exe /rootsuffix Exp /log
```

#### 查看 Activity Log：

日志文件位置：
```
%AppData%\Microsoft\VisualStudio\17.0_<instance_id>Exp\ActivityLog.xml
```

可以使用 Visual Studio 提供的工具查看：
```
"%VS_INSTALL_DIR%\Common7\IDE\ActivityLogViewer.exe"
```

或者直接用文本编辑器打开 XML 文件搜索 "CodeFormatter"。

**Activity Log 中的关键条目**：

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

## 常见问题排查

### 问题 1：断点未触发

**症状**：设置的断点显示为空心圆圈，或者从不触发。

**可能原因和解决方案**：

1. **未附加到正确的进程**
   - 确保附加到实验实例的 devenv.exe
   - 检查调试器类型是否包含 "托管代码"

2. **PDB 文件不匹配**
   - 清理并重新构建解决方案（**生成** > **清理解决方案** 然后 **生成解决方案**）
   - 确保使用 Debug 配置构建

3. **代码优化问题**
   - 确认 Debug 配置中 `<Optimize>false</Optimize>`
   - 确认 `<DebugType>full</DebugType>`

### 问题 2：扩展未加载

**症状**：实验实例中看不到扩展，或扩展显示为已安装但未启用。

**诊断步骤**：

1. **检查扩展是否已安装**：
   - 打开 **扩展** > **管理扩展**
   - 在"已安装"中查找 "CodeFormatter"

2. **检查 VSIX 安装日志**：
   ```
   %LocalAppData%\Microsoft\VisualStudio\17.0_<instance_id>Exp\Extensions\Extensions.log
   ```

3. **手动安装 VSIX**：
   - 双击 `CodeFormatter.vsix` 文件
   - 确保选择安装到实验实例

4. **重置实验实例**：
   ```
   "%VS_INSTALL_DIR%\VSSDK\VisualStudioIntegration\Tools\Bin\CreateExpInstance.exe" /Reset /VSInstance=17.0 /RootSuffix=Exp
   ```

### 问题 3：MEF 组件未加载

**症状**：TextViewCreated 从不被调用。

**诊断步骤**：

1. **检查 MEF 缓存**：
   - 删除 MEF 缓存目录：
     ```
     %LocalAppData%\Microsoft\VisualStudio\17.0_<instance_id>Exp\ComponentModelCache
     ```
   - 重启实验实例

2. **验证 MEF 导出**：
   - 检查 `TextViewCreationListener.cs` 是否有 `[Export]` 特性
   - 检查 `source.extension.vsixmanifest` 是否包含 MEF 组件资产

3. **使用 MEF 诊断工具**：
   - Visual Studio 提供了 MEF 诊断工具来查看已加载的组件
   - 菜单：**工具** > **MEF 诊断**（如果可用）

### 问题 4：格式化或保存时无效果

**症状**：断点触发了，但代码没有变化。

**诊断步骤**：

1. **检查选项设置**：
   - 打开 **工具** > **选项**
   - 导航到 **Code Align** > **General**
   - 确保：
     - ✅ Enable Plugin = true
     - ✅ Enable Align = true
     - ✅ Format On Save = true（如果测试保存功能）

2. **查看日志输出**：
   - 在输出窗口中查找类似以下的消息：
     ```
     [CodeFormatter] ApplyAlignment - Options: EnablePlugin=True, EnableAlign=True, FormatOnSave=True
     ```
   - 如果看到 "Plugin or alignment is disabled"，则是选项被禁用

3. **检查代码是否符合格式化条件**：
   - 变量对齐：需要连续的变量声明或赋值
   - 参数对齐：构造函数需要 >2 个参数，方法需要 >3 个参数

4. **查看异常日志**：
   - 检查输出窗口中是否有 "ERROR" 消息
   - 检查 Activity Log 中的错误条目

---

## 验证扩展是否生效

### 测试用例 1：变量对齐

1. 在实验实例中，创建或打开一个 C# 文件
2. 输入以下代码：

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

3. **测试格式化文档**：
   - 按 `Ctrl+K, Ctrl+D`
   - 预期结果：等号对齐

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

4. **测试保存时格式化**（如果启用）：
   - 撤销上一步的更改
   - 按 `Ctrl+S` 保存
   - 预期结果：等号对齐（如果 Format On Save 已启用）

### 测试用例 2：构造函数参数对齐

输入以下代码：

```csharp
public class MyClass
{
    public MyClass(int param1, string param2, bool param3)
    {
    }
}
```

格式化后应该变成：

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

### 测试用例 3：方法参数对齐

输入以下代码：

```csharp
public class Test
{
    public void MyMethod(int a, string b, bool c, double d)
    {
    }
}
```

格式化后应该变成：

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

## 调试技巧和最佳实践

### 1. 使用条件断点

对于频繁触发的代码（如文本更改事件），使用条件断点可以减少调试中断：

右键断点 > **条件** > 设置条件，例如：
```
pbstrMkDocument.Contains("MyFile.cs")
```

### 2. 监视关键变量

在调试时，添加以下变量到监视窗口：

- `options.EnablePlugin`
- `options.EnableAlign`
- `options.FormatOnSave`
- `textView` （查看是否为 null）
- `formattedText != text` （查看是否有更改）

### 3. 使用 IntelliTrace（如果可用）

IntelliTrace 可以记录调试会话的历史，帮助诊断难以重现的问题。

### 4. 记录时间戳

在诊断性能问题时，可以临时添加时间戳日志：

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... 代码 ...
sw.Stop();
System.Diagnostics.Debug.WriteLine($"[CodeFormatter] Operation took {sw.ElapsedMilliseconds}ms");
```

### 5. 使用 Git 进行对比

如果怀疑某次更改导致问题：

```bash
git log --oneline
git checkout <previous-commit>
```

然后重新测试，确定是哪次提交引入的问题。

---

## 故障排查清单

当扩展不工作时，按以下顺序检查：

- [ ] 1. 扩展是否已安装在实验实例中？
- [ ] 2. 扩展是否已启用？（扩展管理器中查看）
- [ ] 3. 包初始化断点是否触发？
- [ ] 4. TextViewCreated 断点是否触发？（打开 C# 文件时）
- [ ] 5. 输出窗口是否有 "[CodeFormatter]" 日志？
- [ ] 6. Activity Log 中是否有错误？
- [ ] 7. 选项是否都已启用？（Tools > Options > Code Align）
- [ ] 8. 保存/格式化事件断点是否触发？
- [ ] 9. ApplyAlignment 方法是否被调用？
- [ ] 10. formattedText 和原始 text 是否不同？

---

## 获取帮助

如果按照本指南操作后仍然遇到问题，请收集以下信息并提交 Issue：

1. **Visual Studio 版本**：帮助 > 关于 Microsoft Visual Studio
2. **扩展版本**：从 VSIX 清单或扩展管理器中获取
3. **重现步骤**：详细描述如何重现问题
4. **日志输出**：
   - 输出窗口的 "[CodeFormatter]" 日志
   - Activity Log 的相关条目
5. **测试代码**：导致问题的 C# 代码示例
6. **屏幕截图**：如果有帮助的话

---

## 更新日志

### 2025-11-21

- ✅ 在 VSIX 清单中添加了 MEF 组件资产
- ✅ 在所有关键组件中添加了详细的诊断日志
- ✅ 改进了错误处理和日志记录
- ✅ 创建了此调试指南文档

---

## 参考资源

- [Visual Studio SDK 文档](https://docs.microsoft.com/visualstudio/extensibility/)
- [MEF (Managed Extensibility Framework)](https://docs.microsoft.com/dotnet/framework/mef/)
- [Roslyn API 文档](https://docs.microsoft.com/dotnet/csharp/roslyn-sdk/)
- [调试 Visual Studio 扩展](https://docs.microsoft.com/visualstudio/extensibility/debugger/)
