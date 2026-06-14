# Procedure 模块使用说明

Procedure 模块负责管理游戏主流程，例如启动预热、预加载、主菜单、战斗、结算等。它适合承载“当前游戏处于哪个大阶段”的逻辑，业务层通过 `GameApp.Procedure` 访问。

## 定义流程状态

```csharp
public class ProcedureLaunch : ProcedureBase
{
    public override async UniTask OnEnterAsync(CancellationToken cancellationToken)
    {
        bool ready = await GameApp.Res.EnsureInitializedAsync();
        if (!ready)
        {
            return;
        }

        ChangeProcedure<ProcedurePreload>();
    }
}
```

每个流程状态只关心自己的阶段职责，不直接管理整个游戏启动顺序。

## 启动流程

```csharp
GameApp.Procedure.Start(
    owner: this,
    new ProcedureLaunch(),
    new ProcedurePreload(),
    new ProcedureMainMenu());
```

默认会进入传入列表中的第一个流程。也可以指定起始流程：

```csharp
GameApp.Procedure.Start<ProcedureLaunch>(
    this,
    new ProcedureLaunch(),
    new ProcedurePreload(),
    new ProcedureMainMenu());
```

## 流程切换

在 `ProcedureBase` 子类内部：

```csharp
ChangeProcedure<ProcedureMainMenu>();
```

在外部业务中：

```csharp
GameApp.Procedure.ChangeProcedure<ProcedureMainMenu>();
```

## 黑板数据

流程之间需要共享少量启动期数据时，可以使用模块黑板：

```csharp
SetData("CurrentSave", saveData);
SimulationSaveData save = GetData<SimulationSaveData>("CurrentSave");
```

黑板适合存流程临时数据，不建议放大量长期业务状态。长期数据应放到专门的数据模块或存档模块。

## 流程事件

```csharp
public readonly struct ProcedureChangedEvent
public readonly struct ProcedureStoppedEvent
```

这些事件适合调试面板、Loading 表现、埋点统计监听。普通流程状态之间优先直接调用 `ChangeProcedure<T>()`。

## 推荐流程

常见休闲游戏可以从这几个流程开始：

```text
ProcedureLaunch -> ProcedurePreload -> ProcedureMainMenu -> ProcedureBattle -> ProcedureResult
```

复杂项目可以再增加登录、热更新、资源下载、服务器选择等流程，但不要把普通 UI 弹窗和短期玩法状态都塞进 Procedure。
