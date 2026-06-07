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
- `Generate Binding Code` / `Bind References`：不打开 Inspector 时也能直接操作。

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
