# Guide 新手引导模块使用说明

Guide 模块是 SGFCore 的配置驱动新手引导系统。它不再使用“最大引导索引”判断进度，而是精确保存每一个步骤 ID 的完成/跳过状态，避免先完成大 ID 后误判小 ID 已完成的问题。进度通过 `SaveModule` 写入统一存档文件，不再依赖 `PlayerPrefs`。

## 设计目标

- 配表维护主流程，引导步骤可以按 `groupId + order` 组成链，也可以用 `nextId` 指定下一步。
- 每个步骤精确记录完成状态：`IsStepCompleted(id)` 只判断这个 ID 本身。
- 业务层只广播事件或调用触发 API，不需要把 UI、玩法系统和引导模块互相写死。
- 表现层通过 `IGuideView` 接入，默认提供一个简单的 `GuideOverlayView`，项目可以替换成更完整的遮罩、挖洞、高亮、箭头和对白表现。
- 支持对白、目标高亮、强制点击、等待事件、等待 UI、延迟、自定义步骤。

## 初始化

`FrameworkEntry` 已默认注册：

```csharp
RegisterModule(new GuideModule());
```

业务层通过 `GameApp.Guide` 访问：

```csharp
GameApp.Guide.SetSaveOptions("Guide", useEncryption: true);
```

如果不调用 `SetSaveOptions`，默认存档名为 `Guide`，并使用 `SaveModule` 的加密参数。

## 配置字段建议

推荐在 Excel 中建立 `GuideConf`，字段可以按下面设计。生成出的配置类再映射成 `GuideDefinition` 注册到模块。

模块目录下提供了可直接参考的示例表：[Example](Example/README.md)。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | `int` | 步骤唯一 ID。 |
| `groupId` | `string` | 引导链 ID，例如 `MainCity`、`DailyTask`。为空时会把当前步骤视作独立组。 |
| `order` | `int` | 组内顺序。未填写 `nextId` 时，完成后自动找同组下一个可开始步骤。 |
| `trigger` | `string` | 触发键，例如 `EnterMainCity`、`UI:MainForm`。 |
| `type` | `enum_GuideStepType` | 步骤类型：`Dialog`、`Highlight`、`ForceClick`、`WaitEvent`、`WaitUI`、`Delay`、`Custom`。 |
| `prerequisiteIds` | `string` | 前置步骤 ID，支持 `1001,1002`。 |
| `nextId` | `int` | 显式下一步 ID，填 0 则按组内顺序推进。 |
| `targetKey` | `string` | 目标 UI/物体 key，对应场景中的 `GuideTarget`。 |
| `titleKey` / `textKey` | `string` | 多语言 key。为空时使用 `title` / `content`。 |
| `title` / `content` | `string` | 兜底文本或临时调试文本。 |
| `completeEvent` | `string` | 等待事件步骤的完成事件。 |
| `canSkip` | `bool` | 是否允许跳过。 |
| `blockInput` | `bool` | 引导展示时是否阻挡输入。 |
| `showContinueButton` | `bool` | 是否显示继续按钮。 |
| `autoCompleteOnShow` | `bool` | 展示后立刻完成，适合纯标记步骤。 |
| `completeOnTargetClick` | `bool` | 目标被点击后完成，适合强制点击。 |
| `autoCompleteDelay` | `float` | 延迟自动完成秒数。 |
| `customKey` / `param` | `string` | 自定义表现或业务参数。 |

注册示例：

```csharp
GameApp.Guide.RegisterDefinitions(new[]
{
    new GuideDefinition
    {
        id = 1001,
        groupId = "MainCity",
        order = 1,
        trigger = "EnterMainCity",
        type = GuideStepType.Dialog,
        textKey = "guide_main_city_1001",
        showContinueButton = true
    },
    new GuideDefinition
    {
        id = 1002,
        groupId = "MainCity",
        order = 2,
        type = GuideStepType.ForceClick,
        targetKey = "MainCity.BuildButton",
        completeOnTargetClick = true,
        showContinueButton = false
    }
});
```

如果使用配置表生成类，可以用转换函数批量注册：

