# AppMonitor 测试场景配置

本目录包含 MCP 测试场景的所有配置文件和资源。

## 文件说明

### 场景文件
- **Assets/Scenes/AppMonitorTestScene.unity**
  - 主测试场景
  - 包含：
    - `AppMonitorUI` GameObject（带 UIDocument 组件）
    - `Main Camera`（配置为录制 UI 的最佳视角）
    - `Directional Light`（基础光照）
    - `EventSystem`（UI 事件支持）

### 预制体
- **Assets/Prefabs/AppMonitorUI.prefab**
  - 可复用的 UI Toolkit 预制体
  - 包含 UIDocument 组件
  - 引用 AppMonitorSection.uxml

### 配置脚本
- **Assets/Editor/AppMonitorTestSceneSetup.cs**
  - Unity 编辑器扩展
  - 菜单项：`NZ VisualTest/配置 AppMonitor 测试场景`
  - 菜单项：`NZ VisualTest/创建 MCP 调用序列`

### MCP 调用序列
- **Assets/McpCallSequence.json**
  - JSON 格式的 MCP 工具调用序列
  - 包含 9 个步骤的配置流程
  - 可用于自动化场景搭建

## 使用方法

### 方法 1：使用 Unity 编辑器菜单
1. 打开 Unity Editor
2. 选择菜单：`NZ VisualTest/配置 AppMonitor 测试场景`
3. 脚本将自动：
   - 打开场景文件
   - 配置 UIDocument 引用
   - 设置相机参数
   - 保存场景
   - 创建/更新预制体

### 方法 2：使用 MCP 工具
参考 `Assets/McpCallSequence.json` 中的调用序列，按顺序执行：
1. 创建场景
2. 创建 UI GameObject
3. 添加 UIDocument 组件
4. 设置 UXML 引用
5. 配置相机
6. 创建预制体
7. 保存场景

## 相机配置

- **位置**：(0, 0, -10)
- **旋转**：(0, 0, 0)
- **Clear Flags**：Solid Color
- **背景色**：深灰色 (RGB: 0.15, 0.15, 0.15)

此配置适合录制 UI 界面，背景简洁不分散注意力。

## UI Toolkit 配置

- **UXML 路径**：`Packages/com.nz.visualtest/Editor/Windows/Components/AppMonitor/AppMonitorSection.uxml`
- **USS 路径**：`Packages/com.nz.visualtest/Editor/Windows/Components/AppMonitor/AppMonitorSection.uss`

## 注意事项

1. 首次打开场景时，UIDocument 的 UXML 引用可能需要重新配置
2. 使用编辑器脚本 `AppMonitorTestSceneSetup.cs` 可以自动修复引用
3. 预制体保存在 `Assets/Prefabs/` 目录下，可在其他场景中复用

## 录制建议

- 使用纯色背景便于后期处理
- 相机距离 UI 适中，确保内容清晰可见
- 分辨率建议：1920x1080 或 1280x720
