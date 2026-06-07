using UnityEngine;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// UI 绑定并跟随 3D 静态坐标 (Vector3)。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIBindPos : MonoBehaviour
    {
        [Header("绑定坐标")]
        public Vector3 WorldPos;

        [Header("摄像机配置")]
        public Camera WorldCamera;

        [Header("显示控制")]
        public bool HideWhenOutsideViewport = true;
        public bool ClampToParentRect;
        public Vector2 ParentPadding = new Vector2(16f, 16f);
        public bool InteractableWhenVisible;
        public bool BlocksRaycastsWhenHidden;

        private RectTransform _rect;
        private RectTransform _parentRect;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _parentRect = _rect.parent as RectTransform;
            _canvasGroup = GetComponent<CanvasGroup>();

            if (WorldCamera == null)
            {
                WorldCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (WorldCamera == null || _parentRect == null)
            {
                UIWorldBindingUtility.SetVisible(_canvasGroup, false, InteractableWhenVisible, BlocksRaycastsWhenHidden);
                return;
            }

            bool canConvert = UIWorldBindingUtility.TryGetLocalPosition(
                _parentRect,
                WorldPos,
                WorldCamera,
                out Vector2 uiLocalPos,
                out bool insideViewport);

            bool visible = canConvert && (insideViewport || !HideWhenOutsideViewport || ClampToParentRect);
            if (visible)
            {
                _rect.anchoredPosition = ClampToParentRect
                    ? UIWorldBindingUtility.ClampToRect(_parentRect, uiLocalPos, ParentPadding)
                    : uiLocalPos;
            }

            UIWorldBindingUtility.SetVisible(_canvasGroup, visible, InteractableWhenVisible, BlocksRaycastsWhenHidden);
        }

        public void Bind(Vector3 fixedWorldPos)
        {
            WorldPos = fixedWorldPos;
        }
    }
}
