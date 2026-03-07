# AGENTS.md — DevTemplate Unity 项目

## 项目概述

Unity 2022.3+ LTS 项目，包含自定义本地包、可视化测试框架和 MCP 集成。

## 回答语言
所有的回答内容都需要使用中文

### 单测试执行
```bash
# 按测试名称过滤
unity -runTests -testPlatform EditMode -testFilter "Namespace.TestClass.TestMethod"

# 通过 MCP：使用 run_tests 工具，testNames 参数为：["Full.Test.Name"]
```

### 程序集构建
- **无外部构建命令** — Unity 在资源刷新时自动编译
- 在 Unity 控制台或通过 MCP get_console_errors 检查编译错误
- 使用程序集定义文件（.asmdef）进行模块化编译

### 检查/分析
- 未配置显式检查工具
- 使用 MCP script_validation 工具（Basic/Standard/Comprehensive/Strict 级别）
- Unity Rider/VSCode 包提供 IDE 分析功能

## 代码风格指南

### 命名规范
```csharp
// 类/结构体：帕斯卡命名法（PascalCase）
public class VisualTestBase { }

// 接口：帕斯卡命名法，带 'I' 前缀
public interface IMcpResponse { }

// 方法：帕斯卡命名法
public void HandleCommand() { }

// 属性：帕斯卡命名法
public string TestName { get; set; }

// 私有字段：驼峰命名法（camelCase），带下划线前缀
private string _currentJobId;
private GameObject _cameraObject;

// 常量：帕斯卡命名法或全大写
private const int FailureCap = 25;
private const string SessionKeyJobs = "MCPForUnity.TestJobsV1";

// 事件：帕斯卡命名法，方法带 'On' 前缀
public event Action OnTestStarted;
```


### 格式规范
- **缩进**：4 个空格（不使用制表符）
- **大括号**：Allman 风格（左大括号在新行）
- **最大行长度**：约 120 个字符
- **始终使用显式访问修饰符**（public、private、internal）

### 类型安全
- 对非继承设计的类使用 `sealed`
- 对不可变字段优先使用 `readonly`
- 对包内部可见性使用 `internal`
- 对仅编辑器代码使用 `#if UNITY_EDITOR`

### 空值处理
```csharp
// 使用空值条件运算符
var name = job?.JobId;

// 解引用前检查空值
if (string.IsNullOrWhiteSpace(jobId)) return null;

// 使用 ?? 设置默认值
var status = job?.Status ?? TestJobStatus.Running;
```

### 错误处理
```csharp
// 对外部操作使用 try-catch
private static void TryRestoreFromSessionState()
{
    try { /* ... */ }
    catch (Exception ex)
    {
        // 尽力而为：永远不要阻塞编辑器加载
        McpLog.Warn($"[TestJobManager] 失败：{ex.Message}");
    }
}

// 返回错误响应，不要为预期失败抛出异常
return new ErrorResponse("tests_running", new { retry_after_ms = 5000 });
```

## 架构模式

### 程序集组织
```
Scripts
├── Runtime/           # 运行时代码
│   ├── Scripts/
│   └── PackageName.Runtime.asmdef
├── Editor/            # 仅编辑器代码
│   ├── Scripts/
│   └── PackageName.Editor.asmdef
└── Tests/
    ├── Runtime/       # PlayMode 测试
    └── Editor/        # EditMode 测试
```

### 命名空间规范
```csharp
// 与文件夹结构完全匹配
namespace MCPForUnity.Editor.Services { }
namespace NZ.VisualTest.Runtime { }
```

### 测试模式
```csharp
[TestFixture]
public class VisualTestExample : VisualTestBase
{
    [UnityTest]
    public IEnumerator Test_RotatingSquare()
    {
        // 准备
        _square = GameObject.CreatePrimitive(PrimitiveType.Quad);
        
        // 执行
        yield return new WaitForSeconds(2f);
        
        // 断言
        LogInputAction("测试完成");
    }
    
    [UnitySetUp]
    public IEnumerator SetUp() { yield return null; }
    
    [UnityTearDown]
    public IEnumerator TearDown() { yield return null; }
}
```

### 特性（Attributes）
```csharp
// 自定义工具注册
[McpForUnityTool("tool_name", AutoRegister = false)]

// 资源端点
[McpForUnityResource("resource/name")]
```

## 主要依赖项

- **Unity Test Framework** 1.1.33 — 使用 NUnit 进行测试
- **Input System** 1.7.0 — 新的输入处理
- **Recorder** 4.0.3 — 视频/截图录制
- **Newtonsoft.Json** 3.2.1 — JSON 序列化

## 项目结构

```
DevTemplate/
├── Assets/                    # 主游戏资源（模板中为空）
├── ├── Scripts                 # 核心代码
├── localpackage/             # 自定义包
│   ├── com.unity.mcp/       # Unity 版 MCP 集成
│   └── com.nz.visualtest/   # 可视化测试框架
├── Packages/                 # Unity 包清单
├── ProjectSettings/         # Unity 项目设置
└── Library/                 # Unity 缓存（已加入 gitignore）
```

## MCP 集成

项目包含 MCP for Unity（com.coplaydev.unity-mcp），用于 AI 助手集成：

- 可通过 MCP 工具运行测试：`run_tests`、`get_test_job`
- 可通过 `get_console_errors` 访问控制台错误

## 版本控制
Git 每次开始任务前都需要先提交当前的更改

## 本地化

- 主要语言：中文（中文）用于注释和用户可见字符串
- 代码标识符：英文

---

*此文件为 AI 辅助编程生成。项目规范变更时请更新。*
