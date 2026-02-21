# 💾 SaveManager — Unity 通用存档系统

> 一套开箱即用的 Unity 存档解决方案。支持多槽位、自动存档、AES 加密、版本迁移、分布式存档收集。
> 适用于 2D / 3D、任何游戏类型。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `SaveData.cs` | 存档数据基类 + 序列化辅助结构体（Vector3/Vector2/Color） |
| `SaveSettings.cs` | ScriptableObject 全局配置（路径、加密、自动存档等） |
| `ISaveable.cs` | 分布式存档接口（各模块独立管理自己的数据） |
| `SaveManager.cs` | 核心管理器（泛型单例，读写/加密/槽位/自动存档） |

---

## 🚀 快速上手（5 分钟接入）

### 第一步：定义你的游戏数据

```csharp
// MyGameData.cs
[System.Serializable]
public class MyGameData : SaveData
{
    public int playerLevel = 1;
    public float playerHP = 100f;
    public int gold = 0;
    public List<string> inventory = new List<string>();
    public Vector3Serializable playerPosition;
    public string currentScene = "Level1";
}
```

### 第二步：创建具体的 SaveManager

```csharp
// GameSaveManager.cs
// Unity 不支持直接挂载泛型 MonoBehaviour，所以需要这一行具体类
public class GameSaveManager : SaveManager<MyGameData> { }
```

### 第三步：创建 SaveSettings

1. 在 Project 窗口右键 → `Create → SaveSystem → Save Settings`
2. 在 Inspector 中按需配置参数（大多数保持默认即可）

### 第四步：场景配置

1. 创建空物体，命名为 `SaveManager`
2. 挂载 `GameSaveManager` 组件
3. 将 SaveSettings 资产拖入 Inspector 的 Settings 字段

### 第五步：开始使用

```csharp
// 新建游戏
var data = GameSaveManager.Instance.NewGame("我的冒险");
data.playerLevel = 1;
GameSaveManager.Instance.Save(0);

// 加载存档
var loaded = GameSaveManager.Instance.Load(0);
Debug.Log($"等级：{loaded.playerLevel}");

// 更新并保存
GameSaveManager.Instance.CurrentData.gold += 100;
GameSaveManager.Instance.Save(0);
```

---

## 📋 完整 API 参考

### 核心操作

| 方法 | 签名 | 说明 |
|------|------|------|
| `NewGame` | `T NewGame(string displayName = "")` | 创建新存档数据（不写入文件） |
| `Save` | `bool Save(int slotIndex)` | 保存到指定槽位 |
| `Load` | `T Load(int slotIndex)` | 从指定槽位加载 |
| `Delete` | `bool Delete(int slotIndex)` | 删除指定槽位 |
| `DeleteAll` | `void DeleteAll()` | 删除所有存档 |

### 查询

| 方法 | 签名 | 说明 |
|------|------|------|
| `HasSave` | `bool HasSave(int slotIndex)` | 槽位是否有存档 |
| `GetSlotInfo` | `SaveSlotInfo GetSlotInfo(int slotIndex)` | 获取槽位元信息（不加载完整数据） |
| `GetAllSlotInfos` | `SaveSlotInfo[] GetAllSlotInfos()` | 获取所有槽位元信息 |

### 自动存档

| 方法 | 签名 | 说明 |
|------|------|------|
| `StartAutoSave` | `void StartAutoSave()` | 启动自动存档协程 |
| `StopAutoSave` | `void StopAutoSave()` | 停止自动存档 |

### 云存档支持

| 方法 | 签名 | 说明 |
|------|------|------|
| `ExportToJson` | `string ExportToJson(int slotIndex)` | 导出明文 JSON（上传云端） |
| `ImportFromJson` | `bool ImportFromJson(int slotIndex, string json)` | 从 JSON 导入（下载恢复） |

### ISaveable 注册

| 方法 | 签名 | 说明 |
|------|------|------|
| `Register` | `void Register(ISaveable saveable)` | 注册可存档组件 |
| `Unregister` | `void Unregister(ISaveable saveable)` | 注销可存档组件 |

### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `SaveManager<T>` | 全局单例 |
| `CurrentData` | `T` | 当前活跃存档数据 |
| `CurrentSlot` | `int` | 当前槽位索引（-1 = 未加载） |
| `Settings` | `SaveSettings` | 存档设置引用 |

### 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| `OnBeforeSave` | `Action<int, T>` | 保存前（槽位, 数据） |
| `OnAfterSave` | `Action<int, bool>` | 保存后（槽位, 是否成功） |
| `OnAfterLoad` | `Action<int, T>` | 加载后（槽位, 数据/null） |
| `OnAfterDelete` | `Action<int>` | 删除后（槽位） |

---

## 🔧 常见使用场景

