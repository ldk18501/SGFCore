# Guide 示例配置表

这个目录放的是 Guide 模块的示例 Excel 配置，格式遵循 `ConfigExporterEditor` 的四行表头规范：

| 行号 | 内容 |
| --- | --- |
| 第 1 行 | 字段描述 |
| 第 2 行 | 导出标记，`A` 表示客户端导出 |
| 第 3 行 | 字段类型 |
| 第 4 行 | 字段名 |
| 第 5 行起 | 示例数据 |

## 文件说明

| 文件 | 说明 |
| --- | --- |
| `GuideConf.xlsx` | 新手引导步骤主表，包含主城首次引导、每日任务、离线收益、战斗首次胜利等常见引导。 |
| `LanguageConf_CN.xlsx` | 中文引导文案示例。 |
| `LanguageConf_EN.xlsx` | 英文引导文案示例。 |

## GuideConf 示例内容

示例表覆盖了几类常见步骤：

- `Dialog`：进入主城后的欢迎对白。
- `ForceClick`：强制点击主城建造按钮、每日任务按钮、离线收益领取按钮。
- `WaitEvent`：等待 UI 打开或等待任务奖励领取事件。
- `Highlight`：只高亮目标并展示说明。
- `Delay`：短暂停顿后自动推进。
- `Custom`：预留给镜头聚焦、角色动画、特殊表现等项目自定义逻辑。

`type` 字段使用 `enum_GameFramework.Core.GuideStepType`，因为当前导表工具枚举字段按 `int` 写入，所以示例表中填写的是枚举数值：

| 数值 | 类型 |
| --- | --- |
| `0` | `Dialog` |
| `1` | `Highlight` |
| `2` | `ForceClick` |
| `3` | `WaitEvent` |
| `4` | `WaitUI` |
| `5` | `Delay` |
| `6` | `Custom` |

## 使用方式

可以把本目录复制到项目的 Excel 配置目录，或把 `ConfigExportSettings.excelFolder` 临时指向：

```text
Assets/SGFCore/Modules/Guide/Example
```

导出后会生成：

```text
GuideConfConfigGenerated.cs
GuideConf.bytes
LanguageConfConfigGenerated.cs
LanguageTableConf_CN.bytes
LanguageTableConf_EN.bytes
```

加载 `GuideConf` 后，把生成类转换成 `GuideDefinition` 注册到模块：

```csharp
GameApp.Config.RegisterConfig("GuideConf", GuideConf.Load);
await GameApp.Config.TryLoadConfigAsync("GuideConf", "GuideConf");

GameApp.Guide.RegisterDefinitions(GuideConf.List, row => new GuideDefinition
{
    id = row.id,
    groupId = row.groupId,
    order = row.order,
    trigger = row.trigger,
    type = row.type,
    prerequisiteIds = row.prerequisiteIds,
    nextId = row.nextId,
    targetKey = row.targetKey,
    titleKey = row.titleKey,
    textKey = row.textKey,
    title = row.title,
    content = row.content,
    completeEvent = row.completeEvent,
    customKey = row.customKey,
    param = row.param,
    canSkip = row.canSkip,
    blockInput = row.blockInput,
    showContinueButton = row.showContinueButton,
    autoCompleteOnShow = row.autoCompleteOnShow,
    completeOnTargetClick = row.completeOnTargetClick,
    autoCompleteDelay = row.autoCompleteDelay
});
```

触发示例：

```csharp
GameApp.Guide.TryStartByTrigger("EnterMainCity");
GameApp.Guide.NotifyUIOpened("BuildPanel");
GameApp.Guide.NotifyEvent("DailyTaskRewardClaimed");
GameApp.Guide.TryStartByTrigger("BattleFirstWin");
```
