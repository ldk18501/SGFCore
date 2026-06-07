# Time 模块使用说明

Time 模块用于放置、休闲项目里的统一时间判断，业务层通过 `GameApp.Time` 访问。

## 服务器时间

```csharp
GameApp.Time.SyncServerTimestampSeconds(serverTimestamp);
DateTime now = GameApp.Time.Now;
long unix = GameApp.Time.UnixTimeSeconds;
```

模块不会持续请求服务器，只保存一次同步得到的本地偏移量。后续网络层拿到服务端时间戳时继续调用同步即可。

## 跨天刷新

```csharp
GameApp.Time.SetDailyResetHour(4);

bool sameDay = GameApp.Time.IsSameGameDay(lastTime, GameApp.Time.Now);
float seconds = GameApp.Time.GetSecondsToNextDailyReset();
```

模块每帧轻量检查一次游戏日变化，并广播：

```csharp
DailyResetPassedEvent
```

## 离线收益

```csharp
TimeSpan offline = GameApp.Time.GetOfflineDuration(lastOnlineUnixSeconds, maxSeconds: 8 * 3600);
double rewardSeconds = offline.TotalSeconds;
```

建议存档里保存上次在线的 Unix 秒，登录后用 TimeModule 计算离线时长，再由玩法模块决定奖励倍率和上限。
