# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述 / Project Overview

**SGFCore** 是一个轻量级、模块化的 Unity 游戏开发框架，提供完整的游戏开发基础设施。

- **语言**: C# (Unity)
- **命名空间**: `GameFramework.Core`
- **架构模式**: 模块化框架 + 静态门面模式 + 优先级驱动的生命周期管理
- **核心特性**: 零 GC 设计、对象池化、异步资源加载、加密存档、状态机、事件系统

---

## 核心架构 / Core Architecture

### 三层架构系统

#### 1. FrameworkEntry (框架入口)
**位置**: `Base/FrameworkEntry.cs`

- 继承自 `TMonoSingleton<FrameworkEntry>`，是框架的唯一入口点
- 负责所有模块的注册、初始化、更新循环和销毁
- 在 `Update()` 中统一驱动所有模块的 `OnUpdate()`
- 维护两个核心数据结构：
  - `List<IFrameworkModule> _modules` - 按优先级排序的模块列表
  - `Dictionary<Type, IFrameworkModule> _moduleDict` - 快速查找字典

**关键方法**:
```csharp
void RegisterModule(IFrameworkModule module)  // 注册并立即初始化模块
T GetModule<T>() where T : IFrameworkModule   // 获取指定类型的模块
void InitFrameworkModules()                   // 按依赖顺序初始化所有模块
```

#### 2. IFrameworkModule (模块接口)
**位置**: `Base/IFrameworkModule.cs`

所有模块必须实现此接口：

```csharp
public interface IFrameworkModule
{
    int Priority { get; }  // 优先级：数值越小，初始化和更新越早
    void OnInit();         // 模块初始化（注册时立即调用）
    void OnUpdate(float deltaTime, float unscaledDeltaTime);  // 每帧更新
    void OnDestroy();      // 模块销毁清理
}
```

**优先级规则**:
- 0-10: 核心基础设施（日志、事件）
- 11-30: 底层服务（文件系统、加密、存档）
- 31-60: 资源和表现层（资源、UI、音频）
- 61+: 游戏逻辑层（状态机、行为树）

#### 3. GameApp (静态门面)
**位置**: `Base/GameApp.cs`

提供极简的模块访问接口，使用 `??=` 实现懒加载缓存：

```csharp
public static class GameApp
{
    private static EventModule _event;
    public static EventModule Event => _event ??= FrameworkEntry.Instance.GetModule<EventModule>();

    // ... 其他模块属性

    // 便捷方法
    public static void Broadcast<T>(T eventData) where T : struct
    {
        Event.Broadcast(eventData);
    }
}
```

**为什么使用 GameApp？**
- ✅ 简洁：`GameApp.Event.Broadcast(evt)` vs `FrameworkEntry.Instance.GetModule<EventModule>().Broadcast(evt)`
- ✅ 高效：首次访问后缓存引用，避免重复查字典
- ✅ 类型安全：编译时检查，IDE 自动补全

---

## 模块初始化顺序 / Module Initialization Order

**⚠️ 极其重要**: 模块之间存在严格的依赖关系，初始化顺序不能随意更改！

在 `FrameworkEntry.InitFrameworkModules()` 中的标准顺序：

```csharp
// 1. 日志模块 - 必须最先启动，确保后续错误能被记录
RegisterModule(new LogModule());  // Priority: 0

// 2. 事件模块 - 全局通讯血管，其他模块可能在初始化时就需要发送事件
RegisterModule(new EventModule());  // Priority: 10

// 3. 文件系统 - 提供跨平台文件读写能力
RegisterModule(new FileSystemModule());  // Priority: 5

// 4. 加密模块 - 必须在存档模块之前初始化
var crypto = new CryptoModule();
RegisterModule(crypto);  // Priority: 20
crypto.SetCryptoKey("YourSecretKey", "YourSecretIV16!");  // ⚠️ 立即设置密钥

// 5. 存档模块 - 依赖 FileSystem + Crypto
RegisterModule(new SaveModule());  // Priority: 30

// 6. 对象池和定时器 - 基础工具模块
RegisterModule(new PoolModule());   // Priority: 15
RegisterModule(new TimerModule());  // Priority: 16

// 7. 资源加载 - UI、音频等模块依赖它
RegisterModule(new ResourceModule());  // Priority: 40

// 8. 表现层模块
RegisterModule(new UIModule());     // Priority: 50
RegisterModule(new AudioModule());  // Priority: 60
RegisterModule(new ConfigModule()); // Priority: 55

// 9. 游戏逻辑模块
RegisterModule(new FsmModule());    // Priority: 70
```

**依赖关系图**:
```
LogModule (0)
    ↓
EventModule (10) ← FileSystemModule (5)
    ↓                      ↓
PoolModule (15)      CryptoModule (20)
    ↓                      ↓
TimerModule (16)     SaveModule (30)
    ↓                      ↓
ResourceModule (40) ───────┘
    ↓
UIModule (50) / AudioModule (60) / ConfigModule (55)
    ↓
FsmModule (70)
```

---

## 核心模块详解 / Core Modules Deep Dive

### 1. EventModule (事件模块)
**位置**: `Modules/Event/EventModule.cs` | **优先级**: 10

**核心特性**:
- 基于类型的事件系统，使用泛型约束强制事件为 `struct`
- 零 GC 分配（struct 不产生堆内存）
- 使用 `Dictionary<Type, Delegate>` 存储事件委托

