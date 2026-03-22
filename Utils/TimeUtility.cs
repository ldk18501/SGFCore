using System;
using System.Globalization;

namespace GameFramework.Core.Utility
{
    /// <summary>
    /// 全局时间工具类
    /// 涵盖格式化、Unix时间戳转换、安全的跨区域(Culture-Safe)日期解析以及游戏常用日常逻辑
    /// </summary>
    public static class TimeUtility
    {
        // Unix 纪元起点 (UTC)
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // 标准日期格式 (后端/数据库/存档 通用标准)
        public const string StandardDateFormat = "yyyy-MM-dd HH:mm:ss";

        // ==========================================
        // 1. 基础秒数格式化 (常用于 UI 倒计时)
        // ==========================================
        public static string FormatSeconds(float seconds, bool forceHours = false)
        {
            TimeSpan ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (ts.TotalHours >= 1 || forceHours)
            {
                return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            }
            return $"{ts.Minutes:00}:{ts.Seconds:00}";
        }

        // ==========================================
        // 2. Unix 时间戳互相转换 (常用于前后端通信)
        // ==========================================
        
        /// <summary> 获取当前时间的 Unix 时间戳 (秒) </summary>
        public static long CurrentTimestampSeconds() => GetTimestampSeconds(DateTime.UtcNow);

        /// <summary> 获取当前时间的 Unix 时间戳 (毫秒) </summary>
        public static long CurrentTimestampMilliseconds() => GetTimestampMilliseconds(DateTime.UtcNow);

        /// <summary> DateTime 转 Unix 时间戳 (秒) </summary>
        public static long GetTimestampSeconds(DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalSeconds;
        }

        /// <summary> DateTime 转 Unix 时间戳 (毫秒) </summary>
        public static long GetTimestampMilliseconds(DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalMilliseconds;
        }

        /// <summary> Unix 时间戳 (秒) 转本地 DateTime </summary>
        public static DateTime TimestampSecondsToDateTime(long timestamp)
        {
            return UnixEpoch.AddSeconds(timestamp).ToLocalTime();
        }

        /// <summary> Unix 时间戳 (毫秒) 转本地 DateTime </summary>
        public static DateTime TimestampMillisecondsToDateTime(long timestamp)
        {
            return UnixEpoch.AddMilliseconds(timestamp).ToLocalTime();
        }

        // ==========================================
        // 3. Culture-Safe 字符串互相转换 (防区域崩溃)
        // ==========================================

        /// <summary>
        /// 安全格式化为字符串 (忽略设备本地文化，强制标准输出)
        /// </summary>
        public static string FormatDateTime(DateTime dateTime, string format = StandardDateFormat)
        {
            return dateTime.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 安全从字符串解析为 DateTime
        /// </summary>
        public static DateTime ParseDateTime(string dateTimeStr, string format = StandardDateFormat)
        {
            if (DateTime.TryParseExact(dateTimeStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }
            
            // 记录日志，方便排查脏数据
            Log.Warning($"[TimeUtility] 解析日期失败: {dateTimeStr} 格式: {format}");
            return DateTime.MinValue;
        }

        // ==========================================
        // 4. 游戏日常逻辑计算 (签到、刷新)
        // ==========================================

        /// <summary>
        /// 判断两个时间是否在同一天 (可用于每日首充、签到判断)
        /// </summary>
        public static bool IsSameDay(DateTime time1, DateTime time2)
        {
            return time1.Date == time2.Date;
        }

        public static bool IsSameDayFromTimestamps(long timestamp1, long timestamp2)
        {
            return IsSameDay(TimestampSecondsToDateTime(timestamp1), TimestampSecondsToDateTime(timestamp2));
        }

        /// <summary>
        /// 获取下一个每日刷新点的时间 (比如游戏规定每天凌晨 4 点刷新日常)
        /// </summary>
        public static DateTime GetNextDailyResetTime(int resetHour, int resetMinute = 0, int resetSecond = 0)
        {
            DateTime now = DateTime.Now;
            DateTime resetTime = new DateTime(now.Year, now.Month, now.Day, resetHour, resetMinute, resetSecond);

            // 如果今天的时间已经过了刷新点，说明下一次刷新在明天
            if (now >= resetTime)
            {
                resetTime = resetTime.AddDays(1);
            }
            return resetTime;
        }

        /// <summary>
        /// 获取距离下一次日常刷新的剩余秒数 (可直接拿去做 UI 倒计时)
        /// </summary>
        public static float GetSecondsToNextReset(int resetHour, int resetMinute = 0)
        {
            DateTime nextReset = GetNextDailyResetTime(resetHour, resetMinute);
            return (float)(nextReset - DateTime.Now).TotalSeconds;
        }
    }
}