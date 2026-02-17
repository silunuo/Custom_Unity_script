// ============================================================================
// SaveManager.cs — 通用存档管理系统
//
// 功能：
//   1. 单例模式，跨场景持久（DontDestroyOnLoad）
//   2. 泛型设计：SaveManager<T> where T : SaveData, new()
//   3. JSON 序列化（Unity JsonUtility）
//   4. 可选 AES-128 加密，防止玩家篡改存档
//   5. 多存档槽位，支持自动存档
//   6. ISaveable 接口支持分布式存档收集/分发
//   7. 存档元数据查询（不加载完整数据即可获取时间、时长等信息）
//   8. 版本迁移钩子
//
// 快速上手：
//   1. 创建 SaveSettings ScriptableObject（Assets → Create → SaveSystem → Save Settings）
//   2. 创建 MyGameData : SaveData 子类，添加游戏数据字段
//   3. 场景中创建空物体，挂载 SaveManager<MyGameData>（需自己写一行继承，见下方说明）
//   4. 调用 SaveManager<MyGameData>.Instance.Save(0) / Load(0)
//
// ⚠ 因为 Unity 不支持直接挂载泛型 MonoBehaviour，需要创建一行具体类：
//   public class GameSaveManager : SaveManager<MyGameData> { }
//   然后将 GameSaveManager 挂载到场景物体上。
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// 通用存档管理器（泛型单例）。
/// T 为游戏自定义的存档数据类型，必须继承 SaveData 且有无参构造函数。
/// </summary>
/// <typeparam name="T">游戏存档数据类型</typeparam>
public abstract class SaveManager<T> : MonoBehaviour where T : SaveData, new()
{
    // ================================================================
    //  单例
    // ================================================================

    /// <summary>全局唯一实例</summary>
    public static SaveManager<T> Instance { get; private set; }

    // ================================================================
    //  事件
    // ================================================================

    /// <summary>保存前触发（参数：槽位索引, 即将保存的数据）</summary>
    public event Action<int, T> OnBeforeSave;

    /// <summary>保存后触发（参数：槽位索引, 是否成功）</summary>
    public event Action<int, bool> OnAfterSave;

    /// <summary>加载后触发（参数：槽位索引, 加载到的数据，失败时为 null）</summary>
    public event Action<int, T> OnAfterLoad;

    /// <summary>删除存档后触发（参数：槽位索引）</summary>
    public event Action<int> OnAfterDelete;

    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 存档配置 ===")]
    [Tooltip("存档设置资产（ScriptableObject）。拖入通过 Assets → Create → SaveSystem → Save Settings 创建的资产")]
    [SerializeField] private SaveSettings settings;

    // ================================================================
    //  运行时状态
    // ================================================================

    /// <summary>当前活跃的存档数据（最近一次 Load 或 New 的结果）</summary>
    public T CurrentData { get; private set; }

    /// <summary>当前活跃的存档槽位索引（-1 = 未加载任何存档）</summary>
    public int CurrentSlot { get; private set; } = -1;

    /// <summary>获取存档设置的只读引用</summary>
    public SaveSettings Settings => settings;

    // 所有已注册的 ISaveable 组件
    private readonly List<ISaveable> _saveables = new List<ISaveable>();

    // 自动存档协程引用
    private Coroutine _autoSaveCoroutine;

    // 游戏时长计时器（加载时开始计时，保存时写入）
    private float _sessionStartTime;

    // ================================================================
    //  版本迁移（子类可重写）
    // ================================================================

    /// <summary>
    /// 版本迁移钩子。当加载的存档 version 低于当前版本时调用。
    /// 子类重写此方法实现数据迁移逻辑。
    /// </summary>
    /// <param name="data">旧版本的存档数据</param>
    /// <param name="fromVersion">存档文件的版本号</param>
    /// <param name="toVersion">当前代码期望的版本号</param>
    protected virtual void MigrateData(T data, int fromVersion, int toVersion)
    {
        // 默认不做任何迁移，子类按需重写
        // 示例：
        //   if (fromVersion < 2) { data.newField = data.oldField * 2; }
        //   if (fromVersion < 3) { data.anotherField = "default"; }
    }

