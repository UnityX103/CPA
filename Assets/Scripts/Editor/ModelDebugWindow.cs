#if UNITY_EDITOR
using APP.Pomodoro;
using APP.Pomodoro.Model;
using UnityEditor;
using UnityEngine;

namespace APP.Editor
{
    /// <summary>
    /// Model 调试器：运行时直接读写 IGameModel / IPomodoroModel.IsPinned / IPlayerCardModel。
    /// 设计意图：给本次"失焦隐藏"功能提供手动注入入口，替代尚未接入的真实数据源。
    /// </summary>
    public sealed class ModelDebugWindow : EditorWindow
    {
        [MenuItem("Tools/Model 调试器")]
        private static void Open() => GetWindow<ModelDebugWindow>("Model 调试器").Show();

        private Vector2 _scroll;

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("仅运行时可用。进入 Play Mode 后展示 Model 字段。", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawGameModel();
            EditorGUILayout.Space();
            DrawPomodoroModel();
            EditorGUILayout.Space();
            DrawPlayerCardModel();
            EditorGUILayout.EndScrollView();
        }

        private void DrawGameModel()
        {
            EditorGUILayout.LabelField("GameModel", EditorStyles.boldLabel);
            var model = GameApp.Interface.GetModel<IGameModel>();
            if (model == null)
            {
                EditorGUILayout.HelpBox("未注册 IGameModel", MessageType.Warning);
                return;
            }

            bool next = EditorGUILayout.Toggle("IsAppFocused", model.IsAppFocused.Value);
            if (next != model.IsAppFocused.Value)
            {
                model.IsAppFocused.Value = next;
            }
        }

        private void DrawPomodoroModel()
        {
            EditorGUILayout.LabelField("PomodoroModel", EditorStyles.boldLabel);
            var model = GameApp.Interface.GetModel<IPomodoroModel>();
            if (model == null)
            {
                EditorGUILayout.HelpBox("未注册 IPomodoroModel", MessageType.Warning);
                return;
            }

            bool next = EditorGUILayout.Toggle("IsPinned", model.IsPinned.Value);
            if (next != model.IsPinned.Value)
            {
                model.IsPinned.Value = next;
            }
        }

        private void DrawPlayerCardModel()
        {
            var model = GameApp.Interface.GetModel<IPlayerCardModel>();
            int count = model?.Cards?.Count ?? 0;
            EditorGUILayout.LabelField($"PlayerCardModel (Cards = {count})", EditorStyles.boldLabel);
            if (model == null)
            {
                EditorGUILayout.HelpBox("未注册 IPlayerCardModel", MessageType.Warning);
                return;
            }

            var cards = model.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("playerId", card.PlayerId);

                Vector2 nextPos = EditorGUILayout.Vector2Field("Position", card.Position.Value);
                if (nextPos != card.Position.Value) card.Position.Value = nextPos;

                bool nextPin = EditorGUILayout.Toggle("IsPinned", card.IsPinned.Value);
                if (nextPin != card.IsPinned.Value) card.IsPinned.Value = nextPin;
                EditorGUILayout.EndVertical();
            }
        }
    }
}
#endif