**API**:
```csharp
// 订阅事件
GameApp.Event.AddListener<PlayerDiedEvent>(OnPlayerDied);

// 发送事件
GameApp.Event.Broadcast(new PlayerDiedEvent { PlayerId = 1 });

// 取消订阅
GameApp.Event.RemoveListener<PlayerDiedEvent>(OnPlayerDied);
```

**最佳实践**:
```csharp
// ✅ 使用 struct 定义事件（零 GC）
public struct PlayerLevelUpEvent
{
    public int Level;
    public int Exp;
}

// ✅ 在 OnEnable/OnDisable 中订阅/取消订阅
void OnEnable() => GameApp.Event.AddListener<PlayerLevelUpEvent>(OnLevelUp);
void OnDisable() => GameApp.Event.RemoveListener<PlayerLevelUpEvent>(OnLevelUp);

// ❌ 不要使用 class（会产生 GC）
public class PlayerLevelUpEvent { }  // 错误！
```

---

### 2. PoolModule (对象池模块)
**位置**: `Modules/Pool/PoolModule.cs` | **优先级**: 15

**双重池化系统**:
1. **C# 类内存池** - 用于频繁创建的 C# 对象（如定时器任务、音效任务）
2. **GameObject 对象池** - 用于频繁实例化的游戏对象（如子弹、特效）

**IReference 接口**:
```csharp
public interface IReference
{
    void Clear();  // 回收前清理数据，防止脏数据残留
}
```

**API**:
```csharp
// C# 类对象池
var task = GameApp.Pool.AllocateClass<TimerTask>();  // 从池中获取或创建新对象
GameApp.Pool.ReleaseClass(task);                     // 回收到池中

// GameObject 对象池
GameObject bullet = GameApp.Pool.SpawnGameObject("Bullet", bulletPrefab, parent);
GameApp.Pool.RecycleGameObject("Bullet", bullet);
```

**内部实现细节**:
- 使用 `Dictionary<Type, Queue<IReference>>` 存储 C# 对象池
- 使用 `Dictionary<string, Queue<GameObject>>` 存储 GameObject 池
- 回收的 GameObject 会被移动到隐藏的 `_poolRoot` 节点下并设为 `SetActive(false)`

---

### 3. TimerModule (定时器模块)
**位置**: `Modules/Timer/TimerModule.cs` | **优先级**: 16

**核心特性**:
- 支持延迟执行和循环执行
- 支持 `scaled time` 和 `unscaled time`（不受 Time.timeScale 影响）
- 内部使用 PoolModule 实现零 GC 的定时器任务

**API**:
```csharp
// 延迟执行（单次）
long timerId = GameApp.Timer.AddTimer(2.0f, () => {
    Debug.Log("2秒后执行");
}, isUnscaled: false, loopCount: 1);

// 循环执行（无限）
long loopId = GameApp.Timer.AddTimer(1.0f, () => {
    Debug.Log("每秒执行");
}, isUnscaled: false, loopCount: -1);

// 取消定时器
GameApp.Timer.CancelTimer(timerId);
```

**参数说明**:
- `delay`: 延迟时间（秒）
- `callback`: 回调函数
- `isUnscaled`: `true` = 真实时间（不受暂停影响），`false` = 游戏时间
- `loopCount`: `1` = 单次，`-1` = 无限循环，`>1` = 指定次数

**实现细节**:
- 倒序遍历任务列表，安全处理回调中新增定时器的情况
- 循环定时器使用 `CurrentTime -= Delay` 保证精度，而不是重置为 0


---

### 4. ResourceModule (资源模块)
**位置**: `Modules/Res/ResourceModule.cs` | **优先级**: 40

**核心特性**:
- 基于 Unity Addressables 系统
- 使用 UniTask 实现 async/await 异步加载
- 提供资源引用计数和内存释放机制

**API**:
```csharp
// 异步加载资源（AudioClip, Sprite, ScriptableObject 等）
AudioClip clip = await GameApp.Res.LoadAssetAsync<AudioClip>("Audio/BGM_Main");

// 异步实例化 GameObject（Prefab）
GameObject enemy = await GameApp.Res.InstantiateAsync("Prefabs/Enemy", parent);

// 释放资源（重要！防止内存泄漏）
GameApp.Res.ReleaseAsset(clip);

// 销毁实例化的对象（重要！不要用 Destroy()）
GameApp.Res.ReleaseInstance(enemy);
```

**⚠️ 关键注意事项**:
- 通过 `LoadAssetAsync` 加载的资源必须用 `ReleaseAsset()` 释放
- 通过 `InstantiateAsync` 实例化的对象必须用 `ReleaseInstance()` 销毁
- **绝对不要**对 Addressables 实例化的对象直接调用 `GameObject.Destroy()`！

---

### 5. SaveModule (存档模块)
**位置**: `Modules/Save/SaveModule.cs` | **优先级**: 30

**核心特性**:
- 结合 FileSystemModule 和 CryptoModule 实现加密存档
- 支持自动存档（基于脏标记检测）
- 使用 JsonUtility 序列化

**API**:
```csharp
// 保存数据（默认加密）
GameApp.Save.SaveData("PlayerSave", playerData, useEncryption: true);

// 读取数据
PlayerData data = GameApp.Save.LoadData<PlayerData>("PlayerSave", useEncryption: true);

// 检查存档是否存在
bool hasSave = GameApp.Save.HasSave("PlayerSave");

// 删除存档
GameApp.Save.DeleteSave("PlayerSave");
```

**自动存档系统**:
```csharp
// 启用自动存档追踪
GameApp.Save.TrackAutoSave("PlayerSave", saveDataBase, () => {
    GameApp.Save.SaveData("PlayerSave", saveDataBase);
});

// 停止自动存档
GameApp.Save.StopAutoSave("PlayerSave", saveDataBase);
```

