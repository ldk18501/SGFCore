# 编辑器与验证

## 目录

- 修改前检查
- 配置导出
- Addressables 工具
- UI Binding
- 项目巡检
- 测试矩阵
- 交付检查

## 修改前检查

1. 定位真实 SGFCore 根目录和 Unity 项目根目录。
2. 检查 `SGFCore.Runtime.asmdef` 与调用方 asmdef 引用。
3. 检查 Unity Console 是否已有编译错误；先区分既有错误和本次错误。
4. 检查目标资源是否受 Addressables 自动目录规则管理。
5. 检查待改 Prefab/Scene 是否有用户未保存更改；编辑器自动化前先保存或明确保留。
6. 搜索现有注册点、地址常量、FormId、配置表名、红点 path 和 Guide targetKey，避免创建第二套入口。

## 配置导出

菜单：

```text
Tools/Framework/配置表一键导出
```

导出前：

- 关闭占用 Excel 的外部工具，或确认导出器以共享读方式可读取。
- 检查四行表头、客户端标记、类型、字段名和唯一主键。
- 多语言表检查 schema、行数、key 及非 value 字段一致。
- 检查 `ConfigExportSettings` 的输入、生成、扩展、bytes、命名空间和 Addressables Group。

导出后：

- 只接受 Generated 的自动差异，不手改。
- 检查 Ext 未被覆盖。
- 检查 bytes 地址与运行时常量一致。
- Unity 编译完成后运行一次 Preload 加载并检查 `ConfigBatchLoadResult`。
- 对配置协议或导出器改动做旧表回归；生成代码和运行时解码必须同版本。

## Addressables 工具

自动规则：`AddressableAutoBuilder` 只监听 `Assets/ResAddressable`，按相对目录创建 Group，并使用文件名（无扩展名）作为 address。

目录菜单：

```text
Assets/Addressables 管线/1. 一键检查并补全所有缺失的 Group
Assets/Addressables 管线/2. 一键清理空 Group 与失效引用
```

规则：

- 运行清理前确认 Group 不是外部流程暂时为空但仍需保留。
- 自动文件名 address 不能保证唯一，必须运行重复检查。
- 编辑器中看到 entry 不等于 Player 包含最新内容；按项目发布流程 Build/Update Addressables Content。
- 场景、UI Prefab、AudioClip、Config bytes、语言 Sprite 和 ExternalBehaviorTree 都检查实际 address。
- 不把 Editor-only 资产放入 Runtime Group。

## UI Binding

单 Prefab 使用 `GameObject/SGFCore/UI Binding/*`：Add、Generate、Bind、Validate。批量使用：

```text
Tools/SGFCore/UI Binding/Validate All UI Prefabs
Tools/SGFCore/UI Binding/Generate All Binding Code
Tools/SGFCore/UI Binding/Bind All Prefab References
Tools/SGFCore/UI Binding/Validate And Generate All
```

执行顺序：记录 binding -> Generate -> 等待 Unity 编译 -> Bind -> Validate。首次生成不要在编译前强行绑定新字段。检查：

- 业务类为 `partial`。
- 生成类和业务类命名空间完全一致。
- 字段名无重复，目标不为空，组件存在。
- 多选生成数组时顺序符合 hierarchy 和业务预期。
- Prefab 保存后再做批量校验。

## 项目巡检

入口：

```text
Tools/SGFCore/Validation/Run Project Validation
```

当前覆盖：

- Prefab Missing Script。
- Prefab 序列化 Missing Reference。
- Addressables 重复 address。
- Runtime 模块依赖图。

也可单独运行：

```text
Tools/SGFCore/Validation/Validate Runtime Module Graph
Tools/SGFCore/Validation/Find Missing Scripts And References
Tools/SGFCore/Validation/Check Addressables Addresses
```

校验通过不代表资源能在目标平台加载；还需要 Player/PlayMode 冒烟。

## 测试矩阵

按改动选择最小充分集合：

| 改动 | 必测 |
| --- | --- |
| 框架启动/模块 | 冷启动、取消初始化、重复启动、显式 Shutdown、依赖图 |
| Resource | 成功/缺失/取消、重复加载释放、实例释放、scope Dispose、快照基线 |
| Config | 正常导出、错误表头/重复 key、单表失败、批量部分失败、跨表索引 |
| Localization | 偏好恢复、快速连续切换、目标缺失 fallback、文本/图片刷新 |
| UI | 单例并发打开、快速开关、缓存上限、关闭动画、受管资源、Binding |
| Save | 新档、加密/明文、缺密钥、迁移、原子写、备份恢复、损坏隔离、多槽 |
| Scene | Single/Additive、延迟激活、重复并发、取消、卸载、切换中 Shutdown |
| Audio | BGM 切换、循环 SFX 清理、并发抢占、静音/音量持久化 |
| Http | 成功、4xx、5xx、超时、取消、GET 重试、POST 不重试、反序列化失败 |
| Procedure/FSM | 重入切换、异步取消、OnLeave 清理、黑板类型错误 |
| RedPoint | 多 owner、父级聚合、batch、provider 销毁、UI 关闭后状态 |
| Guide | 定义校验、target 延迟出现、事件触发、完成/跳过、进度存档、切场景 |
| BT | 加载失败、owner 提前销毁、Detach、全局暂停恢复 |

仓库自带集成 Demo 时可参考 `Demo/Integration`。当前构建菜单：

```text
SGFCore/Demo/Build Integration Demo
SGFCore/Demo/Open Integration Demo Scene
```

不要把 Demo 的硬编码地址、无加密存档或简化 UI 直接复制到生产项目。

## 交付检查

- Unity Console 无新增编译错误和异常。
- 新程序集引用最小且方向正确，Editor/Runtime 隔离。
- 新资源 address 唯一，目标平台内容已构建。
- 没有手改 Generated 文件。
- 没有未释放资源、未取消 timer、未解绑事件、未停止循环音频或未 Detach 行为树。
- 失败路径不会继续推进 Procedure 或覆盖旧存档。
- 改动说明列出配置/Prefab/Scene/Addressables 等非代码步骤。
