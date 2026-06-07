using System;
using System.Text;

namespace GameFramework.Core.Utility
{
    public static class StringExtension
    {
        public static bool IsNullOrWhiteSpaceEx(this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        public static string ToSafeString(this string value, string fallback = "")
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        public static string ToPascalCase(this string value)
        {
            string camel = ToCamelCase(value);
            return string.IsNullOrEmpty(camel) ? camel : char.ToUpperInvariant(camel[0]) + camel.Substring(1);
        }

        public static string ToCamelCase(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            bool upperNext = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c))
                {
                    upperNext = builder.Length > 0;
                    continue;
                }

                if (builder.Length == 0)
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
                }

                upperNext = false;
            }

            return builder.ToString();
        }

        public static bool EqualsIgnoreCase(this string value, string other)
        {
            return string.Equals(value, other, StringComparison.OrdinalIgnoreCase);
        }
    }
}