### 存档选择界面

```csharp
SaveSlotInfo[] slots = GameSaveManager.Instance.GetAllSlotInfos();

foreach (var slot in slots)
{
    if (slot.exists)
        Debug.Log($"槽位{slot.slotIndex}: {slot.displayName} | {slot.playTime} | {slot.saveTime}");
    else
        Debug.Log($"槽位{slot.slotIndex}: 空");
}
```

### ISaveable 分布式存档

```csharp
public class InventoryManager : MonoBehaviour, ISaveable
{
    public string SaveID => "Inventory";
    private List<string> items = new List<string>();

    [Serializable]
    private class InventorySaveData { public List<string> items; }

    private void OnEnable() => GameSaveManager.Instance?.Register(this);
    private void OnDisable() => GameSaveManager.Instance?.Unregister(this);

    public string OnSave()
    {
        return JsonUtility.ToJson(new InventorySaveData { items = items });
    }

    public void OnLoad(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<InventorySaveData>(json);
        items = data.items ?? new List<string>();
    }
}
```

### 版本迁移

```csharp
public class GameSaveManager : SaveManager<MyGameData>
{
    protected override int CurrentVersion => 3;

    protected override void MigrateData(MyGameData data, int fromVersion, int toVersion)
    {
        if (fromVersion < 2) { data.gold = 0; }
        if (fromVersion < 3) { /* 其他迁移... */ }
        Debug.Log($"存档已从 v{fromVersion} 迁移到 v{toVersion}");
    }
}
```

### 监听存档事件

```csharp
void Start()
{
    GameSaveManager.Instance.OnAfterSave += (slot, success) =>
    {
        if (success) ShowToast("存档成功！");
    };

    GameSaveManager.Instance.OnAfterLoad += (slot, data) =>
    {
        if (data != null) SceneManager.LoadScene(data.currentScene);
    };
}
```

### 云存档

```csharp
// 上传
string json = GameSaveManager.Instance.ExportToJson(0);
await CloudService.Upload("save_slot_0", json);

// 下载
string cloudJson = await CloudService.Download("save_slot_0");
GameSaveManager.Instance.ImportFromJson(0, cloudJson);
```

---

## ⚙️ SaveSettings 配置说明

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `saveFolderName` | "SaveData" | 存档文件夹名 |
| `fileExtension` | "sav" | 文件扩展名 |
| `filePrefix` | "save" | 文件名前缀 |
| `maxSlots` | 5 | 最大槽位数 |
| `autoSaveSlotIndex` | 0 | 自动存档槽位（-1 禁用） |
| `enableAutoSave` | false | 是否启用自动存档 |
| `autoSaveInterval` | 120s | 自动存档间隔 |
| `enableEncryption` | false | 是否加密 |
| `encryptionKey` | (默认值) | AES 密钥（**必须更换！**） |
| `encryptionIV` | (默认值) | AES IV（**必须更换！**） |
| `enableDebugLog` | true | 是否输出日志 |

### 存档文件位置

| 平台 | 路径 |
|------|------|
| Windows | `%USERPROFILE%/AppData/LocalLow/{公司名}/{产品名}/SaveData/` |
| macOS | `~/Library/Application Support/{公司名}/{产品名}/SaveData/` |
| Android | `/data/data/{包名}/files/SaveData/` |
| iOS | `Application/Documents/SaveData/` |

---

## ❓ FAQ

**Q：为什么不直接用 PlayerPrefs？**
A：PlayerPrefs 有大小限制（WebGL 约 1MB），不支持复杂数据结构，且不同平台存储位置不一致。文件系统更灵活可靠。

**Q：为什么需要单独写 `GameSaveManager : SaveManager<MyGameData>`？**
A：Unity 的序列化系统不支持直接挂载泛型 MonoBehaviour。这一行继承是必要的妥协。

**Q：加密安全吗？**
A：AES-128 对防止普通玩家手动修改存档足够了。但密钥嵌在客户端代码中，理论上可以被逆向。如需更高安全性，建议服务端校验。

**Q：支持 WebGL 吗？**
A：WebGL 的 `Application.persistentDataPath` 使用 IndexedDB，File.IO 操作可能有限制。WebGL 平台建议使用 PlayerPrefs 或 IndexedDB JS 插件替代。

**Q：自动存档会卡顿吗？**
A：一般不会。对于合理大小的存档（< 1MB），耗时通常在几毫秒内。

**Q：如何处理存档损坏？**
A：Load() 内部有 try-catch，损坏的文件会返回 null。可以在 OnAfterLoad 事件中检测并提示玩家。

---

## 📜 版本历史

| 版本 | 说明 |
|------|------|
| v1.0 | 初始版本：多槽位、AES 加密、自动存档、ISaveable、版本迁移、云存档导入导出 |
