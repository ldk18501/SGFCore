---
name: sgf-unity-game-dev
description: 基于 SGFCore 开发、重构、审查和调试 Unity 游戏功能的工程规范与接口指南。用于涉及 FrameworkEntry/GameApp、模块生命周期、自定义模块、UniTask、Addressables 资源与场景、Excel 配置表导出、Localization、本地存档、UI 与 UI Binding、事件、对象池、定时器、时间、音频、HTTP、FSM、Procedure、红点、Guide、Behavior Designer 或 SGFCore 编辑器工具的任务；也用于新项目接入 SGFCore、检查错误调用、资源泄漏和配置管线问题。
---

# SGF Unity 游戏开发

## 执行总流程

1. 先读取当前项目及上级目录中的 `AGENTS.md`，遵守项目约束。
2. 定位当前项目实际使用的 SGFCore 源码。优先搜索 `SGFCore.Runtime.asmdef`、`Base/FrameworkEntry.cs`、`Base/GameApp.cs` 和 `Modules/*/README.md`。不要假设框架一定位于 `Assets/SGFCore`。
3. 读取 [架构与启动](references/architecture-and-bootstrap.md)，确认依赖、命名空间、初始化入口、模块边界和异步生命周期。
4. 根据任务只读取相关引用：
   - 配置表或多语言：读取 [配置表与本地化](references/config-and-localization.md)。
   - Addressables、场景或音频：读取 [资源、场景与音频](references/resources-scenes-audio.md)。
   - UI、绑定、弹窗或通用 UI 组件：读取 [UI 开发](references/ui-development.md)。
   - 存档、事件、文件、网络、池、时间等：读取 [数据与基础服务](references/data-and-services.md)。
   - Procedure、FSM、红点、引导或行为树：读取 [玩法系统](references/gameplay-systems.md)。
   - 编辑器自动化、导表、Addressables 管线或验收：读取 [编辑器与验证](references/editor-and-validation.md)。
   - 需要快速查找模块入口、API 名称或源码位置：读取 [API 索引](references/api-index.md)。
5. 修改前先检查调用方 asmdef、目标 Prefab/Scene/Addressables 配置和现有业务范式；尽量复用项目常量、数据模型与注册入口。
6. 实现后按风险验证编译、资源地址、配置加载结果、生命周期清理和对应的 Unity Editor 校验项。

## 硬性规范

- 以当前项目源码为最终事实。引用文档是本仓库 2026-07 快照；若签名或行为与实际源码不同，以实际源码为准并在结果中说明差异。
- 使用 `await FrameworkEntry.Instance.InitFrameworkModulesAsync(...)` 等待框架 Ready 后再启动业务。不要在新代码中调用已过时的同步入口。
- 业务层优先通过 `GameApp` 门面访问内建模块；模块组装放在 composition root，不在业务类中散落 `GetModule`。
- 为可取消的异步流程传递 `CancellationToken`。MonoBehaviour 优先使用 `GetCancellationTokenOnDestroy()`；Procedure 必须观察 `OnEnterAsync` 的 token。
- 对外部输入、资源、配置、场景和网络优先使用返回结果的 `Try*`/`*Result` API，并显式处理失败。不要让失败静默变成默认值后继续流程。
- 严格维护所有权：`LoadAssetAsync` 对应 `ReleaseAsset`，`InstantiateAsync` 对应 `ReleaseInstance`，Addressables 实例不得直接 `Destroy`。局部流程优先使用 `ResourceScope`。
- 不在业务流程调用全局 `ResourceModule.ReleaseAll`；它只用于框架退出或诊断兜底。
- 配置导出的 `*ConfigGenerated.cs` 禁止手改；业务扩展只写同名 `partial` 的 `*ConfigExt.cs`。配置名称、Addressables 地址和语言后缀必须一致且集中管理。
- 新 UI 优先使用 `UIFormBase<TData>` 和 `IUIFormData`；UI 内事件优先用 `Subscribe<T>`；动态资源优先用 UIForm 提供的受管接口。
- 全局事件使用 `struct`。非 UI 监听者必须成对调用 `AddListener`/`RemoveListener`，并在禁用、离开状态或销毁时解除。
- Procedure 只管理游戏大阶段；局部角色或玩法状态使用 FSM；短暂 UI 状态不要塞进 Procedure。
- 存档默认加密。使用加密前必须配置有效密钥；明确不需要加密时显式传 `useEncryption: false`。任何密钥、令牌和真实服务地址都不得硬编码进公开仓库。
- 不混用 SGFCore 管理的生命周期和绕过框架的原生调用。例如不要直接卸载 SGFCore 追踪的场景、销毁 Addressables 实例或自行写入 SGFCore 存档目录。

## 实现取舍

- 新增框架能力时，先判断能否作为项目业务模块实现；只有跨项目通用、生命周期明确且依赖关系可声明的能力才进入 SGFCore 核心。
- 自定义模块实现 `IFrameworkModule` 或 `IAsyncFrameworkModule`，在框架初始化开始前注册，并显式声明直接依赖的具体模块类型。
- 用强类型数据和 `BlackboardKey<T>` 代替长期存在的字符串键；资源地址、配置名、UI FormId、红点 path、引导 targetKey 统一定义在项目常量或生成代码中。
- 保持 `GameFramework.Core`、`GameFramework.Core.UI`、`GameFramework.Core.Utility` 的现有边界；不要为了少写 `using` 把业务类型放进框架命名空间。
- 发现文档示例与当前推荐行为冲突时，优先采用源码中未过时、带结果和支持取消的接口。

## 完成标准

- 代码引用正确程序集与命名空间，Unity/C# 编译通过。
- 框架初始化、异步取消、失败分支和关闭路径可执行。
- 每一次动态资源获取都有清晰 owner 和释放点。
- 配置表完成注册、导出、Addressables 地址设置和失败检查；生成文件未被手改。
- UI 注册、层级、缓存上限、关闭方式和绑定引用正确。
- 事件、定时器、音频句柄、行为树、红点 provider 或 Guide installer 在生命周期结束时清理。
- 运行 `Tools/SGFCore/Validation/Run Project Validation`，并执行与改动相关的 EditMode/PlayMode 或集成冒烟测试。
