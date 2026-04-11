#if UNITY_EDITOR
using System.Collections.Generic;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Model;
using UnityEditor;
using UnityEngine;

namespace APP.Editor
{
    /// <summary>
    /// 网络模拟器：在编辑器中模拟远程玩家加入/离开/状态变化，
    /// 用于测试 PlayerCardManager 和 PlayerCardView 的 UI 显示。
    /// 无需真实 WebSocket 连接，直接通过 TypeEventSystem 发送事件。
    /// </summary>
    public sealed class NetworkSimulatorWindow : EditorWindow
    {
        [MenuItem("Tools/网络模拟器 (PlayerCard 测试)")]
        private static void Open()
        {
            GetWindow<NetworkSimulatorWindow>("网络模拟器").Show();
        }

        // ─── 模拟玩家池 ──────────────────────────────────────
        private readonly List<SimPlayer> _players = new List<SimPlayer>();
        private Vector2 _scrollPos;
        private int _nextId = 1;

        // ─── 快速添加 ───────────────────────────────────────
        private string _quickName = "";

        private sealed class SimPlayer
        {
            public string Id;
            public string Name;
            public PomodoroPhase Phase;
            public int RemainingSeconds = 1500;
            public int CurrentRound = 1;
            public int TotalRounds = 4;
            public bool IsRunning;
            public bool AutoTick; // 自动倒计时
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        // ─── 自动倒计时驱动 ────────────────────────────────
        private double _lastTickTime;

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTickTime < 1.0) return;
            _lastTickTime = now;

            bool dirty = false;
            foreach (var p in _players)
            {
                if (!p.AutoTick || !p.IsRunning || p.RemainingSeconds <= 0) continue;

                p.RemainingSeconds--;
                if (p.RemainingSeconds <= 0)
                {
                    AdvancePhase(p);
                }

                SendStateUpdate(p);
                dirty = true;
            }

            if (dirty) Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("网络模拟器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在 Play 模式下使用。模拟远程玩家加入/离开/状态变化，\n" +
                "通过 TypeEventSystem 发送事件驱动 PlayerCardManager。",
                MessageType.Info);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("请先进入 Play 模式！", MessageType.Warning);
                return;
            }

