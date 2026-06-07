using GameFramework.Core.Utility;
using UnityEngine;

namespace GameFramework.Core.UI
{
    internal static class UIWorldBindingUtility
    {
        public static bool TryGetLocalPosition(
            RectTransform parentRect,
            Vector3 worldPosition,
            Camera worldCamera,
            out Vector2 localPosition,
            out bool insideViewport)
        {
            localPosition = Vector2.zero;
            insideViewport = false;

            if (parentRect == null || worldCamera == null)
            {
                return false;
            }

            Vector3 viewportPosition = worldCamera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z < 0f)
            {
                return false;
            }

            insideViewport = viewportPosition.x >= 0f
                && viewportPosition.x <= 1f
                && viewportPosition.y >= 0f
                && viewportPosition.y <= 1f;

            Camera uiCamera = GetUICamera(parentRect);
            return parentRect.WorldToUIPosition(worldPosition, worldCamera, uiCamera, out localPosition);
        }

        public static Vector2 ClampToRect(RectTransform parentRect, Vector2 localPosition, Vector2 padding)
        {
            if (parentRect == null)
            {
                return localPosition;
            }

            Rect rect = parentRect.rect;
            float minX = rect.xMin + padding.x;
            float maxX = rect.xMax - padding.x;
            float minY = rect.yMin + padding.y;
            float maxY = rect.yMax - padding.y;

            if (minX > maxX)
            {
                float centerX = rect.center.x;
                minX = centerX;
                maxX = centerX;
            }

            if (minY > maxY)
            {
                float centerY = rect.center.y;
                minY = centerY;
                maxY = centerY;
            }

            return new Vector2(
                Mathf.Clamp(localPosition.x, minX, maxX),
                Mathf.Clamp(localPosition.y, minY, maxY));
        }

        public static void SetVisible(CanvasGroup canvasGroup, bool visible, bool interactableWhenVisible, bool blocksRaycastsWhenHidden)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible && interactableWhenVisible;
            canvasGroup.blocksRaycasts = visible || blocksRaycastsWhenHidden;
        }

        private static Camera GetUICamera(RectTransform parentRect)
        {
            Canvas canvas = parentRect.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return null;
                }

                if (canvas.worldCamera != null)
                {
                    return canvas.worldCamera;
                }
            }

            UIRoot root = Object.FindObjectOfType<UIRoot>();
            if (root == null || root.RootCanvas == null || root.RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return root.UICamera;
        }
    }
}
