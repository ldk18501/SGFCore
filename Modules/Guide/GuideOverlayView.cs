using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core
{
    public sealed class GuideOverlayView : MonoBehaviour, IGuideView
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _contentText;
        [SerializeField] private RectTransform _highlightFrame;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _skipButton;
        [SerializeField] private bool _followTarget = true;

        private readonly Vector3[] _worldCorners = new Vector3[4];
        private GuideViewContext _context;

        public bool IsShowing { get; private set; }

        private void Awake()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(OnContinueClicked);
            }

            if (_skipButton != null)
            {
                _skipButton.onClick.AddListener(OnSkipClicked);
            }
        }

        private void OnDestroy()
        {
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(OnContinueClicked);
            }

            if (_skipButton != null)
            {
                _skipButton.onClick.RemoveListener(OnSkipClicked);
            }
        }

        private void Update()
        {
            if (IsShowing && _followTarget)
            {
                RefreshTarget(_context);
            }
        }

        public void Show(GuideViewContext context)
        {
            _context = context;
            IsShowing = true;
            gameObject.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = context?.Definition == null || context.Definition.blockInput;
            }

            if (_titleText != null)
            {
                _titleText.text = context?.ResolvedTitle ?? string.Empty;
                _titleText.gameObject.SetActive(!string.IsNullOrEmpty(_titleText.text));
            }

            if (_contentText != null)
            {
                _contentText.text = context?.ResolvedContent ?? string.Empty;
            }

            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(context != null && context.ShowContinueButton);
            }

            if (_skipButton != null)
            {
                _skipButton.gameObject.SetActive(context != null && context.CanSkip);
            }

            RefreshTarget(context);
        }

        public void RefreshTarget(GuideViewContext context)
        {
            if (_highlightFrame == null)
            {
                return;
            }

            GuideTarget target = context?.Target;
            if (target == null)
            {
                _highlightFrame.gameObject.SetActive(false);
                return;
            }

            RectTransform rootRect = _highlightFrame.parent as RectTransform;
            if (rootRect == null)
            {
                _highlightFrame.gameObject.SetActive(false);
                return;
            }

            Canvas canvas = rootRect.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = canvas.worldCamera;
            }

            RectTransform targetRect = target.RectTransform;
            if (targetRect == null)
            {
                Camera worldCamera = Camera.main;
                if (worldCamera == null || target.TargetTransform == null)
                {
                    _highlightFrame.gameObject.SetActive(false);
                    return;
                }

                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                    worldCamera,
                    target.TargetTransform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootRect,
                    screenPoint,
                    uiCamera,
                    out Vector2 localPoint);

                _highlightFrame.gameObject.SetActive(true);
                _highlightFrame.anchoredPosition = localPoint;
                _highlightFrame.sizeDelta = target.WorldHighlightSize;
                return;
            }

            targetRect.GetWorldCorners(_worldCorners);
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;

            for (int i = 0; i < _worldCorners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, _worldCorners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootRect,
                    screenPoint,
                    uiCamera,
                    out Vector2 localPoint);

                if (i == 0)
                {
                    min = localPoint;
                    max = localPoint;
                }
                else
                {
                    min = Vector2.Min(min, localPoint);
                    max = Vector2.Max(max, localPoint);
                }
            }

            _highlightFrame.gameObject.SetActive(true);
            _highlightFrame.anchoredPosition = (min + max) * 0.5f;
            _highlightFrame.sizeDelta = max - min;
        }

        public void Hide()
        {
            IsShowing = false;
            _context = null;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        private void OnContinueClicked()
        {
            GuideModule module = GameApp.Guide;
            if (module != null)
            {
                module.CompleteCurrentStep();
            }
        }

        private void OnSkipClicked()
        {
            GuideModule module = GameApp.Guide;
            if (module != null)
            {
                module.SkipCurrentStep();
            }
        }
    }
}
