using System;
using System.Collections.Generic;
using APP.Pomodoro.Command;
using APP.Pomodoro.Config;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using Kirurobo;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PomodoroPanelController : MonoBehaviour, IController
    {
        // ─── Inspector 引用 ──────────────────────────────────────
        [Header("配置表")]
        [SerializeField] private PomodoroConfig _config;

        [Header("UniWindowController（可留空，运行时自动查找）")]
        [SerializeField] private UniWindowController _uwc;

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument _uiDocument;
        private AudioSource _audioSource;

        // 主面板元素
        private Label _labelPhase;
        private Label _labelTimer;
        private Label _labelRound;
        private Button _btnStartPause;
        private Button _btnSkipPhase;
        private Button _btnReset;
        private Button _btnSettings;
        private Button _btnCloseApp;

        // 设置面板元素
        private VisualElement _settingsPanel;
        private IntegerField _fieldFocusMinutes;
        private IntegerField _fieldBreakMinutes;
        private IntegerField _fieldRounds;
        private Toggle _toggleAutoJump;
        private Toggle _toggleAnchorTop;
        private DropdownField _dropdownSound;
        private DropdownField _dropdownTargetMonitor;
        private Button _btnApplySettings;
        private Button _btnCloseSettings;

        // 完成提示横幅
        private VisualElement _completionBanner;

        // 缓存
        private IPomodoroModel _model;
        private bool _settingsOpen;

        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── Unity 生命周期 ──────────────────────────────────────

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _audioSource = GetComponent<AudioSource>();

            if (_uwc == null)
            {
                _uwc = FindAnyObjectByType<UniWindowController>();
            }
        }

        private void Start()
        {
            // 1. 初始化 Architecture（注册 Model/System）
            _ = GameApp.Interface;

            // 2. 从配置表写入默认参数
            this.SendCommand(new Cmd_PomodoroInitialize(_config, _uwc));

            // 3. 绑定 Model 缓存
            _model = this.GetModel<IPomodoroModel>();
            RegisterPersistenceCallbacks();

            // 4. 绑定 UI
            BindUI();

            // 5. 订阅事件
            this.RegisterEvent<E_PomodoroPhaseChanged>(OnPhaseChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<E_PomodoroCycleCompleted>(OnCycleCompleted)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 6. 订阅 Model 变化刷新 UI
            _model.RemainingSeconds.RegisterWithInitValue(OnRemainingSecondsChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.CurrentPhase.RegisterWithInitValue(OnCurrentPhaseChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.CurrentRound.RegisterWithInitValue(OnCurrentRoundChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.TotalRounds.RegisterWithInitValue(OnTotalRoundsChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.IsRunning.RegisterWithInitValue(OnIsRunningChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 锚点变化 → 更新 CSS class（全屏透明窗口通过样式决定卡片位置）
            _model.WindowAnchor.RegisterWithInitValue(OnWindowAnchorChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void Update()
        {
            this.SendCommand(new Cmd_PomodoroTick(Time.deltaTime));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                this.SendCommand(new Cmd_PomodoroRevertTopmost());
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SavePersistentState(true);
            }
        }

        private void OnApplicationQuit()
        {
            SavePersistentState(true);
        }

        // ─── UI 绑定 ─────────────────────────────────────────────

        private void BindUI()
        {
            VisualElement root = _uiDocument.rootVisualElement;

            // 主面板
            _labelPhase = root.Q<Label>("label-phase");
            _labelTimer = root.Q<Label>("label-timer");
            _labelRound = root.Q<Label>("label-round");
            _btnStartPause = root.Q<Button>("btn-start-pause");
            _btnSkipPhase = root.Q<Button>("btn-skip-phase");
            _btnReset = root.Q<Button>("btn-reset");
            _btnSettings = root.Q<Button>("btn-settings");
            _btnCloseApp = root.Q<Button>("btn-close-app");
            _completionBanner = root.Q<VisualElement>("completion-banner");

            // 设置面板
            _settingsPanel = root.Q<VisualElement>("settings-panel");
            _fieldFocusMinutes = root.Q<IntegerField>("field-focus-minutes");
            _fieldBreakMinutes = root.Q<IntegerField>("field-break-minutes");
            _fieldRounds = root.Q<IntegerField>("field-rounds");
            _toggleAutoJump = root.Q<Toggle>("toggle-auto-jump");
            _toggleAnchorTop = root.Q<Toggle>("toggle-anchor-top");
            _dropdownSound = root.Q<DropdownField>("dropdown-sound");
            _dropdownTargetMonitor = root.Q<DropdownField>("dropdown-target-monitor");
            _btnApplySettings = root.Q<Button>("btn-apply-settings");
            _btnCloseSettings = root.Q<Button>("btn-close-settings");

            // 初始状态
            _settingsPanel?.AddToClassList("hidden");
            _completionBanner?.AddToClassList("hidden");

            // 主面板按钮事件
            RegisterButtonOnPointerUp(_btnStartPause, OnStartPauseClicked);
            RegisterButtonOnPointerUp(_btnSkipPhase, OnSkipPhaseClicked);
            RegisterButtonOnPointerUp(_btnReset, () => this.SendCommand(new Cmd_PomodoroReset()));
            RegisterButtonOnPointerUp(_btnSettings, ToggleSettings);
            RegisterButtonOnPointerUp(_btnCloseApp, OnCloseAppClicked);

            // 设置面板按钮事件
            RegisterButtonOnPointerUp(_btnApplySettings, OnApplySettings);
            RegisterButtonOnPointerUp(_btnCloseSettings, ToggleSettings);

            // 置顶 Toggle
            _toggleAnchorTop?.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                this.SendCommand(new Cmd_PomodoroSetTopmost(evt.newValue));
            });

            // 填充下拉列表
            RefreshSoundDropdown();
            RefreshMonitorDropdown();

            // 初始化设置面板字段的当前值
            SyncSettingsFieldsFromModel();
        }

        // ─── 按钮回调 ────────────────────────────────────────────

        private void OnStartPauseClicked()
        {
            if (_model.IsRunning.Value)
            {
                this.SendCommand(new Cmd_PomodoroPause());
            }
            else
            {
                this.SendCommand(new Cmd_PomodoroStart());
            }
        }

        private void OnCloseAppClicked()
        {
            SavePersistentState(true);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnSkipPhaseClicked()
        {
            this.SendCommand(new Cmd_PomodoroSkipCurrentPhase());
        }

        private void OnApplySettings()
        {
            int focusMin = _fieldFocusMinutes?.value ?? _config?.DefaultFocusMinutes ?? 25;
            int breakMin = _fieldBreakMinutes?.value ?? _config?.DefaultBreakMinutes ?? 5;
            int rounds = _fieldRounds?.value ?? _config?.DefaultRounds ?? 4;
            bool autoJump = _toggleAutoJump?.value ?? true;
            int monitorIndex = GetSelectedMonitorIndex();

            this.SendCommand(new Cmd_PomodoroApplyMetaSettings(autoJump, GetSoundIndex()));
            this.SendCommand(new Cmd_PomodoroSetMonitor(monitorIndex));
            this.SendCommand(new Cmd_PomodoroApplySettings(focusMin, breakMin, rounds, resetProgress: true));
            ToggleSettings();
        }

        private void ToggleSettings()
        {
            _settingsOpen = !_settingsOpen;
            if (_settingsOpen)
            {
                RefreshMonitorDropdown();
                SyncSettingsFieldsFromModel();
                _settingsPanel?.RemoveFromClassList("hidden");
            }
            else
            {
                _settingsPanel?.AddToClassList("hidden");
            }
        }

        // ─── Model 变化回调 ──────────────────────────────────────

        private void OnRemainingSecondsChanged(int seconds)
        {
            if (_labelTimer != null)
            {
                int m = seconds / 60;
                int s = seconds % 60;
                _labelTimer.text = $"{m:00}:{s:00}";
            }
        }

        private void OnCurrentPhaseChanged(PomodoroPhase phase)
        {
            if (_labelPhase == null)
            {
                return;
            }

            _labelPhase.text = phase switch
            {
                PomodoroPhase.Focus => "专注",
                PomodoroPhase.Break => "休息",
                PomodoroPhase.Completed => "完成！",
                _ => string.Empty,
            };

            // 切换卡片主题色
            VisualElement root = _uiDocument.rootVisualElement;
            root.RemoveFromClassList("phase-focus");
            root.RemoveFromClassList("phase-break");
            root.RemoveFromClassList("phase-completed");
            root.AddToClassList(phase switch
            {
                PomodoroPhase.Break => "phase-break",
                PomodoroPhase.Completed => "phase-completed",
                _ => "phase-focus",
            });
        }

        private void OnCurrentRoundChanged(int round)
        {
            if (_labelRound != null)
            {
                _labelRound.text = $"{round} / {_model.TotalRounds.Value}";
            }
        }

        private void OnTotalRoundsChanged(int _)
        {
            OnCurrentRoundChanged(_model?.CurrentRound.Value ?? 1);
        }

        private void OnIsRunningChanged(bool running)
        {
            if (_btnStartPause != null)
            {
                _btnStartPause.text = running ? "暂停" : "开始";
            }

            UpdateSkipButtonVisibility(running);
        }

        private void OnWindowAnchorChanged(PomodoroWindowAnchor anchor)
        {
            VisualElement root = _uiDocument?.rootVisualElement;
            if (root == null)
            {
                return;
            }

            // 全屏透明窗口下通过 CSS class 控制卡片吸附位置
            if (anchor == PomodoroWindowAnchor.Bottom)
            {
                root.AddToClassList("anchor-bottom");
            }
            else
            {
                root.RemoveFromClassList("anchor-bottom");
            }
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnPhaseChanged(E_PomodoroPhaseChanged evt)
        {
            // 阶段切换时隐藏完成横幅（如果刚开始新轮次）
            if (evt.Phase != PomodoroPhase.Completed)
            {
                _completionBanner?.AddToClassList("hidden");
            }

            if (_model.AutoJumpToTopOnComplete.Value)
            {
                this.SendCommand(new Cmd_PomodoroJumpToScreenTop());
            }
        }

        private void OnCycleCompleted(E_PomodoroCycleCompleted evt)
        {
            // 显示完成横幅
            _completionBanner?.RemoveFromClassList("hidden");

            // 播放音效
            PlayCompletionSound();
        }

        // ─── 辅助 ────────────────────────────────────────────────

        private void PlayCompletionSound()
        {
            if (_config == null || _audioSource == null)
            {
                return;
            }

            AudioClip clip = _config.GetCompletionClip(_model.CompletionClipIndex.Value);
            if (clip == null)
            {
                return;
            }

            _audioSource.PlayOneShot(clip, _config.CompletionVolume);
        }

        private void RegisterPersistenceCallbacks()
        {
            if (_model == null)
            {
                return;
            }

            _model.FocusDurationSeconds.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.BreakDurationSeconds.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.TotalRounds.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.CurrentRound.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.RemainingSeconds.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.CurrentPhase.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.IsRunning.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.IsTopmost.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.WindowAnchor.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.AutoJumpToTopOnComplete.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.TargetMonitorIndex.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.CompletionClipIndex.Register(_ => SavePersistentState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void SavePersistentState(bool flushToDisk)
        {
            if (_model == null)
            {
                return;
            }

            PomodoroPersistence.Save(_model, flushToDisk);
        }

        private static void RegisterButtonOnPointerUp(Button button, Action onPointerUp)
        {
            if (button == null || onPointerUp == null)
            {
                return;
            }

            button.RegisterCallback<PointerUpEvent>(evt =>
            {
                onPointerUp();
            });
        }

        private void UpdateSkipButtonVisibility(bool isRunning)
        {
            if (_btnSkipPhase == null)
            {
                return;
            }

            if (isRunning)
            {
                _btnSkipPhase.RemoveFromClassList("hidden");
            }
            else
            {
                _btnSkipPhase.AddToClassList("hidden");
            }
        }

        private int GetSoundIndex()
        {
            if (_dropdownSound == null || _config?.CompletionClips == null || _config.CompletionClips.Count == 0)
            {
                return 0;
            }

            return Mathf.Clamp(_dropdownSound.index, 0, _config.CompletionClips.Count - 1);
        }

        private int GetSelectedMonitorIndex()
        {
            if (_dropdownTargetMonitor == null ||
                _dropdownTargetMonitor.choices == null ||
                _dropdownTargetMonitor.choices.Count == 0)
            {
                return _model?.TargetMonitorIndex.Value ?? 0;
            }

            return Mathf.Clamp(_dropdownTargetMonitor.index, 0, _dropdownTargetMonitor.choices.Count - 1);
        }

        private void RefreshSoundDropdown()
        {
            if (_dropdownSound == null || _config?.CompletionClips == null)
            {
                return;
            }

            var choices = new List<string>();
            for (int i = 0; i < _config.CompletionClips.Count; i++)
            {
                AudioClip clip = _config.CompletionClips[i];
                choices.Add(clip != null ? clip.name : $"音效 {i + 1}");
            }

            if (choices.Count == 0)
            {
                choices.Add("（无可用音效）");
            }

            _dropdownSound.choices = choices;
            _dropdownSound.index = Mathf.Clamp(
                _model?.CompletionClipIndex.Value ?? 0, 0, choices.Count - 1);
        }

        private void RefreshMonitorDropdown()
        {
            if (_dropdownTargetMonitor == null)
            {
                return;
            }

            int monitorCount = Mathf.Max(1, UniWindowController.GetMonitorCount());
            var choices = new List<string>(monitorCount);
            for (int i = 0; i < monitorCount; i++)
            {
                Rect rect = UniWindowController.GetMonitorRect(i);
                int w = Mathf.RoundToInt(rect.width);
                int h = Mathf.RoundToInt(rect.height);
                choices.Add(w > 0 && h > 0 ? $"屏幕 {i + 1}  ({w}×{h})" : $"屏幕 {i + 1}");
            }

            _dropdownTargetMonitor.choices = choices;
            int savedIndex = _model?.TargetMonitorIndex.Value ?? 0;
            _dropdownTargetMonitor.index = Mathf.Clamp(savedIndex, 0, choices.Count - 1);
        }

        private void SyncSettingsFieldsFromModel()
        {
            if (_model == null)
            {
                return;
            }

            if (_fieldFocusMinutes != null)
            {
                _fieldFocusMinutes.value = _model.FocusDurationSeconds.Value / 60;
            }
            if (_fieldBreakMinutes != null)
            {
                _fieldBreakMinutes.value = _model.BreakDurationSeconds.Value / 60;
            }
            if (_fieldRounds != null)
            {
                _fieldRounds.value = _model.TotalRounds.Value;
            }
            if (_toggleAutoJump != null)
            {
                _toggleAutoJump.SetValueWithoutNotify(_model.AutoJumpToTopOnComplete.Value);
            }
            if (_toggleAnchorTop != null)
            {
                _toggleAnchorTop.SetValueWithoutNotify(_model.IsTopmost.Value);
            }
            if (_dropdownTargetMonitor != null)
            {
                int safeIndex = Mathf.Clamp(
                    _model.TargetMonitorIndex.Value, 0,
                    Mathf.Max(0, _dropdownTargetMonitor.choices.Count - 1));
                _dropdownTargetMonitor.SetValueWithoutNotify(
                    _dropdownTargetMonitor.choices.Count > 0
                        ? _dropdownTargetMonitor.choices[safeIndex]
                        : string.Empty);
            }
        }
    }
}