**存档路径**: `Application.persistentDataPath/Saves/{saveName}.sav`

---

### 6. CryptoModule (加密模块)
**位置**: `Modules/Crypto/CryptoModule.cs` | **优先级**: 20

**核心特性**:
- 基于 AES 对称加密（CBC 模式 + PKCS7 填充）
- 支持字符串和字节流加密

**初始化**:
```csharp
var crypto = new CryptoModule();
FrameworkEntry.Instance.RegisterModule(crypto);

// ⚠️ 必须立即设置密钥
crypto.SetCryptoKey("YourSecretKey1234", "YourSecretIV16!");
// Key 长度: 16/24/32 字符
// IV 长度: 必须 16 字符
```

**API**:
```csharp
// 字符串加密/解密
string encrypted = GameApp.Crypto.EncryptString("sensitive data");
string decrypted = GameApp.Crypto.DecryptString(encrypted);

// 字节流加密/解密（用于二进制文件、AssetBundle 保护）
byte[] encryptedBytes = GameApp.Crypto.EncryptBytes(plainBytes);
byte[] decryptedBytes = GameApp.Crypto.DecryptBytes(encryptedBytes);
```

**⚠️ 安全提示**:
- 生产环境必须更改默认密钥
- 密钥不要硬编码在代码中，考虑从服务器获取或使用混淆
- 密钥和 IV 不要提交到版本控制系统


---

### 7. FsmModule (状态机模块)
**位置**: `Modules/FSM/FSMModule.cs` | **优先级**: 70

**核心特性**:
- 泛型状态机实现，支持任意类型的 Owner
- 状态预缓存，零 GC 状态切换
- 内置黑板系统（Blackboard）用于状态间数据共享

**创建状态机**:
```csharp
// 创建状态机并传入所有状态实例
var fsm = GameApp.Fsm.CreateFsm("PlayerFSM", this,
    new IdleState(),
    new MoveState(),
    new AttackState()
);

// 启动状态机（进入初始状态）
fsm.Start<IdleState>();

// 切换状态
fsm.ChangeState<AttackState>();

// 销毁状态机
GameApp.Fsm.DestroyFsm("PlayerFSM");
```

**定义状态**:
```csharp
public class IdleState : FsmState<Player>
{
    // 状态初始化（状态机创建时调用一次）
    protected override void OnInit() { }

    // 进入状态
    public override void OnEnter()
    {
        Debug.Log("进入待机状态");
        Owner.PlayAnimation("Idle");
    }

    // 状态更新
    public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
    {
        if (Input.GetKey(KeyCode.W))
        {
            ChangeState<MoveState>();  // 切换到移动状态
        }
    }

    // 离开状态
    public override void OnLeave()
    {
        Debug.Log("离开待机状态");
    }

    // 状态销毁
    public override void OnDestroy() { }
}
```

**黑板系统**:
```csharp
// 存储数据
fsm.SetData("EnemyTarget", enemyTransform);

// 读取数据
Transform target = fsm.GetData<Transform>("EnemyTarget");
```

**实现细节**:
- 所有状态在创建时预先缓存在 `Dictionary<Type, FsmState<T>>`
- 状态切换时自动调用 `CurrentState.OnLeave()` 和 `NewState.OnEnter()`
- FsmModule 在 `OnUpdate()` 中统一驱动所有状态机的更新


---

### 8. UIModule (UI模块)
**位置**: `Modules/UI/UIModule.cs` | **优先级**: 50

**核心特性**:
- 基于 UniTask 的异步 UI 加载
- 单例模式防止重复打开
- UI 缓存池（休眠/唤醒机制）
- 自动层级管理和 SortingOrder 计算
- 支持入场/退场动画

**UI 层级**:
```csharp
public enum UILayer
{
    Background = 0,  // 背景层
    Normal = 1,      // 普通层
    Popup = 2,       // 弹窗层
    Top = 3,         // 顶层（系统提示）
    Guide = 4        // 引导层
}
```

**注册 UI**:
```csharp
GameApp.UI.RegisterUI(
    formId: 1001,
    address: "UI/MainMenu",
    type: typeof(MainMenuWindow),
    layer: UILayer.Normal,
    isSingleton: true,   // 单例模式
    isCached: true       // 关闭时缓存而不销毁
);
```

**打开/关闭 UI**:
```csharp
// 异步打开 UI（返回 SerialId）
int serialId = await GameApp.UI.OpenUIAsync(1001, arg1, arg2);

// 关闭 UI（立即关闭）
GameApp.UI.CloseUI(serialId);

// 异步关闭 UI（等待退场动画）
await GameApp.UI.CloseUIAsync(serialId);
```

**UI 生命周期**:
```csharp
public class MainMenuWindow : UIFormBase
{
    // 初始化（仅全新实例化时调用一次）
    public override void OnInit() { }

    // 打开（每次打开都会调用）
    public override void OnOpen(params object[] args) { }

    // 关闭
    public override void OnClose() { }

    // 销毁（仅非缓存 UI 才会调用）
    public override void OnDestroyUI() { }
}
```

**缓存机制**:
- `isCached = true`: 关闭时移动到隐藏节点，下次打开秒开
- `isCached = false`: 关闭时彻底销毁，释放内存

**单例机制**:
- `isSingleton = true`: 同一 UI 只能存在一个实例，重复打开会刷新参数
- `isSingleton = false`: 可以同时打开多个实例


---

