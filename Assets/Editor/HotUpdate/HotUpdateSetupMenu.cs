#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditorInternal;
using UnityEngine;

namespace App.Editor.HotUpdate
{
    /// <summary>
    /// 一键把 HybridCLR + Addressables 配齐到本项目约定的形状：
    ///   - HybridCLR.hotUpdateAssemblyDefinitions = [ App.Hotfix.asmdef ]
    ///   - Addressables Profile "LocalDev"
    ///       RemoteLoadPath  = http://localhost:9000/AA/[BuildTarget]
    ///       RemoteBuildPath = [ProjectRoot]/ServerData/AA/[BuildTarget]
    ///   - Groups: Default, Hotfix (Remote), AOTMeta (Remote)
    /// </summary>
    public static class HotUpdateSetupMenu
    {
        private const string MenuRoot = "Tools/CPA/HotUpdate";

        private const string HotfixAsmdefPath = "Assets/HotUpdate/Hotfix/App.Hotfix.asmdef";
        private const string ProfileName = "LocalDev";

        // RemoteLoadPath：CDN URL（Unity Cloud / UOS Asset Streaming，按 bucket+badge=latest 取最新发布版）。
        // bucket id 来自项目分发后台；上传写到 cdn/.uas-credentials.env 的 UAS_BUCKET_ID，
        // 这里写死的是「客户端拉包」用的 load bucket（一般和 upload bucket 同一个）。
        // 改 bucket 时记得同步改 cdn/.uas-credentials.env 里的 UAS_LOAD_BUCKET_ID 和这里。
        private const string CdnLoadBucket = "205577ad-32ea-4714-a837-6552d4797293";
        private const string RemoteLoadUrl =
            "https://a.unity.cn/client_api/v1/buckets/" + CdnLoadBucket +
            "/release_by_badge/latest/content/[BuildTarget]";
        private static readonly string RemoteBuildDir = "[UnityEngine.AddressableAssets.Addressables.RuntimePath]/../../../../ServerData/AA/[BuildTarget]";
        private const string GroupDefault = "Default";
        private const string GroupHotfix = "Hotfix";
        private const string GroupAotMeta = "AOTMeta";

        [MenuItem(MenuRoot + "/Configure HybridCLR + Addressables", priority = 20)]
        public static void ConfigureAll()
        {
            try
            {
                ConfigureHybridCLR();
                ConfigureAddressables();
                AssetDatabase.SaveAssets();
                Debug.Log("[HotUpdate Setup] 全部完成。");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HotUpdate Setup][ERR] {ex}");
            }
        }

        // ─── HybridCLR ──────────────────────────────────────────

        private static void ConfigureHybridCLR()
        {
            var settings = HybridCLRSettings.Instance;
            var hotfixAsmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(HotfixAsmdefPath);
            if (hotfixAsmdef == null)
            {
                Debug.LogError($"[HybridCLR Setup] 找不到 {HotfixAsmdefPath}，确认 App.Hotfix.asmdef 已存在。");
                return;
            }

            var existing = settings.hotUpdateAssemblyDefinitions ?? Array.Empty<AssemblyDefinitionAsset>();
            if (!existing.Contains(hotfixAsmdef))
            {
                settings.hotUpdateAssemblyDefinitions = existing.Append(hotfixAsmdef).ToArray();
                Debug.Log("[HybridCLR Setup] 已把 App.Hotfix.asmdef 加入 hotUpdateAssemblyDefinitions。");
            }
            else
            {
                Debug.Log("[HybridCLR Setup] App.Hotfix.asmdef 已在列表中。");
            }

            // 默认补充元数据先空着，等真正用到泛型 AOT 时再用 HybridCLR/Generate/All 自动填。
            settings.enable = true;
            HybridCLRSettings.Save();
        }

        // ─── Addressables ─────────────────────────────────────────

