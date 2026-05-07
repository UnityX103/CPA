using System;
using System.IO;
using APP.Pomodoro.Event;
using QFramework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace APP.Pomodoro.View
{
    public sealed class VideoCompletionOverlay : MonoBehaviour, IController
    {
        private VideoPlayer _player;
        private RenderTexture _renderTexture;
        private GameObject _canvasGo;
        private RawImage _rawImage;
        private Material _chromaMaterial;

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return APP.Pomodoro.GameApp.Interface;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject go = new GameObject("[VideoCompletionOverlay]");
            DontDestroyOnLoad(go);
            go.AddComponent<VideoCompletionOverlay>();
        }

        private void Awake()
        {
            this.RegisterEvent<E_RequestPlayCompletionVideo>(OnRequest)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void OnRequest(E_RequestPlayCompletionVideo evt)
        {
            try
            {
                Play(evt.VideoPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VideoCompletionOverlay] 播放失败: {ex.Message}");
                Hide();
            }
        }

        private void Play(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[VideoCompletionOverlay] 无效视频路径: {path}");
                return;
            }

            Hide();

            int width = Mathf.Max(2, Screen.width);
            int height = Mathf.Max(2, Screen.height);
            _renderTexture = new RenderTexture(width, height, 0);

            _canvasGo = new GameObject("VideoCanvas");
            _canvasGo.transform.SetParent(transform, worldPositionStays: false);

            Canvas canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            _canvasGo.AddComponent<GraphicRaycaster>();

            GameObject imageGo = new GameObject("VideoRawImage");
            imageGo.transform.SetParent(_canvasGo.transform, worldPositionStays: false);

            _rawImage = imageGo.AddComponent<RawImage>();
            RectTransform rt = _rawImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _rawImage.texture = _renderTexture;

            Material sourceMaterial = Resources.Load<Material>("PomodoroVideoChromaKey");
            if (sourceMaterial != null)
            {
                // 默认对视频做绿幕扣除，绿色通道远高于红蓝时按 alpha 渐变透明。
                _chromaMaterial = new Material(sourceMaterial);
                _rawImage.material = _chromaMaterial;
            }
            else
            {
                Debug.LogWarning("[VideoCompletionOverlay] 未找到 Resources/PomodoroVideoChromaKey 材质，将使用默认材质播放。");
            }

            Button btn = imageGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Hide);

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.source = VideoSource.Url;
            _player.url = path.StartsWith("file://", StringComparison.Ordinal) ? path : $"file://{path}";
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _renderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.loopPointReached += _ => Hide();
            _player.errorReceived += (_, msg) =>
            {
                Debug.LogWarning($"[VideoCompletionOverlay] VideoPlayer error: {msg}");
                Hide();
            };
            _player.Play();
        }

        private void Hide()
        {
            if (_player != null)
            {
                _player.Stop();
                Destroy(_player);
                _player = null;
            }

            if (_canvasGo != null)
            {
                if (_rawImage != null)
                {
                    _rawImage.material = null;
                    _rawImage.texture = null;
                }

                Destroy(_canvasGo);
                _canvasGo = null;
                _rawImage = null;
            }

            if (_chromaMaterial != null)
            {
                Destroy(_chromaMaterial);
                _chromaMaterial = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        private void OnDestroy()
        {
            Hide();
        }
    }
}
