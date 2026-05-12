using UnityEngine;

namespace App.Hotfix
{
    /// <summary>
    /// 热更新代码的入口。AOT 侧 Bootstrap 加载完 App.Hotfix.dll 后，
    /// 通过反射调 public static void Start()。
    ///
    /// 起步阶段只做"Hello from hotfix"，证明热更新链路通畅；
    /// 后续业务代码迁入本程序集后，由这里负责把 GameApp.Interface（QFramework Architecture）
    /// 注册并启动整个游戏（替换原来 APP.Runtime 的入口）。
    /// </summary>
    public static class HotfixEntry
    {
        public static void Start()
        {
            Debug.Log("[Hotfix] HotfixEntry.Start() — hot-update 代码已被 AOT 侧成功调用 ✅");
            // TODO(P10): 在迁移完业务代码后，这里要负责：
            //   1) GameApp.Interface.InitArchitecture() — 触发 QFramework 注册
            //   2) 通过 Addressables 加载 UI Toolkit 根 UXML 实例化首屏
            //   3) 把场景中各 ViewController 的 hotfix 实现挂回 GameObject（或反射桥接）
        }
    }
}
