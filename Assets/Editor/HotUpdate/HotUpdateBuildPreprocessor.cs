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
        // 优先读 HybridCLRSettings.Instance.patchAOTAssemblies；为空时用下面的兜底列表。
        // 兜底只列基础几个；如果运行时报 MissingMethodException 再按需补。
        private static readonly string[] AotPatchAssembliesFallback =
        {
            "mscorlib",
            "System",
            "System.Core",
            "UnityEngine.CoreModule"
        };

        private static IEnumerable<string> ResolveAotPatchAssemblies()
        {
            var configured = HybridCLRSettings.Instance.patchAOTAssemblies;
            if (configured != null && configured.Length > 0)
            {
                return configured;
            }
            return AotPatchAssembliesFallback;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            // HybridCLR.StripAOTDllCommand.GenerateStripedAOTDlls 内部会嵌套调一次 BuildPipeline.BuildPlayer
            // 去做 IL2CPP strip，临时把 EditorUserBuildSettings.buildScriptsOnly 置 true。Unity 会把这次内嵌
            // build 也广播给所有 IPreprocessBuildWithReport——如果我们在这个嵌套上下文里再调 GenerateAll，会
            // 触发第三层 BuildPlayer，撞 "Cannot start a new build because there is already a build in progress"
            // 并把外层 build 整体拉黑。这里以 buildScriptsOnly 为闸门：嵌套 strip build 直接跳过，让 HybridCLR
            // 自己内部那一轮顺利完成；外层"正经"BuildPlayer 才走我们的完整生成链。
            if (EditorUserBuildSettings.buildScriptsOnly)
            {
                Debug.Log("[HotUpdate Build] OnPreprocessBuild：检测到 buildScriptsOnly=true（HybridCLR strip 内嵌 build），跳过。");
                return;
            }

            RunFullForTarget(report.summary.platform, source: "BuildPlayer preprocess");
        }

        /// <summary>
        /// 完整链路（BuildPlayer 自动触发或菜单"重新打包"时走）：
        ///   HybridCLR GenerateAll → 拷热更新 DLL + AOT 裁剪 DLL → AA 文件夹 entry 注册 → AA Build。
        /// 注意：GenerateAll 内部会 BuildPipeline.BuildPlayer 一次去 strip AOT，所以**只能在 BuildPlayer
        /// 上下文里调用**（即由 OnPreprocessBuild 触发），不能直接在普通菜单点击的代码路径里跑，否则会撞
        /// "Cannot start a new build because there is already a build in progress"。
        /// </summary>
        public static void RunFullForTarget(BuildTarget target, string source)
        {
            if (!HybridCLRSettings.Instance.enable)
            {
                Debug.Log($"[HotUpdate Build][{source}] HybridCLRSettings.enable=false，跳过热更新生成。");
                return;
            }

            Debug.Log($"[HotUpdate Build][{source}] ===== FULL begin: target={target} =====");

            try
            {
                PrebuildCommand.GenerateAll();
            }
            catch (Exception ex)
            {
                throw new BuildFailedException($"[HotUpdate Build] HybridCLR GenerateAll 失败：{ex}");
            }

            PrepareGenDirectory();
            CopyHotfixDll(target);
            CopyAotMetaDlls(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterIntoAddressables();
            BuildAddressables();
            UploadToCdn(target, source);

            Debug.Log($"[HotUpdate Build][{source}] ===== FULL done =====");
        }

        /// <summary>
        /// 增量热更新链路（"热更新 Mac/Win" 菜单走这里）：
        ///   只编译热更新 DLL（PlayerBuildInterface.CompilePlayerScripts，不嵌套 BuildPlayer）→
        ///   拷热更新 DLL → 注册 AA → AA Build。
        /// **不重生 AOT meta**——AOT meta 只能在 Rebuild 时通过 IL2CPP strip 重新产出，且会随 player
        /// 一起发布。增量热更只更新 hotfix 业务代码，AOT meta 复用上次 Rebuild 的产物。
        /// </summary>
        public static void RunHotfixOnlyForTarget(BuildTarget target, string source)
        {
            if (!HybridCLRSettings.Instance.enable)
            {
                Debug.Log($"[HotUpdate Build][{source}] HybridCLRSettings.enable=false，跳过热更新生成。");
                return;
            }

            Debug.Log($"[HotUpdate Build][{source}] ===== HOTFIX-ONLY begin: target={target} =====");

            try
            {
                CompileDllCommand.CompileDll(target);
            }
            catch (Exception ex)
            {
                throw new BuildFailedException($"[HotUpdate Build] HybridCLR CompileDll 失败：{ex}");
            }

            PrepareGenDirectory();
            CopyHotfixDll(target);

            // AOT meta 本次不重生（重生只能走 BuildPlayer 内嵌 strip），但若上一次 Rebuild 留下了
            // AssembliesPostIl2CppStrip/<Target>/，仍然把现成的 AOT DLL 同步到 AOTMeta 目录，
            // 这样 AA 包里始终带上 AOT meta，不至于因为只跑 hotfix-only 就丢掉。
            CopyAotMetaDlls(target);

            if (!Directory.EnumerateFiles(AotMetaGenDir, "*.dll.bytes").Any())
            {
                Debug.LogWarning(
                    $"[HotUpdate Build][{source}] {AotMetaGenDir} 为空——尚未做过完整 Rebuild。" +
                    " 当前 AA 包不会含 AOT meta，运行时遇到泛型 AOT 实例化会抛 MissingMethodException。" +
                    " 请先执行 Tools/CPA/HotUpdate/Rebuild Mac (或 Win) 至少一次。");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterIntoAddressables();
            BuildAddressables();
            UploadToCdn(target, source);

            Debug.Log($"[HotUpdate Build][{source}] ===== HOTFIX-ONLY done =====");
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
            foreach (var asmName in ResolveAotPatchAssemblies())
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

            // 旧实现是逐文件 entry，现在统一改为「整文件夹作为 entry」放到组里：
            //   - Hotfix      文件夹 → 组 Hotfix，address="Hotfix"
            //                  运行时按 sub-asset 寻址 "Hotfix/App.Hotfix.dll.bytes"
            //   - AOTMeta     文件夹 → 组 AOTMeta，address="AOTMeta"，label="aotmeta"
            //                  AA 文件夹 entry 上挂的 label 会自动传播到 sub-location，
            //                  Bootstrap 仍然走 LoadAssetsAsync<TextAsset>("aotmeta") 拉整批。
            // 先把上一轮的旧 per-file entry 清掉，避免迁移后两套 entry 重复打包。
            CleanupLegacyEntries(aa, hotfixGroup);
            CleanupLegacyEntries(aa, aotMetaGroup);

            EnsureFolderEntry(aa, hotfixGroup, HotfixGenDir, address: "Hotfix", label: null);
            EnsureFolderEntry(aa, aotMetaGroup, AotMetaGenDir, address: "AOTMeta", label: "aotmeta");

            EditorUtility.SetDirty(aa);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolderEntry(AddressableAssetSettings aa, AddressableAssetGroup group,
            string folderAssetPath, string address, string label)
        {
            string guid = AssetDatabase.AssetPathToGUID(folderAssetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new BuildFailedException(
                    $"[HotUpdate Build] 取不到文件夹 GUID：{folderAssetPath}（先 AssetDatabase.Refresh 再重试）");
            }

            var entry = aa.CreateOrMoveEntry(guid, group, postEvent: false, readOnly: false);
            entry.address = address;
            if (!string.IsNullOrEmpty(label))
            {
                aa.AddLabel(label, postEvent: false);
                entry.SetLabel(label, enable: true, force: true, postEvent: false);
            }

            Debug.Log(
                $"[HotUpdate Build] addressable(folder): group='{group.Name}' address='{address}' label='{label ?? "<none>"}' -> {folderAssetPath}");
        }

        private static void CleanupLegacyEntries(AddressableAssetSettings aa, AddressableAssetGroup group)
        {
            if (group == null || group.entries == null) return;
            var stale = group.entries.ToList();
            foreach (var entry in stale)
            {
                aa.RemoveAssetEntry(entry.guid, postEvent: false);
            }
            if (stale.Count > 0)
            {
                Debug.Log($"[HotUpdate Build] 清理旧 entries：group='{group.Name}' 共 {stale.Count} 个");
            }
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

        /// <summary>
        /// AA 包打完后，调 cdn/upload-aa.sh 把 ServerData/AA/&lt;Target&gt;/ 同步到 UOS CDN，
        /// 创建 release 并把 badge=latest 指向新 release。
        /// 凭据走 cdn/.uas-credentials.env，不入 Player；本步骤只在 Editor 内跑。
        /// 脚本失败会向 Console 写错误但不抛 BuildFailedException——AA 包本身仍然落在 ServerData/，
        /// 用户可以人工跑 cdn/upload-aa.sh 复盘失败原因，不影响主链路。
        /// </summary>
        private static void UploadToCdn(BuildTarget target, string source)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string script = Path.Combine(projectRoot, "cdn", "upload-aa.sh");
            if (!File.Exists(script))
            {
                Debug.LogWarning($"[HotUpdate Build][{source}] cdn/upload-aa.sh 不存在，跳过 CDN 上传：{script}");
                return;
            }

            string targetArg = target.ToString(); // StandaloneOSX / StandaloneWindows64
            Debug.Log($"[HotUpdate Build][{source}] CDN 上传开始：bash {script} {targetArg}");

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{script}\" {targetArg}",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    if (proc == null)
                    {
                        Debug.LogError($"[HotUpdate Build][{source}] CDN 上传：无法启动 bash 进程");
                        return;
                    }

                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (!string.IsNullOrEmpty(stdout)) Debug.Log("[CDN upload] " + stdout.TrimEnd());
                    if (proc.ExitCode == 0)
                    {
                        Debug.Log($"[HotUpdate Build][{source}] CDN 上传完成（exit 0）");
                    }
                    else
                    {
                        Debug.LogError($"[HotUpdate Build][{source}] CDN 上传失败 exit={proc.ExitCode}\n{stderr.TrimEnd()}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HotUpdate Build][{source}] CDN 上传抛异常：{ex}");
            }
        }
    }
}
#endif
