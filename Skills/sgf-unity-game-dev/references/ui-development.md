# UI 开发

## 目录

- 场景与注册
- 强类型 UI
- 生命周期
- 缓存和并发
- 事件、异步与资源
- UI Binding
- 通用组件
- 联动规范

## 场景与注册

启动场景必须存在一个有效 `UIRoot`，并配置：

```text
RootCanvas / UICamera
Background / Common / Popup / Top / Guide / System
```

`UIModule` 初始化时查找 `UIRoot` 并设为常驻。缺少层节点会退化到 Root 并记录错误，不能作为正式配置。

集中定义 FormId 和地址并注册：

```csharp
public static class UIIds
{
    public const int MainMenu = 1001;
    public const int RewardPopup = 2001;
}

GameApp.UI.RegisterUI(
    UIIds.MainMenu,
    "UI/MainMenu",
    typeof(MainMenuForm),
    UILayer.Common,
    isSingleton: true,
    isCached: true,
    maxCachedInstances: 1);
```

Prefab 必须：

- 是 Addressables 实例。
- 挂载注册时指定的 `UIFormBase` 子类。
- 具备 `Canvas` 和 `GraphicRaycaster`；基类有 RequireComponent。
- 位于正确的 UI asmdef 可见范围。

## 强类型 UI

新界面使用：

```csharp
public sealed class RewardPopupData : IUIFormData
{
    public int RewardId;
    public int Count;
}

public partial class RewardPopupForm : UIFormBase<RewardPopupData>
{
    protected override void OnOpen(RewardPopupData data)
    {
        Refresh(data.RewardId, data.Count);
    }

    public override void OnClose()
    {
        CancelPerOpenWork();
        base.OnClose();
    }
}
```

打开和关闭：

```csharp
RewardPopupForm form = await GameApp.UI.OpenUIAsync<RewardPopupForm, RewardPopupData>(
    new RewardPopupData { RewardId = id, Count = count });

if (form != null)
{
    await GameApp.UI.CloseUIAsync(form.SerialId);
}
```

保留无类型 `params object[]` 只用于兼容旧界面。Typed Form 的参数必须恰好一个且类型正确，否则打开失败。Typed Form 覆盖 `OnClose` 时调用 `base.OnClose()`，确保 `Data` 清空。

## 生命周期

| 回调 | 时机 | 放置内容 |
| --- | --- | --- |
| `Awake` | Unity 实例创建 | 本地组件缓存，不访问尚未就绪业务 |
| `OnInit` | 全新 UI 实例初始化一次 | 固定监听器、不可变结构初始化 |
| `OnOpen` | 每次显示或单例重复打开 | 刷新参数、订阅框架事件、开始本次任务 |
| `OnClose` | 每次关闭，退场动画前 | 停止本次任务、保存临时 UI 状态 |
| `OnDestroyUI` | 实例彻底销毁 | 释放非框架托管内容 |

`CloseUI` 是 fire-and-forget；调用方需要等待退场动画和缓存/销毁完成时使用 `CloseUIAsync`。

不要从外部直接 `SetActive(false)` 或 `Destroy(form.gameObject)`；必须由 `UIModule` 维护活跃列表、排序、缓存和 Addressables 实例句柄。

## 缓存和并发

- 单例界面的并发打开会合并为一个任务；后续调用可能再次触发 `OnOpen` 刷新参数。
- 非单例界面允许多个实例，缓存时必须设置明确上限；默认上限为 3。
- `isCached: true` 的关闭会把实例移到隐藏池，不执行 `OnDestroyUI`。
- 缓存达到上限或 `isCached: false` 时才彻底销毁。
- 关闭会立即禁用交互并取消当前过渡动画，避免快速连点竞态。

选择缓存：

- 高频、创建成本高、内容可完全重置：缓存。
- 低频、占内存大、持有大量动态资源：不缓存。
- 非单例弹窗：给出小而明确的 `maxCachedInstances`。

## 事件、异步与资源

界面事件优先：

```csharp
protected override void OnOpen(InventoryFormData data)
{
    Subscribe<ItemChangedEvent>(OnItemChanged);
}
```

关闭时 `Subscribe<T>` 注册的事件会自动解除。同一事件类型在一次打开周期只能订阅一次；若需要多个处理器，组合为一个处理函数。

UnityEvent、Button.onClick、第三方回调和静态 C# 事件不属于 `Subscribe<T>` 的托管范围。缓存界面会反复 OnOpen/OnClose，必须在 `OnClose` 成对移除，避免重复触发。

