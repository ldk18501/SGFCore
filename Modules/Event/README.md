# Event 模块使用说明

Event 模块是基于事件类型的发布/订阅系统。事件类型必须是 `struct`，这样可以减少 GC，并且比字符串事件更安全。

## 定义事件

```csharp
public struct GoldChangedEvent
{
    public int CurrentGold;
}
```

## 监听和广播

```csharp
private void OnEnable()
{
    GameApp.Event.AddListener<GoldChangedEvent>(OnGoldChanged);
}

private void OnDisable()
{
    GameApp.Event.RemoveListener<GoldChangedEvent>(OnGoldChanged);
}

private void OnGoldChanged(GoldChangedEvent evt)
{
    Debug.Log(evt.CurrentGold);
}

GameApp.Event.Broadcast(new GoldChangedEvent { CurrentGold = 100 });
// 或者：
GameApp.Broadcast(new GoldChangedEvent { CurrentGold = 100 });
```

## UI 内推荐写法

在 `UIFormBase` 派生界面中，优先使用 `Subscribe<T>()`：

```csharp
protected override void OnOpen(params object[] args)
{
    Subscribe<GoldChangedEvent>(OnGoldChanged);
}
```

界面关闭时会自动解除订阅。

## 注意事项

- 不要重复注册同一个回调，否则会收到多次事件。
- 事件结构体只放必要数据，复杂查询让监听方自己从业务数据源读取。
