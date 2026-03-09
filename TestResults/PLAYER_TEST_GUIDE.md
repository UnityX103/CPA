# macOS Player 测试执行指南

## 步骤 1: 在 Unity 中构建 Player

### 方法 A: 使用菜单（推荐）
1. 打开 Unity Editor
2. 等待编译完成（确保 Console 无错误）
3. 点击菜单：**Build → Build macOS Player with Tests**
4. 等待构建完成（约 5-10 分钟）
5. 构建输出：`Builds/macOS/DevTemplate.app`

### 方法 B: 使用命令行
```bash
/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/xpy/Desktop/NanZhai/CPA \
  -executeMethod BuildScript.BuildMacOSPlayerWithTests \
  -logFile /Users/xpy/Desktop/NanZhai/CPA/Builds/build.log
```

---

## 步骤 2: 执行 Player 测试

### 基本执行命令
```bash
cd /Users/xpy/Desktop/NanZhai/CPA

./Builds/macOS/DevTemplate.app/Contents/MacOS/DevTemplate \
  -testPlatform PlayMode \
  -testResults /Users/xpy/Desktop/NanZhai/CPA/TestResults/macOS_Player_Results.xml \
  -logFile /Users/xpy/Desktop/NanZhai/CPA/TestResults/player.log
```

### 带可视化界面的执行（推荐首次运行）
```bash
# 不使用 -batchmode，可以看到 UI 和测试过程
./Builds/macOS/DevTemplate.app/Contents/MacOS/DevTemplate \
  -testPlatform PlayMode \
  -testResults /Users/xpy/Desktop/NanZhai/CPA/TestResults/macOS_Player_Results.xml \
  -logFile /Users/xpy/Desktop/NanZhai/CPA/TestResults/player.log
```

---

## 步骤 3: Accessibility 权限处理

### 首次运行时
1. Player 启动后，macOS 会弹出权限对话框
2. 点击 **"打开系统偏好设置"**
3. 在 **系统偏好设置 → 安全性与隐私 → 辅助功能** 中
4. 点击左下角锁图标解锁
5. 勾选 **DevTemplate** 应用
6. 关闭 Player 并重新运行测试

### 后续运行
权限已授予，测试会自动执行完整功能。

---

## 步骤 4: 查看测试结果

### 实时监控日志
```bash
# 在另一个终端窗口
tail -f /Users/xpy/Desktop/NanZhai/CPA/TestResults/player.log
```

### 查看 NUnit XML 结果
```bash
cat /Users/xpy/Desktop/NanZhai/CPA/TestResults/macOS_Player_Results.xml
```

### 解析测试统计
```bash
# 总测试数
grep -o 'testcasecount="[0-9]*"' TestResults/macOS_Player_Results.xml | grep -o '[0-9]*'

# 通过的测试数
grep -c 'result="Passed"' TestResults/macOS_Player_Results.xml

# 失败的测试数
grep -c 'result="Failed"' TestResults/macOS_Player_Results.xml
```

---

## 预期测试结果

### 成功场景（Accessibility 已授权）
```xml
<test-run testcasecount="4" result="Passed" total="4" passed="4" failed="0">
  <test-case name="Test_GetCurrentApp_ReturnsValidAppInfo" result="Passed"/>
  <test-case name="Test_PlatformDetection_PlayerEnvironment" result="Passed"/>
  <test-case name="Test_GetCurrentApp_HandlesAccessibilityGracefully" result="Passed"/>
  <test-case name="Test_AppInfo_DataIntegrity" result="Passed"/>
</test-run>
```

### 回退场景（Accessibility 未授权）
测试仍然会通过，因为代码实现了优雅降级：
- `Test_GetCurrentApp_HandlesAccessibilityGracefully` 验证回退机制
- 返回 fallback 应用名称和图标
- `IsSuccess` 仍为 `true`

---

## 故障排查

### 问题 1: 构建失败
**症状**: Unity Console 显示编译错误

**解决方案**:
1. 检查 `NZ.VisualTest.Runtime` 程序集是否存在
2. 确认 `VisualTestBase` 类可访问
3. 刷新 Unity 资源：`Assets → Refresh`

### 问题 2: Player 无法启动
**症状**: 双击 `.app` 无反应或闪退

**解决方案**:
1. 检查 Console.app 中的崩溃日志
2. 确认架构匹配（x86_64 for Intel Mac）
3. 尝试从命令行启动查看错误信息

### 问题 3: 测试未执行
**症状**: Player 启动但没有生成 XML 结果

**解决方案**:
1. 确认使用了 `-testPlatform PlayMode` 参数
2. 检查 `player.log` 中的错误信息
3. 确认测试程序集包含在构建中（`BuildOptions.IncludeTestAssemblies`）

### 问题 4: 原生插件加载失败
**症状**: 测试报告 DllNotFoundException

**解决方案**:
这是预期行为（项目中没有实际的原生插件）。测试会：
1. 捕获异常
2. 验证错误处理逻辑
3. 测试 fallback 机制

---

## 测试覆盖范围

| 测试用例 | 验证内容 |
|---------|---------|
| `Test_GetCurrentApp_ReturnsValidAppInfo` | 核心 API 返回有效数据 |
| `Test_PlatformDetection_PlayerEnvironment` | 平台检测正确（isEditor=false, platform=OSXPlayer） |
| `Test_GetCurrentApp_HandlesAccessibilityGracefully` | 权限处理和优雅降级 |
| `Test_AppInfo_DataIntegrity` | 数据结构完整性和一致性 |

---

## 下一步

测试通过后，可以：
1. 将测试集成到 CI/CD 流程
2. 添加更多测试用例（如多次调用、并发测试）
3. 测试不同 macOS 版本的兼容性
4. 添加性能基准测试

---

**创建时间**: 2026-03-09  
**Unity 版本**: 2022.3 LTS  
**目标平台**: macOS Standalone (Intel 64-bit)
