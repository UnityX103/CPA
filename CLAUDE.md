# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 回答语言

所有回答内容必须使用**中文**。

## 项目概述

Unity 6（6000.0.25f1）桌面宠物小游戏。使用 **QFramework** 作为架构框架，**UniWindowController** 实现透明窗口与点击穿透，macOS 原生插件通过 Objective-C 调用无障碍 API 获取前台应用信息。

**透明窗口**：`UniWindowController`（`isTransparent=true`，`hitTestType=1` 基于 Alpha 点击穿透）

## 常用命令

### 构建

```bash
# 命令行构建（自动签名 + 验证）
./build_macos.sh

# 查看构建日志
cat build.log
```

Unity 编辑器菜单：**Build → Build and Package macOS App**

### 编译检查

Unity 在资源刷新时自动编译，无需手动命令。通过以下方式查看错误：
- Unity Console 窗口
- MCP 工具：`read_console`（filter types: error/warning）
- MCP 工具：`validate_script`（级别：basic / standard）

### 测试

通过 MCP 运行单个测试：`run_tests`，参数 `testNames: ["Full.Test.Name"]`

## QFramework 架构规范

项目使用 QFramework v1.0（`Assets/Scripts/QFramework.cs`，单文件，约 950 行）。

### 四层结构与职责边界

```
表现层  ViewController（MonoBehaviour）   接收输入、渲染状态；只读 System/Model；写操作必须发 Command
系统层  System（AbstractSystem）          多个 ViewController 共享的逻辑；只读 Model；下行通信发 Event
数据层  Model（AbstractModel）            数据定义与 CRUD；只访问 Utility；状态变更发 Event
工具层  Utility（IUtility）               基础设施封装（存储、序列化、网络等）；无依赖
```

### 层级通信规则（严格执行）

| 方向 | 方式 | 说明 |
|------|------|------|
| 上层 → 下层 | 方法调用 | Controller/System 直接调用 Model/Utility 方法 |
| 下层 → 上层 | Event | Model/System 发事件，上层订阅；**严禁下层持有上层引用** |
| Controller → 状态修改 | Command | **所有**状态写操作必须封装为 Command，不得直接修改 |
| 跨层查询 | Query | 需要返回值的跨层查询使用 Query，不用 Command |

**禁止行为：**
- Model/System 中调用 Controller 方法或持有其引用
- Controller 直接修改 Model 属性（必须走 Command）
- Command 持有字段状态（Command 是无状态的操作对象）
- System 直接修改 Model 属性（应通过 Command 或 Model 提供的方法）

### Architecture 注册

```csharp
public class GameApp : Architecture<GameApp>
{
    protected override void Init()
    {
        // 注册顺序：Utility → Model → System
        RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());
        RegisterModel<IPetModel>(new PetModel());
        RegisterSystem<IInputSystem>(new InputSystem());
    }
}
```

### 各层实现模板

**Controller（MonoBehaviour）**
```csharp
public class PetController : MonoBehaviour, IController
{
    IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

    private void Start()
    {
        // 读取数据
        var model = this.GetModel<IPetModel>();
        // 订阅事件
        this.RegisterEvent<PetMoodChangedEvent>(OnMoodChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void OnButtonClick()
    {
        // 写操作必须用 Command
        this.SendCommand(new FeedPetCommand());
    }

    private void OnMoodChanged(PetMoodChangedEvent e) { /* 刷新 UI */ }
}
```

**Command（无状态）**
```csharp
public class FeedPetCommand : AbstractCommand
{
    protected override void OnExecute()
    {
        var model = this.GetModel<IPetModel>();
        model.Hunger.Value -= 10;
        this.SendEvent(new PetFedEvent());
    }
}

// 带参数：通过构造函数传入
public class SetPetNameCommand : AbstractCommand
{
    private readonly string _name;
    public SetPetNameCommand(string name) => _name = name;

    protected override void OnExecute() =>
        this.GetModel<IPetModel>().Name.Value = _name;
}
```

**Query（带返回值的跨层查询）**
```csharp
public class GetPetHappinessQuery : AbstractQuery<float>
{
    protected override float OnDo()
    {
        var model = this.GetModel<IPetModel>();
        return model.Hunger.Value * 0.5f + model.Energy.Value * 0.5f;
    }
}
// 调用：float happiness = this.SendQuery(new GetPetHappinessQuery());
```