### 9. AudioModule (音频模块)
**位置**: `Modules/Audio/AudioModule.cs` | **优先级**: 60

**核心特性**:
- 支持 BGM 和 SFX（2D/3D 音效）
- AudioSource 对象池化
- 单例防重模式（防止同一音效重复播放）
- 随机变调（Pitch）
- 3D 音效支持跟随目标移动
- 异步加载音频资源

**BGM API**:
```csharp
// 播放背景音乐
await GameApp.Audio.PlayBGMAsync("Audio/BGM_Battle");

// 停止背景音乐
GameApp.Audio.StopBGM();

// 设置 BGM 音量
GameApp.Audio.SetBGMVolume(0.8f);
```

**SFX API**:
```csharp
// 播放 2D 音效
long audioId = GameApp.Audio.PlaySFX(
    address: "Audio/SFX_Click",
    is3D: false,
    loop: false,
    isSingleton: true,      // 防止连续点击产生多个音效
    pitchRange: 0.1f        // 随机变调 ±0.1
);

// 播放 3D 音效（固定位置）
GameApp.Audio.PlaySFX(
    address: "Audio/SFX_Explosion",
    is3D: true,
    position: explosionPos,
    minDistance: 5f,
    maxDistance: 50f
);

// 播放 3D 音效（跟随目标）
GameApp.Audio.PlaySFX(
    address: "Audio/SFX_Footstep",
    followTarget: playerTransform,
    loop: true
);

// 停止指定音效
GameApp.Audio.StopAudio(audioId);

// 停止所有音效
GameApp.Audio.StopAllSFX();

// 停止所有声音（BGM + SFX）
GameApp.Audio.StopAll();
```

**实现细节**:
- 使用 `Queue<AudioSource>` 实现 AudioSource 对象池
- 音效任务使用 PoolModule 的 C# 对象池（零 GC）
- 单例音效使用 `Dictionary<string, AudioTask>` 防重
- 3D 音效跟随在 `OnUpdate()` 中实时更新坐标


---

## 单例模式 / Singleton Patterns

### TMonoSingleton<T> (MonoBehaviour 单例)
**位置**: `Base/Singleton/TMonoSingleton.cs`

**特性**:
- 自动创建 GameObject 并挂载组件
- DontDestroyOnLoad 跨场景持久化
- 防止多实例（自动销毁重复实例）
- 防止程序退出时创建幽灵节点

**使用方法**:
```csharp
public class GameManager : TMonoSingleton<GameManager>
{
    protected override void Awake()
    {
        base.Awake();  // 必须调用
        // 初始化代码
    }
}

// 访问
GameManager.Instance.DoSomething();
```

---

### TSingleton<T> (纯 C# 单例)
**位置**: `Base/Singleton/TSingleton.cs`

**特性**:
- 线程安全（双重检查锁）
- 使用反射调用私有构造函数
- 强制子类使用私有构造函数

**使用方法**:
```csharp
public class DataManager : TSingleton<DataManager>
{
    private DataManager() { }  // 私有构造函数

    // 业务逻辑
}

// 访问
DataManager.Instance.LoadData();
```


---

## 开发最佳实践 / Development Best Practices

### 1. 模块访问规范

```csharp
// ✅ 正确：使用 GameApp 静态门面
GameApp.Event.Broadcast(evt);
GameApp.UI.OpenUIAsync(1001);
GameApp.Pool.SpawnGameObject("Bullet", prefab);

// ❌ 错误：直接调用 FrameworkEntry（冗长且低效）
FrameworkEntry.Instance.GetModule<EventModule>().Broadcast(evt);
```

---

### 2. 事件系统规范

```csharp
// ✅ 正确：使用 struct（零 GC）
public struct PlayerDiedEvent
{
    public int PlayerId;
    public Vector3 Position;
}

// ❌ 错误：使用 class（产生 GC）
public class PlayerDiedEvent { }

// ✅ 正确：在生命周期方法中订阅/取消订阅
void OnEnable()
{
    GameApp.Event.AddListener<PlayerDiedEvent>(OnPlayerDied);
}

void OnDisable()
{
    GameApp.Event.RemoveListener<PlayerDiedEvent>(OnPlayerDied);
}
```

---

### 3. 对象池使用规范

```csharp
// ✅ 正确：频繁创建的对象使用对象池
GameObject bullet = GameApp.Pool.SpawnGameObject("Bullet", bulletPrefab);
// 使用完毕后回收
GameApp.Pool.RecycleGameObject("Bullet", bullet);

// ❌ 错误：频繁 Instantiate/Destroy（产生大量 GC）
GameObject bullet = Instantiate(bulletPrefab);
Destroy(bullet);
```

---

### 4. 资源加载和释放规范

```csharp
// ✅ 正确：加载后记得释放
AudioClip clip = await GameApp.Res.LoadAssetAsync<AudioClip>("Audio/BGM");
// 使用完毕后释放
GameApp.Res.ReleaseAsset(clip);

// ✅ 正确：实例化后使用 ReleaseInstance
GameObject enemy = await GameApp.Res.InstantiateAsync("Prefabs/Enemy");
// 销毁时使用 ReleaseInstance
GameApp.Res.ReleaseInstance(enemy);

// ❌ 错误：不释放资源（内存泄漏）
AudioClip clip = await GameApp.Res.LoadAssetAsync<AudioClip>("Audio/BGM");
// 忘记释放！

// ❌ 错误：对 Addressables 对象使用 Destroy
Destroy(enemy);  // 错误！会导致引用计数错误
```

---

### 5. 定时器使用规范

