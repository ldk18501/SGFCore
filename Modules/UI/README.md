# UI 模块使用说明

UI 模块负责界面注册、异步打开关闭、层级排序、单例界面、缓存和生命周期管理。

## 场景准备

场景中需要有 `UIRoot`，它维护不同 UI 层级节点。`UIModule` 初始化时会查找场景中的 `UIRoot` 并设置为常驻。

## 注册界面

```csharp
GameApp.UI.RegisterUI(
    formId: 1001,
    address: "UI/MainMenu",
    type: typeof(MainMenuForm),
    layer: UILayer.Normal,
    isSingleton: true,
    isCached: true);
```

## 打开和关闭

```csharp
int serialId = await GameApp.UI.OpenUIAsync(1001, arg1, arg2);
GameApp.UI.CloseUI(serialId);

MainMenuForm form = await GameApp.UI.OpenUIAsync<MainMenuForm>();
GameApp.UI.CloseUI<MainMenuForm>();
```

如果需要等待退场动画：

```csharp
await GameApp.UI.CloseUIAsync(serialId);
```

## 界面生命周期

界面脚本继承 `UIFormBase`：

```csharp
public class MainMenuForm : UIFormBase
{
    public override void OnInit() { }
    public override void OnOpen(params object[] args) { }
    public override void OnClose() { }
    public override void OnDestroyUI() { }
}
```

## UI 内资源和事件

`UIFormBase` 提供自动清理能力：

```csharp
Subscribe<GoldChangedEvent>(OnGoldChanged);

Sprite icon = await LoadAssetAsync<Sprite>("UI/IconCoin");
GameObject item = await InstantiateAsync("UI/Item", itemRoot);
```

关闭 UI 会自动退订事件，销毁 UI 会自动释放通过这些接口加载的资源。

## 动画

在 UI 子节点挂 `UITweenElement`，打开/关闭界面时会自动播放入场/退场动画。

重复使用的动画参数可创建 `UITweenProfile` ScriptableObject，并在多个 `UITweenElement` 上引用。元素会在布局完成后捕获位置、缩放和透明度基线，避免重复开关后逐渐漂移。

正式界面数据推荐实现 `IUIFormData` 并继承 `UIFormBase<TData>`，代替无类型 `params object[]`。单例界面的并发打开请求会合并；关闭会取消尚未完成的入场动画并临时关闭射线交互。缓存数量由 `UIConfig.MaxCachedInstances` 控制。

## UI Binding Editor

UI Binding Editor 用来减少手动拖引用和运行时 `Find`。业务界面脚本需要继承 `UIFormBase`，并声明为 `partial class`：

```csharp
public partial class MainMenuForm : UIFormBase
{
    public override void OnOpen(params object[] args)
    {
        m_StartButton.onClick.AddListener(OnStartClicked);
    }
}
```

推荐流程：

1. 打开 UI Prefab，选中需要绑定的子节点。
2. 右键 `SGFCore/UI Binding/Add Private`，选择要绑定的组件类型。
3. 在 `UIFormBase` Inspector 中点击 `生成绑定代码`。
4. 等 Unity 编译完成后，点击 `绑定引用`。
5. 点击 `校验` 检查重复字段名、命名不规范、目标为空、组件缺失等问题。

生成文件默认放在 `Assets/Scripts/UIBindings`，可在 Inspector 的 `生成目录` 中调整。生成结果类似：

```csharp
public partial class MainMenuForm
{
    [Space(10)]
    [Header("UI Binding")]
    [SerializeField] private Button m_StartButton;
}
```

右键菜单还支持：

- `Add Protected`：生成 `protected` 字段，给派生类使用。
- `Add Public`：生成 `public` 字段，少用，只适合需要外部显式访问的 UI。
- `Add Header`：在生成代码中插入 `[Header]` 分组。
- `Remove Selected`：从绑定记录中移除当前选中节点。
- `Generate Binding Code` / `Bind References` / `Validate`：不打开 Inspector 时也能直接操作。
- `Generate And Bind`：适合脚本已经生成过的 Prefab 快速刷新引用；首次生成仍建议等 Unity 编译后再绑定。

批处理菜单：

```text
Tools/SGFCore/UI Binding/Validate All UI Prefabs
Tools/SGFCore/UI Binding/Generate All Binding Code
Tools/SGFCore/UI Binding/Bind All Prefab References
Tools/SGFCore/UI Binding/Validate And Generate All
```

注意事项：

- 生成代码依赖 `partial class`，否则同名类拆分文件无法编译。
- 多选同类型节点会生成数组字段，例如 `Image[] m_IconArray`。
- 绑定记录只保存到 UI Prefab 上，不会污染业务代码。

## 通用 UI 组件

`Components` 目录提供一组可直接挂在 UI Prefab 上的小组件：

- `UIRaycastArea`：无绘制开销的点击区域，可替代透明 `Image`。
- `UIButtonSound`：按钮点击音效，默认播放 `UIButtonSound.DefaultClickSoundAddress`。
- `UIButtonCooldown`：点击后短时间禁用按钮，防止重复点击。
- `UIHoldRepeatButton`：长按重复触发，适合加减数量、连续升级。
- `UINumberCounter`：数字滚动显示，支持 `Text` 和 `TMP_Text`，可输出原始数字、单位缩写或自定义格式。
- `UIStepProgressBar`：多段式进度条，适合奖励节点、章节进度、分段血条。
- `UIVirtualList`：固定尺寸 Item 的虚拟滚动列表，适合背包、任务、商店、排行。
- `UINetImage`：网络图片组件，支持占位图、失败图、取消请求和静态缓存。
- `UIModalOverlay`：弹窗遮罩点击行为。
- `UIToast`：轻量 Toast 展示器。
- `UILoadingOverlay`：通用 Loading 遮罩。
- `UIConfirmDialog`：通用确认弹窗绑定脚本。

