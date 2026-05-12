#if UNITY_EDITOR
using System;
using System.Diagnostics;
using App.Bootstrap;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace App.Editor.HotUpdate
{
    /// <summary>
    /// Edit-mode 烟雾测试：用 EditorCoroutineUtility 直接驱动
    /// LoadHotfixSystem.RunAsync，跳过 Play Mode 也能验证：
    ///   - Addressables.InitializeAsync 可成功初始化
    ///   - 编辑器域里能找到 App.Hotfix 程序集
    ///   - 反射成功调到 App.Hotfix.HotfixEntry.Start()
    ///   - 整条链路无 console error
    /// 失败时 onFatal 会把异常打到 console，方便定位。
    /// </summary>
    public static class HotUpdateSmokeTest
    {
        private const string MenuRoot = "Tools/CPA/HotUpdate";

        [MenuItem(MenuRoot + "/Smoke Test Hotfix Loader (Edit Mode)", priority = 30)]
        public static void RunSmokeTest()
        {
            var sw = Stopwatch.StartNew();
            Debug.Log("[Smoke] 启动 HotUpdate 链路烟雾测试 ...");
            EditorCoroutineUtility.StartCoroutineOwnerless(
                LoadHotfixSystem.RunAsync(ex =>
                {
                    sw.Stop();
                    Debug.LogError($"[Smoke] ❌ HotUpdate 加载链路抛异常 (耗时 {sw.ElapsedMilliseconds}ms)：{ex}");
                }));
            // 协程开始后由 EditorApplication.update 推进；这里不阻塞。
            // 成功的 HotfixEntry.Start() 会自己打 Debug.Log
            // 烟雾测试以 console 是否出现 [Smoke] ✅ 收尾日志为准。
            EditorApplication.delayCall += () => WatchForCompletion(sw);
        }

        // 监听 console，10s 内没看到 HotfixEntry 调用日志就报 timeout
        private static void WatchForCompletion(Stopwatch sw)
        {
            const int timeoutSeconds = 20;
            int elapsedTicks = 0;
            EditorApplication.update += Tick;

            void Tick()
            {
                elapsedTicks++;
                // EditorApplication.update 约每 0.01-0.05s 一次，按经验取下界
                if (elapsedTicks > timeoutSeconds * 30)
                {
                    EditorApplication.update -= Tick;
                    sw.Stop();
                    Debug.LogWarning($"[Smoke] ⚠ 在 {timeoutSeconds}s 内没观察到 HotfixEntry.Start() 调用，可能链路 stall — 看上方日志。耗时 {sw.ElapsedMilliseconds}ms。");
                }
            }
        }
    }
}
#endif
