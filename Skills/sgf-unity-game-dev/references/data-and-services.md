# 数据与基础服务

## 目录

- Event
- Save
- FileSystem 与 Crypto
- Pool
- Timer 与 Time
- Http
- Log 与 Utils

## Event

事件必须是轻量 `struct`：

```csharp
public readonly struct GoldChangedEvent
{
    public readonly int CurrentGold;

    public GoldChangedEvent(int currentGold)
    {
        CurrentGold = currentGold;
    }
}
```

非 UI 生命周期成对订阅：

```csharp
private void OnEnable()
{
    GameApp.Event.AddListener<GoldChangedEvent>(OnGoldChanged);
}

private void OnDisable()
{
    GameApp.Event.RemoveListener<GoldChangedEvent>(OnGoldChanged);
}

GameApp.Broadcast(new GoldChangedEvent(currentGold));
```

规则：

- 事件只携带通知所需快照或 ID，不携带庞大可变对象图。
- 不重复注册同一 handler。
- UI 使用 `UIFormBase.Subscribe<T>` 自动解绑。
- 事件用于降低耦合，不用于替代有返回值的同步查询或命令。
- 回调中可继续增删监听，模块使用快照分发；仍要避免事件环和无界级联。
- 用 `GetListenerCount<T>` 做诊断，不把监听数量作为业务逻辑。

## Save

定义根存档：

```csharp
[Serializable]
public sealed class PlayerSaveData : SaveDataBase
{
    public int Gold;
    public InventorySaveNode Inventory = new InventorySaveNode();

    public PlayerSaveData()
    {
        SaveVersion = 2;
        SaveModuleName = "Player";
    }

    public override void OnBindContext()
    {
        Inventory?.BindContext(this);
    }

    public void AddGold(int value)
    {
        Gold += value;
        MarkDirty();
    }
}

[Serializable]
public sealed class InventorySaveNode : SaveDataNode
{
    public List<int> ItemIds = new List<int>();

    public void Add(int itemId)
    {
        ItemIds.Add(itemId);
        MarkDirty();
    }
}
```

`SaveDataNode` 的根上下文是 `[NonSerialized]`，必须在 `OnBindContext` 绑定，否则子节点脏标记会丢失。

加载与保存：

```csharp
GameApp.Save.SetCurrentSlot("Slot_1");
PlayerSaveData save = GameApp.Save.LoadData<PlayerSaveData>("Player");

SaveOperationResult result = GameApp.Save.TrySaveData("Player", save);
if (!result.Success)
{
    Log.Error(result.Error);
}
```

规则：

- 默认 `useEncryption: true`。必须先让 `CryptoModule.IsInitialized` 为 true。
- 不加密的开发/公开数据显式传 `useEncryption: false`，读写两端保持一致。
- 加密缺少密钥时，读档返回新对象但不会隔离或覆盖原文件；业务启动应把缺少密钥视为配置失败，不能误当新用户继续保存。
- 优先使用 `TrySaveData` 检查写盘结果；只有能接受仅记录日志时才用 `SaveData`。
- 数据修改后调用 `MarkDirty`；成功保存才清 dirty，失败保留 dirty。
- 用 `OnBeforeSave` 做保存前规范化，用 `OnAfterLoad` 做反序列化后恢复，不在其中执行网络或资源加载。
- 多槽位使用显式 slot 重载或 `SetCurrentSlot`；模块化数据用 `SaveModuleData/LoadModuleData`，文件名会标准化为 `Module_{Name}.sav`。
- 复杂 UnityEngine.Object、循环引用、多态和字典不适合 `JsonUtility`；存 ID 和基础数据，或在项目侧增加 serializer adapter。
- 当前 `FrameworkConfig` 的 Crypto Key/IV 在所有模块初始化后才注入；自定义模块不要在 `OnInit` 读取加密存档，应在框架 Ready 后由 Preload 显式初始化。
- `LoadData` 在主档和备份都不可恢复时会创建新对象。对付费、奖励、进度等关键存档，使用 `HasSave`、Loaded/Recovered 事件或项目级结果包装区分“首次新档”和“旧档读取失败”，避免随后把新对象覆盖到旧档位置。

涉及奖励、货币或库存的本地事务，优先执行：复制候选状态 → 修改候选 → `TrySaveData` 原子写成功 → 替换内存状态 → 广播事件/刷新红点。写盘失败时不得先发放内存奖励或发布成功事件。

迁移：

```csharp
public sealed class PlayerSaveMigration : ISaveDataMigration<PlayerSaveData>
{
    public int TargetVersion => 2;

    public void Migrate(PlayerSaveData data, int fromVersion)
    {
        if (fromVersion < 2)
        {
            data.Gold = Math.Max(0, data.Gold);
        }
    }
}

GameApp.Save.RegisterMigration(new PlayerSaveMigration());
```

在第一次读档前注册所有迁移。迁移必须幂等、只向前，并保留可恢复数据。

自动存档：设置 `IsAutoSaveEnabled`、`AutoSaveInterval`，或使用 `TrackAutoSave`。关闭 owner 时调用 `StopAutoSave`；不要为同一对象重复追踪。备份与损坏隔离默认开启，恢复会广播 `SaveDataRecoveredEvent`。

## FileSystem 与 Crypto

`GameApp.FileSystem` 的相对路径自动落在 `Application.persistentDataPath`：

```csharp
GameApp.FileSystem.WriteTextAtomic("Cache/state.json", json);
string json = GameApp.FileSystem.ReadText("Cache/state.json");
```

