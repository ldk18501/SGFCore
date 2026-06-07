using UnityEngine;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// UI 绑定并跟随 3D 动态物体 (Transform)。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIBindTrs : MonoBehaviour
    {
        [Header("绑定目标")]
        public Transform Target;
        public Vector3 WorldOffset = new Vector3(0, 2f, 0);

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
            if (Target == null || WorldCamera == null || _parentRect == null)
            {
                UIWorldBindingUtility.SetVisible(_canvasGroup, false, InteractableWhenVisible, BlocksRaycastsWhenHidden);
                return;
            }

            Vector3 targetWorldPos = Target.position + WorldOffset;
            bool canConvert = UIWorldBindingUtility.TryGetLocalPosition(
                _parentRect,
                targetWorldPos,
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

        /// <summary>提供给代码动态绑定的接口。</summary>
        public void Bind(Transform target, Vector3 worldOffset)
        {
            Target = target;
            WorldOffset = worldOffset;
        }
    }
}
