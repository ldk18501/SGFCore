using System;
using GameFramework.Core.Utility;

namespace GameFramework.Core
{
    public readonly struct ServerTimeSyncedEvent
    {
        public readonly DateTime ServerUtcTime;
        public readonly double OffsetSeconds;

        public ServerTimeSyncedEvent(DateTime serverUtcTime, double offsetSeconds)
        {
            ServerUtcTime = serverUtcTime;
            OffsetSeconds = offsetSeconds;
        }
    }

    public readonly struct DailyResetPassedEvent
    {
        public readonly DateTime PreviousTime;
        public readonly DateTime CurrentTime;
        public readonly int ResetHour;

        public DailyResetPassedEvent(DateTime previousTime, DateTime currentTime, int resetHour)
        {
            PreviousTime = previousTime;
            CurrentTime = currentTime;
            ResetHour = resetHour;
        }
    }

    /// <summary>
    /// 游戏时间模块：管理服务器时间偏移、离线收益时间和跨天刷新判断。
    /// </summary>
    public sealed class TimeModule : IFrameworkModule
    {
        public int Priority => 17;

        public bool HasServerTime { get; private set; }
        public double ServerOffsetSeconds { get; private set; }
        public int DailyResetHour { get; private set; } = 0;

        private DateTime _lastNow;

        public DateTime UtcNow => DateTime.UtcNow.AddSeconds(ServerOffsetSeconds);
        public DateTime Now => UtcNow.ToLocalTime();
        public long UnixTimeSeconds => TimeUtility.GetTimestampSeconds(UtcNow);
        public long UnixTimeMilliseconds => TimeUtility.GetTimestampMilliseconds(UtcNow);

        public void OnInit()
        {
            _lastNow = Now;
            Log.Module("Time", "时间模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            DateTime current = Now;
            if (HasDailyResetPassed(_lastNow, current, DailyResetHour))
            {
                GameApp.Event?.Broadcast(new DailyResetPassedEvent(_lastNow, current, DailyResetHour));
            }

            _lastNow = current;
        }

        public void OnDestroy()
        {
        }

        public void SetDailyResetHour(int hour)
        {
            DailyResetHour = Math.Max(0, Math.Min(23, hour));
        }

        public void SyncServerTime(DateTime serverUtcTime)
        {
            DateTime utc = serverUtcTime.Kind == DateTimeKind.Utc
                ? serverUtcTime
                : serverUtcTime.ToUniversalTime();
            ServerOffsetSeconds = (utc - DateTime.UtcNow).TotalSeconds;
            HasServerTime = true;
            _lastNow = Now;
            GameApp.Event?.Broadcast(new ServerTimeSyncedEvent(utc, ServerOffsetSeconds));
        }

        public void SyncServerTimestampSeconds(long timestampSeconds)
        {
            SyncServerTime(TimeUtility.TimestampSecondsToUtcDateTime(timestampSeconds));
        }

        public void ClearServerTime()
        {
            HasServerTime = false;
            ServerOffsetSeconds = 0d;
            _lastNow = Now;
        }

        public TimeSpan GetOfflineDuration(long lastOnlineUnixSeconds, double maxSeconds = -1d)
        {
            long seconds = Math.Max(0, UnixTimeSeconds - lastOnlineUnixSeconds);
            if (maxSeconds > 0d)
            {
                seconds = Math.Min(seconds, (long)maxSeconds);
            }

            return TimeSpan.FromSeconds(seconds);
        }

        public bool IsSameGameDay(DateTime a, DateTime b)
        {
            return GetGameDay(a, DailyResetHour) == GetGameDay(b, DailyResetHour);
        }

        public bool IsSameGameDay(long unixSecondsA, long unixSecondsB)
        {
            return IsSameGameDay(
                TimeUtility.TimestampSecondsToDateTime(unixSecondsA),
                TimeUtility.TimestampSecondsToDateTime(unixSecondsB));
        }

        public DateTime GetNextDailyResetTime()
        {
            return GetNextDailyResetTime(Now, DailyResetHour);
        }

        public float GetSecondsToNextDailyReset()
        {
            return (float)Math.Max(0d, (GetNextDailyResetTime() - Now).TotalSeconds);
        }

        public static bool HasDailyResetPassed(DateTime previousTime, DateTime currentTime, int resetHour = 0)
        {
            return GetGameDay(previousTime, resetHour) != GetGameDay(currentTime, resetHour);
        }

        public static DateTime GetNextDailyResetTime(DateTime now, int resetHour)
        {
            resetHour = Math.Max(0, Math.Min(23, resetHour));
            DateTime todayReset = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0, now.Kind);
            return now >= todayReset ? todayReset.AddDays(1) : todayReset;
        }

        private static DateTime GetGameDay(DateTime time, int resetHour)
        {
            resetHour = Math.Max(0, Math.Min(23, resetHour));
            return time.AddHours(-resetHour).Date;
        }
    }
}
