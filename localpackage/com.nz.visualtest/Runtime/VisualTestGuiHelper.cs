using System.Collections.Generic;
using UnityEngine;

namespace NZ.VisualTest.Runtime
{
    /// <summary>
    /// 可视化测试 GUI 辅助组件
    /// 挂载在测试场景辅助 GameObject 上，负责在屏幕上实时显示当前输入操作
    /// </summary>
    public class VisualTestGuiHelper : MonoBehaviour
    {
        private const int MaxHistoryCount = 5;
        private const float CurrentActionDisplayDuration = 2f;

        private readonly List<string> _actionHistory = new List<string>();
        private string _currentAction = string.Empty;
        private float _currentActionTimer = 0f;

        private GUIStyle _historyStyle;
        private GUIStyle _currentActionStyle;
        private GUIStyle _backgroundStyle;

        private void Awake()
        {
            InitStyles();
        }

        private void InitStyles()
        {
            // 历史记录样式：小字灰色
            _historyStyle = new GUIStyle();
            _historyStyle.fontSize = 14;
            _historyStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            _historyStyle.padding = new RectOffset(8, 8, 4, 4);

            // 当前操作样式：大字白色
            _currentActionStyle = new GUIStyle();
            _currentActionStyle.fontSize = 24;
            _currentActionStyle.fontStyle = FontStyle.Bold;
            _currentActionStyle.alignment = TextAnchor.MiddleCenter;
            _currentActionStyle.padding = new RectOffset(16, 16, 8, 8);

            // 背景样式：黑色半透明
            _backgroundStyle = new GUIStyle();
            var bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            bgTexture.Apply();
            _backgroundStyle.normal.background = bgTexture;
            _backgroundStyle.padding = new RectOffset(6, 6, 4, 4);
        }

        private void Update()
        {
            if (_currentActionTimer > 0f)
            {
                _currentActionTimer -= Time.deltaTime;
                if (_currentActionTimer <= 0f)
                {
                    _currentActionTimer = 0f;
                }
            }
        }

        /// <summary>
        /// 记录一条输入操作，显示在屏幕上并加入历史列表
        /// </summary>
        public void LogAction(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // 更新当前操作（底部大字显示）
            _currentAction = text;
            _currentActionTimer = CurrentActionDisplayDuration;

            // 加入历史记录（顶部小字）
            _actionHistory.Add(text);
            if (_actionHistory.Count > MaxHistoryCount)
            {
                _actionHistory.RemoveAt(0);
            }
        }

        private void OnGUI()
        {
            // 确保样式已初始化（OnGUI 可能在 Awake 前调用）
            if (_historyStyle == null)
            {
                InitStyles();
            }

            DrawHistoryPanel();
            DrawCurrentActionPanel();
        }

        private void DrawHistoryPanel()
        {
            if (_actionHistory.Count == 0)
                return;

            float x = 10f;
            float y = 10f;
            float width = 300f;
            float lineHeight = 24f;
            float panelHeight = _actionHistory.Count * lineHeight + 12f;

            // 绘制背景
            GUI.Box(new Rect(x - 4, y - 4, width + 8, panelHeight), GUIContent.none, _backgroundStyle);

            // 绘制历史条目（最旧的在上，最新的在下）
            for (int i = 0; i < _actionHistory.Count; i++)
            {
                // 越旧的条目越透明
                float alpha = 0.4f + 0.6f * ((float)(i + 1) / _actionHistory.Count);
                Color originalColor = _historyStyle.normal.textColor;
                _historyStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, alpha);

                GUI.Label(new Rect(x, y + i * lineHeight, width, lineHeight), _actionHistory[i], _historyStyle);

                _historyStyle.normal.textColor = originalColor;
            }
        }

        private void DrawCurrentActionPanel()
        {
            if (_currentActionTimer <= 0f || string.IsNullOrEmpty(_currentAction))
                return;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float panelWidth = screenWidth * 0.6f;
            float panelHeight = 56f;
            float x = (screenWidth - panelWidth) * 0.5f;
            float y = screenHeight - panelHeight - 30f;

            // 淡出效果：最后 0.5 秒淡出
            float alpha = Mathf.Clamp01(_currentActionTimer / 0.5f);
            Color textColor = new Color(1f, 1f, 1f, alpha);
            _currentActionStyle.normal.textColor = textColor;

            // 绘制半透明背景
            Color bgColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha * 0.85f);
            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none, _backgroundStyle);
            GUI.color = bgColor;

            // 绘制文字
            GUI.Label(new Rect(x, y, panelWidth, panelHeight), _currentAction, _currentActionStyle);
        }
    }
}