            DrawQuickAdd();
            EditorGUILayout.Space(8);
            DrawBatchActions();
            EditorGUILayout.Space(8);
            DrawPlayerList();
        }

        // ─── 快速添加区 ─────────────────────────────────────
        private void DrawQuickAdd()
        {
            EditorGUILayout.BeginHorizontal();
            _quickName = EditorGUILayout.TextField("玩家昵称", _quickName);
            if (GUILayout.Button("添加玩家", GUILayout.Width(80)))
            {
                string name = string.IsNullOrEmpty(_quickName) ? $"玩家{_nextId}" : _quickName;
                AddPlayer(name);
                _quickName = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加 3 个测试玩家"))
            {
                AddPlayer("小明");
                AddPlayer("小红");
                AddPlayer("小刚");
            }

            if (GUILayout.Button("发送 Snapshot 事件"))
            {
                SendSnapshotEvent();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ─── 批量操作 ───────────────────────────────────────
        private void DrawBatchActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全部开始"))
            {
                foreach (var p in _players)
                {
                    p.IsRunning = true;
                    p.AutoTick = true;
                    SendStateUpdate(p);
                }
            }

            if (GUILayout.Button("全部暂停"))
            {
                foreach (var p in _players)
                {
                    p.IsRunning = false;
                    SendStateUpdate(p);
                }
            }

            if (GUILayout.Button("全部移除"))
            {
                for (int i = _players.Count - 1; i >= 0; i--)
                {
                    SendPlayerLeft(_players[i].Id);
                }
                _players.Clear();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ─── 玩家列表 ───────────────────────────────────────
        private void DrawPlayerList()
        {
            EditorGUILayout.LabelField($"模拟玩家 ({_players.Count})", EditorStyles.boldLabel);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = _players.Count - 1; i >= 0; i--)
            {
                DrawPlayerRow(_players[i], i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPlayerRow(SimPlayer p, int index)
        {
            EditorGUILayout.BeginVertical("box");

            // 第一行：名称 + ID + 移除
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{p.Name} ({p.Id})", EditorStyles.boldLabel);
            if (GUILayout.Button("移除", GUILayout.Width(50)))
            {
                SendPlayerLeft(p.Id);
                _players.RemoveAt(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            // 第二行：阶段 + 剩余时间
            EditorGUILayout.BeginHorizontal();
            var newPhase = (PomodoroPhase)EditorGUILayout.EnumPopup("阶段", p.Phase);
            if (newPhase != p.Phase)
            {
                p.Phase = newPhase;
                p.RemainingSeconds = GetDefaultSeconds(newPhase);
                SendStateUpdate(p);
            }

            int mins = p.RemainingSeconds / 60;
            int secs = p.RemainingSeconds % 60;
            EditorGUILayout.LabelField($"{mins:D2}:{secs:D2}", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            // 第三行：轮次 + 运行状态
            EditorGUILayout.BeginHorizontal();
            int newRound = EditorGUILayout.IntSlider("轮次", p.CurrentRound, 1, p.TotalRounds);
            if (newRound != p.CurrentRound)
            {
                p.CurrentRound = newRound;
                SendStateUpdate(p);
            }
            EditorGUILayout.EndHorizontal();

            // 第四行：控制按钮
            EditorGUILayout.BeginHorizontal();
            bool wasRunning = p.IsRunning;
            if (GUILayout.Button(p.IsRunning ? "暂停" : "开始"))
            {
                p.IsRunning = !p.IsRunning;
                p.AutoTick = p.IsRunning;
                SendStateUpdate(p);
            }

            if (GUILayout.Button("重置"))
            {
                p.Phase = PomodoroPhase.Focus;
                p.RemainingSeconds = 1500;
                p.CurrentRound = 1;
                p.IsRunning = false;
                p.AutoTick = false;
                SendStateUpdate(p);
            }

            p.AutoTick = EditorGUILayout.ToggleLeft("自动倒计时", p.AutoTick, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ─── 核心操作 ───────────────────────────────────────

        private void AddPlayer(string name)
        {
            var player = new SimPlayer
            {
                Id = $"sim-{_nextId++}",
                Name = name,
                Phase = PomodoroPhase.Focus,
                RemainingSeconds = 1500,
                CurrentRound = 1,
                TotalRounds = 4,
                IsRunning = false,
            };
            _players.Add(player);

            var data = ToRemotePlayerData(player);

            // 先更新 RoomModel
            var roomModel = GameApp.Interface.GetModel<IRoomModel>();
            roomModel.AddOrUpdateRemotePlayer(data);

            // 发送 PlayerJoined 事件
            GameApp.Interface.SendEvent(new E_PlayerJoined(data));

            Debug.Log($"[NetworkSimulator] 玩家 '{name}' (id={player.Id}) 已加入");
        }

        private void SendPlayerLeft(string playerId)
        {
            var roomModel = GameApp.Interface.GetModel<IRoomModel>();
            roomModel.RemoveRemotePlayer(playerId);

            GameApp.Interface.SendEvent(new E_PlayerLeft(playerId));

            Debug.Log($"[NetworkSimulator] 玩家 (id={playerId}) 已离开");
        }

        private void SendStateUpdate(SimPlayer p)
        {
            if (!Application.isPlaying) return;

            var data = ToRemotePlayerData(p);
            var roomModel = GameApp.Interface.GetModel<IRoomModel>();
            roomModel.AddOrUpdateRemotePlayer(data);

            GameApp.Interface.SendEvent(new E_RemoteStateUpdated(p.Id));
        }

        private void SendSnapshotEvent()
        {
            var list = new List<RemotePlayerData>();
            foreach (var p in _players)
            {
                list.Add(ToRemotePlayerData(p));
            }

            var roomModel = GameApp.Interface.GetModel<IRoomModel>();
            roomModel.ClearRemotePlayers();
            foreach (var d in list)
            {
                roomModel.AddOrUpdateRemotePlayer(d);
            }

            GameApp.Interface.SendEvent(new E_RoomSnapshot(list));

            Debug.Log($"[NetworkSimulator] 已发送 Snapshot，包含 {list.Count} 个玩家");
        }

        // ─── 工具方法 ───────────────────────────────────────

        private static RemotePlayerData ToRemotePlayerData(SimPlayer p)
        {
            return new RemotePlayerData
            {
                PlayerId = p.Id,
                PlayerName = p.Name,
                Phase = p.Phase,
                RemainingSeconds = p.RemainingSeconds,
                CurrentRound = p.CurrentRound,
                TotalRounds = p.TotalRounds,
                IsRunning = p.IsRunning,
            };
        }

        private static int GetDefaultSeconds(PomodoroPhase phase)
        {
            switch (phase)
            {
                case PomodoroPhase.Focus: return 1500;
                case PomodoroPhase.Break: return 300;
                case PomodoroPhase.Completed: return 0;
                default: return 1500;
            }
        }

        private static void AdvancePhase(SimPlayer p)
        {
            switch (p.Phase)
            {
                case PomodoroPhase.Focus:
                    p.Phase = PomodoroPhase.Break;
                    p.RemainingSeconds = 300;
                    break;
                case PomodoroPhase.Break:
                    p.CurrentRound++;
                    if (p.CurrentRound > p.TotalRounds)
                    {
                        p.Phase = PomodoroPhase.Completed;
                        p.RemainingSeconds = 0;
                        p.IsRunning = false;
                        p.AutoTick = false;
                    }
                    else
                    {
                        p.Phase = PomodoroPhase.Focus;
                        p.RemainingSeconds = 1500;
                    }
                    break;
                case PomodoroPhase.Completed:
                    p.IsRunning = false;
                    p.AutoTick = false;
                    break;
            }
        }
    }
}
#endif
