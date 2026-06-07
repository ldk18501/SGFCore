using UnityEngine;

namespace GameFramework.Core.Utility
{
    public static class RectTransformExtension
    {
        public static void StretchToParent(this RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        public static void SetAnchoredX(this RectTransform rectTransform, float x)
        {
            if (rectTransform == null)
            {
                return;
            }

            Vector2 position = rectTransform.anchoredPosition;
            position.x = x;
            rectTransform.anchoredPosition = position;
        }

        public static void SetAnchoredY(this RectTransform rectTransform, float y)
        {
            if (rectTransform == null)
            {
                return;
            }

            Vector2 position = rectTransform.anchoredPosition;
            position.y = y;
            rectTransform.anchoredPosition = position;
        }

        public static bool ContainsScreenPoint(this RectTransform rectTransform, Vector2 screenPoint, Camera camera = null)
        {
            return rectTransform != null &&
                   RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, camera);
        }
    }
}
