# SGFCore - Simple Game Framework Core

一个轻量级、模块化的 Unity 游戏开发框架，提供完整的游戏开发基础设施。

## 📋 目录

- [特性](#特性)
- [快速开始](#快速开始)
- [核心架构](#核心架构)
- [模块说明](#模块说明)
- [使用示例](#使用示例)
- [最佳实践](#最佳实践)

---

## ✨ 特性

- **模块化设计**：所有功能以独立模块形式组织，按需加载
- **优先级管理**：模块初始化和更新顺序可控
- **单例模式**：提供 MonoBehaviour 和普通单例基类
- **静态门面**：通过 `GameApp` 快速访问各模块，无需冗长调用
- **完整生命周期**：统一的初始化、更新、销毁流程
- **高性能**：模块引用缓存，避免重复查找

---

## 🚀 快速开始

### 1. 创建游戏入口

在场景中创建一个空物体，挂载入口脚本：

```csharp
using UnityEngine;
using GameFramework.Core;

public class GameEntry : MonoBehaviour
{
    private void Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;

        InitFramework();
    }

    private void InitFramework()
    {
        var framework = FrameworkEntry.Instance;

        // 按顺序注册模块
        framework.RegisterModule(new LogModule());
        framework.RegisterModule(new EventModule());
        framework.RegisterModule(new FileSystemModule());
        framework.RegisterModule(new SaveModule());
        framework.RegisterModule(new TimerModule());
        framework.RegisterModule(new PoolModule());
        framework.RegisterModule(new ResourceModule());
        framework.RegisterModule(new UIModule());
        framework.RegisterModule(new AudioModule());
        framework.RegisterModule(new ConfigModule());
        framework.RegisterModule(new FsmModule());

        Log.Info("框架初始化完成！");
    }
}
```

### 2. 使用模块

通过 `GameApp` 静态门面访问模块：

```csharp
// 发送事件
GameApp.Event.Broadcast(new PlayerLevelUpEvent { Level = 10 });

// 播放音效
GameApp.Audio.PlaySound("button_click");

// 加载资源
GameApp.Res.LoadAssetAsync<GameObject>("Prefabs/Player", (obj) => {
    Instantiate(obj);
});

// 显示UI
GameApp.UI.OpenWindow<MainMenuWindow>();
```

---

## 🏗️ 核心架构

### 框架入口 (FrameworkEntry)

框架的核心管理器，负责：
- 模块注册与生命周期管理
- 按优先级排序模块
- 统一的 Update 调度

### 静态门面 (GameApp)

提供便捷的模块访问接口，自动缓存引用：

```csharp
public static class GameApp
{
    public static EventModule Event { get; }
    public static FileSystemModule FileSystem { get; }
    public static SaveModule Save { get; }
    public static ResourceModule Res { get; }
    public static TimerModule Timer { get; }
    public static PoolModule Pool { get; }
    public static UIModule UI { get; }
    public static ConfigModule Config { get; }
    public static AudioModule Audio { get; }
    public static FsmModule Fsm { get; }
    public static BTModule BT { get; }
    public static LocalizationModule Loc { get; }
    public static HttpModule Http { get; }
}
```

### 模块接口 (IFrameworkModule)

所有模块必须实现的接口：

```csharp
public interface IFrameworkModule
{
    int Priority { get; }              // 优先级
    void OnInit();                     // 初始化
    void OnUpdate(float deltaTime, float unscaledDeltaTime);  // 更新
    void OnDestroy();                  // 销毁
}
```

---

## 📦 模块说明

### 核心模块

#### 1. LogModule (日志模块)
提供统一的日志输出接口。

```csharp
Log.Info("普通信息");
Log.Warning("警告信息");
Log.Error("错误信息");
```

#### 2. EventModule (事件模块)
基于类型的事件系统。

```csharp
// 定义事件
public struct PlayerDiedEvent { public int PlayerId; }

// 订阅
GameApp.Event.Subscribe<PlayerDiedEvent>(OnPlayerDied);

// 发送
GameApp.Event.Broadcast(new PlayerDiedEvent { PlayerId = 1 });

// 取消订阅
GameApp.Event.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
```

#### 3. FileSystemModule (文件系统模块)
跨平台文件读写。

```csharp
GameApp.FileSystem.WriteAllText("config.txt", "content");
string content = GameApp.FileSystem.ReadAllText("config.txt");
bool exists = GameApp.FileSystem.FileExists("config.txt");
```

#### 4. SaveModule (存档模块)
支持加密的存档系统。

```csharp
GameApp.Save.SaveData("player_data", playerData);
var data = GameApp.Save.LoadData<PlayerData>("player_data");
GameApp.Save.DeleteSave("player_data");
```

#### 5. CryptoModule (加密模块)
AES 加密/解密。

```csharp
crypto.SetCryptoKey("YourSecretKey", "YourSecretIV16");
string encrypted = crypto.Encrypt("sensitive data");
string decrypted = crypto.Decrypt(encrypted);
```

### 资源与表现模块

#### 6. ResourceModule (资源模块)
统一的资源加载接口。

```csharp
// 异步加载
GameApp.Res.LoadAssetAsync<GameObject>("Prefabs/Enemy", (obj) => {
    Instantiate(obj);
});

// 同步加载
var prefab = GameApp.Res.LoadAsset<GameObject>("Prefabs/Player");
```

#### 7. UIModule (UI模块)
UI 窗口管理系统。

```csharp
GameApp.UI.OpenWindow<MainMenuWindow>();
GameApp.UI.CloseWindow<MainMenuWindow>();
```

#### 8. AudioModule (音频模块)
音效和背景音乐管理。

```csharp
GameApp.Audio.PlayMusic("bgm_main");
GameApp.Audio.PlaySound("sfx_explosion");
GameApp.Audio.SetMusicVolume(0.8f);
```

#### 9. PoolModule (对象池模块)
GameObject 对象池管理。

```csharp
var bullet = GameApp.Pool.Spawn("Bullet");
GameApp.Pool.Despawn(bullet);
```

### 逻辑模块

#### 10. TimerModule (定时器模块)
灵活的定时器系统。

```csharp
// 延迟执行
GameApp.Timer.DelayCall(2.0f, () => {
    Debug.Log("2秒后执行");
});

// 循环执行
int timerId = GameApp.Timer.LoopCall(1.0f, () => {
    Debug.Log("每秒执行");
});

// 取消定时器
GameApp.Timer.CancelTimer(timerId);
```

#### 11. FSMModule (状态机模块)
有限状态机系统。

```csharp
// 创建状态机
var fsm = GameApp.Fsm.CreateFsm("PlayerFSM", this,
    new IdleState(),
    new MoveState(),
    new AttackState()
);

// 启动并切换状态
fsm.Start<IdleState>();
fsm.ChangeState<AttackState>();
```

#### 12. BTModule (行为树模块)
行为树系统，用于 AI 逻辑。

```csharp
var bt = GameApp.BT.CreateBehaviorTree("EnemyAI", this);
bt.AddNode(new PatrolAction());
bt.AddNode(new ChaseAction());
```

#### 13. ConfigModule (配置表模块)
游戏配置数据管理。

```csharp
GameApp.Config.LoadConfig<ItemConfig>();
var itemData = GameApp.Config.GetConfig<ItemConfig>(1001);
```

#### 14. LocalizationModule (本地化模块)
多语言支持。

```csharp
GameApp.Loc.SetLanguage(SystemLanguage.Chinese);
string text = GameApp.Loc.GetText("ui_start_game");
```

#### 15. HttpModule (网络模块)
HTTP 请求封装。

```csharp
GameApp.Http.Get("https://api.example.com/data", (response) => {
    Debug.Log(response);
});
```

---

## 💡 使用示例

### 完整的游戏流程示例

```csharp
public class GameDemoEntry : MonoBehaviour
{
    private void Start()
    {
        InitFramework();
        StartGameProcedure();
    }

    private void StartGameProcedure()
    {
        // 创建游戏流程状态机
        var procedureFsm = GameApp.Fsm.CreateFsm(
            "GameProcedure",
            this,
            new ProcedureLaunch(),    // 启动
            new ProcedurePreload(),   // 预加载
            new ProcedureMainMenu()   // 主菜单
        );

        procedureFsm.Start<ProcedureLaunch>();
    }
}
```

### 状态机示例

```csharp
public class ProcedureLaunch : FSMState
{
    protected override void OnEnter()
    {
        Log.Info("进入启动流程");

        // 检查资源完整性
        CheckResources();

        // 进入下一个状态
        Fsm.ChangeState<ProcedurePreload>();
    }
}
```

### 事件通信示例

```csharp
// 定义事件结构
public struct GameStartEvent { }
public struct PlayerLevelUpEvent
{
    public int Level;
    public int Exp;
}

// 订阅事件
void OnEnable()
{
    GameApp.Event.Subscribe<PlayerLevelUpEvent>(OnPlayerLevelUp);
}

void OnDisable()
{
    GameApp.Event.Unsubscribe<PlayerLevelUpEvent>(OnPlayerLevelUp);
}

void OnPlayerLevelUp(PlayerLevelUpEvent evt)
{
    Debug.Log($"玩家升级到 {evt.Level} 级");
}

// 发送事件
GameApp.Broadcast(new PlayerLevelUpEvent { Level = 10, Exp = 1000 });
```

---

## 🎯 最佳实践

### 1. 模块初始化顺序

**严格按照依赖关系初始化模块：**

```csharp
// ✅ 正确的顺序
framework.RegisterModule(new LogModule());        // 1. 日志最先
framework.RegisterModule(new EventModule());      // 2. 事件系统
framework.RegisterModule(new FileSystemModule()); // 3. 文件系统
framework.RegisterModule(new CryptoModule());     // 4. 加密模块
framework.RegisterModule(new SaveModule());       // 5. 存档（依赖文件和加密）
framework.RegisterModule(new TimerModule());      // 6. 定时器
framework.RegisterModule(new PoolModule());       // 7. 对象池
framework.RegisterModule(new ResourceModule());   // 8. 资源加载
framework.RegisterModule(new UIModule());         // 9. UI系统
framework.RegisterModule(new AudioModule());      // 10. 音频
framework.RegisterModule(new ConfigModule());     // 11. 配置表
framework.RegisterModule(new FsmModule());        // 12. 状态机
```

### 2. 使用静态门面访问模块

```csharp
// ✅ 推荐：简洁高效
GameApp.Event.Broadcast(evt);
GameApp.UI.OpenWindow<MainMenu>();

// ❌ 不推荐：冗长且每次查字典
FrameworkEntry.Instance.GetModule<EventModule>().Broadcast(evt);
```

### 3. 事件使用规范

```csharp
// ✅ 使用 struct 定义事件（避免 GC）
public struct PlayerDiedEvent
{
    public int PlayerId;
    public Vector3 Position;
}

// ❌ 不要使用 class（会产生 GC）
public class PlayerDiedEvent { }
```

### 4. 对象池使用

```csharp
// ✅ 频繁创建的对象使用对象池
var bullet = GameApp.Pool.Spawn("Bullet");
// 使用完毕后回收
GameApp.Pool.Despawn(bullet);

// ❌ 不要频繁 Instantiate/Destroy
var bullet = Instantiate(bulletPrefab);
Destroy(bullet);
```

### 5. 单例模式使用

```csharp
// MonoBehaviour 单例
public class GameManager : TMonoSingleton<GameManager>
{
    protected override void Awake()
    {
        base.Awake();
        // 初始化代码
    }
}

// 普通单例
public class DataManager : TSingleton<DataManager>
{
    protected override void OnInit()
    {
        // 初始化代码
    }
}
```

---

## 📁 项目结构

```
SGFCore/
├── Base/                          # 框架核心
│   ├── FrameworkEntry.cs         # 框架入口
│   ├── GameApp.cs                # 静态门面
│   ├── IFrameworkModule.cs       # 模块接口
│   └── Singleton/                # 单例基类
│       ├── TMonoSingleton.cs
│       └── TSingleton.cs
├── Modules/                       # 功能模块
│   ├── Audio/                    # 音频模块
│   ├── BehaviorTree/             # 行为树模块
│   ├── Config/                   # 配置表模块
│   ├── Crypto/                   # 加密模块
│   ├── Debugger/                 # 日志模块
│   ├── Event/                    # 事件模块
│   ├── FSM/                      # 状态机模块
│   ├── FileIO/                   # 文件系统模块
│   ├── Localization/             # 本地化模块
│   ├── Network/                  # 网络模块
│   ├── Pool/                     # 对象池模块
│   ├── Res/                      # 资源模块
│   ├── Save/                     # 存档模块
│   ├── Timer/                    # 定时器模块
│   └── UI/                       # UI模块
├── Utils/                         # 工具类
│   ├── CollectionExtension.cs    # 集合扩展
│   ├── NumberExtension.cs        # 数值扩展
│   ├── TimeUtility.cs            # 时间工具
│   └── TransformExtension.cs     # Transform扩展
├── Demo/                          # 示例代码
│   ├── GameDemoEntry.cs          # 示例入口
│   ├── ProcedureLaunch.cs        # 启动流程
│   ├── ProcedurePreload.cs       # 预加载流程
│   └── ProcedureMainMenu.cs      # 主菜单流程
└── Plugins/                       # 第三方插件
```

---

## 🛠️ 工具类

### CollectionExtension (集合扩展)
```csharp
// 随机获取元素
var item = list.GetRandom();

// 打乱顺序
list.Shuffle();
```

### NumberExtension (数值扩展)
```csharp
// 限制范围
float value = 150f.Clamp(0f, 100f); // 返回 100

// 百分比转换
int percent = 75.ToPercent(100); // 返回 75%
```

### TimeUtility (时间工具)
```csharp
// 获取时间戳
long timestamp = TimeUtility.GetTimestamp();

// 时间戳转DateTime
DateTime dt = TimeUtility.TimestampToDateTime(timestamp);

// 格式化时间
string timeStr = TimeUtility.FormatTime(3665); // "01:01:05"
```

### TransformExtension (Transform扩展)
```csharp
// 重置Transform
transform.Reset();

// 设置位置
transform.SetPositionX(10f);
transform.SetPositionY(5f);
```

---

## ❓ 常见问题

### Q: 如何自定义模块？

实现 `IFrameworkModule` 接口：

```csharp
public class CustomModule : IFrameworkModule
{
    public int Priority => 100;

    public void OnInit()
    {
        Debug.Log("自定义模块初始化");
    }

    public void OnUpdate(float deltaTime, float unscaledDeltaTime)
    {
        // 每帧更新逻辑
    }

    public void OnDestroy()
    {
        // 清理资源
    }
}

// 注册模块
framework.RegisterModule(new CustomModule());
```

### Q: 如何修改加密密钥？

在初始化时设置：

```csharp
var crypto = new CryptoModule();
framework.RegisterModule(crypto);
crypto.SetCryptoKey("YourKey", "YourIV16Chars!!"); // IV必须16字符
```

### Q: 模块优先级如何设置？

优先级数值越小，初始化和更新越早：

```csharp
public int Priority => 0;   // 最先执行
public int Priority => 100; // 较晚执行
```

### Q: 如何在 GameApp 中添加新模块？

在 `GameApp.cs` 中添加属性：

```csharp
private static YourModule _yourModule;
public static YourModule YourModule => _yourModule ??= FrameworkEntry.Instance.GetModule<YourModule>();
```

---

## ⚡ 性能优化建议

1. **事件使用 struct**：避免 GC 分配
2. **对象池化**：频繁创建的对象使用对象池
3. **异步加载**：大资源使用异步加载，避免卡顿
4. **模块缓存**：通过 GameApp 访问模块，自动缓存引用
5. **定时器管理**：及时取消不需要的定时器

---

## 📝 注意事项

- 模块初始化顺序很重要，必须按依赖关系注册
- 加密密钥和 IV 必须妥善保管，不要提交到版本控制
- 事件订阅后记得在 OnDisable/OnDestroy 中取消订阅
- 对象池中的对象回收前要重置状态
- FSM 状态切换时会自动调用 OnExit 和 OnEnter

---

## 🔗 相关资源

- Unity 官方文档：https://docs.unity3d.com/
- C# 编程指南：https://docs.microsoft.com/zh-cn/dotnet/csharp/

---

## 📄 许可证

本框架采用 MIT 许可证，可自由用于商业和个人项目。

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request 来改进这个框架！

---

**SGFCore** - 让 Unity 游戏开发更简单、更高效！
