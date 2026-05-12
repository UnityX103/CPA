#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace App.Editor.HotUpdate
{
    /// <summary>
    /// 玩家 build 前自动跑完整热更新生成链：
    ///   1) HybridCLR.PrebuildCommand.GenerateAll() — 编译 hot-update DLL、生成 link.xml、
    ///      AOTGenericReference、MethodBridge、裁剪后的 AOT DLL。
    ///   2) 把 App.Hotfix.dll 和需要 AOT 补元数据的 DLL 复制到 Assets/HotUpdateGen/，
    ///      改名 .bytes，挂进对应 Addressables 组。
    ///   3) 触发 Addressables 远端构建 (ContentBuilder)，产物落到
    ///      ServerData/AA/[BuildTarget]/，由 Tools/AAServer 托管。
    /// 之后 BuildPipeline.BuildPlayer 继续走原本流程。
    /// </summary>
    public sealed class HotUpdateBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100; // 比 AA 自己的预处理器更早一点

        // ─── 生成产物路径约定 ────────────────────────────────────────
        private const string HotUpdateGenDir = "Assets/HotUpdateGen";
        private const string HotfixGenDir = HotUpdateGenDir + "/Hotfix";
        private const string AotMetaGenDir = HotUpdateGenDir + "/AOTMeta";

        // 需要补 AOT 泛型元数据的常见 AOT 程序集白名单。HybridCLR 的 AOTGenericReference
        // 会扫描热更新代码用了哪些泛型实例化，再回头要求这些 AOT DLL 提供 metadata。
        // 先列基础几个；如果运行时报 MissingMethodException 再按需补。
        private static readonly string[] AotPatchAssemblies =
        {
            "mscorlib",
            "System",
            "System.Core",
            "UnityEngine.CoreModule"
        };

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!HybridCLRSettings.Instance.enable)
            {
                Debug.Log("[HotUpdate Build] HybridCLRSettings.enable=false，跳过热更新生成。");
                return;
            }

            BuildTarget target = report.summary.platform;
            Debug.Log($"[HotUpdate Build] ===== preprocess: target={target} =====");

            // 1) HybridCLR 全套生成 + 编译热更新 DLL
            try
            {
                PrebuildCommand.GenerateAll();
            }
            catch (Exception ex)
            {
                throw new BuildFailedException($"[HotUpdate Build] HybridCLR GenerateAll 失败：{ex}");
            }

            // 2) 拷贝产物到 Assets/HotUpdateGen 并挂 Addressables
            PrepareGenDirectory();
            CopyHotfixDll(target);
            CopyAotMetaDlls(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterIntoAddressables();

            // 3) Addressables 远端构建（写到 ServerData/）
            BuildAddressables();

            Debug.Log("[HotUpdate Build] ===== preprocess done =====");
        }

        // ─── 步骤实现 ───────────────────────────────────────────────

        private static void PrepareGenDirectory()
        {
            if (!Directory.Exists(HotfixGenDir)) Directory.CreateDirectory(HotfixGenDir);
            if (!Directory.Exists(AotMetaGenDir)) Directory.CreateDirectory(AotMetaGenDir);
        }

        private static void CopyHotfixDll(BuildTarget target)
        {
            string srcDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            foreach (var asmName in HybridCLRSettings.Instance.hotUpdateAssemblyDefinitions?
                .Select(asd => asd.name) ?? Enumerable.Empty<string>())
            {
                string srcPath = Path.Combine(srcDir, $"{asmName}.dll");
                if (!File.Exists(srcPath))
                {
                    Debug.LogWarning($"[HotUpdate Build] hot-update dll 不存在：{srcPath}");
                    continue;
                }
                string dstPath = $"{HotfixGenDir}/{asmName}.dll.bytes";
                File.Copy(srcPath, dstPath, overwrite: true);
                Debug.Log($"[HotUpdate Build] copy {srcPath} -> {dstPath}");
            }
        }

        private static void CopyAotMetaDlls(BuildTarget target)
        {
            string srcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            if (!Directory.Exists(srcDir))
            {
                Debug.LogWarning($"[HotUpdate Build] AOT 裁剪后 DLL 目录不存在：{srcDir}");
                return;
            }
            foreach (var asmName in AotPatchAssemblies)
            {
                string srcPath = Path.Combine(srcDir, $"{asmName}.dll");
                if (!File.Exists(srcPath))
                {
                    // 不是所有平台都有这些；缺了不致命
                    continue;
                }
                string dstPath = $"{AotMetaGenDir}/{asmName}.dll.bytes";
                File.Copy(srcPath, dstPath, overwrite: true);
                Debug.Log($"[HotUpdate Build] copy AOT meta {srcPath} -> {dstPath}");
            }
        }

        private static void RegisterIntoAddressables()
        {
            var aa = AddressableAssetSettingsDefaultObject.Settings;
            if (aa == null)
            {
                throw new BuildFailedException("[HotUpdate Build] AddressableAssetSettings 未初始化。请先跑 Tools/CPA/HotUpdate/Configure HybridCLR + Addressables。");
            }

            var hotfixGroup = aa.FindGroup("Hotfix") ?? throw new BuildFailedException("[HotUpdate Build] 找不到 Addressables Group 'Hotfix'");
            var aotMetaGroup = aa.FindGroup("AOTMeta") ?? throw new BuildFailedException("[HotUpdate Build] 找不到 Addressables Group 'AOTMeta'");

            // Hotfix DLL：固定 address = "App.Hotfix.dll"，与 LoadHotfixSystem.HotfixDllAddress 对齐
            foreach (var path in Directory.EnumerateFiles(HotfixGenDir, "*.dll.bytes"))
            {
                string assetPath = path.Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning($"[HotUpdate Build] {assetPath} 没拿到 GUID，跳过。");
                    continue;
                }
                string asmName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(assetPath)); // 两次去 .bytes / .dll
                var entry = aa.CreateOrMoveEntry(guid, hotfixGroup, postEvent: false, readOnly: false);
                entry.address = $"{asmName}.dll";
                Debug.Log($"[HotUpdate Build] addressable: {entry.address} -> {assetPath}");
            }

            // AOT meta DLL：用 label="aotmeta" 一次性 LoadAssetsAsync<TextAsset>(label)
            foreach (var path in Directory.EnumerateFiles(AotMetaGenDir, "*.dll.bytes"))
            {
                string assetPath = path.Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid)) continue;
                var entry = aa.CreateOrMoveEntry(guid, aotMetaGroup, postEvent: false, readOnly: false);
                entry.address = Path.GetFileName(assetPath);
                entry.SetLabel("aotmeta", true, true, postEvent: false);
                Debug.Log($"[HotUpdate Build] addressable+label: {entry.address} -> {assetPath}");
            }

            EditorUtility.SetDirty(aa);
            AssetDatabase.SaveAssets();
        }

        private static void BuildAddressables()
        {
            Debug.Log("[HotUpdate Build] 触发 Addressables BuildPlayerContent ...");
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new BuildFailedException($"[HotUpdate Build] Addressables 构建失败：{result.Error}");
            }
            Debug.Log($"[HotUpdate Build] Addressables 构建完成，耗时 {result.Duration:F2}s，输出 {result.OutputPath}");
        }
    }
}
#endif
