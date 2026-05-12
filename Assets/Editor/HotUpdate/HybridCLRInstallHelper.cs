#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using HybridCLR.Editor;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEngine;

namespace App.Editor.HotUpdate
{
    /// <summary>
    /// HybridCLR 装机辅助。
    ///
    /// 背景：在 Unity 6.3 + macOS 上，HybridCLR 自带的 InstallerController 把 il2cpp 源
    /// 默认指向 `<editor>/PlaybackEngines/iOSSupport/il2cpp`，且在 RunInitLocalIl2CppData
    /// 里要把 `build/deploy_arm64` 复制到 `build/deploy`。Mac IL2CPP 模块的 libil2cpp 源
    /// 位置不同（`Unity.app/Contents/Resources/Scripting/il2cpp`），且 `build/` 下只有
    /// 单一的 `deploy/`（没有 arm64/x86_64 拆分）。直接走默认路径会先报 "Failed to Copy"
    /// 再把编辑器拖崩。
    ///
    /// 本辅助提供 Mac 专用装机路径，完全绕开崩溃步骤：
    ///   1) 从 Mac IL2CPP 模块把整个 il2cpp 源拷到 HybridCLRData/LocalIl2CppData-OSXEditor/il2cpp
    ///   2) 把 hybridclr_repo + il2cpp_plus_repo 合并产物覆盖到 il2cpp/libil2cpp/
    ///   3) 写 libil2cpp-version.txt 作为安装完成标记
    /// </summary>
    public static class HybridCLRInstallHelper
    {
        private const string MenuRoot = "Tools/CPA/HotUpdate";

        [MenuItem(MenuRoot + "/Install HybridCLR (Mac safe)", priority = 0)]
        public static void InstallMacSafe()
        {
            try
            {
                var ctrl = new InstallerController();
                Debug.Log($"[HybridCLR] PackageVersion={ctrl.PackageVersion} Compat={ctrl.GetCompatibleType()} HasInstalled={ctrl.HasInstalledHybridCLR()}");
                if (ctrl.GetCompatibleType() == InstallerController.CompatibleType.Incompatible)
                {
                    Debug.LogError($"[HybridCLR] Unity {Application.unityVersion} 不兼容，需要 ≥ {ctrl.GetCurrentUnityVersionMinCompatibleVersionStr()}");
                    return;
                }
                if (ctrl.HasInstalledHybridCLR())
                {
                    Debug.Log($"[HybridCLR] 已安装 libil2cpp={ctrl.InstalledLibil2cppVersion}，跳过。");
                    return;
                }

                string editorIl2cppPath = $"{EditorApplication.applicationContentsPath}/Resources/Scripting/il2cpp";
                if (!Directory.Exists(Path.Combine(editorIl2cppPath, "libil2cpp")))
                {
                    Debug.LogError(
                        $"[HybridCLR] {editorIl2cppPath}/libil2cpp 不存在。" +
                        "请通过 Unity Hub 安装 Mac Build Support (IL2CPP) 模块后再试。");
                    return;
                }
                if (!Directory.Exists(Path.Combine(editorIl2cppPath, "build", "deploy")))
                {
                    Debug.LogError(
                        $"[HybridCLR] {editorIl2cppPath}/build/deploy 不存在，Mac IL2CPP 模块可能损坏。" +
                        "建议在 Unity Hub 里 Add Modules 重装一次。");
                    return;
                }

                string mergedLibil2cpp = PrepareMergedLibil2cpp(ctrl);
                if (string.IsNullOrEmpty(mergedLibil2cpp))
                {
                    Debug.LogError("[HybridCLR] hybridclr / il2cpp_plus 仓库合并失败。");
                    return;
                }
                Debug.Log($"[HybridCLR] merged libil2cpp = {mergedLibil2cpp}");

                MacSafeRunInit(editorIl2cppPath, mergedLibil2cpp);
                ctrl.WriteLocalVersion();
                Debug.Log($"[HybridCLR] 装机完成。HasInstalled={ctrl.HasInstalledHybridCLR()} libil2cpp={ctrl.InstalledLibil2cppVersion}");
            }
            catch (Exception ex) { LogChain(ex); }
        }

        [MenuItem(MenuRoot + "/Check Install Status", priority = 10)]
        public static void CheckStatus()
        {
            var ctrl = new InstallerController();
            Debug.Log($"[HybridCLR] HasInstalled={ctrl.HasInstalledHybridCLR()} libil2cpp={ctrl.InstalledLibil2cppVersion} pkg={ctrl.PackageVersion}");
            Debug.Log($"[HybridCLR] LocalUnityDataDir={SettingsUtil.LocalUnityDataDir}");
            Debug.Log($"[HybridCLR] LocalIl2CppDir={SettingsUtil.LocalIl2CppDir}");
        }

