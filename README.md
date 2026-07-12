# SGFCore 使用说明

SGFCore 是一个面向 Unity 休闲游戏和中小型项目的轻量游戏框架。框架入口是 `FrameworkEntry`，常用模块通过 `GameApp` 静态门面访问，例如 `GameApp.Res`、`GameApp.UI`、`GameApp.Event`。

## 快速接入

1. 在启动场景放置或自动创建 `FrameworkEntry`。
2. 等待框架完成显式依赖排序和所有同步/异步模块初始化：

```csharp
bool ready = await FrameworkEntry.Instance.InitFrameworkModulesAsync(
    frameworkConfig,
    cancellationToken);
if (!ready)
{
    return;
}
```

3. 业务层通过 `GameApp` 使用模块，尽量避免直接保存模块内部对象引用。

框架不再使用数字 `Priority` 推断顺序。内建模块在 `FrameworkEntry` 的 composition root 中声明直接依赖，经稳定拓扑排序后初始化，并按相反顺序关闭。自定义模块也应在初始化开始前通过 `RegisterModule(module, dependencyTypes...)` 完成组装。

## 模块目录

| 模块 | 说明 |
| --- | --- |
| [Debugger](Modules/Debugger/README.md) | 日志输出封装，统一 Info、Warning、Error、Fatal、Module 日志。 |
| [FileIO](Modules/FileIO/README.md) | 跨平台文件读写封装，默认写入 Unity `persistentDataPath`。 |
| [Event](Modules/Event/README.md) | 基于 struct 类型的事件发布/订阅系统。 |
| [Pool](Modules/Pool/README.md) | C# 引用池和 GameObject 对象池。 |
| [Timer](Modules/Timer/README.md) | 帧驱动定时器，支持循环、暂停、真实时间。 |
| [Time](Modules/Time/README.md) | 服务器时间偏移、离线时长、游戏日和跨天刷新判断。 |
| [Crypto](Modules/Crypto/README.md) | AES 字符串/字节数组加密解密。 |
| [Save](Modules/Save/README.md) | 本地存档读写、加密、脏标记自动保存。 |
| [Res](Modules/Res/README.md) | Addressables 资源加载、实例化、释放和句柄追踪。 |
| [Config](Modules/Config/README.md) | 二进制配置表加载和解析分发。 |
| [Scene](Modules/Scene/README.md) | Addressables 场景加载、切换和卸载。 |
| [Procedure](Modules/Procedure/README.md) | 游戏主流程管理，例如启动、预加载、主菜单、战斗和结算。 |
| [Localization](Modules/Localization/README.md) | 多语言表加载、语言切换、UI 文本/图片刷新。 |
| [UI](Modules/UI/README.md) | UI 注册、打开关闭、层级排序、缓存、Binding Editor 和常用 UI 组件。 |
| [RedPoint](Modules/RedPoint/README.md) | 低耦合红点系统，支持 UI 监听、条件计算和树形聚合。 |
| [Guide](Modules/Guide/README.md) | 配置驱动新手引导，支持精确步骤进度、统一存档、事件触发和可替换引导表现层。 |
| [Audio](Modules/Audio/README.md) | BGM、2D/3D 音效、跟随音效、音量和播放句柄控制。 |
| [FSM](Modules/FSM/README.md) | 泛型有限状态机，适合流程、角色、玩法状态。 |
| [BehaviorTree](Modules/BehaviorTree/README.md) | Behavior Designer 行为树加载、挂载、暂停和释放。 |
| [Network](Modules/Network/README.md) | 基于 UnityWebRequest + UniTask 的 GET/POST JSON 请求。 |
| [EditorTools](Modules/EditorTools/README.md) | Prefab 缺失引用、缺失脚本、Addressables 地址等项目巡检工具。 |
| [Utils](Utils/README.md) | Transform、RectTransform、String、Collection、Random、Time、Color 等低耦合扩展。 |

## 推荐约定

- 初始化顺序交给 `FrameworkEntry` 的显式依赖图管理，不依赖 Unity 脚本执行顺序或魔法优先级数字。
- 资源通过 `GameApp.Res` 加载后，使用对应的 `ReleaseAsset` 或 `ReleaseInstance` 释放。
- UI 内部事件订阅优先使用 `UIFormBase.Subscribe<T>()`，关闭界面时会自动解除。
- 业务状态变化优先广播事件，显示层或条件层监听事件后刷新，减少模块与业务互相调用。
- 红点这类跨界面提示优先使用 `RedPointConditionProvider` 或 `RedPointConditionBadge` 提供叶子条件，父级 UI 只显示聚合结果。
- 新手引导优先使用配置表维护步骤，用 `GuideSignalEvent` 或 `GameApp.Guide.NotifyEvent` 触发，不要在玩法逻辑中直接操作引导 UI。