    /// <summary>
    /// 当前代码期望的存档版本号。
    /// 子类通过重写此属性来推动版本迁移。
    /// </summary>
    protected virtual int CurrentVersion => 1;

    // ================================================================
    //  Unity 生命周期
    // ================================================================

    protected virtual void Awake()
    {
        // 单例初始化
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 设置检查
        if (settings == null)
        {
            Debug.LogError("[SaveManager] 未设置 SaveSettings！请在 Inspector 中拖入配置资产。");
            return;
        }

        // 确保存档文件夹存在
        EnsureSaveDirectory();

        // 启动自动存档（如果配置了）
        if (settings.enableAutoSave && settings.autoSaveSlotIndex >= 0)
        {
            StartAutoSave();
        }
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    protected virtual void OnApplicationPause(bool pause)
    {
        // 移动端切后台时自动保存当前存档
        if (pause && CurrentData != null && CurrentSlot >= 0)
        {
            Save(CurrentSlot);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        // 退出时保存当前存档
        if (CurrentData != null && CurrentSlot >= 0)
        {
            Save(CurrentSlot);
        }
    }

    // ================================================================
    //  核心 API — 保存
    // ================================================================

    /// <summary>
    /// 将当前数据保存到指定槽位。
    /// 自动更新元数据（时间戳、游戏时长），收集 ISaveable 数据，
    /// 序列化为 JSON 并写入文件（可选加密）。
    /// </summary>
    /// <param name="slotIndex">目标槽位索引（从 0 开始）</param>
    /// <returns>是否保存成功</returns>
    public bool Save(int slotIndex)
    {
        // 参数校验
        if (settings == null)
        {
            Debug.LogError("[SaveManager] SaveSettings 未配置！");
            return false;
        }

        if (slotIndex < 0 || slotIndex >= settings.maxSlots)
        {
            Debug.LogError($"[SaveManager] 槽位索引越界：{slotIndex}（最大 {settings.maxSlots - 1}）");
            return false;
        }

        if (CurrentData == null)
        {
            Debug.LogError("[SaveManager] 没有活跃的存档数据！请先调用 NewGame() 或 Load()。");
            return false;
        }

        try
        {
            // 更新游戏时长
            CurrentData.playTimeSeconds += (Time.realtimeSinceStartup - _sessionStartTime);
            _sessionStartTime = Time.realtimeSinceStartup; // 重置计时起点

            // 更新时间戳
            if (string.IsNullOrEmpty(CurrentData.createdAt))
            {
                CurrentData.MarkAsNew(); // 首次保存
            }
            else
            {
                CurrentData.MarkAsUpdated(); // 后续保存
            }

            // 设置版本号
            CurrentData.version = CurrentVersion;

            // 收集所有 ISaveable 组件的数据
            CollectSaveableData();

            // 触发保存前事件（外部系统可在此时修改数据）
            OnBeforeSave?.Invoke(slotIndex, CurrentData);

            // 序列化为 JSON
            string json = JsonUtility.ToJson(CurrentData, true); // true = 格式化输出

            // ⚠ JsonUtility 不支持 Dictionary 序列化，需要额外处理
            // 如果 moduleData 有内容，使用自定义序列化
            if (CurrentData.moduleData != null && CurrentData.moduleData.Count > 0)
            {
                json = SerializeWithDictionary(CurrentData);
            }

            // 可选加密
            if (settings.enableEncryption)
            {
                json = Encrypt(json);
            }

            // 写入文件
            string filePath = settings.GetSaveFilePath(slotIndex);
            File.WriteAllText(filePath, json, Encoding.UTF8);

            CurrentSlot = slotIndex;

            if (settings.enableDebugLog)
            {
                Debug.Log($"[SaveManager] 已保存到槽位 {slotIndex}：{filePath}");
            }

            // 触发保存后事件
            OnAfterSave?.Invoke(slotIndex, true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] 保存失败（槽位 {slotIndex}）：{ex.Message}\n{ex.StackTrace}");
            OnAfterSave?.Invoke(slotIndex, false);
            return false;
        }
    }

    // ================================================================
    //  核心 API — 加载
    // ================================================================

    /// <summary>
    /// 从指定槽位加载存档数据。
    /// 反序列化文件内容，执行版本迁移（如需要），分发数据到 ISaveable 组件。
    /// 加载成功后 CurrentData 和 CurrentSlot 会被更新。
    /// </summary>
    /// <param name="slotIndex">目标槽位索引</param>
    /// <returns>加载到的存档数据，失败返回 null</returns>
    public T Load(int slotIndex)
    {
        if (settings == null)
        {
            Debug.LogError("[SaveManager] SaveSettings 未配置！");
            return null;
        }

        if (slotIndex < 0 || slotIndex >= settings.maxSlots)
        {
            Debug.LogError($"[SaveManager] 槽位索引越界：{slotIndex}");
            return null;
        }

        string filePath = settings.GetSaveFilePath(slotIndex);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[SaveManager] 槽位 {slotIndex} 无存档文件：{filePath}");
            return null;
        }

        try
        {
            // 读取文件
            string json = File.ReadAllText(filePath, Encoding.UTF8);

            // 可选解密
            if (settings.enableEncryption)
            {
                json = Decrypt(json);
            }

            // 反序列化
            T data = DeserializeWithDictionary(json);

            // 版本迁移
            if (data.version < CurrentVersion)
            {
                if (settings.enableDebugLog)
                {
                    Debug.Log($"[SaveManager] 检测到旧版存档 v{data.version} → v{CurrentVersion}，执行迁移...");
                }
                MigrateData(data, data.version, CurrentVersion);
                data.version = CurrentVersion;
            }

            // 设置为当前活跃数据
            CurrentData = data;
            CurrentSlot = slotIndex;
            _sessionStartTime = Time.realtimeSinceStartup;

            // 分发数据到 ISaveable 组件
            DistributeSaveableData();

            if (settings.enableDebugLog)
            {
                Debug.Log($"[SaveManager] 已从槽位 {slotIndex} 加载存档（游戏时长：{data.GetFormattedPlayTime()}）");
            }

            // 触发加载后事件
            OnAfterLoad?.Invoke(slotIndex, data);
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] 加载失败（槽位 {slotIndex}）：{ex.Message}\n{ex.StackTrace}");
            OnAfterLoad?.Invoke(slotIndex, null);
            return null;
        }
    }