```csharp
// ✅ 正确：保存定时器 ID 并在不需要时取消
long timerId = GameApp.Timer.AddTimer(5.0f, OnTimeout);

void OnDestroy()
{
    GameApp.Timer.CancelTimer(timerId);  // 及时取消
}

// ❌ 错误：不取消定时器（可能导致空引用）
GameApp.Timer.AddTimer(5.0f, () => {
    this.DoSomething();  // 如果对象已销毁，会报错
});
```


---

### 6. 状态机使用规范

```csharp
// ✅ 正确：在创建时传入所有状态实例
var fsm = GameApp.Fsm.CreateFsm("PlayerFSM", this,
    new IdleState(),
    new MoveState(),
    new AttackState()
);

// ✅ 正确：使用 Owner 访问宿主
public class IdleState : FsmState<Player>
{
    public override void OnEnter()
    {
        Owner.PlayAnimation("Idle");  // Owner 是 Player 类型
    }
}

// ✅ 正确：使用黑板共享数据
fsm.SetData("Target", enemyTransform);
Transform target = fsm.GetData<Transform>("Target");
```

---

### 7. UI 开发规范

```csharp
// ✅ 正确：先注册 UI 配置
GameApp.UI.RegisterUI(1001, "UI/MainMenu", typeof(MainMenuWindow), UILayer.Normal);

// ✅ 正确：使用异步打开
int serialId = await GameApp.UI.OpenUIAsync(1001, param1, param2);

// ✅ 正确：在 UI 类中接收参数
public override void OnOpen(params object[] args)
{
    if (args.Length > 0)
    {
        int level = (int)args[0];
    }
}

// ✅ 正确：关闭时使用 SerialId
GameApp.UI.CloseUI(serialId);
```


---

## 常见问题 / Common Issues

### Q1: 如何添加自定义模块？

**步骤**:
1. 创建类实现 `IFrameworkModule` 接口
2. 设置合适的 `Priority` 值
3. 在 `FrameworkEntry.InitFrameworkModules()` 中注册
4. 在 `GameApp.cs` 中添加静态属性

```csharp
// 1. 创建模块
public class CustomModule : IFrameworkModule
{
    public int Priority => 80;
    public void OnInit() { }
    public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
    public void OnDestroy() { }
}

// 2. 注册模块（在 FrameworkEntry.cs）
RegisterModule(new CustomModule());

// 3. 添加到 GameApp（在 GameApp.cs）
private static CustomModule _custom;
public static CustomModule Custom => _custom ??= FrameworkEntry.Instance.GetModule<CustomModule>();
```

---

### Q2: 模块初始化顺序错误会怎样？

**问题**: 如果 SaveModule 在 CryptoModule 之前初始化，会导致：
- SaveModule 获取不到 CryptoModule 引用
- 加密功能失效
- 可能抛出 NullReferenceException

**解决**: 严格按照依赖关系初始化，参考本文档的"模块初始化顺序"章节。

---

### Q3: 为什么事件必须用 struct？

**原因**:
- `class` 是引用类型，每次 `new` 都会在堆上分配内存，产生 GC
- `struct` 是值类型，在栈上分配，不产生 GC
- 高频事件（如每帧触发）使用 struct 可以显著减少 GC 压力

---

### Q4: 如何更改加密密钥？

```csharp
// 在 FrameworkEntry.InitFrameworkModules() 中
var crypto = new CryptoModule();
RegisterModule(crypto);

// ⚠️ 立即设置自定义密钥
crypto.SetCryptoKey("MyGameSecretKey123", "MyGameIV1234567");
// Key: 16/24/32 字符
// IV: 必须 16 字符
```


---

### Q5: UI 缓存和单例的区别？

**单例模式** (`isSingleton`):
- 控制同一时间只能存在一个实例
- 重复打开会刷新参数而不是创建新实例

**缓存模式** (`isCached`):
- 控制关闭时是否销毁
- `true`: 关闭时休眠到缓存池，下次秒开
- `false`: 关闭时彻底销毁，释放内存

**组合使用**:
```csharp
// 主菜单：单例 + 缓存（频繁打开，只需一个）
RegisterUI(1001, "UI/MainMenu", typeof(MainMenuWindow), 
    UILayer.Normal, isSingleton: true, isCached: true);

// 提示框：非单例 + 非缓存（可能同时多个，用完即销毁）
RegisterUI(2001, "UI/TipDialog", typeof(TipDialog), 
    UILayer.Popup, isSingleton: false, isCached: false);
```

---

## 关键注意事项 / Critical Notes

### ⚠️ 内存管理

1. **Addressables 资源必须释放**
   - `LoadAssetAsync` → 必须 `ReleaseAsset`
   - `InstantiateAsync` → 必须 `ReleaseInstance`
   - 不释放会导致内存泄漏

2. **对象池回收前清理状态**
   - GameObject 回收前重置位置、旋转、缩放
   - C# 对象实现 `IReference.Clear()` 清理数据

3. **定时器及时取消**
   - 在 `OnDestroy` 中取消所有定时器
   - 避免对象销毁后回调仍然执行

### ⚠️ 线程安全

- EventModule 不是线程安全的，只能在主线程使用
- 如需在异步任务中发送事件，使用 `UniTask.SwitchToMainThread()`

### ⚠️ 性能优化

1. **事件使用 struct** - 避免 GC
2. **频繁创建的对象使用对象池** - 子弹、特效、敌人
3. **资源异步加载** - 避免卡顿
4. **UI 缓存** - 频繁打开的 UI 启用缓存


---

## 项目结构 / Project Structure