### 按钮音效

```csharp
UIButtonSound.DefaultClickSoundAddress = "UI_Click";
```

给按钮预制体挂 `UIButtonSound` 后，点击时会调用 `GameApp.Audio.PlaySFX`。如果不同按钮要使用不同音效，关闭 `Use Default Address` 并填写 `Sound Address`。

### 长按重复

`UIHoldRepeatButton` 可以直接重复调用 Button 的 `onClick`，也可以关闭 `Invoke Button On Repeat`，改用组件自己的 `OnRepeat` 事件。复杂按钮建议把单击和长按逻辑分开，避免松手时 Unity Button 再触发一次普通点击。

### 数字滚动

```csharp
counter.SetValue(coinCount);
counter.SetValueImmediately(0);
```

`Format Mode` 设为 `Unit` 时会复用 `NumberExtension.ToUnitString`，适合放置类游戏的大数字显示。

## 世界 UI 跟随

`Follow` 目录提供两种世界 UI 绑定：

- `UIBindTrs`：跟随一个动态 `Transform`。
- `UIBindPos`：跟随一个固定世界坐标。

它们都支持：

- `Hide When Outside Viewport`：目标离开屏幕时隐藏。
- `Clamp To Parent Rect`：目标离开屏幕时夹到父节点边缘。
- `Parent Padding`：夹边时保留安全边距。
- `Interactable When Visible` / `Blocks Raycasts When Hidden`：同步控制 `CanvasGroup` 的交互状态。

## 虚拟滚动列表

`UIVirtualList` 用于大量固定尺寸 Item。它不魔改 `ScrollRect`，只要求：

- 节点上有 `ScrollRect`。
- `ScrollRect.content` 指向 Content。
- Content 下有一个 Item 模板。
- Item 高宽固定，列表通过回调刷新内容。

代码接入：

```csharp
[SerializeField] private UIVirtualList _list;

private readonly List<ItemData> _items = new List<ItemData>();

private void Awake()
{
    _list.SetItemRenderer(RefreshItem);
}

private void RefreshList()
{
    _list.SetDataCount(_items.Count, resetPosition: true);
}

private void RefreshItem(int index, GameObject item)
{
    ItemView view = item.GetComponent<ItemView>();
    view.Refresh(_items[index]);
}
```

Inspector 里常用配置：

- `Layout Mode`：`Vertical`、`Horizontal`、`VerticalGrid`。
- `Item Size`：Item 固定尺寸。
- `Spacing` / `Padding`：间距和边距。
- `Constraint Count`：`VerticalGrid` 的列数。
- `Extra Buffer`：视口外额外保留的缓存行，减少快速滑动时的闪烁。

如果不想写代码回调，也可以在 `On Refresh Item` 里用 Inspector 绑定事件。

## 网络图片

`UINetImage` 挂在带 `Image` 的节点上：

```csharp
_avatarImage.Load(player.AvatarUrl);
```

支持：

- `Placeholder Sprite`：请求中显示。
- `Error Sprite`：失败时显示。
- `Use Cache`：相同 URL 复用已下载 Sprite。
- `Cancel On Disable`：节点隐藏时取消未完成请求。
- `Set Native Size`：下载完成后按图片原始尺寸设置 UI。

缓存清理：

```csharp
UINetImage.RemoveCache(url);
UINetImage.ClearCache();
```

## 弹窗体验组件

### 遮罩点击关闭

给弹窗背景遮罩挂 `UIModalOverlay`。遮罩节点需要有可接收射线的 `Graphic`，例如 `Image` 或 `UIRaycastArea`；如果节点没有 Graphic，组件 Reset 时会自动补一个无绘制开销的 `UIRaycastArea`。点击后可选择：

- `None`：只派发事件。
- `SetTargetInactive`：隐藏目标对象。
- `DestroyTarget`：销毁目标对象。
- `CloseUIForm`：调用 `GameApp.UI.CloseUI` 关闭所在 `UIFormBase`。

### Toast

在某个常驻 UI 节点上挂 `UIToast`，拖好 `CanvasGroup` 和文本引用：

```csharp
UIToast.ShowGlobal("金币不足");
UIToast.Instance.Show("升级成功", 1.2f);
UIToast.Instance.ShowImmediately("网络断开");
```

`Show` 会排队显示，`ShowImmediately` 会清空队列并立刻显示新内容。

### Loading

在 Loading 遮罩 Prefab 上挂 `UILoadingOverlay`：

```csharp
UILoadingOverlay.ShowGlobal("加载中...");
UILoadingOverlay.Instance.SetProgress(0.5f);
UILoadingOverlay.HideGlobal();
```

默认支持引用计数：多个流程同时 `Show` 时，需要对应次数 `Hide` 后才会真正隐藏。需要强制清理时调用：

```csharp
UILoadingOverlay.Instance.ForceHide();
```

### ConfirmDialog

确认弹窗 Prefab 上挂 `UIConfirmDialog`，拖好标题、正文、确认/取消/关闭按钮：

```csharp
_confirmDialog.Configure(
    "退出关卡",
    "当前进度尚未保存，确定退出吗？",
    onConfirm: ExitLevel,
    onCancel: null,
    confirmText: "退出",
    cancelText: "继续");
```

按钮也可以直接在 Inspector 的 `On Confirm` / `On Cancel` 里绑定逻辑。关闭行为由 `Close Action` 控制，可隐藏自身、销毁对象或关闭所在 UIForm。
