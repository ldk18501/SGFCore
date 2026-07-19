# 架构与启动

## 目录

- 源码确认
- 依赖与命名空间
- 标准启动
- 内建模块依赖图
- 自定义模块
- 异步与失败语义
- 模块选择

## 源码确认

先从当前 Unity 项目根目录搜索：

```text
SGFCore.Runtime.asmdef
Base/FrameworkEntry.cs
Base/GameApp.cs
Modules/*/README.md
```

如果源码被放在 Package、Git submodule 或自定义目录，所有后续路径都以实际根目录为准。优先读取当前模块的 `.cs` 与 README；不要只凭本引用猜签名。

## 依赖与命名空间

当前 `SGFCore.Runtime.asmdef` 直接依赖：

```text
UniTask
UniTask.Addressables
PrimeTween.Runtime
BehaviorDesigner.Runtime
Unity.Addressables
Unity.ResourceManager
Unity.TextMeshPro
```

业务程序集至少引用 `SGFCore.Runtime`；直接使用 `UniTask` 时还要引用 `UniTask`。Editor 代码必须放在 Editor 程序集或 Editor 目录，不能泄漏到 Runtime。

常用命名空间：

| 命名空间 | 内容 |
| --- | --- |
| `GameFramework.Core` | 框架入口、GameApp、绝大多数模块和事件 |
| `GameFramework.Core.UI` | UIForm、UILayer、UI 组件、本地化 UI、红点 Badge |
| `GameFramework.Core.Utility` | 扩展方法和通用工具 |
| `GameFramework.Editor` | SGFCore 编辑器工具 |

配置生成类默认没有命名空间；项目可在 `ConfigExportSettings.namespaceName` 中指定。调用前检查当前项目设置。当前内建 `LocalizationModule` 直接引用全局 `LanguageConf`，因此语言表应保持默认全局命名空间；若要统一迁入项目命名空间，必须同步修改本地化运行时代码或增加明确 adapter。

## 标准启动

在唯一 Bootstrap 中执行：

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Core;
using UnityEngine;

public sealed class GameEntry : MonoBehaviour
{
    [SerializeField] private FrameworkConfig _frameworkConfig;

    private void Start()
    {
        StartAsync(this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
    }

    private async UniTask StartAsync(CancellationToken token)
    {
        bool ready = await FrameworkEntry.Instance.InitFrameworkModulesAsync(
            _frameworkConfig,
            token);
        if (!ready)
        {
            Log.Error("框架初始化失败或被取消。");
            return;
        }

        GameApp.Procedure.Start(
            this,
            new ProcedureLaunch(),
            new ProcedurePreload(),
            new ProcedureMainMenu());
    }
}
```

规则：

- 只保留一个业务启动入口。
- 框架返回 Ready 后 Addressables 已初始化，普通业务无需再次调用 `EnsureInitializedAsync`。
- 显式退出或测试重启时优先 `await FrameworkEntry.Instance.ShutdownFrameworkAsync(token)`。
- `FrameworkEntry` 会自动创建为常驻 MonoSingleton，但正式场景可显式放置它以便检查配置。
- `FrameworkConfig` 当前可在启动时配置 Crypto Key/IV；密钥必须是项目自有值。
- 当前源码在全部模块 `OnInit/OnInitAsync` 完成后才应用 `FrameworkConfig` 的 Crypto 配置。自定义模块不得在 `OnInit` 中读取加密存档或要求 `GameApp.Crypto.IsInitialized`；把这类业务初始化放到框架 Ready 之后的 Preload，或提供显式 `InitializeAfterPreload`。

## 内建模块依赖图

`FrameworkEntry` 使用稳定拓扑排序初始化，销毁时逆序执行：

| 模块 | 直接依赖 |
| --- | --- |
| Log | 无 |
| FileSystem | Log |
| Event | Log |
| Pool | Log |
| Timer | Pool |
| Time | Event |
| Crypto | Log |
| Save | FileSystem、Crypto、Timer、Event |
| Resource | Log |
| Scene | Resource、Event |
| Config | Resource、Event |
| Localization | Config、Event |
| RedPoint | Event |
| UI | Resource、Event |
| Guide | Save、Event、Localization |
| Audio | Resource、Pool、Event |
| FSM | Log |
| Procedure | Event |
| BehaviorTree | Resource |
| Http | Event |

不要使用脚本执行顺序或自定义数字 Priority 模拟这些依赖。

## 自定义模块

实现生命周期接口：

```csharp
public sealed class InventoryModule : IFrameworkModule
{
    public void OnInit() { }
    public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
    public void OnDestroy() { }
}
```

需要等待准备或释放时实现 `IAsyncFrameworkModule`。在初始化开始前注册：

```csharp
FrameworkEntry.Instance.RegisterModule(
    new InventoryModule(),
    typeof(ConfigModule),
    typeof(SaveModule),
    typeof(EventModule));
```

依赖必须使用实现 `IFrameworkModule` 的具体模块类型，不能缺失、依赖自身或构成循环。自定义模块没有自动加入 `GameApp`；项目可创建自己的业务门面，或在 composition root 缓存 `GetModule<InventoryModule>()`。

## 异步与失败语义

- 初始化取消：`InitFrameworkModulesAsync` 返回 `false`，并回滚已经初始化的模块。
- 初始化异常：框架回滚后重新抛出异常；Bootstrap 应记录异常并停止业务。
- 模块关闭：异步销毁先执行，随后同步 `OnDestroy`，整体按初始化逆序。
- `OnInit` 只完成模块结构、依赖引用和轻量状态准备；依赖配置表、加密存档或业务资源的数据装载放在框架 Ready 之后。
- Unity 对象相关异步必须绑定生命周期 token；不要使用无法取消的 `async void`，Unity 事件入口除外且必须内部捕获异常。
- 对资源、配置、场景、网络等使用 `Try*` 或 Result 结构；成功后再推进 Procedure。

## 模块选择

| 需求 | 使用 |
| --- | --- |
| 游戏大阶段 | Procedure |
| 角色/玩法局部状态 | FSM |
| 模块间通知 | Event |
| Addressables 数据资源/Prefab | Resource |
| Addressables 场景 | Scene |
| Excel 静态数据 | Config |
| 本地持久化状态 | Save |
| 短延迟或循环回调 | Timer |
| 服务器时间、跨天、离线时长 | Time |
| UI 界面生命周期 | UI |
| 条件聚合提示 | RedPoint |
| 配置驱动新手引导 | Guide |
| BGM/SFX | Audio |
| JSON HTTP | Http |
| 外部 Behavior Designer 树 | BT |
