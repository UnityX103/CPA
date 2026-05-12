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
        private const string RemoteLoadUrl = "http://localhost:9000/AA/[BuildTarget]";
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
    }
}
#endif