```
SGFCore/
├── Base/                          # 框架核心
│   ├── FrameworkEntry.cs         # 框架入口（模块管理器）
│   ├── GameApp.cs                # 静态门面（快速访问）
│   ├── IFrameworkModule.cs       # 模块接口
│   └── Singleton/                # 单例基类
│       ├── TMonoSingleton.cs     # MonoBehaviour 单例
│       └── TSingleton.cs         # 纯 C# 单例
│
├── Modules/                       # 功能模块
│   ├── Audio/                    # 音频模块（BGM + SFX）
│   ├── BehaviorTree/             # 行为树模块（AI）
│   ├── Config/                   # 配置表模块
│   ├── Crypto/                   # 加密模块（AES）
│   ├── Debugger/                 # 日志模块
│   ├── Event/                    # 事件模块（发布/订阅）
│   ├── FSM/                      # 状态机模块
│   ├── FileIO/                   # 文件系统模块
│   ├── Localization/             # 本地化模块
│   ├── Network/                  # 网络模块（HTTP）
│   ├── Pool/                     # 对象池模块
│   ├── Res/                      # 资源模块（Addressables）
│   ├── Save/                     # 存档模块
│   ├── Scene/                    # 场景模块
│   ├── Timer/                    # 定时器模块
│   └── UI/                       # UI 模块
│
├── Utils/                         # 工具类
│   ├── CollectionExtension.cs    # 集合扩展
│   ├── NumberExtension.cs        # 数值扩展
│   ├── TimeUtility.cs            # 时间工具
│   └── TransformExtension.cs     # Transform 扩展
│
├── Demo/                          # 示例代码
│   ├── GameDemoEntry.cs          # 示例入口
│   ├── ProcedureLaunch.cs        # 启动流程
│   ├── ProcedurePreload.cs       # 预加载流程
│   └── ProcedureMainMenu.cs      # 主菜单流程
│
├── Prefabs/                       # 预制体
└── README.md                      # 详细中文文档
```


---

## 工具类 / Utility Classes

### CollectionExtension (集合扩展)
**位置**: `Utils/CollectionExtension.cs`

```csharp
// 随机获取元素
var item = list.GetRandom();

// 打乱顺序
list.Shuffle();
```

### NumberExtension (数值扩展)
**位置**: `Utils/NumberExtension.cs`

```csharp
// 限制范围
float value = 150f.Clamp(0f, 100f);  // 返回 100

// 百分比转换
int percent = 75.ToPercent(100);  // 返回 75%
```

### TimeUtility (时间工具)
**位置**: `Utils/TimeUtility.cs`

```csharp
// 获取时间戳
long timestamp = TimeUtility.GetTimestamp();

// 时间戳转 DateTime
DateTime dt = TimeUtility.TimestampToDateTime(timestamp);

// 格式化时间
string timeStr = TimeUtility.FormatTime(3665);  // "01:01:05"
```

### TransformExtension (Transform 扩展)
**位置**: `Utils/TransformExtension.cs`

```csharp
// 重置 Transform
transform.Reset();

// 设置位置
transform.SetPositionX(10f);
transform.SetPositionY(5f);
transform.SetPositionZ(0f);
```


---

## 完整代码示例 / Complete Code Examples

### 示例 1: 游戏入口初始化

```csharp
using UnityEngine;
using GameFramework.Core;

public class GameEntry : MonoBehaviour
{
    private void Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        
        // 初始化框架
        FrameworkEntry.Instance.InitFrameworkModules();
        
        // 创建游戏流程状态机
        StartGameProcedure();
    }
    
    private void StartGameProcedure()
    {
        var fsm = GameApp.Fsm.CreateFsm("GameProcedure", this,
            new ProcedureLaunch(),
            new ProcedurePreload(),
            new ProcedureMainMenu()
        );
        
        fsm.Start<ProcedureLaunch>();
    }
}
```

### 示例 2: 状态机流程

```csharp
// 启动流程
public class ProcedureLaunch : FsmState<GameEntry>
{
    public override void OnEnter()
    {
        Log.Info("进入启动流程");
        
        // 检查资源完整性
        CheckResources();
        
        // 进入预加载流程
        ChangeState<ProcedurePreload>();
    }
    
    private void CheckResources()
    {
        // 资源检查逻辑
    }
}

// 预加载流程
public class ProcedurePreload : FsmState<GameEntry>
{
    public override async void OnEnter()
    {
        Log.Info("进入预加载流程");
        
        // 加载配置表
        await GameApp.Config.LoadAllConfigs();
        
        // 进入主菜单
        ChangeState<ProcedureMainMenu>();
    }
}
```


### 示例 3: 事件系统使用

```csharp
// 定义事件
public struct PlayerLevelUpEvent
{
    public int Level;
    public int Exp;
}

public struct PlayerDiedEvent
{
    public int PlayerId;
    public Vector3 Position;
}

// 订阅和发送事件
public class Player : MonoBehaviour
{
    void OnEnable()
    {
        GameApp.Event.AddListener<PlayerDiedEvent>(OnPlayerDied);
    }
    
    void OnDisable()
    {
        GameApp.Event.RemoveListener<PlayerDiedEvent>(OnPlayerDied);
    }
    
    void LevelUp(int newLevel, int exp)
    {
        // 发送升级事件
        GameApp.Broadcast(new PlayerLevelUpEvent 
        { 
            Level = newLevel, 
            Exp = exp 
        });
    }
    
    void OnPlayerDied(PlayerDiedEvent evt)
    {
        Debug.Log($"玩家 {evt.PlayerId} 在 {evt.Position} 死亡");
    }
}
```

