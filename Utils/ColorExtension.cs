using UnityEngine;

namespace GameFramework.Core.Utility
{
    public static class ColorExtension
    {
        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        public static string ToHtmlRGB(this Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        public static string ToHtmlRGBA(this Color color)
        {
            return ColorUtility.ToHtmlStringRGBA(color);
        }

        public static bool TryParseHtmlColor(string html, out Color color)
        {
            return ColorUtility.TryParseHtmlString(html, out color);
        }
    }
}
