using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 通用 Loading 遮罩，支持引用计数、提示文案和进度条。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class UILoadingOverlay : MonoBehaviour
    {
        public static UILoadingOverlay Instance { get; private set; }

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _uguiMessageText;
        [SerializeField] private TMP_Text _tmpMessageText;
        [SerializeField] private Image _progressFill;
        [SerializeField] private bool _registerAsInstance = true;
        [SerializeField] private bool _useReferenceCount = true;
        [SerializeField] private float _fadeDuration = 0.1f;
        [SerializeField] private bool _useUnscaledTime = true;

        private Coroutine _fadeCoroutine;
        private int _showCount;

        public int ShowCount => _showCount;
        public bool IsShowing => _showCount > 0;

        private void Reset()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _root = gameObject;
            _uguiMessageText = GetComponentInChildren<Text>(true);
            _tmpMessageText = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            CacheReferences();
            if (_registerAsInstance)
            {
                Instance = this;
            }

            ForceHide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void ShowGlobal(string message = "")
        {
            Instance?.Show(message);
        }

        public static void HideGlobal()
        {
            Instance?.Hide();
        }

        public void Show(string message = "", bool additive = true)
        {
            CacheReferences();
            _showCount = _useReferenceCount && additive ? _showCount + 1 : 1;
            SetMessage(message);
            SetRootActive(true);
            FadeTo(1f);
        }

        public void Hide(bool additive = true)
        {
            if (_useReferenceCount && additive)
            {
                _showCount = Mathf.Max(0, _showCount - 1);
            }
            else
            {
                _showCount = 0;
            }

            if (_showCount <= 0)
            {
                FadeTo(0f, true);
            }
        }

        public void ForceHide()
        {
            _showCount = 0;
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            SetRootActive(false);
        }

        public void SetMessage(string message)
        {
            CacheReferences();
            if (_tmpMessageText != null)
            {
                _tmpMessageText.text = message;
            }

            if (_uguiMessageText != null)
            {
                _uguiMessageText.text = message;
            }
        }

        public void SetProgress(float normalizedProgress)
        {
            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01(normalizedProgress);
            }
        }

        private void FadeTo(float alpha, bool deactivateWhenDone = false)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeRoutine(alpha, deactivateWhenDone));
        }

        private IEnumerator FadeRoutine(float targetAlpha, bool deactivateWhenDone)
        {
            CacheReferences();
            if (_canvasGroup == null)
            {
                yield break;
            }

            _canvasGroup.interactable = targetAlpha > 0f;
            _canvasGroup.blocksRaycasts = targetAlpha > 0f;

            float startAlpha = _canvasGroup.alpha;
            if (_fadeDuration <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < _fadeDuration)
                {
                    elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / _fadeDuration));
                    yield return null;
                }

                _canvasGroup.alpha = targetAlpha;
            }

            _canvasGroup.interactable = targetAlpha > 0f;
            _canvasGroup.blocksRaycasts = targetAlpha > 0f;

            if (deactivateWhenDone)
            {
                SetRootActive(false);
            }

            _fadeCoroutine = null;
        }

        private void SetRootActive(bool active)
        {
            GameObject root = _root != null ? _root : gameObject;
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        private void CacheReferences()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_root == null)
            {
                _root = gameObject;
            }

            if (_uguiMessageText == null)
            {
                _uguiMessageText = GetComponentInChildren<Text>(true);
            }

            if (_tmpMessageText == null)
            {
                _tmpMessageText = GetComponentInChildren<TMP_Text>(true);
            }
        }
    }
}