### 示例 4: 对象池使用

```csharp
public class BulletManager : MonoBehaviour
{
    public GameObject bulletPrefab;
    
    void FireBullet()
    {
        // 从对象池获取子弹
        GameObject bullet = GameApp.Pool.SpawnGameObject(
            "Bullet", 
            bulletPrefab, 
            transform
        );
        
        // 3秒后回收
        GameApp.Timer.AddTimer(3.0f, () => {
            GameApp.Pool.RecycleGameObject("Bullet", bullet);
        });
    }
}
```


### 示例 5: UI 系统使用

```csharp
// UI 窗口定义
public class MainMenuWindow : UIFormBase
{
    public override void OnInit()
    {
        // 绑定按钮事件
        GetButton("StartButton").onClick.AddListener(OnStartClick);
    }
    
    public override void OnOpen(params object[] args)
    {
        // 接收参数
        if (args.Length > 0)
        {
            int playerLevel = (int)args[0];
            UpdateLevelDisplay(playerLevel);
        }
    }
    
    private void OnStartClick()
    {
        // 关闭当前窗口
        GameApp.UI.CloseUI(SerialId);
        
        // 打开游戏窗口
        GameApp.UI.OpenUIAsync(2001);
    }
}

// 注册和打开 UI
public class UIManager : MonoBehaviour
{
    void Start()
    {
        // 注册 UI
        GameApp.UI.RegisterUI(
            1001, 
            "UI/MainMenu", 
            typeof(MainMenuWindow), 
            UILayer.Normal,
            isSingleton: true,
            isCached: true
        );
        
        // 打开 UI
        OpenMainMenu();
    }
    
    async void OpenMainMenu()
    {
        int serialId = await GameApp.UI.OpenUIAsync(1001, playerLevel: 10);
    }
}
```


### 示例 6: 资源加载和释放

```csharp
public class ResourceExample : MonoBehaviour
{
    private AudioClip bgmClip;
    private GameObject enemyInstance;
    
    async void Start()
    {
        // 加载音频资源
        bgmClip = await GameApp.Res.LoadAssetAsync<AudioClip>("Audio/BGM_Battle");
        
        // 实例化敌人
        enemyInstance = await GameApp.Res.InstantiateAsync("Prefabs/Enemy", transform);
    }
    
    void OnDestroy()
    {
        // 释放资源
        if (bgmClip != null)
        {
            GameApp.Res.ReleaseAsset(bgmClip);
        }
        
        // 销毁实例
        if (enemyInstance != null)
        {
            GameApp.Res.ReleaseInstance(enemyInstance);
        }
    }
}
```

### 示例 7: 存档系统使用

```csharp
// 定义存档数据
[System.Serializable]
public class PlayerSaveData
{
    public int Level;
    public int Exp;
    public int Gold;
    public Vector3 Position;
}

public class SaveExample : MonoBehaviour
{
    void SaveGame()
    {
        var saveData = new PlayerSaveData
        {
            Level = 10,
            Exp = 5000,
            Gold = 1000,
            Position = transform.position
        };
        
        // 保存（加密）
        GameApp.Save.SaveData("PlayerSave", saveData, useEncryption: true);
    }
    
    void LoadGame()
    {
        // 读取（解密）
        var saveData = GameApp.Save.LoadData<PlayerSaveData>("PlayerSave", useEncryption: true);
        
        transform.position = saveData.Position;
        Debug.Log($"等级: {saveData.Level}, 金币: {saveData.Gold}");
    }
}
```


### 示例 8: 音频系统使用

```csharp
public class AudioExample : MonoBehaviour
{
    async void Start()
    {
        // 播放背景音乐
        await GameApp.Audio.PlayBGMAsync("Audio/BGM_Main");
        
        // 设置音量
        GameApp.Audio.SetBGMVolume(0.7f);
        GameApp.Audio.SetSFXVolume(0.8f);
    }
    
    void OnButtonClick()
    {
        // 播放 2D 音效（单例防重）
        GameApp.Audio.PlaySFX(
            "Audio/SFX_Click", 
            is3D: false,
            isSingleton: true,
            pitchRange: 0.1f
        );
    }
    
    void OnExplosion(Vector3 position)
    {
        // 播放 3D 音效
        GameApp.Audio.PlaySFX(
            "Audio/SFX_Explosion",
            is3D: true,
            position: position,
            minDistance: 5f,
            maxDistance: 50f
        );
    }
    
    void OnPlayerMove(Transform player)
    {
        // 播放跟随音效
        long audioId = GameApp.Audio.PlaySFX(
            "Audio/SFX_Footstep",
            followTarget: player,
            loop: true
        );
        
        // 停止时取消
        GameApp.Timer.AddTimer(5.0f, () => {
            GameApp.Audio.StopAudio(audioId);
        });
    }
}
```


---

## 快速参考 / Quick Reference

### 模块访问速查

```csharp
GameApp.Event      // 事件模块
GameApp.Pool       // 对象池模块
GameApp.Timer      // 定时器模块
GameApp.Res        // 资源模块
GameApp.Save       // 存档模块
GameApp.UI         // UI 模块
GameApp.Audio      // 音频模块
GameApp.Fsm        // 状态机模块
GameApp.Config     // 配置模块
GameApp.FileSystem // 文件系统模块
GameApp.Loc        // 本地化模块
GameApp.Http       // 网络模块
GameApp.Scene      // 场景模块
```

### 常用 API 速查