        private static void ConfigureAddressables()
        {
            var aa = AddressableAssetSettingsDefaultObject.Settings;
            if (aa == null)
            {
                aa = AddressableAssetSettingsDefaultObject.GetSettings(true);
                Debug.Log("[AA Setup] AddressableAssetSettings 不存在，已创建。");
            }

            // 1) Profile
            string profileId = aa.profileSettings.GetProfileId(ProfileName);
            if (string.IsNullOrEmpty(profileId))
            {
                profileId = aa.profileSettings.AddProfile(ProfileName, aa.activeProfileId);
                Debug.Log($"[AA Setup] 新建 Profile '{ProfileName}'。");
            }
            aa.activeProfileId = profileId;

            // RemoteLoadPath / RemoteBuildPath：用 [BuildTarget] 占位，AA 在 build/load 时替换。
            SetProfileEntry(aa, profileId, AddressableAssetSettings.kRemoteLoadPath, RemoteLoadUrl);
            SetProfileEntry(aa, profileId, AddressableAssetSettings.kRemoteBuildPath, RemoteBuildDir);

            // 2) Groups
            EnsureGroup(aa, GroupDefault, isRemote: false);
            EnsureGroup(aa, GroupHotfix, isRemote: true);
            EnsureGroup(aa, GroupAotMeta, isRemote: true);

            // 3) 让 catalog 也走 remote，方便客户端启动时拉到最新清单。
            aa.BuildRemoteCatalog = true;
            aa.RemoteCatalogLoadPath = new ProfileValueReference();
            aa.RemoteCatalogLoadPath.SetVariableByName(aa, AddressableAssetSettings.kRemoteLoadPath);
            aa.RemoteCatalogBuildPath = new ProfileValueReference();
            aa.RemoteCatalogBuildPath.SetVariableByName(aa, AddressableAssetSettings.kRemoteBuildPath);

            EditorUtility.SetDirty(aa);
        }

        private static void SetProfileEntry(AddressableAssetSettings aa, string profileId, string varName, string value)
        {
            string current = aa.profileSettings.GetValueByName(profileId, varName);
            if (current != value)
            {
                aa.profileSettings.SetValue(profileId, varName, value);
                Debug.Log($"[AA Setup] Profile '{ProfileName}' set {varName} = {value}");
            }
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings aa, string name, bool isRemote)
        {
            var group = aa.FindGroup(name);
            if (group != null) return group;

            var templates = new List<AddressableAssetGroupSchema>();
            // 创建带 BundledAssetGroupSchema + ContentUpdateGroupSchema 的标准组
            group = aa.CreateGroup(
                name,
                setAsDefaultGroup: name == GroupDefault,
                readOnly: false,
                postEvent: true,
                schemasToCopy: null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));

            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (isRemote)
            {
                bundled.BuildPath.SetVariableByName(aa, AddressableAssetSettings.kRemoteBuildPath);
                bundled.LoadPath.SetVariableByName(aa, AddressableAssetSettings.kRemoteLoadPath);
            }
            else
            {
                bundled.BuildPath.SetVariableByName(aa, AddressableAssetSettings.kLocalBuildPath);
                bundled.LoadPath.SetVariableByName(aa, AddressableAssetSettings.kLocalLoadPath);
            }
            bundled.UseAssetBundleCrc = false;
            bundled.UseAssetBundleCrcForCachedBundles = false;
            bundled.IncludeInBuild = true;