存档必须优先走 Save；Addressables/配置内容不走 FileSystem。需要微信/抖音等平台时，实现对应 `IFileSystemStrategy`，不要让默认分支返回假路径。

Crypto 配置：

- Key 的 UTF-8 字节长度必须为 16、24 或 32。
- 兼容 IV 的 UTF-8 字节长度必须为 16。
- `EncryptAuthenticatedString/DecryptAuthenticatedString` 使用 `SGF2` 随机 IV + HMAC，并兼容读旧固定 IV 格式。
- 普通 `EncryptString/EncryptBytes` 是低层接口；存档优先由 Save 调用认证格式。
- 本地密钥只能提高篡改成本，不等于服务端反作弊。不要记录密钥明文。

## Pool

C# 引用池类型实现 `IReference.Clear()`：

```csharp
DamageInfo info = GameApp.Pool.AllocateClass<DamageInfo>();
try
{
    Use(info);
}
finally
{
    GameApp.Pool.ReleaseClass(info);
}
```

GameObject 池：

```csharp
GameApp.Pool.SetPoolConfig("Bullet", new PoolConfig
{
    MaxCapacity = 200,
    PrewarmCount = 20
});

GameApp.Pool.PrewarmGameObject("Bullet", bulletPrefab, 20);
GameObject bullet = GameApp.Pool.SpawnGameObject("Bullet", bulletPrefab, parent);
GameApp.Pool.RecycleGameObject("Bullet", bullet);
```

池对象可实现 `IPoolable.OnSpawned/OnDespawned` 重置状态。同一 `poolName` 只能绑定同一 Prefab，Spawn/Recycle 名称必须一致。不要把 `ResourceModule.InstantiateAsync` 的 Addressables 实例塞入普通 GameObject 池；如需 Addressables 对象池，必须设计让池持有并最终释放原始 Addressables 句柄的专用 owner。

当前源码不会自动消费 `PoolConfig.PrewarmCount`；需要预热时仍要显式调用 `PrewarmGameObject`。不要只设置字段就假设已经创建实例。

## Timer 与 Time

Timer：

```csharp
long id = GameApp.Timer.AddTimer(
    10f,
    AutoSave,
    isUnscaled: true,
    loopCount: -1);

GameApp.Timer.PauseTimer(id);
GameApp.Timer.ResumeTimer(id);
float remaining = GameApp.Timer.GetRemainingTime(id);
GameApp.Timer.CancelTimer(id);
```

- `loopCount: 1` 单次，`-1` 无限。
- owner 结束时取消 timer；不要依赖回调里目标对象变为 null。
- 暂停界面、网络超时、自动保存使用 unscaled；受游戏速度影响的玩法计时使用 scaled。
- 不在业务关闭时调用 `CancelAllTimers`，以免影响其他 owner。

Time：

```csharp
GameApp.Time.SetServerTimeZone(timeZoneId);
GameApp.Time.SyncServerTimestampSeconds(serverTimestamp);
GameApp.Time.SetDailyResetHour(4);

DateTime now = GameApp.Time.Now;
bool sameGameDay = GameApp.Time.IsSameGameDay(lastTime, now);
TimeSpan offline = GameApp.Time.GetOfflineDuration(lastOnlineUnix, 8 * 3600);
```

Time 只维护服务器时间偏移，不主动联网。后端响应拿到可信时间戳后再同步。跨天监听 `DailyResetPassedEvent`；离线收益倍率和封顶属于业务模块，不写进 TimeModule。

## Http

优先使用统一结果：

```csharp
var options = new HttpRequestOptions
{
    Timeout = 5,
    RetryCount = 2,
    RetryDelay = 0.3f,
    UseExponentialBackoff = true,
    RetryJitter = 0.2f
};
options.Headers["X-Request-Id"] = requestId;

HttpResult<RankResponse> result = await GameApp.Http.GetResultAsync<RankResponse>(
    url,
    options,
    cancellationToken);

if (!result.Success)
{
    HandleHttpError(result.ErrorType, result.StatusCode, result.Error);
    return;
}
```

认证和公共头通过 `SetAuthToken`、`SetDefaultHeader` 集中设置，登出时清空。GET 只重试临时网络错误、超时、408、429 和 5xx；普通 4xx、取消和反序列化失败不重试。POST 默认不重试；只有后端具备幂等键并明确允许时才设 `RetryNonIdempotent = true`。

当前使用 `JsonUtility`，不支持字典、顶层数组、复杂多态。不要让 HttpModule 绑定具体业务协议；业务层定义 request/response envelope。监听 `HttpRequestCompletedEvent` 只做监控和埋点。

## Log 与 Utils

日志级别：

```csharp
Log.Info(message);
Log.Warning(message);
Log.Error(message);
Log.Fatal(message);
Log.Module("Inventory", message);
```

不要记录密钥、token、完整个人数据或存档明文。高频循环避免逐帧日志。文件日志按队列、flush、大小和保留数控制；诊断时查看 `LogModule.PendingCount` 与 `CurrentLogFilePath`。

`GameFramework.Core.Utility` 提供 Transform、RectTransform、Collection、String、Number、Random、Color、Time 扩展。只用于低耦合通用逻辑；不要把业务规则塞进 Utils。随机奖励等权威逻辑不能依赖客户端 `RandomUtility` 防作弊。

源码入口：`Modules/Event`、`Save`、`FileIO`、`Crypto`、`Pool`、`Timer`、`Time`、`Network`、`Debugger`、`Utils`。