        [MenuItem(MenuRoot + "/Reset Install (delete LocalIl2CppData)", priority = 11)]
        public static void ResetInstall()
        {
            if (!EditorUtility.DisplayDialog(
                "重置 HybridCLR 安装",
                $"会删除 {SettingsUtil.LocalUnityDataDir}，但保留已 clone 的 hybridclr_repo / il2cpp_plus_repo。继续？",
                "继续", "取消"))
            {
                return;
            }
            try
            {
                if (Directory.Exists(SettingsUtil.LocalUnityDataDir))
                {
                    Directory.Delete(SettingsUtil.LocalUnityDataDir, recursive: true);
                    Debug.Log($"[HybridCLR] 已删除 {SettingsUtil.LocalUnityDataDir}");
                }
                else
                {
                    Debug.Log("[HybridCLR] 没有遗留 LocalUnityData 目录，无需重置。");
                }
            }
            catch (Exception ex) { LogChain(ex); }
        }

        // ─── 内部步骤 ───────────────────────────────────────────────

        private static string PrepareMergedLibil2cpp(InstallerController ctrl)
        {
            string projectDir = Directory.GetParent(Application.dataPath)!.ToString();
            string mergedPath = $"{projectDir}/HybridCLRData/il2cpp_plus_repo/libil2cpp";
            if (Directory.Exists(Path.Combine(mergedPath, "hybridclr")))
            {
                return mergedPath;
            }

            var mi = typeof(InstallerController).GetMethod(
                "PrepareLibil2cppWithHybridclrFromGitRepo",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi == null) throw new InvalidOperationException("找不到 PrepareLibil2cppWithHybridclrFromGitRepo 方法");
            return (string)mi.Invoke(ctrl, null);
        }

        /// <summary>
        /// 复刻 InstallerController.RunInitLocalIl2CppData，但跳过 iOSSupport 才有的
        /// `build/deploy_arm64 -> deploy` split 拷贝。Mac IL2CPP 下 `build/deploy/` 已经
        /// 跟 il2cpp 源一起拷贝过去了，不需要再分发。
        /// </summary>
        private static void MacSafeRunInit(string editorIl2cppPath, string mergedLibil2cppPath)
        {
            string workDir = SettingsUtil.HybridCLRDataDir;
            Directory.CreateDirectory(workDir);

            string localUnityDataDir = SettingsUtil.LocalUnityDataDir;
            RecreateDir(localUnityDataDir);

            // 1. 把整个 Mac IL2CPP 源拷过去（含 build/deploy/）
            CopyDir(editorIl2cppPath, SettingsUtil.LocalIl2CppDir);

            // 2. 覆盖 libil2cpp 为 hybridclr 合并版
            string dstLibil2cpp = $"{SettingsUtil.LocalIl2CppDir}/libil2cpp";
            CopyDir(mergedLibil2cppPath, dstLibil2cpp);

            // 3. 清掉 IL2CPP 构建缓存，避免老内容跟新源混搭
            string buildCacheDir = $"{Directory.GetParent(Application.dataPath)!}/Library/Il2cppBuildCache";
            if (Directory.Exists(buildCacheDir))
            {
                try { Directory.Delete(buildCacheDir, recursive: true); }
                catch (Exception ex) { Debug.LogWarning($"[HybridCLR] 清 Il2cppBuildCache 失败：{ex.Message}"); }
            }

            // 4. 验证关键文件
            string genDir = $"{dstLibil2cpp}/hybridclr/generated";
            Directory.CreateDirectory(genDir); // RuntimeApi 加载元数据时会走到这里
        }

        private static void RecreateDir(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            Directory.CreateDirectory(path);
        }

        private static void CopyDir(string src, string dst)
        {
            if (!Directory.Exists(src))
            {
                throw new DirectoryNotFoundException($"source dir 不存在：{src}");
            }
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(src, dir);
                Directory.CreateDirectory(Path.Combine(dst, rel));
            }
            foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(src, file);
                string target = Path.Combine(dst, rel);
                File.Copy(file, target, overwrite: true);
            }
        }

        private static void LogChain(Exception ex)
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                Debug.LogError($"[HybridCLR][ERR] {cur.GetType().FullName}: {cur.Message}\n{cur.StackTrace}");
            }
        }
    }
}
#endif