    // ================================================================
    //  核心 API — 新建存档
    // ================================================================

    /// <summary>
    /// 创建一份全新的存档数据并设为当前活跃数据。
    /// 不会自动写入文件，需手动调用 Save() 保存。
    /// </summary>
    /// <param name="displayName">存档显示名称（可选）</param>
    /// <returns>新创建的存档数据实例</returns>
    public T NewGame(string displayName = "")
    {
        CurrentData = new T();
        CurrentData.MarkAsNew();
        CurrentData.displayName = displayName;
        CurrentData.version = CurrentVersion;
        CurrentSlot = -1; // 尚未绑定槽位
        _sessionStartTime = Time.realtimeSinceStartup;

        if (settings.enableDebugLog)
        {
            Debug.Log("[SaveManager] 已创建新存档");
        }

        return CurrentData;
    }

    // ================================================================
    //  核心 API — 删除存档
    // ================================================================

    /// <summary>
    /// 删除指定槽位的存档文件。
    /// 如果删除的是当前活跃槽位，会清除 CurrentData。
    /// </summary>
    /// <param name="slotIndex">目标槽位索引</param>
    /// <returns>是否删除成功</returns>
    public bool Delete(int slotIndex)
    {
        if (settings == null) return false;

        string filePath = settings.GetSaveFilePath(slotIndex);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[SaveManager] 槽位 {slotIndex} 不存在，无需删除");
            return false;
        }