UIForm 提供受管资源：

```csharp
Sprite icon = await LoadAssetAsync<Sprite>(iconAddress);
GameObject item = await InstantiateAsync(itemAddress, itemRoot);
```

这些资源在 UI 彻底销毁时释放，不是在缓存关闭时释放。因此：

- 适合和 UI 实例同生命周期的资源。
- 不要由外部再次释放。
- 每次 OnOpen 重复加载会重复持有句柄；应缓存字段并用一次性异步初始化任务守卫。`OnInit` 是同步回调，不要改成无法管理异常的 `async void`。
- 只属于一次打开周期的耗时任务，界面自己创建 CTS，在 OnClose 取消；基类资源 token 只在实例销毁时取消。

## UI Binding

业务类声明为 `partial`：

```csharp
public partial class MainMenuForm : UIFormBase<MainMenuData>
{
    protected override void OnOpen(MainMenuData data)
    {
        m_StartButton.onClick.AddListener(OnStartClicked);
    }

    public override void OnClose()
    {
        m_StartButton.onClick.RemoveListener(OnStartClicked);
        base.OnClose();
    }
}
```

推荐操作：

1. 打开 UI Prefab，选中待绑定子节点。
2. 使用 `GameObject/SGFCore/UI Binding/Add Private`；只有派生类需要时用 Protected，尽量不用 Public。
3. 生成绑定代码。
4. 等 Unity 编译完成。
5. 绑定引用。
6. 运行 Validate。

首次生成不要直接依赖 `Generate And Bind` 跨过编译阶段。默认生成目录是 `Assets/Scripts/UIBindings`，可在 UIForm Inspector 修改。生成文件只保存字段声明，绑定记录保存在 Prefab；业务行为写在手工 partial 文件。

批处理菜单：

```text
Tools/SGFCore/UI Binding/Validate All UI Prefabs
Tools/SGFCore/UI Binding/Generate All Binding Code
Tools/SGFCore/UI Binding/Bind All Prefab References
Tools/SGFCore/UI Binding/Validate And Generate All
```

避免运行时 `Transform.Find`；字段改名或层级变化后重新生成、绑定和校验。

## 通用组件

| 组件 | 用途 | 关键规则 |
| --- | --- | --- |
| `UITweenElement/Profile` | 入退场动效 | 复用 Profile，布局完成后捕获 baseline |
| `UIRaycastArea` | 无绘制点击区域 | 替代透明 Image |
| `UIButtonSound` | 点击音效 | 统一 Default address 或显式覆盖 |
| `UIButtonCooldown` | 防重复点击 | 用于提交、跳转等不可重入操作 |
| `UIHoldRepeatButton` | 长按重复 | 区分普通 click 与 repeat |
| `UINumberCounter` | 数字滚动/大数缩写 | 可复用 `NumberExtension` |
| `UIStepProgressBar` | 多段进度 | 明确 segment 权重 |
| `UIVirtualList` | 固定尺寸大列表 | renderer 只刷新复用 item，不保存错误 index |
| `UINetImage` | 网络图片 | 支持取消、占位、错误图和静态缓存 |
| `UIModalOverlay` | 遮罩点击行为 | 用 UIRaycastArea 接收射线 |
| `UIToast` | 排队/立即提示 | 常驻实例统一调用 |
| `UILoadingOverlay` | 引用计数 Loading | Show/Hide 次数必须对称，异常兜底 ForceHide |
| `UIConfirmDialog` | 确认弹窗 | 明确 CloseAction 和回调清理 |
| `UIBindTrs/UIBindPos` | 世界 UI 跟随 | 配置 Camera、越界隐藏/夹边和 CanvasGroup |
| `SafeAreaController` | 安全区 | 真机验证横竖屏和刘海屏 |

## 联动规范

- 文本/图片本地化优先用 `LocalizedText`、`LocalizedTextTmp`、`LocalizedImage`。
- 红点显示用 `RedPointBadge`；条件最好在 Provider 或 ConditionBadge 中维护。
- 引导目标挂 `GuideTarget`，使用稳定 targetKey，不依赖节点层级路径。
- UI 点击音效走 `GameApp.Audio`；网络提交显示 Loading 并处理 Result。
- 列表 item 如来自 Addressables，不要与 UIVirtualList 的普通模板克隆混用不同释放路径。

源码入口：`Modules/UI/UIModule.cs`、`UIFormBase.cs`、`UIFormData.cs`、`UIRoot.cs`、`Editor/*`、`Components/*`。