**Model**
```csharp
public interface IPetModel : IModel
{
    BindableProperty<string> Name { get; }
    BindableProperty<int> Hunger { get; }
}

public class PetModel : AbstractModel, IPetModel
{
    public BindableProperty<string> Name { get; } = new BindableProperty<string>("小猫");
    public BindableProperty<int> Hunger { get; } = new BindableProperty<int>(50);

    protected override void OnInit()
    {
        // 从持久化加载初始值
        var storage = this.GetUtility<IStorageUtility>();
        Name.Value = storage.Load("PetName", "小猫");
        // 监听变更自动持久化
        Name.Register(v => storage.Save("PetName", v));
    }
}
```

**System**
```csharp
public class TimerSystem : AbstractSystem, ITimerSystem
{
    protected override void OnInit()
    {
        // 注册定时逻辑，通过 Event 通知上层
    }

    public void OnMinutePassed()
    {
        this.GetModel<IPetModel>().Hunger.Value += 1;
        this.SendEvent(new HungerIncreasedEvent());
    }
}
```

### BindableProperty 用法

```csharp
// 订阅变化（含初始值回调）
model.Hunger.RegisterWithInitValue(v => hungerBar.value = v)
    .UnRegisterWhenGameObjectDestroyed(gameObject);

// 只订阅后续变化
model.Hunger.Register(v => Debug.Log($"饥饿度变为 {v}"));

// 直接读写
int current = model.Hunger.Value;
model.Hunger.Value = 30;

// 不触发事件的静默赋值
model.Hunger.SetValueWithoutEvent(30);
```

### TypeEventSystem（全局事件）

```csharp
// 定义事件（struct）
public struct PetFedEvent { }
public struct PetMoodChangedEvent { public float NewMood; }

// 发送
this.SendEvent<PetFedEvent>();
this.SendEvent(new PetMoodChangedEvent { NewMood = 0.8f });

// 订阅（在 Architecture 内的层）
this.RegisterEvent<PetFedEvent>(OnPetFed);

// 全局订阅（不在 Architecture 内）
TypeEventSystem.Global.Register<PetFedEvent>(OnPetFed)
    .UnRegisterWhenGameObjectDestroyed(gameObject);
```

## 文件与程序集组织

```
Assets/Scripts/
├── QFramework.cs               ← QFramework 单文件（不修改）
├── GameApp.cs                  ← Architecture 注册入口
├── Models/                     ← IModel 实现
├── Systems/                    ← ISystem 实现
├── Commands/                   ← ICommand 实现（无状态）
├── Queries/                    ← IQuery 实现
├── Events/                     ← Event struct 定义
├── Utilities/                  ← IUtility 实现
└── Controllers/                ← MonoBehaviour Controllers
```

**无 asmdef**时归入 Assembly-CSharp。如需隔离，遵循：
```
Runtime 代码  → *.Runtime.asmdef
Editor 代码   → *.Editor.asmdef（含 #if UNITY_EDITOR 守卫）
命名空间      → 与文件夹结构完全对应
```

## 代码风格

- **缩进**：4 个空格，Allman 大括号风格
- **命名**：类/方法/属性 → PascalCase；私有字段 → `_camelCase`；接口 → `IFoo`；Event struct → `XxxEvent`
- **修饰符**：所有成员显式声明访问级别；非继承类用 `sealed`；不可变字段用 `readonly`
- **注释**：中文；代码标识符：英文
- **错误处理**：外部操作用 try-catch + 日志；预期失败返回错误响应，不抛异常
- **QFramework 特例**：Command/Query 类名用动词短语（`FeedPetCommand`、`GetHappinessQuery`）；Event 用名词短语（`PetFedEvent`）

## MCP 集成

项目内置 MCP for Unity，Claude Code 可直接操控编辑器：

| 工具 | 用途 |
|------|------|
| `run_tests` | 执行指定测试 |
| `get_test_job` | 查询测试执行状态 |
| `read_console` | 读取 Unity 控制台日志 |
| `validate_script` | 静态代码分析 |
| `manage_scene` | 场景操作 |
| `manage_gameobject` | GameObject 操作 |

## 版本控制

每次开始新任务前，先提交当前未提交的改动（`git commit`）。