            EditorUtility.SetDirty(group);
            Debug.Log($"[AA Setup] 新建 group '{name}' (remote={isRemote})。");
            return group;
        }

        // ─── 四个一键工作流 ────────────────────────────────────────
        //   Rebuild Mac / Win  —— 完整重新打包：先跑热更新生成链，再 BuildPlayer
        //                         （Preprocessor 会在 BuildPlayer 阶段再被自动调一次，
        //                          但 GenerateAll 是幂等的，重复执行无副作用）
        //   HotUpdate Mac / Win —— 只跑热更新生成 + AA 构建，产物落在 ServerData/AA/[Target]/
        //                          不触发 BuildPlayer，发布到 RemoteLoadPath 服务器即可推热更
        //
        // 触发前会先 SwitchActiveBuildTarget 到目标平台，确保 IL2CPP strip 目录 / AOT DLL 一致。

        /// <summary>
        /// 顶部快捷：根据当前 ActiveBuildTarget 自动跑增量热更新。
        ///   - 调 HybridCLR CompileDll 编译最新 hot-update DLL
        ///   - 把 hot-update DLL 和已存在的 AOT meta DLL 拷到 Assets/HotUpdateGen/
        ///   - 把 Hotfix / AOTMeta 文件夹 entry 注册到对应 AA 组
        ///   - 触发 Addressables 远端构建
        /// 适合「只改 App.Hotfix.dll，想重出 AA 包」的常见循环。
        /// AOT meta 重生仍需走 Rebuild（StripAOTDllCommand 必须嵌进 BuildPlayer）。
        /// </summary>
        [MenuItem(MenuRoot + "/热更新当前平台 _F5", priority = 10)]
        public static void HotUpdateCurrentPlatform()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (!IsSupportedStandaloneTarget(target))
            {
                Debug.LogError($"[HotUpdate Menu][ERR] 当前 ActiveBuildTarget={target} 不在支持的桌面平台列表里（仅支持 StandaloneOSX / StandaloneWindows64）。请先切换 Build Target。");
                return;
            }

            try
            {
                Debug.Log($"[HotUpdate Menu] 热更新当前平台：{target}");
                HotUpdateBuildPreprocessor.RunHotfixOnlyForTarget(target, source: "MenuItem:热更新当前平台");
                Debug.Log($"[HotUpdate Menu] 热更新当前平台 完成。产物在 ServerData/AA/{target}/");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HotUpdate Menu][ERR] 热更新当前平台 失败：{ex}");
            }
        }

        private static bool IsSupportedStandaloneTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneOSX
                || target == BuildTarget.StandaloneWindows64
                || target == BuildTarget.StandaloneWindows;
        }

        [MenuItem(MenuRoot + "/Rebuild Mac", priority = 40)]
        public static void RebuildMac()
        {
            if (!EnsureActiveBuildTarget(BuildTarget.StandaloneOSX)) return;
            BuildScript.BuildRunAndVerifyMacOS();
        }

        [MenuItem(MenuRoot + "/HotUpdate Mac", priority = 41)]
        public static void HotUpdateMac()
        {
            if (!EnsureActiveBuildTarget(BuildTarget.StandaloneOSX)) return;
            try
            {
                HotUpdateBuildPreprocessor.RunHotfixOnlyForTarget(BuildTarget.StandaloneOSX, source: "MenuItem:HotUpdateMac");
                Debug.Log("[HotUpdate Menu] HotUpdate Mac 完成。产物在 ServerData/AA/StandaloneOSX/");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HotUpdate Menu][ERR] HotUpdate Mac 失败：{ex}");
            }
        }

        [MenuItem(MenuRoot + "/Rebuild Win", priority = 42)]
        public static void RebuildWin()
        {
            if (!EnsureActiveBuildTarget(BuildTarget.StandaloneWindows64)) return;
            BuildScript.BuildWindows64();
        }

        [MenuItem(MenuRoot + "/HotUpdate Win", priority = 43)]
        public static void HotUpdateWin()
        {
            if (!EnsureActiveBuildTarget(BuildTarget.StandaloneWindows64)) return;
            try
            {
                HotUpdateBuildPreprocessor.RunHotfixOnlyForTarget(BuildTarget.StandaloneWindows64, source: "MenuItem:HotUpdateWin");
                Debug.Log("[HotUpdate Menu] HotUpdate Win 完成。产物在 ServerData/AA/StandaloneWindows64/");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HotUpdate Menu][ERR] HotUpdate Win 失败：{ex}");
            }
        }

        // HybridCLR 的 GetHotUpdateDllsOutputDirByTarget / GetAssembliesPostIl2CppStripDir 都按 ActiveBuildTarget
        // 取目录，跨平台时必须先切 ActiveBuildTarget 才能拿到对应平台的 AOT 裁剪产物。
        private static bool EnsureActiveBuildTarget(BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target) return true;

            BuildTargetGroup group = target == BuildTarget.StandaloneWindows64
                ? BuildTargetGroup.Standalone
                : BuildTargetGroup.Standalone;

            Debug.Log($"[HotUpdate Menu] 切换 ActiveBuildTarget: {EditorUserBuildSettings.activeBuildTarget} -> {target}（可能耗时几十秒）");
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            if (!ok)
            {
                Debug.LogError($"[HotUpdate Menu][ERR] SwitchActiveBuildTarget 失败，target={target}（可能缺该平台 Build Support 模块）");
                return false;
            }
            return true;
        }
    }
}
#endif
