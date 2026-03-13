using System;
using System.Collections;
using System.Collections.Generic;
using CPA.Monitoring;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZ.VisualTest.Runtime
{
    public sealed class AppMonitorPanel : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private float _refreshInterval = 1f;

        private const string DefaultAppName = "未检测到应用";
        private const string DefaultWindowTitle = "等待窗口信息…";
        private const string PermissionDeniedHint = "请在系统设置 > 安全性与隐私 > 辅助功能中授权";
        private const string UnsupportedHint = "当前为 Editor 环境，已跳过原生层调用";
        private const string QueryFailedPrefix = "获取应用信息失败：";

        private VisualElement _appIconElement;
        private Label _appNameLabel;
        private Label _windowTitleLabel;
        private Label _statusLabel;

        private Coroutine _refreshCoroutine;
        private readonly IconCache _iconCache = new IconCache();

        private void Awake()
        {
            Debug.Log("[AppMonitorPanel] Awake 开始");
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
                Debug.Log($"[AppMonitorPanel] 自动获取 UIDocument: {(_uiDocument != null ? "成功" : "失败")}");
            }
            else
            {
                Debug.Log("[AppMonitorPanel] UIDocument 已通过 Inspector 赋值");
            }

            if (_uiDocument == null)
            {
                Debug.LogError("[AppMonitorPanel] UIDocument 组件未找到，脚本禁用");
                enabled = false;
            }
            else
            {
                Debug.Log("[AppMonitorPanel] Awake 完成，UIDocument 就绪");
            }
        }

        private void OnEnable()
        {
            Debug.Log("[AppMonitorPanel] OnEnable 开始");
            if (_uiDocument == null)
            {
                Debug.LogError("[AppMonitorPanel] OnEnable: UIDocument 为 null，跳过");
                return;
            }

            var root = _uiDocument.rootVisualElement;
            Debug.Log($"[AppMonitorPanel] rootVisualElement: {(root != null ? "存在" : "为 null")}");

            _appIconElement = root.Q<VisualElement>("app-icon");
            _appNameLabel = root.Q<Label>("app-name");
            _windowTitleLabel = root.Q<Label>("window-title");
            _statusLabel = root.Q<Label>("status-label") ?? _windowTitleLabel;

            Debug.Log($"[AppMonitorPanel] UI 元素查找结果: app-icon={(_appIconElement != null ? "√" : "×")}, app-name={(_appNameLabel != null ? "√" : "×")}, window-title={(_windowTitleLabel != null ? "√" : "×")}, status-label={(_statusLabel != null ? "√" : "×")}");

            if (_appIconElement == null || _appNameLabel == null || _windowTitleLabel == null)
            {
                Debug.LogError("[AppMonitorPanel] UXML 缺少必要元素：app-icon / app-name / window-title，停止刷新");
                return;
            }

            Debug.Log("[AppMonitorPanel] 所有 UI 元素就绪，开始刷新循环");
            ShowUnknownState();
            StartRefresh();
        }

        private void OnDisable()
        {
            StopRefresh();
            _iconCache.Clear();
        }

        private void StartRefresh()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
            }

            _refreshCoroutine = StartCoroutine(RefreshLoop());
        }

        private void StopRefresh()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private IEnumerator RefreshLoop()
        {
            Debug.Log("[AppMonitorPanel] RefreshLoop 开始");
            int loopCount = 0;
            while (true)
            {
                loopCount++;
                Debug.Log($"[AppMonitorPanel] 第 {loopCount} 次刷新, CanCallNativeMonitor={CanCallNativeMonitor()}, platform={Application.platform}, isEditor={Application.isEditor}, IsPermissionGranted={AppMonitor.Instance.IsPermissionGranted}");
                if (!CanCallNativeMonitor())
                {
                    Debug.LogWarning("[AppMonitorPanel] 不支持原生监控，显示不支持状态");
                    ShowUnsupportedState();
                    yield return new WaitForSeconds(_refreshInterval);
                    continue;
                }

                try
                {
                    Debug.Log("[AppMonitorPanel] 调用 AppMonitor.Instance.GetCurrentApp()");
                    AppInfo info = AppMonitor.Instance.GetCurrentApp();
                    Debug.Log($"[AppMonitorPanel] GetCurrentApp 返回: IsSuccess={info?.IsSuccess}, AppName='{info?.AppName}', ErrorCode={info?.ErrorCode}, ErrorMessage='{info?.ErrorMessage}'");
                    UpdateUI(info);
                }
                catch (PermissionDeniedException ex)
                {
                    Debug.LogWarning($"[AppMonitorPanel] PermissionDeniedException: {ex.Message}");
                    ShowPermissionDeniedState();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[AppMonitorPanel] 刷新异常: {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
                    ShowQueryFailedState(exception.Message);
                }

                yield return new WaitForSeconds(_refreshInterval);
            }
        }

        private void UpdateUI(AppInfo info)
        {
            Debug.Log($"[AppMonitorPanel] UpdateUI: info={(info == null ? "null" : $"IsSuccess={info.IsSuccess}")}");
            if (info == null)
            {
                ShowUnknownState();
                return;
            }

            if (!info.IsSuccess)
            {
                if (info.Icon != null)
                {
                    Destroy(info.Icon);
                }

                Debug.LogWarning($"[AppMonitorPanel] 获取失败: {info.ErrorMessage}");
                ShowQueryFailedState(info.ErrorMessage);
                return;
            }

            _statusLabel.RemoveFromClassList("error-state");
            _appNameLabel.text = string.IsNullOrWhiteSpace(info.AppName) ? DefaultAppName : info.AppName;
            _windowTitleLabel.text = string.IsNullOrWhiteSpace(info.WindowTitle) ? DefaultWindowTitle : info.WindowTitle;
            Debug.Log($"[AppMonitorPanel] UI 更新: AppName='{_appNameLabel.text}', WindowTitle='{_windowTitleLabel.text}'");

            Texture2D iconTexture = ResolveIconWithCache(info);
            _appIconElement.style.backgroundImage = new StyleBackground(iconTexture);
        }

        private Texture2D ResolveIconWithCache(AppInfo info)
        {
            Texture2D incomingIcon = info.Icon;
            string cacheKey = BuildIconCacheKey(info);

            Texture2D cachedIcon = _iconCache.GetOrAdd(cacheKey, () => incomingIcon);
            if (!ReferenceEquals(cachedIcon, incomingIcon) && incomingIcon != null)
            {
                Destroy(incomingIcon);
            }

            return cachedIcon;
        }

        private static string BuildIconCacheKey(AppInfo info)
        {
            if (info == null)
            {
                return "unknown-app";
            }

            if (!string.IsNullOrWhiteSpace(info.AppName))
            {
                return info.AppName.Trim();
            }

            return "unknown-app";
        }

        private void ShowUnknownState()
        {
            _statusLabel?.RemoveFromClassList("error-state");

            if (_appNameLabel != null)
            {
                _appNameLabel.text = DefaultAppName;
            }

            if (_windowTitleLabel != null)
            {
                _windowTitleLabel.text = DefaultWindowTitle;
            }

            if (_appIconElement != null)
            {
                _appIconElement.style.backgroundImage = new StyleBackground((Texture2D)null);
            }
        }

        private void ShowUnsupportedState()
        {
            _statusLabel?.RemoveFromClassList("error-state");

            if (_appNameLabel != null)
            {
                _appNameLabel.text = DefaultAppName;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = UnsupportedHint;
            }

            if (_appIconElement != null)
            {
                _appIconElement.style.backgroundImage = new StyleBackground((Texture2D)null);
            }
        }

        private void ShowPermissionDeniedState()
        {
            if (_appNameLabel != null)
            {
                _appNameLabel.text = "权限不足";
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = PermissionDeniedHint;
                _statusLabel.AddToClassList("error-state");
            }

            if (_appIconElement != null)
            {
                _appIconElement.style.backgroundImage = new StyleBackground((Texture2D)null);
            }
        }

        private void ShowQueryFailedState(string message)
        {
            _statusLabel?.AddToClassList("error-state");

            if (_appNameLabel != null)
            {
                _appNameLabel.text = "读取失败";
            }

            if (_statusLabel != null)
            {
                string detail = string.IsNullOrWhiteSpace(message) ? "未知错误" : message;
                _statusLabel.text = $"{QueryFailedPrefix}{detail}";
            }

            if (_appIconElement != null)
            {
                _appIconElement.style.backgroundImage = new StyleBackground((Texture2D)null);
            }
        }

        private static bool CanCallNativeMonitor()
        {
            return !Application.isEditor && Application.platform == RuntimePlatform.OSXPlayer;
        }

        private sealed class IconCache
        {
            private readonly Dictionary<string, Texture2D> _cache = new();
            private readonly Queue<string> _accessOrder = new();
            private readonly Dictionary<string, int> _accessCounts = new();

            private const int MaxSize = 50;

            public Texture2D GetOrAdd(string key, Func<Texture2D> loader)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return loader?.Invoke();
                }

                if (_cache.TryGetValue(key, out Texture2D cachedTexture))
                {
                    Touch(key);
                    return cachedTexture;
                }

                Texture2D loadedTexture = loader?.Invoke();
                if (loadedTexture == null)
                {
                    return null;
                }

                _cache[key] = loadedTexture;
                Touch(key);
                TrimToLimit();

                return loadedTexture;
            }

            public void Clear()
            {
                foreach (var pair in _cache)
                {
                    if (pair.Value != null)
                    {
                        UnityEngine.Object.Destroy(pair.Value);
                    }
                }

                _cache.Clear();
                _accessOrder.Clear();
                _accessCounts.Clear();
            }

            private void Touch(string key)
            {
                _accessOrder.Enqueue(key);

                if (_accessCounts.TryGetValue(key, out int count))
                {
                    _accessCounts[key] = count + 1;
                    return;
                }

                _accessCounts[key] = 1;
            }

            private void TrimToLimit()
            {
                while (_cache.Count > MaxSize && _accessOrder.Count > 0)
                {
                    string oldestKey = _accessOrder.Dequeue();
                    if (!_accessCounts.TryGetValue(oldestKey, out int accessCount))
                    {
                        continue;
                    }

                    accessCount -= 1;
                    if (accessCount > 0)
                    {
                        _accessCounts[oldestKey] = accessCount;
                        continue;
                    }

                    _accessCounts.Remove(oldestKey);

                    if (_cache.TryGetValue(oldestKey, out Texture2D texture))
                    {
                        if (texture != null)
                        {
                            UnityEngine.Object.Destroy(texture);
                        }

                        _cache.Remove(oldestKey);
                    }
                }
            }
        }
    }
}