```csharp
GameApp.Guide.RegisterDefinitions(GuideConf.List, row => new GuideDefinition
{
    id = row.id,
    groupId = row.groupId,
    order = row.order,
    trigger = row.trigger,
    type = row.type,
    targetKey = row.targetKey,
    textKey = row.textKey,
    prerequisiteIds = row.prerequisiteIds,
    nextId = row.nextId,
    canSkip = row.canSkip,
    blockInput = row.blockInput,
    showContinueButton = row.showContinueButton,
    autoCompleteDelay = row.autoCompleteDelay,
    completeEvent = row.completeEvent,
    completeOnTargetClick = row.completeOnTargetClick
});
```

## 触发引导

直接触发：

```csharp
GameApp.Guide.TryStartByTrigger("EnterMainCity");
```

事件触发：

```csharp
GameApp.Broadcast(new GuideSignalEvent("EnterMainCity"));
```

UI 打开时可以约定使用 `UI:` 前缀：

```csharp
GameApp.Guide.NotifyUIOpened("MainForm"); // 等价于事件 key: UI:MainForm
```

等待事件步骤完成：

```csharp
GameApp.Guide.NotifyEvent("PlayerClickedBuild");
// 或
GameApp.Broadcast(new GuideSignalEvent("PlayerClickedBuild"));
```

## UI 目标绑定

给需要被引导指向的按钮或节点挂 `GuideTarget`：

```csharp
// Inspector 中填写 targetKey:
// MainCity.BuildButton
```

当配置中的 `targetKey` 与 `GuideTarget.TargetKey` 一致时，`GuideOverlayView` 会自动定位高亮框。如果步骤设置了 `completeOnTargetClick = true`，点击该目标会完成当前步骤。

## 引导表现层

默认表现层是 `GuideOverlayView`，适合快速 Demo 和功能验证。把它挂到引导遮罩 UI 上，然后注册：

```csharp
GuideOverlayView view = guideOverlay.GetComponent<GuideOverlayView>();
GameApp.Guide.SetView(view);
```

正式项目如果需要挖洞遮罩、箭头动画、手指动画、对话角色等，可以自己实现：

```csharp
public sealed class MyGuideView : MonoBehaviour, IGuideView
{
    public bool IsShowing { get; private set; }

    public void Show(GuideViewContext context) {}
    public void RefreshTarget(GuideViewContext context) {}
    public void Hide() {}
}
```

## 进度接口

```csharp
bool done = GameApp.Guide.IsStepCompleted(1001);
bool skipped = GameApp.Guide.IsStepSkipped(1001);
bool finished = GameApp.Guide.IsStepFinished(1001);
bool groupDone = GameApp.Guide.IsGuideCompleted("MainCity");
bool groupFinished = GameApp.Guide.IsGuideFinished("MainCity");
```

`Completed` 和 `Skipped` 分开保存。模块启动判断使用 `Finished`，所以跳过的步骤不会反复弹出；但你仍然可以精确区分“真的完成”和“被跳过”。

调试时可以重置：

```csharp
GameApp.Guide.ResetStep(1001);
GameApp.Guide.ResetGroup("MainCity");
GameApp.Guide.ClearProgress();
```

## 引导事件

模块会广播以下事件：

```csharp
GuideStartedEvent
GuideStepStartedEvent
GuideStepCompletedEvent
GuideCompletedEvent
GuideProgressChangedEvent
```

这些事件适合调试面板、埋点、QA 工具和 UI 刷新。普通玩法逻辑尽量只发 `GuideSignalEvent`，不要反向依赖引导内部状态。

## 配置建议

- 每个功能使用独立 `groupId`，不要把所有引导堆成一条大链。
- `id` 只作为唯一标识，不承担进度大小含义。
- 主线步骤用 `groupId + order`，分支步骤用 `prerequisiteIds` 和不同 `trigger`。
- 强制点击步骤建议设置 `showContinueButton = false`、`completeOnTargetClick = true`。
- `PlayerPrefs` 只适合轻量偏好值，引导进度使用 `SaveModule` 统一保存。
