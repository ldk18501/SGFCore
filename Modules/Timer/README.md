# Timer 模块使用说明

Timer 模块提供帧驱动定时器，适合倒计时、延迟执行、循环检查等轻量逻辑。

## 单次定时器

```csharp
long timerId = GameApp.Timer.AddTimer(2f, () =>
{
    Debug.Log("2 秒后执行");
});
```

## 循环定时器

```csharp
// 每 1 秒执行一次，执行 5 次
long timerId = GameApp.Timer.AddTimer(1f, Tick, loopCount: 5);

// 无限循环
long loopId = GameApp.Timer.AddTimer(1f, Tick, loopCount: -1);
```

## 控制定时器

```csharp
GameApp.Timer.PauseTimer(timerId);
GameApp.Timer.ResumeTimer(timerId);
float remain = GameApp.Timer.GetRemainingTime(timerId);
GameApp.Timer.CancelTimer(timerId);
GameApp.Timer.CancelAllTimers();
```

## 真实时间

```csharp
GameApp.Timer.AddTimer(10f, AutoSave, isUnscaled: true);
```

`isUnscaled: true` 不受 `Time.timeScale` 影响，适合自动存档、网络超时、暂停菜单倒计时。
