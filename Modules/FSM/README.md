# FSM 模块使用说明

FSM 模块提供泛型有限状态机，适合游戏流程、角色状态、玩法阶段等明确状态切换逻辑。

## 定义状态

```csharp
public class ProcedureLaunch : FsmState<GameDemoEntry>
{
    public override void OnEnter()
    {
        ChangeState<ProcedurePreload>();
    }
}

public class ProcedurePreload : FsmState<GameDemoEntry>
{
    public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
    {
    }
}
```

## 创建和启动

```csharp
var fsm = GameApp.Fsm.CreateFsm(
    "GameProcedure",
    this,
    new ProcedureLaunch(),
    new ProcedurePreload(),
    new ProcedureMainMenu());

fsm.Start<ProcedureLaunch>();
```

## 切换状态

状态内部可以直接调用：

```csharp
ChangeState<ProcedureMainMenu>();
```

外部可以通过 `IFsm<T>`：

```csharp
fsm.ChangeState<ProcedureMainMenu>();
```

## 黑板数据

```csharp
fsm.SetData("LevelId", 3);
int levelId = fsm.GetData<int>("LevelId");
```

跨状态共享的正式字段推荐用强类型 Key：

```csharp
static readonly BlackboardKey<int> LevelIdKey = new BlackboardKey<int>("LevelId");
fsm.SetData(LevelIdKey, 3);
fsm.TryGetData(LevelIdKey, out int levelId);
```

状态切换已做可重入保护：在 `OnEnter`/`OnLeave` 中再次请求切换时会进入串行队列，并对循环跳转设置保护上限。

## 销毁

```csharp
GameApp.Fsm.DestroyFsm("GameProcedure");
```

## 注意事项

- 同一个 FSM 内的状态实例会复用，不要把一次性临时数据留在状态字段里不清理。
- `OnLeave` 里取消计时器、事件和异步操作更安全。
