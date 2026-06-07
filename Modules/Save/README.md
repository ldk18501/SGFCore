# Save 模块使用说明

Save 模块负责本地存档读写，默认保存到 `persistentDataPath/Saves/{Slot}`，支持加密、版本号、数据迁移、多存档槽、模块化存档和脏标记自动存档。

## 定义存档

```csharp
[Serializable]
public class PlayerSaveData : SaveDataBase
{
    public int Gold;

    public PlayerSaveData()
    {
        SaveVersion = 2;
        SaveModuleName = "Player";
    }

    public void AddGold(int value)
    {
        Gold += value;
        MarkDirty();
    }
}
```

## 多槽位

```csharp
GameApp.Save.SetCurrentSlot("Slot_1");
PlayerSaveData data = GameApp.Save.LoadData<PlayerSaveData>("Player");
GameApp.Save.SaveData("Player", data);

string[] slots = GameApp.Save.GetSlots();
GameApp.Save.DeleteSlot("Slot_1");
```

也可以显式传槽位：

```csharp
GameApp.Save.SaveData("Slot_2", "Player", data);
PlayerSaveData slot2Data = GameApp.Save.LoadData<PlayerSaveData>("Slot_2", "Player");
```

## 模块化存档

```csharp
GameApp.Save.SaveModuleData("Inventory", inventorySave);
InventorySaveData data = GameApp.Save.LoadModuleData<InventorySaveData>("Inventory");
```

模块化接口会把文件名整理为 `Module_{ModuleName}.sav`，适合后续玩法模块独立保存。

## 版本迁移

```csharp
public sealed class PlayerSaveMigration : ISaveDataMigration<PlayerSaveData>
{
    public int TargetVersion => 2;

    public void Migrate(PlayerSaveData data, int fromVersion)
    {
        if (fromVersion < 2)
        {
            data.Gold = Math.Max(0, data.Gold);
        }
    }
}

GameApp.Save.RegisterMigration(new PlayerSaveMigration());
```

读取存档时如果旧版本小于 `TargetVersion`，模块会调用迁移逻辑并广播 `SaveDataMigratedEvent`。

## 自动存档和事件

`SaveDataBase` 支持脏标记。读取存档后，如果开启自动存档，`SaveModule` 会用 `TimerModule` 定时检查 dirty 状态，只在数据变化后写盘。

```csharp
data.IsAutoSaveEnabled = true;
data.AutoSaveInterval = 30f;
```

事件：

```csharp
SaveDataLoadedEvent
SaveDataSavedEvent
SaveDataDirtyEvent
SaveDataMigratedEvent
SaveDataRecoveredEvent
```

## 损坏恢复策略

默认开启：

```csharp
GameApp.Save.EnableBackupOnSave = true;
GameApp.Save.EnableCorruptFileQuarantine = true;
```

每次覆盖主存档前，模块会把旧主档复制成：

```text
Player.sav.bak
```

读档时如果主档解密失败、JSON 损坏或反序列化为空，会自动尝试读取 `.bak`。备份恢复成功后：

- 损坏主档会被移动为 `Player.corrupt_yyyyMMddHHmmss.sav`。
- `.bak` 会复制回主档路径。
- 模块会广播 `SaveDataRecoveredEvent`。

如果主档和备份都不可用，才会创建新存档对象，避免游戏直接崩溃。

## 注意事项

- 存档类要能被 `JsonUtility` 序列化，字段通常使用 public 字段或 `[SerializeField]`。
- 修改数据后记得调用 `MarkDirty()`，否则自动存档不会触发。
- 复杂引用关系不要直接塞进存档，建议存 ID 和基础数据。
