# RedPoint 模块使用说明

RedPoint 模块是 SGFCore 的低耦合红点系统。核心思想是：业务只负责改变数据并广播事件，红点条件自己监听事件、计算状态，UI 只绑定 path 显示结果。

## Path 约定

红点使用 `.` 分隔层级：

```text
Main.Task.Daily
Main.Task.Achievement
Main.Shop.FreeGift
```

子节点数量会自动向父节点聚合。`Main.Task.Daily` 变为 1 后，`Main.Task` 和 `Main` 都会自动变为激活。

## 纯显示：RedPointBadge

适合父级入口或只需要显示聚合结果的 UI。

1. 给红点图片或红点容器挂 `RedPointBadge`。
2. Inspector 中填写 `_path`，例如 `Main.Task`。
3. 子节点变化时它会自动刷新。

也可以运行时设置：

```csharp
badge.SetPath("Main.Task");
badge.Refresh();
```

## UI 条件：RedPointConditionBadge

适合和某个 UI 绑定的红点条件。示例：

```csharp
public class RedPointConditionBadge_DailyTask : RedPointConditionBadge
{
    protected override void RegisterTriggers()
    {
        SubscribeTrigger<TaskChangedEvent>();
        SubscribeTrigger<RewardClaimedEvent>();
    }

    protected override bool IsReady()
    {
        return DailyTaskModel.HasRewardToClaim();
    }
}
```

Inspector 中把 path 填为：

```text
Main.Task.Daily
```

当监听到事件后，组件会重新计算自身条件，并写入红点树。

## 纯条件：RedPointConditionProvider

如果子界面没有打开，但父级入口仍然要显示子项红点，推荐使用 Provider。

```csharp
public class DailyTaskRedPointProvider : RedPointConditionProvider
{
    protected override void RegisterTriggers()
    {
        SubscribeTrigger<TaskChangedEvent>();
    }

    protected override int GetRedPointCount()
    {
        return DailyTaskModel.GetClaimableCount();
    }
}
```

Provider 不负责显示 UI，只负责把某个叶子 path 的数量注册进 `RedPointModule`。

## 命令式接口

保留给少数简单场景：

```csharp
GameApp.RedPoint.SetCount("Main.Mail", unreadMailCount, this);
GameApp.RedPoint.ClearCount("Main.Mail", this);
```

批量刷新多个叶子节点时，用批次合并监听器与全局事件通知：

```csharp
using (GameApp.RedPoint.BeginBatch())
{
    GameApp.RedPoint.SetCount("Main.Mail", unreadMailCount, this);
    GameApp.RedPoint.SetCount("Main.Task.Daily", dailyCount, this);
}
```

如果不想业务耦合红点模块，优先使用 `RedPointConditionBadge` 或 `RedPointConditionProvider`。

## 监听变化

```csharp
GameApp.RedPoint.AddListener("Main.Task", OnRedPointChanged);

private void OnRedPointChanged(RedPointSnapshot snapshot)
{
    bool active = snapshot.IsActive;
    int count = snapshot.Count;
}
```

模块也会广播 `RedPointChangedEvent`，需要全局观察时可以监听事件。

## 推荐结构

- 入口按钮：挂 `RedPointBadge`，监听父级 path。
- 子功能按钮：挂 `RedPointBadge` 或 `RedPointConditionBadge`。
- 不依赖 UI 生命周期的红点条件：挂在常驻节点上的 `RedPointConditionProvider`。
