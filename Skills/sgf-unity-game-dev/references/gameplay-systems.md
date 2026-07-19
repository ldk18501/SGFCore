# 玩法系统

## 目录

- Procedure 与 FSM
- 强类型黑板
- RedPoint
- Guide
- BehaviorTree
- 系统协作

## Procedure 与 FSM

| 维度 | Procedure | FSM |
| --- | --- | --- |
| 范围 | 游戏全局大阶段 | 角色、玩法、局部系统状态 |
| 入口 | `GameApp.Procedure` | `GameApp.Fsm` |
| 异步进入 | 支持 `OnEnterAsync(token)` | 同步生命周期 |
| 更新 | 过渡时暂停当前 Update | 每帧驱动所有 FSM |
| 典型状态 | Launch/Preload/MainMenu/Battle/Result | Idle/Move/Attack/Stun |

Procedure：

```csharp
public sealed class ProcedurePreload : ProcedureBase
{
    public override async UniTask OnEnterAsync(CancellationToken token)
    {
        GameConfigs.RegisterAll();
        var configs = new Dictionary<string, string>
        {
            { GameConfigs.Item, GameConfigs.Item },
            { GameConfigs.Level, GameConfigs.Level }
        };
        ConfigBatchLoadResult result =
            await GameApp.Config.TryLoadConfigsBatchAsync(configs, token);

        token.ThrowIfCancellationRequested();
        if (!result.Success)
        {
            Log.Error("预加载配置失败。");
            return;
        }

        ChangeProcedure<ProcedureMainMenu>();
    }

    public override void OnLeave()
    {
        // 取消本流程计时器、事件、音频句柄和局部资源。
    }
}
```

模块会串行处理切换；新切换会取消旧 `OnEnterAsync`。不要吞掉 token，不要在取消后继续切换流程。

FSM：

```csharp
IFsm<Player> fsm = GameApp.Fsm.CreateFsm(
    "Player.Main",
    player,
    new PlayerIdleState(),
    new PlayerMoveState(),
    new PlayerAttackState());

fsm.Start<PlayerIdleState>();
```

状态实例会复用。`OnLeave` 清理事件、timer、临时引用；不要让一次进入的数据残留。状态内部重入切换会串行排队并有循环保护，但仍应避免 A/B 无条件互跳。

owner 销毁时调用 `GameApp.Fsm.DestroyFsm(name)`。FSM 名称全局唯一并集中定义。

## 强类型黑板

长期键使用：

```csharp
public static class GameplayKeys
{
    public static readonly BlackboardKey<int> LevelId = new BlackboardKey<int>("LevelId");
    public static readonly BlackboardKey<PlayerSaveData> PlayerSave =
        new BlackboardKey<PlayerSaveData>("PlayerSave");
}
```

Procedure/FSM 都支持 `SetData` 和 `TryGetData`。黑板只存少量流程临时上下文；长期可变状态放业务 Model/Module，持久状态放 Save。不要把整个场景对象图或需要释放的 ResourceScope 随意放黑板。

## RedPoint

Path 使用 `.` 表示层级：

```text
Main.Task.Daily
Main.Task.Achievement
Main.Shop.FreeGift
```

子节点会向父节点聚合。

优先级：

1. 不依赖 UI 生命周期的叶子条件：`RedPointConditionProvider`。
2. 仅在 UI 打开时存在的条件：`RedPointConditionBadge`。
3. 只显示聚合结果：`RedPointBadge`。
4. 极简单命令式场景：`SetCount/ClearCount`。

Provider 示例：

```csharp
public sealed class DailyTaskRedPointProvider : RedPointConditionProvider
{
    protected override void RegisterTriggers()
    {
        SubscribeTrigger<TaskChangedEvent>();
        SubscribeTrigger<RewardClaimedEvent>();
    }

    protected override int GetRedPointCount()
    {
        return DailyTaskModel.GetClaimableCount();
    }
}
```

批量更新：

```csharp
using (GameApp.RedPoint.BeginBatch())
{
    GameApp.RedPoint.SetCount("Main.Mail", unreadMail, owner);
    GameApp.RedPoint.SetCount("Main.Task.Daily", dailyCount, owner);
}
```

规则：