```csharp
// 事件
GameApp.Event.AddListener<T>(handler)
GameApp.Event.Broadcast<T>(eventData)
GameApp.Event.RemoveListener<T>(handler)

// 对象池
GameApp.Pool.SpawnGameObject(name, prefab, parent)
GameApp.Pool.RecycleGameObject(name, obj)
GameApp.Pool.AllocateClass<T>()
GameApp.Pool.ReleaseClass(obj)

// 定时器
GameApp.Timer.AddTimer(delay, callback, isUnscaled, loopCount)
GameApp.Timer.CancelTimer(timerId)

// 资源
await GameApp.Res.LoadAssetAsync<T>(address)
await GameApp.Res.InstantiateAsync(address, parent)
GameApp.Res.ReleaseAsset(asset)
GameApp.Res.ReleaseInstance(instance)

// 存档
GameApp.Save.SaveData(name, data, useEncryption)
GameApp.Save.LoadData<T>(name, useEncryption)
GameApp.Save.HasSave(name)
GameApp.Save.DeleteSave(name)

// UI
await GameApp.UI.OpenUIAsync(formId, args)
GameApp.UI.CloseUI(serialId)
await GameApp.UI.CloseUIAsync(serialId)

// 音频
await GameApp.Audio.PlayBGMAsync(address)
GameApp.Audio.PlaySFX(address, is3D, position, loop, isSingleton)
GameApp.Audio.StopAudio(audioId)
GameApp.Audio.StopAllSFX()

// 状态机
GameApp.Fsm.CreateFsm<T>(name, owner, states)
fsm.Start<TState>()
fsm.ChangeState<TState>()
GameApp.Fsm.DestroyFsm(name)
```


---

## 性能优化建议 / Performance Optimization

### 1. 零 GC 设计
- ✅ 事件使用 `struct` 而非 `class`
- ✅ 频繁创建的对象使用对象池
- ✅ 定时器任务使用内存池
- ✅ 音效任务使用内存池

### 2. 异步加载
- ✅ 使用 `async/await` 异步加载资源
- ✅ 避免在主线程阻塞加载大资源
- ✅ UI 使用异步打开

### 3. 对象池化
- ✅ 子弹、特效、敌人等频繁创建的对象
- ✅ AudioSource 组件池化
- ✅ UI 缓存池

### 4. 资源管理
- ✅ 及时释放不用的资源
- ✅ 使用 Addressables 引用计数
- ✅ UI 缓存减少重复加载

### 5. 模块缓存
- ✅ GameApp 使用 `??=` 缓存模块引用
- ✅ 避免重复调用 `GetModule<T>()`


---

## 开发工作流 / Development Workflow

### 新项目启动流程

1. **创建游戏入口脚本**
   - 继承 MonoBehaviour
   - 调用 `FrameworkEntry.Instance.InitFrameworkModules()`

2. **修改加密密钥**
   - 在 `FrameworkEntry.cs` 中修改 `crypto.SetCryptoKey()`

3. **创建游戏流程状态机**
   - 定义启动、预加载、主菜单等流程状态
   - 使用 FsmModule 管理游戏流程

4. **注册 UI 配置**
   - 在初始化时注册所有 UI 窗口

5. **开始开发游戏逻辑**

### 添加新功能流程

1. **评估是否需要新模块**
   - 如果是通用功能，考虑创建新模块
   - 如果是业务逻辑，直接使用现有模块

2. **使用现有模块**
   - 通过 `GameApp.X` 访问模块
   - 遵循本文档的最佳实践

3. **测试和优化**
   - 检查是否有内存泄漏
   - 检查是否有 GC 分配
   - 使用 Profiler 分析性能


---

## 调试技巧 / Debugging Tips

### 1. 日志系统
```csharp
Log.Info("普通信息");
Log.Warning("警告信息");
Log.Error("错误信息");
Log.Fatal("致命错误");
Log.Module("ModuleName", "模块日志");
```

### 2. 常见错误排查

**NullReferenceException in GetModule**
- 原因：模块未注册或初始化顺序错误
- 解决：检查 `InitFrameworkModules()` 中的注册顺序

**内存持续增长**
- 原因：资源未释放或定时器未取消
- 解决：使用 Profiler 检查，确保调用 `ReleaseAsset/ReleaseInstance`

**事件未触发**
- 原因：忘记订阅或已取消订阅
- 解决：检查 `OnEnable/OnDisable` 中的订阅逻辑

**UI 打开失败**
- 原因：未注册 UI 或资源路径错误
- 解决：检查 `RegisterUI` 调用和 Addressables 配置


---

## 总结 / Summary

### 核心设计理念

1. **模块化** - 所有功能以独立模块形式组织
2. **优先级驱动** - 模块按依赖关系自动排序
3. **静态门面** - 通过 GameApp 快速访问，简洁高效
4. **零 GC 设计** - 对象池化、struct 事件、内存复用
5. **异步优先** - 使用 UniTask 实现流畅的异步加载

### 关键要点

- ⚠️ **严格遵守模块初始化顺序**
- ⚠️ **始终使用 GameApp 访问模块**
- ⚠️ **事件必须使用 struct**
- ⚠️ **资源加载后必须释放**
- ⚠️ **定时器使用后必须取消**
- ⚠️ **生产环境必须更改加密密钥**

### 文档资源

- **详细中文文档**: `README.md`
- **示例代码**: `Demo/` 目录
- **Unity 官方文档**: https://docs.unity3d.com/
- **Addressables 文档**: https://docs.unity3d.com/Packages/com.unity.addressables@latest

---

**SGFCore** - 让 Unity 游戏开发更简单、更高效！

