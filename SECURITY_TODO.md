# 严重漏洞 / 安全待办

> 项目处于**开发阶段**，暂不打算上线。本文件登记所有上线前必须处理的安全 / 发布阻断问题，先记录，不在代码里加运行期校验。
> 真正要发布版本前，按本表逐项落地。

## 来源

2026-05-12 通过 `/codex:adversarial-review` 对当前工作树（HybridCLR + Addressables 热更新链路）做对抗审查识别。

---

## TODO-SEC-1 (critical) — Addressables 发布 profile 错配，发布后无法启动

**Where**: `Assets/AddressableAssetsData/AddressableAssetSettings.asset` 当前 `m_ActiveProfileId` = `765c260185b03451797cbfb2291d7292` (LocalDev)
- LocalDev 的 `Remote.LoadPath` = `http://localhost:9000/AA/[BuildTarget]`
- Build Settings 现在只勾 `Init.unity`；`Bootstrap` 默认 `Addressables.LoadSceneAsync("MainV2")`
- 后果：离开开发机，本机 9000 没起 → catalog/bundle 都拉不到 → 启动卡在 Init 场景

**Fix when shipping**:
- 新建生产 profile，`Remote.LoadPath` 指向 HTTPS CDN
- 构建脚本（`build_macos.sh` / HotUpdateBuildPreprocessor）切到该 profile
- 或者把 `MainV2` 改成本地资源放进 player 包内
- 另：增加构建期校验，active profile 是 LocalDev / undefined 时直接 `BuildFailedException`

---

## TODO-SEC-2 (high) — 热更新 DLL 没有完整性校验

**Where**: `Assets/HotUpdate/Bootstrap/LoadHotfixSystem.cs:110-121`
- 非 Editor 路径直接 `Addressables.LoadAssetAsync<TextAsset>(HotfixDllAddress)` 拿字节
- 立刻 `Assembly.Load(dllHandle.Result.bytes)` 执行
- 与 TODO-SEC-1 合在一起：远端 catalog/bundle 在明文 HTTP 上传输，链路可被替换 → 任意代码执行

**Fix when shipping**:
- 构建期对 `App.Hotfix.dll.bytes` 算 SHA256，写到 `Assets/StreamingAssets/HotfixManifest/hashes.json`（StreamingAssets 内容随 .app Bundle 一起被 Apple 签名 + 公证，构成可信锚）
- 运行期对加载到的字节算 SHA256，比对清单，错就 `onFatal` 中止 `Assembly.Load`
- 若要更高强度：嵌入 RSA 公钥 + 服务端签名验证（防止攻击者同步替换 hashes.json 和 DLL）
- 远端必须 HTTPS

> 设计稿已经写过一遍后撤销，预期增量：
> - 新建 `Assets/HotUpdate/Bootstrap/HotfixManifest.cs`（`[Serializable]` POCO，address + sha256）
> - `HotUpdateBuildPreprocessor` 在 `CopyHotfixDll` 后写 manifest
> - `LoadHotfixSystem` 在 `Assembly.Load` 前比对

---

## TODO-SEC-3 (medium) — 明文 HTTP 热更新通道

**Where**: 同 TODO-SEC-1，`Remote.LoadPath` 是 `http://`
- 即使把 profile 切到生产，也要确认 URL 是 `https://`
- `ProjectSettings.asset` 里 `useInsecureWebHTTP` / NSAppTransportSecurity 也要关闭明文 HTTP 例外
- 否则中间人可注入恶意 catalog / bundle

**Fix when shipping**:
- 生产 profile 强制 HTTPS
- 在 PlayerSettings / Info.plist 关掉 ATS 明文豁免
- 构建期 guard：active profile 的 `Remote.LoadPath` 以 `http://` 开头就 fail build

---

## TODO-SEC-4 (high) — Addressables 客户端固定绑定可变 `latest` badge，无版本隔离/回滚

**Where**:
- `Assets/AddressableAssetsData/AddressableAssetSettings.asset:96-97` — LocalDev profile 的 `Remote.LoadPath` = `https://a.unity.cn/client_api/v1/buckets/205577ad-…/release_by_badge/latest/content/[BuildTarget]`
- `cdn/uos/publish.sh` 默认 `UAS_BADGE=latest`，每次跑都会把 latest 重新指到新 release

**后果**:
- 任意一次 publish 都会立刻改变**所有**用同一份 player 装机的客户端能拉到的内容
- 没有版本/渠道/灰度隔离：不能把内测渠道固定在某个 release，也不能在线上出问题时把 latest 回滚到上一个稳定 release
- 跨多个客户端版本共用 latest，若新发布的 catalog 不兼容旧 player 的 hotfix DLL ABI，会同时砸所有用户

**Fix when shipping**:
- 客户端按 PlayerVersion 拼出 `release_by_badge/<channel-vN>/content/...`，channel 例如 `live` / `beta` / `internal`，N 跟 player bundleVersion 对齐
- publish.sh 增加 `--badge <name>` 显式参数；线上发布默认必须显式传 `--badge live-vN`，不允许默认 latest
- 维持一个稳定的 `live-vN-rollback` 候选 badge：发布前先把它指到当前 stable release，验证完才挪 channel badge
- 构建期 guard：active profile 的 `Remote.LoadPath` 包含字面 "release_by_badge/latest" 时 fail build

---

## TODO-SEC-5 (medium) — uas auth 缓存不绑定到当前 .env，多 bucket / 共享机风险

**Where**: `cdn/uos/_lib.sh` 的 `uas_login_if_needed`
- 只要 `uas auth info` 不报错就跳过登录
- 不校验缓存的身份是来自当前 `.env` 还是上一次切走的另一个 app/bucket

**后果**:
- 共享开发机 / CI runner 上，前一个项目登录的 keychain 缓存可能让脚本带着错的身份继续跑
- 多 bucket 权限的 service account 切换时，本地缓存可能让 `entries sync` 写到错的 bucket
- 直到 release/badge 阶段才暴露错配，但此时数据已经写出去了

**Fix when shipping**:
- 比对 `uas auth info` 输出的 App ID / project / org 与当前 `.env` 是否一致；不一致先 logout 再 login
- CI 流水线统一设 `FORCE_LOGIN=1`（已在 `_lib.sh` 中支持，但要在 CI 模板里显式打开）
- 或彻底放弃 keychain 缓存路径，全程走 `--uos_app_id` + `--uos_app_service_secret` per-command 鉴权（待 CLI 修复 per-command 错误解析后可行）

---

## 备注：已落地的修复

- ✅ `Assets/Editor/MacOSBuildPostProcessor.cs` — HybridCLR 临时 BuildPipeline 跑 strip 阶段触发 `[PostProcessBuild]`，`MacOSBuildPostProcessor` 试图往临时目录拷 entitlements 抛 `DirectoryNotFoundException`，已加入 `.app` Bundle 早期判定后跳过
- ✅ `Assets/Scenes/Init.unity` — UniWindowController 的 `m_Enabled=0` prefab override 改回 1，否则透明 / 置顶 / 点击穿透都不工作（属功能 bug，非安全问题）
