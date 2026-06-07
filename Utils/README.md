# Utils / Extensions 使用说明

Utils 目录只保留低耦合、高频、无业务污染的工具。

## 当前内容

| 文件 | 说明 |
| --- | --- |
| `TransformExtension.cs` | 子节点销毁、递归设置 Layer、本地坐标快捷设置、递归查找。 |
| `RectTransformExtension.cs` | UI 拉伸到父节点、锚点坐标快捷设置、屏幕点命中判断。 |
| `CollectionExtension.cs` | 洗牌、随机元素、安全取值、空集合判断。 |
| `NumberExtension.cs` | 放置类常用数字缩写。 |
| `StringExtension.cs` | 字符串安全处理、大小写命名转换、忽略大小写比较。 |
| `RandomUtility.cs` | 概率判断、闭区间随机、权重随机。 |
| `ColorExtension.cs` | Alpha 修改、HTML 颜色转换。 |
| `TimeUtility.cs` | Unix 时间、标准时间格式、每日刷新辅助。 |

## 约定

- 工具方法不直接依赖业务模块。
- 热路径避免 LINQ 和不必要分配。
- 与 Unity 类型强相关的扩展放在 Unity 对应类型文件里，例如 `RectTransformExtension`。
