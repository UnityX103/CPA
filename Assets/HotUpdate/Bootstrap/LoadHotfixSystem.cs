using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;
#if !UNITY_EDITOR
using HybridCLR;
#endif

namespace App.Bootstrap
{
    /// <summary>
    /// 真正的"按顺序加载热更新"逻辑。和 BootstrapEntry 解耦以方便单测/复用。
    ///
    /// 流程：
    ///   1) Addressables.InitializeAsync()
    ///   2) 拉取 catalog（默认 AA 内置 RemoteCatalog 会自动跟着 1 一起 init，所以这里不再单独 ck）。
    ///   3) 用 label = "aotmeta" 拉所有 AOT 元数据 dll（每个 *.dll.bytes 是一个 TextAsset）。
    ///   4) 调 HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly 注册元数据。
    ///   5) 用 address = "App.Hotfix.dll" 拉热更新 dll TextAsset，再 Assembly.Load。
    ///   6) 反射调用 App.Hotfix.HotfixEntry.Start()。
    ///
    /// Editor 下，App.Hotfix 已经在编辑器域里编译并存在，跳过 dll 拉取，直接拿域里的 Assembly。
    /// </summary>
    public static class LoadHotfixSystem
    {
        public const string HotfixAssemblyName = "App.Hotfix";
        public const string HotfixEntryTypeName = "App.Hotfix.HotfixEntry";
        public const string HotfixEntryMethodName = "Start";

        public const string HotfixDllAddress = "App.Hotfix.dll";
        public const string AotMetadataLabel = "aotmeta";

        /// <summary>
        /// 入口。onFatal != null 时所有异常都丢给它，永远不重抛。
        /// </summary>
        public static IEnumerator RunAsync(Action<Exception> onFatal)
        {
            // 1) Addressables 初始化
            AsyncOperationHandle<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator> initHandle = default;
            Exception caught = null;
            try
            {
                initHandle = Addressables.InitializeAsync(false);
            }
            catch (Exception ex) { caught = ex; }
            if (caught != null) { onFatal?.Invoke(caught); yield break; }

            yield return initHandle;
            if (initHandle.Status != AsyncOperationStatus.Succeeded)
            {
                onFatal?.Invoke(new InvalidOperationException(
                    $"Addressables.InitializeAsync 失败：{initHandle.OperationException}"));
                Addressables.Release(initHandle);
                yield break;
            }
            Debug.Log("[Bootstrap] Addressables initialized.");
            Addressables.Release(initHandle);

            // 2) 加载 AOT 元数据 (仅打包时需要)
#if !UNITY_EDITOR
            var aotHandle = Addressables.LoadAssetsAsync<TextAsset>(AotMetadataLabel, null);
            yield return aotHandle;
            if (aotHandle.Status != AsyncOperationStatus.Succeeded)
            {
                // 没有 aotmeta 不致命 — 项目可能还没用泛型；但 warn 一下。
                Debug.LogWarning(
                    $"[Bootstrap] 没找到 label='{AotMetadataLabel}' 的 AOT 元数据 DLL；" +
                    "如果热更新代码里用到了泛型 AOT 实例化，运行时会抛 MissingMethodException。");
            }
            else
            {
                foreach (var ta in aotHandle.Result)
                {
                    try
                    {
                        var err = RuntimeApi.LoadMetadataForAOTAssembly(ta.bytes, HomologousImageMode.SuperSet);
                        Debug.Log($"[Bootstrap] LoadMetadataForAOTAssembly {ta.name}.dll => {err}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Bootstrap] LoadMetadataForAOTAssembly({ta.name}) 抛异常: {ex}");
                    }
                }
            }
            Addressables.Release(aotHandle);
#endif

            // 3) 加载热更新 DLL
            Assembly hotfixAssembly = null;
#if UNITY_EDITOR
            // 编辑器下直接拿已编译的 App.Hotfix
            hotfixAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == HotfixAssemblyName);
            if (hotfixAssembly == null)
            {
                onFatal?.Invoke(new InvalidOperationException(
                    $"编辑器域里找不到 {HotfixAssemblyName} 程序集 — 是不是 asmdef 缺失？"));
                yield break;
            }
#else
            var dllHandle = Addressables.LoadAssetAsync<TextAsset>(HotfixDllAddress);
            yield return dllHandle;
            if (dllHandle.Status != AsyncOperationStatus.Succeeded || dllHandle.Result == null)
            {
                onFatal?.Invoke(new InvalidOperationException(
                    $"Addressables 加载 '{HotfixDllAddress}' 失败：{dllHandle.OperationException}"));
                if (dllHandle.IsValid()) Addressables.Release(dllHandle);
                yield break;
            }
            try
            {
                hotfixAssembly = Assembly.Load(dllHandle.Result.bytes);
                Debug.Log($"[Bootstrap] Hotfix 已加载，{hotfixAssembly.FullName}");
            }
            catch (Exception ex)
            {
                onFatal?.Invoke(ex);
                Addressables.Release(dllHandle);
                yield break;
            }
            Addressables.Release(dllHandle);
#endif

            // 4) 反射调用入口
            try
            {
                var entryType = hotfixAssembly.GetType(HotfixEntryTypeName, throwOnError: false);
                if (entryType == null)
                {
                    onFatal?.Invoke(new MissingMethodException(
                        $"在 {hotfixAssembly.GetName().Name} 里找不到类型 {HotfixEntryTypeName}"));
                    yield break;
                }
                var method = entryType.GetMethod(HotfixEntryMethodName,
                    BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    onFatal?.Invoke(new MissingMethodException(
                        $"{HotfixEntryTypeName} 缺少 public static {HotfixEntryMethodName}() 入口"));
                    yield break;
                }
                method.Invoke(null, null);
                Debug.Log("[Bootstrap] HotfixEntry.Start() 已调用");
            }
            catch (Exception ex)
            {
                onFatal?.Invoke(ex);
            }
        }
    }
}
