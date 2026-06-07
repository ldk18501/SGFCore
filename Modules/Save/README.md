# Save 模块使用说明

Save 模块负责本地存档读写，默认保存到 `persistentDataPath/Saves`，并支持加密和脏标记自动存档。

## 定义存档

```csharp
[Serializable]
public class PlayerSaveData : SaveDataBase
{
    public int Gold;

    public void AddGold(int value)
    {
        Gold += value;
        MarkDirty();
    }
}
```

## 保存和读取

```csharp
PlayerSaveData data = GameApp.Save.LoadData<PlayerSaveData>("Player");
data.AddGold(100);

GameApp.Save.SaveData("Player", data);
```

默认开启加密。如果是调试期想查看 JSON，可以传 `false`：

```csharp
GameApp.Save.SaveData("Player_Debug", data, useEncryption: false);
```

## 存档管理

```csharp
bool hasSave = GameApp.Save.HasSave("Player");
string path = GameApp.Save.GetSaveFilePath("Player");
GameApp.Save.DeleteSave("Player");
```

## 自动存档

`SaveDataBase` 支持脏标记。读取存档后，如果开启自动存档，`SaveModule` 会用 `TimerModule` 定时检查 dirty 状态，只在数据变化后写盘。

## 注意事项

- 存档类要能被 `JsonUtility` 序列化，字段通常使用 public 字段或 `[SerializeField]`。
- 修改数据后记得调用 `MarkDirty()`，否则自动存档不会触发。
- 复杂引用关系不要直接塞进存档，建议存 ID 和基础数据。
