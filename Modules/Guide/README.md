# Guide 引导模块

Guide 是 SGFCore 的配置驱动引导系统。正式项目接入后，业务层只需要在启动阶段调用一次 `StartGuide`；后续引导什么时候触发、是否满足条件、执行什么表现、如何完成，都由配置表和项目侧注册的 Trigger / Condition / Action 决定。

这个模块不再兼容旧的手动触发式 API。旧 GuideModule 的设计问题是业务代码需要到处调用引导方法，新设计改为：业务代码只广播自己的业务事件，Guide 通过安装器监听这些事件并按配置自动判断。

## 启动方式

```csharp
GuideStartOptions options = new GuideStartOptions
{
    Definitions = GuideTableAdapter.ToDefinitions(GuideConf.List),
    View = guideOverlayView,
    SaveName = "Guide",
    ValidateOnStart = true
};

options.Installers.Add(new RpgGuideInstaller());
options.Installers.Add(new UiGuideInstaller());

GameApp.Guide.StartGuide(options);
```

项目代码不需要修改 SGFCore。正式项目把自己的扩展写在项目工程中，例如：

```csharp
public sealed class RpgGuideInstaller : IGuideInstaller
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

        registry.RegisterCondition("MenuClosed", (context, parameter) =>
            !GameApp.UI.IsOpen(parameter));

        registry.RegisterAction("ShowBlackCircleMask", (context, parameter) =>
            MyGuideMask.ShowCircle(context.ViewContext.Target, parameter));
    }
}
```

业务层仍然只做自己的事：

```csharp
GameApp.Event.Broadcast(new PlayerLevelUpEvent(2));
```

Guide 会因为 `RpgGuideInstaller` 注册了 `PlayerLevelUp` 触发器而自动收到事件，然后匹配配置表。

## 配置模型

每一条引导配置由四类信息组成：

| 字段 | 作用 | 示例 |
| --- | --- | --- |
| `id` | 引导步骤唯一 id | `2001` |
| `groupId` | 一组连续引导的分组 | `RpgMain` |
| `order` | 同组排序 | `1` |
| `nextId` | 显式下一步，为空或 `0` 时按同组 `order` 推进 | `2002` |
| `prerequisiteIds` | 前置引导 id，自动转成 `StepFinished(id)` 条件 | `1001;1002` |
| `trigger` | 唤醒这条引导的触发器 | `PlayerLevelUp` |
| `triggerConditions` | 对触发事件本身的判断 | `TriggerParam(level>=2)` |
| `startConditions` | 开始前必须满足的外部状态 | `MenuClosed(SecondMenu);TargetExists(Main.MenuButton)` |
| `skipConditions` | 满足时自动跳过这条引导 | `GuideFinished(RpgMain)` |
| `action` | 引导开始后执行的功能 | `ShowBlackCircleMask(radius=96)` |
| `completion` | 当前步骤如何完成 | `TargetClick(Main.MenuButton)` |
| `targetKey` | 引导目标对象 key | `Main.MenuButton` |
| `titleKey/textKey` | 本地化 key | `guide_open_menu_title` |
| `title/content` | 无本地化时的兜底文本 | `打开主菜单` |
| `canSkip` | 是否允许表现层手动跳过 | `true` |
| `blockInput` | 表现层是否阻挡输入 | `true` |
| `showContinueButton` | 是否显示继续按钮 | `false` |

`triggerConditions`、`startConditions`、`skipConditions`、`action` 使用统一表达式：

```text
Name(param)
Name:param
NameA(foo);NameB(bar)
```

## 内置能力

内置条件：

| 条件 | 说明 |
| --- | --- |
| `Always` / `Never` | 永远通过或永远不通过 |
| `StepFinished(1001)` | 某步骤已完成或已跳过 |
| `StepCompleted(1001)` | 某步骤真实完成 |
| `StepSkipped(1001)` | 某步骤被跳过 |
| `StepNotFinished(1001)` | 某步骤还未完成也未跳过 |
| `GuideFinished(RpgMain)` | 某组引导全部结束 |
| `TargetExists(Main.MenuButton)` | 当前场景存在对应 `GuideTarget` |
| `TriggerParam(level>=2)` | 检查触发参数或事件 payload |

内置动作：

| 动作 | 说明 |
| --- | --- |
| `Overlay` | 调用当前 `IGuideView` 显示 |
| `Dialog` | 调用当前 `IGuideView` 显示 |
| `Highlight` | 调用当前 `IGuideView` 显示 |
| `ForceClick` | 调用当前 `IGuideView` 显示 |
| `OverlayCircle` | 调用当前 `IGuideView` 显示 |

这些内置动作只是通用入口。大型项目应在项目侧注册更完整的表现动作，例如挖洞遮罩、圆形遮罩、箭头、手指动画、镜头锁定、剧情暂停等。

完成方式：

| completion | 说明 |
| --- | --- |
| `Manual` | 由表现层按钮或项目代码完成 |
| `TargetClick(key)` | 点击指定 `GuideTarget` 完成 |
| `Event(key)` | 收到同名触发或 `GuideSignalEvent(key)` 完成 |
| `Delay(seconds)` | 延迟后完成 |
| `Auto` / `Immediate` | 动作执行后立即完成 |

## 目标对象

需要被引导指向的 UI 或世界对象挂 `GuideTarget`，填写稳定的 `targetKey`。对象启用时会自动注册，注册会触发：

```text
Target:{targetKey}
TargetRegistered
```

对象被点击时会触发：

```text
TargetClick:{targetKey}
TargetClick
```

因此配置表可以写：

```text
trigger = Target:Main.MenuButton
completion = TargetClick(Main.MenuButton)
```

## 表现层

表现层使用 `IGuideView`：

```csharp
public interface IGuideView
{
    bool IsShowing { get; }
    void Show(GuideViewContext context);
    void RefreshTarget(GuideViewContext context);
    void Hide();
}
```

`GuideOverlayView` 只是基础示例。正式项目可以完全替换表现层，也可以只把复杂表现做成自定义 Action。

## 校验

`StartGuide` 默认会校验配置是否引用了未注册的条件、动作、未知完成方式、缺失前置 id、缺失 nextId 等问题：

```csharp
List<string> errors = GameApp.Guide.ValidateDefinitions();
```

建议在 Editor 导表流程中也调用同样的校验逻辑，尽早发现配置错误。
