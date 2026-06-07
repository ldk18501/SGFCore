using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 轻量 Toast 展示器，支持队列、淡入淡出和 UGUI/TMP 文本。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIToast : MonoBehaviour
    {
        private struct ToastRequest
        {
            public string Message;
            public float Duration;

            public ToastRequest(string message, float duration)
            {
                Message = message;
                Duration = duration;
            }
        }

        public static UIToast Instance { get; private set; }

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _uguiText;
        [SerializeField] private TMP_Text _tmpText;
        [SerializeField] private float _defaultDuration = 1.5f;
        [SerializeField] private float _fadeDuration = 0.15f;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private bool _registerAsInstance = true;

        private readonly Queue<ToastRequest> _queue = new Queue<ToastRequest>();
        private Coroutine _toastCoroutine;

        private void Reset()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _uguiText = GetComponentInChildren<Text>(true);
            _tmpText = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            CacheReferences();
            if (_registerAsInstance)
            {
                Instance = this;
            }

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void ShowGlobal(string message, float duration = -1f)
        {
            if (Instance != null)
            {
                Instance.Show(message, duration);
            }
        }

        public void Show(string message, float duration = -1f)
        {
            _queue.Enqueue(new ToastRequest(message, duration > 0f ? duration : _defaultDuration));
            if (_toastCoroutine == null && isActiveAndEnabled)
            {
                _toastCoroutine = StartCoroutine(ProcessQueue());
            }
        }

        public void ShowImmediately(string message, float duration = -1f)
        {
            _queue.Clear();
            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }

            _queue.Enqueue(new ToastRequest(message, duration > 0f ? duration : _defaultDuration));
            if (isActiveAndEnabled)
            {
                _toastCoroutine = StartCoroutine(ProcessQueue());
            }
        }

        public void Hide()
        {
            _queue.Clear();
            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }

            SetVisible(false);
        }

        private IEnumerator ProcessQueue()
        {
            while (_queue.Count > 0)
            {
                ToastRequest request = _queue.Dequeue();
                SetText(request.Message);
                yield return FadeTo(1f);
                yield return Wait(request.Duration);
                yield return FadeTo(0f);
            }

            _toastCoroutine = null;
            SetVisible(false);
        }

        private IEnumerator FadeTo(float targetAlpha)
        {
            CacheReferences();
            if (_canvasGroup == null)
            {
                yield break;
            }

            _canvasGroup.blocksRaycasts = false;
            float startAlpha = _canvasGroup.alpha;
            if (_fadeDuration <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / _fadeDuration));
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
        }

        private IEnumerator Wait(float seconds)
        {
            float endTime = (_useUnscaledTime ? Time.unscaledTime : Time.time) + Mathf.Max(0f, seconds);
            while ((_useUnscaledTime ? Time.unscaledTime : Time.time) < endTime)
            {
                yield return null;
            }
        }

        private void SetText(string message)
        {
            CacheReferences();
            if (_tmpText != null)
            {
                _tmpText.text = message;
            }

            if (_uguiText != null)
            {
                _uguiText.text = message;
            }
        }

        private void SetVisible(bool visible)
        {
            CacheReferences();
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void CacheReferences()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_uguiText == null)
            {
                _uguiText = GetComponentInChildren<Text>(true);
            }

            if (_tmpText == null)
            {
                _tmpText = GetComponentInChildren<TMP_Text>(true);
            }
        }
    }
}
