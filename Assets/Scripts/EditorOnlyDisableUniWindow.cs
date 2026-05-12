#if UNITY_EDITOR
using Kirurobo;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 编辑器专用：场景加载时关闭 UniWindowController，避免在 Editor Play Mode 下
/// 触发透明窗口 / 点击穿透等桌面副作用。构建后整个文件被编译器剔除。
/// BeforeSceneLoad 阶段场景对象还没生成，所以这里只挂回调，真正禁用走 sceneLoaded；
/// 不解绑回调以便 additive 加载或后续切场景时新出现的 UniWindowController 也能被压下去。
/// </summary>
internal static class EditorOnlyDisableUniWindow
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterOnSceneLoaded()
    {
        var uwc = Object.FindFirstObjectByType<UniWindowController>();
        Debug.LogError(uwc);
        if (uwc != null )
        {
            uwc.gameObject.SetActive(false); 
        }
    }
}
#endif