- 稳定 path 集中定义；不同业务不能复用同一叶子 path 表示不同含义。
- 多来源写同一 path 时必须传稳定 owner；销毁时 `ClearOwner(owner)`。
- UI 只展示快照，不主动扫描业务数据。
- 父入口只监听父 path，不重复计算子条件。
- 全局观察可监听 `RedPointChangedEvent`；具体 UI 优先 `AddListener(path, ...)` 或 Badge。

## Guide

Guide 是配置驱动系统，不使用旧式“业务到处手动调用引导 UI”的模式。

接入流程：

1. 用配置表定义 `GuideDefinition` 字段。
2. 在项目侧实现配置行到 `GuideDefinition` 的 adapter。
3. 实现一个或多个 `IGuideInstaller`，注册项目 Trigger/Condition/Action。
4. 为 UI/世界目标挂 `GuideTarget` 并填写稳定 targetKey。
5. 提供 `IGuideView` 实现。
6. 启动时调用一次 `StartGuide` 并校验。

```csharp
var options = new GuideStartOptions
{
    Definitions = GuideTableAdapter.ToDefinitions(GuideConf.List),
    View = guideView,
    SaveName = "Guide",
    UseEncryption = true,
    ValidateOnStart = true,
    AutoEvaluateOnStart = true
};
options.Installers.Add(new MainGuideInstaller());

GameApp.Guide.StartGuide(options);
```

加密进度要求 Crypto 已配置；开发项目若明确不加密，显式 `UseEncryption = false`。

Installer：

```csharp
public sealed class MainGuideInstaller : IGuideInstaller
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

        registry.RegisterCondition("MenuClosed", (context, formName) =>
            !ProjectUIRegistry.IsOpen(formName));

        registry.RegisterAction("ShowCircleMask", (context, parameter) =>
            MyGuideMask.ShowCircle(context.ViewContext.Target, parameter));
    }
}
```

业务只广播自己的结构体事件，Guide trigger 负责桥接。

配置字段：`id/groupId/order/nextId/prerequisiteIds/trigger/triggerConditions/startConditions/skipConditions/action/completion/targetKey/titleKey/textKey/title/content/canSkip/blockInput/showContinueButton`。

表达式支持：

```text
Name(param)
Name:param
NameA(foo);NameB(bar)
```

内置条件包括 Always、Never、StepFinished/Completed/Skipped、GuideFinished、TargetExists、TriggerParam。完成方式包括 Manual、TargetClick、Event、Delay、Auto/Immediate。

启动前或导表后调用 `ValidateDefinitions`。把未注册条件/动作、未知 completion、缺失 prerequisite/nextId 当成内容错误，不在运行时忽略。

自定义 `IGuideView` 只负责展示/刷新目标/隐藏；调度、进度和条件仍由 GuideModule 管理。切场景或登出时 `StopGuide`，需要重置时使用 `ResetStep/ResetGroup/ClearProgress`，这些操作属于明确的产品行为，不能在普通关闭 UI 时误调。

## BehaviorTree

```csharp
BehaviorTree tree = await GameApp.BT.AttachTreeAsync(
    enemy.gameObject,
    "AI/EnemyMelee",
    autoStart: true);

// enemy 销毁前
GameApp.BT.DetachTree(tree);
```

外部树必须是 Addressables `ExternalBehaviorTree`，并依赖 Behavior Designer。模块会加载资源、添加 BehaviorTree 组件并追踪句柄。owner 销毁前主动 Detach；不要直接 Destroy 组件或自行释放 ExternalBehaviorTree。

暂停游戏 AI 可用 `PauseAllAI/ResumeAllAI`，但局部暂停优先控制具体树，避免影响后台或 UI 展示角色。

## 系统协作

推荐数据流：

```text
业务 Model 改变
  -> MarkDirty / Save
  -> Broadcast struct event
  -> UI 刷新
  -> RedPoint Provider 重算
  -> Guide Trigger 匹配
```

避免 UI 直接修改红点树、Guide 直接修改玩法数据、Procedure 承担具体角色状态、FSM 负责全局场景切换。跨系统协作用事件通知，权威状态仍保留在单一业务 owner。

源码入口：`Modules/Procedure`、`FSM`、`RedPoint`、`Guide`、`BehaviorTree`。
