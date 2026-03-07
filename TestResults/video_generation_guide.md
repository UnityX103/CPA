# 如何生成 macOS 应用监控测试视频

## 当前状态
- ✅ Unity 编辑器已打开
- ✅ 所有代码已实现并编译通过
- ✅ 测试场景已配置
- ⏳ 需要运行测试生成视频

## 方法 1：通过 Unity Test Runner（推荐）

### 步骤：
1. 在 Unity 编辑器中，打开 **Window > General > Test Runner**
2. 切换到 **PlayMode** 标签
3. 找到测试：`NZ.VisualTest.Tests.AppMonitorVisualTest`
4. 点击 **Run All** 或右键单个测试点击 **Run**
5. 等待测试完成（约 10-30 秒）
6. 测试视频将自动保存到：`TestVideo/` 目录

### 预期结果：
- 测试通过（绿色勾号）
- 生成视频文件：`TestVideo/Test_AppMonitor_*.mp4`
- 视频内容：显示应用监控界面，实时获取聚焦应用的图标和名称

## 方法 2：通过菜单命令

1. 在 Unity 编辑器中，选择 **NZ VisualTest > 配置 AppMonitor 测试场景**
2. 打开 **Window > General > Test Runner**
3. 运行 PlayMode 测试

## 方法 3：通过代码触发（如果上述方法不可用）

在 Unity 编辑器中创建一个临时脚本：

```csharp
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

public class RunVisualTest
{
    [MenuItem("NZ VisualTest/Run AppMonitor Test")]
    static void RunTest()
    {
        var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter()
        {
            testMode = TestMode.PlayMode,
            testNames = new[] { "NZ.VisualTest.Tests.AppMonitorVisualTest" }
        };
        testRunnerApi.Execute(new ExecutionSettings(filter));
    }
}
```

## 视频位置

测试完成后，视频文件将保存在：
```
/Users/xpy/Desktop/NanZhai/CPA/TestVideo/
```

文件名格式：
```
Test_AppMonitor_DisplaysCurrentApp_20260307_171234.mp4
Test_AppMonitor_UpdatesOnFocusChange_20260307_171245.mp4
Test_AppMonitor_HandlesPermissionDenied_20260307_171256.mp4
```

## 故障排除

### 如果视频未生成：

1. **检查 Unity Recorder 包**：
   - Window > Package Manager
   - 搜索 "Recorder"
   - 确保已安装 `com.unity.recorder`

2. **检查 VisualTestBase 配置**：
   - 打开 `localpackage/com.nz.visualtest/Runtime/VisualTestBase.cs`
   - 确认 `StartRecording()` 方法被调用

3. **检查控制台错误**：
   - 打开 Unity Console（Ctrl/Cmd + Shift + C）
   - 查看是否有录制相关错误

4. **手动录制**：
   - Window > General > Recorder > Recorder Window
   - 添加 Movie Recorder
   - 手动运行测试并录制

## 需要帮助？

如果遇到问题，请提供：
1. Unity Console 的错误信息
2. Test Runner 的测试结果截图
3. TestVideo 目录的内容（`ls -la TestVideo/`）

---
生成时间：2026-03-07 17:24