        try
        {
            File.Delete(filePath);

            // 如果删除的是当前活跃存档，清除引用
            if (slotIndex == CurrentSlot)
            {
                CurrentData = null;
                CurrentSlot = -1;
            }

            if (settings.enableDebugLog)
            {
                Debug.Log($"[SaveManager] 已删除槽位 {slotIndex} 的存档");
            }

            OnAfterDelete?.Invoke(slotIndex);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] 删除失败（槽位 {slotIndex}）：{ex.Message}");
            return false;
        }
    }

    // ================================================================
    //  查询 API
    // ================================================================

    /// <summary>
    /// 检查指定槽位是否存在存档文件。
    /// </summary>
    public bool HasSave(int slotIndex)
    {
        if (settings == null) return false;
        return File.Exists(settings.GetSaveFilePath(slotIndex));
    }

    /// <summary>
    /// 获取指定槽位的存档元信息（不加载完整数据，仅读取基础字段）。
    /// 适合在存档选择界面展示每个槽位的概要信息。
    /// </summary>
    /// <param name="slotIndex">目标槽位索引</param>
    /// <returns>存档元信息，槽位不存在返回 null</returns>
    public SaveSlotInfo GetSlotInfo(int slotIndex)
    {
        if (!HasSave(slotIndex)) return null;

        try
        {
            string json = File.ReadAllText(settings.GetSaveFilePath(slotIndex), Encoding.UTF8);

            if (settings.enableEncryption)
            {
                json = Decrypt(json);
            }

            // 用基类反序列化，只读取元数据字段，避免加载完整游戏数据
            SaveData baseData = JsonUtility.FromJson<SaveData>(json);

            return new SaveSlotInfo
            {
                slotIndex = slotIndex,
                displayName = baseData.displayName,
                saveTime = baseData.GetFormattedSaveTime(),
                playTime = baseData.GetFormattedPlayTime(),
                version = baseData.version,
                exists = true
            };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] 读取槽位 {slotIndex} 元信息失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取所有槽位的元信息数组。
    /// 不存在存档的槽位返回 exists = false 的空信息。
    /// </summary>
    public SaveSlotInfo[] GetAllSlotInfos()
    {
        var infos = new SaveSlotInfo[settings.maxSlots];
        for (int i = 0; i < settings.maxSlots; i++)
        {
            infos[i] = GetSlotInfo(i) ?? new SaveSlotInfo
            {
                slotIndex = i,
                exists = false
            };
        }
        return infos;
    }

    /// <summary>
    /// 删除所有存档文件。慎用！
    /// </summary>
    public void DeleteAll()
    {
        for (int i = 0; i < settings.maxSlots; i++)
        {
            if (HasSave(i)) Delete(i);
        }
        CurrentData = null;
        CurrentSlot = -1;

        if (settings.enableDebugLog)
        {
            Debug.Log("[SaveManager] 已删除所有存档");
        }
    }

    // ================================================================
    //  ISaveable 注册
    // ================================================================

    /// <summary>
    /// 注册一个 ISaveable 组件。
    /// 建议在组件的 OnEnable 中调用。
    /// </summary>
    public void Register(ISaveable saveable)
    {
        if (saveable == null) return;

        // 防止重复注册
        if (!_saveables.Any(s => s.SaveID == saveable.SaveID))
        {
            _saveables.Add(saveable);

            if (settings != null && settings.enableDebugLog)
            {
                Debug.Log($"[SaveManager] 已注册 ISaveable：{saveable.SaveID}");
            }
        }
    }

    /// <summary>
    /// 注销一个 ISaveable 组件。
    /// 建议在组件的 OnDisable 中调用。
    /// </summary>
    public void Unregister(ISaveable saveable)
    {
        if (saveable == null) return;
        _saveables.RemoveAll(s => s.SaveID == saveable.SaveID);
    }

    // ================================================================
    //  自动存档
    // ================================================================

    /// <summary>启动自动存档协程</summary>
    public void StartAutoSave()
    {
        if (_autoSaveCoroutine != null) StopCoroutine(_autoSaveCoroutine);
        _autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
    }

    /// <summary>停止自动存档协程</summary>
    public void StopAutoSave()
    {
        if (_autoSaveCoroutine != null)
        {
            StopCoroutine(_autoSaveCoroutine);
            _autoSaveCoroutine = null;
        }
    }

    /// <summary>自动存档协程</summary>
    private IEnumerator AutoSaveCoroutine()
    {
        // 缓存 WaitForSeconds 避免 GC
        WaitForSeconds wait = new WaitForSeconds(settings.autoSaveInterval);

        while (true)
        {
            yield return wait;

            // 仅在有活跃数据时自动保存
            if (CurrentData != null)
            {
                int targetSlot = settings.autoSaveSlotIndex >= 0
                    ? settings.autoSaveSlotIndex
                    : CurrentSlot;

                if (targetSlot >= 0)
                {
                    Save(targetSlot);

                    if (settings.enableDebugLog)
                    {
                        Debug.Log($"[SaveManager] 自动存档完成（槽位 {targetSlot}）");
                    }
                }
            }
        }
    }

    // ================================================================
    //  导入/导出（云存档支持）
    // ================================================================

    /// <summary>
    /// 将指定槽位的存档导出为 JSON 字符串（明文）。
    /// 适合上传到云端。
    /// </summary>
    public string ExportToJson(int slotIndex)
    {
        if (!HasSave(slotIndex)) return null;

        string json = File.ReadAllText(settings.GetSaveFilePath(slotIndex), Encoding.UTF8);

        // 如果是加密的，解密后导出明文
        if (settings.enableEncryption)
        {
            json = Decrypt(json);
        }

        return json;
    }

    /// <summary>
    /// 从 JSON 字符串导入存档到指定槽位。
    /// 适合从云端下载后恢复。
    /// </summary>
    /// <param name="slotIndex">目标槽位</param>
    /// <param name="json">明文 JSON 字符串</param>
    /// <returns>是否导入成功</returns>
    public bool ImportFromJson(int slotIndex, string json)
    {
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            // 验证 JSON 有效性
            JsonUtility.FromJson<SaveData>(json);

            // 如需加密，加密后写入
            string content = settings.enableEncryption ? Encrypt(json) : json;
            File.WriteAllText(settings.GetSaveFilePath(slotIndex), content, Encoding.UTF8);

            if (settings.enableDebugLog)
            {
                Debug.Log($"[SaveManager] 已导入存档到槽位 {slotIndex}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] 导入失败：{ex.Message}");
            return false;
        }
    }

    // ================================================================
    //  ISaveable 数据收集与分发
    // ================================================================

    /// <summary>遍历所有已注册的 ISaveable，收集数据到 CurrentData.moduleData</summary>
    private void CollectSaveableData()
    {
        foreach (var saveable in _saveables)
        {
            try
            {
                string json = saveable.OnSave();
                if (json != null)
                {
                    CurrentData.moduleData[saveable.SaveID] = json;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 收集 {saveable.SaveID} 数据失败：{ex.Message}");
            }
        }
    }

    /// <summary>将 CurrentData.moduleData 中的数据分发到对应的 ISaveable</summary>
    private void DistributeSaveableData()
    {
        foreach (var saveable in _saveables)
        {
            try
            {
                // 从字典中查找该模块的数据
                CurrentData.moduleData.TryGetValue(saveable.SaveID, out string json);
                saveable.OnLoad(json); // json 可能为 null，组件自行处理
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 分发 {saveable.SaveID} 数据失败：{ex.Message}");
            }
        }
    }

    // ================================================================
    //  序列化（支持 Dictionary）
    // ================================================================

    // JsonUtility 不支持 Dictionary，这里用包装类实现

    /// <summary>字典序列化包装：单个键值对</summary>
    [Serializable]
    private class DictEntry
    {
        public string key;
        public string value;
    }

    /// <summary>字典序列化包装：键值对列表</summary>
    [Serializable]
    private class DictWrapper
    {
        public List<DictEntry> entries = new List<DictEntry>();
    }

    /// <summary>
    /// 自定义序列化：先用 JsonUtility 序列化主体，再手动嵌入 Dictionary 数据。
    /// </summary>
    private string SerializeWithDictionary(T data)
    {
        // 临时保存字典并清空（JsonUtility 会忽略 Dictionary）
        var dictBackup = new Dictionary<string, string>(data.moduleData);
        data.moduleData.Clear();

        // 序列化主体
        string mainJson = JsonUtility.ToJson(data, true);

        // 恢复字典
        data.moduleData = dictBackup;

        // 将字典转为可序列化的列表
        var wrapper = new DictWrapper();
        foreach (var kvp in dictBackup)
        {
            wrapper.entries.Add(new DictEntry { key = kvp.Key, value = kvp.Value });
        }
        string dictJson = JsonUtility.ToJson(wrapper);

        // 在主 JSON 结束大括号前插入字典数据
        mainJson = mainJson.TrimEnd().TrimEnd('}');
        mainJson += $",\n    \"__moduleData__\": {dictJson}\n}}";

        return mainJson;
    }

    /// <summary>
    /// 自定义反序列化：先用 JsonUtility 反序列化主体，再手动解析 Dictionary。
    /// </summary>
    private T DeserializeWithDictionary(string json)
    {
        // 先用 JsonUtility 反序列化（会忽略 __moduleData__）
        T data = JsonUtility.FromJson<T>(json);

        // 手动提取 __moduleData__ 部分
        if (data.moduleData == null)
        {
            data.moduleData = new Dictionary<string, string>();
        }

        // 查找 __moduleData__ 在 JSON 中的位置
        string marker = "\"__moduleData__\":";
        int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);

        if (markerIndex >= 0)
        {
            int startIndex = markerIndex + marker.Length;
            string dictPart = ExtractJsonObject(json, startIndex);

            if (!string.IsNullOrEmpty(dictPart))
            {
                var wrapper = JsonUtility.FromJson<DictWrapper>(dictPart);
                if (wrapper?.entries != null)
                {
                    foreach (var entry in wrapper.entries)
                    {
                        data.moduleData[entry.key] = entry.value;
                    }
                }
            }
        }

        return data;
    }

    /// <summary>
    /// 从 JSON 字符串的指定位置提取一个完整的 JSON 对象（匹配大括号层级）。
    /// </summary>
    private string ExtractJsonObject(string json, int startIndex)
    {
        int i = startIndex;
        while (i < json.Length && json[i] != '{') i++;
        if (i >= json.Length) return null;

        int depth = 0;
        int objStart = i;

        for (; i < json.Length; i++)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}') depth--;

            if (depth == 0)
            {
                return json.Substring(objStart, i - objStart + 1);
            }
        }

        return null;
    }

    // ================================================================
    //  AES 加密/解密
    // ================================================================

    /// <summary>使用 AES-128 加密字符串。</summary>
    private string Encrypt(string plainText)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(settings.encryptionKey);
        byte[] ivBytes = Encoding.UTF8.GetBytes(settings.encryptionIV);

        using (Aes aes = Aes.Create())
        {
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform encryptor = aes.CreateEncryptor();

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    /// <summary>使用 AES-128 解密字符串。</summary>
    private string Decrypt(string cipherText)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(settings.encryptionKey);
        byte[] ivBytes = Encoding.UTF8.GetBytes(settings.encryptionIV);
        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform decryptor = aes.CreateDecryptor();

            using (MemoryStream ms = new MemoryStream(cipherBytes))
            using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (StreamReader sr = new StreamReader(cs, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }
    }

    // ================================================================
    //  文件系统工具
    // ================================================================

    /// <summary>确保存档文件夹存在</summary>
    private void EnsureSaveDirectory()
    {
        string dir = settings.GetSaveFolderPath();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);

            if (settings.enableDebugLog)
            {
                Debug.Log($"[SaveManager] 已创建存档目录：{dir}");
            }
        }
    }

    /// <summary>获取存档目录的完整路径（方便调试时在文件管理器中查看）</summary>
    public string GetSaveDirectoryPath()
    {
        return settings?.GetSaveFolderPath() ?? "未配置";
    }
}

// ============================================================================
// SaveSlotInfo — 存档槽位元信息
// 用于在存档选择界面展示每个槽位的概要信息，无需加载完整游戏数据。
// ============================================================================

/// <summary>
/// 存档槽位的概要信息（轻量级，不包含完整游戏数据）。
/// </summary>
[Serializable]
public class SaveSlotInfo
{
    /// <summary>槽位索引</summary>
    public int slotIndex;

    /// <summary>该槽位是否存在存档</summary>
    public bool exists;

    /// <summary>存档显示名称</summary>
    public string displayName;

    /// <summary>格式化的保存时间（如 "2025/03/15 14:30"）</summary>
    public string saveTime;

    /// <summary>格式化的游戏时长（如 "02:35:17"）</summary>
    public string playTime;

    /// <summary>存档版本号</summary>
    public int version;
}
