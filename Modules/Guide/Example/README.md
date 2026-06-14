# Guide 示例配置表

本目录提供 Guide 模块的新配置表样例。示例表遵循 SGFCore 导表工具常用的四行表头：

| 行号 | 内容 |
| --- | --- |
| 第 1 行 | 字段描述 |
| 第 2 行 | 导出标记，`A` 表示客户端导出 |
| 第 3 行 | 字段类型 |
| 第 4 行 | 字段名 |
| 第 5 行起 | 示例数据 |

## GuideConf.xlsx 字段

| 字段 | 说明 |
| --- | --- |
| `id` | 引导步骤唯一 id |
| `groupId` | 引导组 id，同组可按 `order` 串联 |
| `order` | 同组顺序 |
| `nextId` | 显式下一步 id，为 `0` 时按同组 `order` 推进 |
| `prerequisiteIds` | 前置引导 id，多个用 `;` 分隔 |
| `trigger` | 唤醒这条引导的触发器名称 |
| `triggerConditions` | 触发事件参数条件 |
| `startConditions` | 开始前状态条件 |
| `skipConditions` | 满足时自动跳过 |
| `action` | 引导功能动作，多个用 `;` 分隔 |
| `completion` | 当前步骤完成方式 |
| `targetKey` | 目标 `GuideTarget` 的 key |
| `titleKey` / `textKey` | 本地化 key |
| `title` / `content` | 兜底文案 |
| `canSkip` | 是否允许跳过 |
| `blockInput` | 是否阻挡输入 |
| `showContinueButton` | 是否显示继续按钮 |

## 示例内容

示例表包含三类典型配置：

| id | 场景 |
| --- | --- |
| `2001` | RPG 主角升到 2 级后，如果二级菜单未打开且主菜单按钮存在，则显示圆形遮罩并引导点击 |
| `2002` | 点击主菜单后继续引导打开技能按钮 |
| `3001` | 每日任务红点出现后，引导点击每日任务入口 |
| `4001` | 收到离线收益信号后，等待领取按钮存在并引导领取 |
| `5001` | 首胜事件触发后展示一次说明，延迟自动完成 |

这些触发器和项目条件都应由项目侧 `IGuideInstaller` 注册，而不是修改 SGFCore。

```csharp
public sealed class ExampleGuideInstaller : IGuideInstaller
{
    public void Install(IGuideRegistry registry)
    {
        registry.RegisterTrigger(
            "PlayerLevelUp",
            new GuideEventTrigger<PlayerLevelUpEvent>(
                "PlayerLevelUp",
                payloadFactory: e => e,
                parametersFactory: e => new Dictionary<string, string>
                {
                    { "level", e.Level.ToString() }
                }));

        registry.RegisterTrigger(
            "DailyTaskAvailable",
            new GuideEventTrigger<DailyTaskAvailableEvent>("DailyTaskAvailable"));

        registry.RegisterCondition("MenuClosed", (context, parameter) =>
            !GameApp.UI.IsOpen(parameter));

        registry.RegisterAction("ShowBlackCircleMask", (context, parameter) =>
            ExampleGuideMask.ShowCircle(context.ViewContext.Target, parameter));
    }
}
```

启动时只调用一次：

```csharp
GuideStartOptions options = new GuideStartOptions
{
    Definitions = GuideTableAdapter.ToDefinitions(GuideConf.List),
    View = guideOverlayView
};
options.Installers.Add(new ExampleGuideInstaller());

GameApp.Guide.StartGuide(options);
```

之后业务代码只广播业务事件：

```csharp
GameApp.Event.Broadcast(new PlayerLevelUpEvent(2));
GameApp.Event.Broadcast(new DailyTaskAvailableEvent());
GameApp.Event.Broadcast(new GuideSignalEvent("OfflineRewardReady"));
GameApp.Event.Broadcast(new GuideSignalEvent("BattleFirstWin"));
```

`GuideTarget` 的注册和点击会自动转换成 `Target:{key}`、`TargetRegistered`、`TargetClick:{key}`、`TargetClick` 触发，不需要业务手动调用引导模块。
