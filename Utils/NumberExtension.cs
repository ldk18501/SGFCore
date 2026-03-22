using System;
using System.Numerics;

namespace GameFramework.Core.Utility
{
    /// <summary>
    /// 数字格式化扩展
    /// 业务层直接调用：1000000L.ToUnitString() -> "1M"
    /// </summary>
    public static class NumberExtension
    {
        // 经典放置类游戏的单位后缀
        private static readonly string[] SUFFIXES = 
        { 
            "", "K", "M", "B", "T", "aa", "bb", "cc", "dd", "ee", "ff", "gg", 
            "hh", "ii", "jj", "kk", "ll", "mm", "nn", "oo", "pp", "qq", "rr", "ss", "tt", "uu", "vv", "ww", "xx", "yy", "zz" 
        };

        /// <summary>
        /// 将 long 转换为带后缀的短字符串 (最高支持到 10^18，即 E 级别)
        /// </summary>
        public static string ToUnitString(this long value, int decimals = 2)
        {
            if (value < 1000) return value.ToString();

            int suffixIndex = 0;
            double dValue = value;

            while (dValue >= 1000d && suffixIndex < SUFFIXES.Length - 1)
            {
                dValue /= 1000d;
                suffixIndex++;
            }

            // 使用 "0.##" 这种格式糖，自动抹除末尾的 0
            string format = "0." + new string('#', decimals);
            return dValue.ToString(format) + SUFFIXES[suffixIndex];
        }

        /// <summary>
        /// 将 double 转换为带后缀的短字符串 (最高支持 10^308)
        /// </summary>
        public static string ToUnitString(this double value, int decimals = 2)
        {
            if (value < 1000d) return Math.Round(value, decimals).ToString();

            // 利用对数快速计算阶层，极大地优化了 while 循环带来的性能开销
            int suffixIndex = (int)(Math.Log10(value) / 3);
            
            if (suffixIndex >= SUFFIXES.Length)
            {
                // 如果超过了 zz，可以直接输出科学计数法，比如 1.2e100
                return value.ToString("0.##e+0"); 
            }

            double divisor = Math.Pow(10, suffixIndex * 3);
            double shortValue = value / divisor;

            string format = "0." + new string('#', decimals);
            return shortValue.ToString(format) + SUFFIXES[suffixIndex];
        }

        /// <summary>
        /// 将 BigInteger 转换为带后缀的短字符串 (支持无限大)
        /// </summary>
        public static string ToUnitString(this BigInteger value, int decimals = 2)
        {
            if (value < 1000) return value.ToString();

            // 对于大整数，直接转字符串看长度，计算指数是最快且不会溢出的做法
            string numStr = value.ToString();
            int length = numStr.Length;
            int suffixIndex = (length - 1) / 3;

            if (suffixIndex >= SUFFIXES.Length)
            {
                return value.ToString("E2"); // 超出预设后缀，使用科学计数法
            }

            int remainder = length % 3;
            int headLength = remainder == 0 ? 3 : remainder;

            string head = numStr.Substring(0, headLength);
            
            // 如果不需要小数位，或者字符串不够切，直接返回
            if (decimals <= 0 || numStr.Length <= headLength)
            {
                return head + SUFFIXES[suffixIndex];
            }

            // 截取小数部分
            int decimalLength = Math.Min(decimals, numStr.Length - headLength);
            string decimalPart = numStr.Substring(headLength, decimalLength);

            // 去除末尾多余的 0
            decimalPart = decimalPart.TrimEnd('0');

            if (string.IsNullOrEmpty(decimalPart))
            {
                return head + SUFFIXES[suffixIndex];
            }

            return $"{head}.{decimalPart}{SUFFIXES[suffixIndex]}";
        }
    }
}