// ============================================================================
// SaveSettings.cs — 存档系统配置（ScriptableObject）
//
// 在 Unity 中通过 Assets → Create → SaveSystem → Save Settings 创建。
// 集中管理存档路径、加密开关、自动存档等全局配置。
// ============================================================================

using UnityEngine;

/// <summary>
/// 存档系统的全局配置。
/// 通过 ScriptableObject 实现，不同项目/平台可创建不同的配置资产。
/// </summary>
[CreateAssetMenu(fileName = "SaveSettings", menuName = "SaveSystem/Save Settings")]
public class SaveSettings : ScriptableObject
{
    // ===== 存档路径 =====

    [Header("存档路径")]
    [Tooltip("存档文件夹名称（位于 Application.persistentDataPath 下）")]
    public string saveFolderName = "SaveData";

    [Tooltip("存档文件扩展名（不含点号）")]
    public string fileExtension = "sav";

    [Tooltip("存档文件名前缀，最终文件名格式：{prefix}_slot{N}.{ext}")]
    public string filePrefix = "save";

    // ===== 存档槽位 =====

    [Header("存档槽位")]
    [Tooltip("最大存档槽位数量")]
    [Range(1, 99)]
    public int maxSlots = 5;

    [Tooltip("自动存档使用的槽位索引（-1 = 不使用自动存档）")]
    [Range(-1, 98)]
    public int autoSaveSlotIndex = 0;

    // ===== 自动存档 =====

    [Header("自动存档")]
    [Tooltip("是否启用自动存档")]
    public bool enableAutoSave = false;

    [Tooltip("自动存档间隔（秒），仅在 enableAutoSave = true 时生效")]
    [Range(30f, 600f)]
    public float autoSaveInterval = 120f;

    // ===== 加密 =====

    [Header("加密（可选）")]
    [Tooltip("是否启用 AES 加密。开启后存档文件为密文，防止玩家手动修改")]
    public bool enableEncryption = false;

    [Tooltip("AES 加密密钥（必须恰好 16 个字符 = 128 位）。请更换为自己的密钥！")]
    public string encryptionKey = "YourKey16Chars!!";

    [Tooltip("AES 初始化向量（必须恰好 16 个字符）。请更换为自己的 IV！")]
    public string encryptionIV = "YourIV_16Chars!!";

    // ===== 调试 =====

    [Header("调试")]
    [Tooltip("是否在控制台输出存档操作日志")]
    public bool enableDebugLog = true;

    // ===== 辅助方法 =====

    /// <summary>
    /// 获取完整的存档文件夹路径。
    /// </summary>
    public string GetSaveFolderPath()
    {
        return System.IO.Path.Combine(Application.persistentDataPath, saveFolderName);
    }

    /// <summary>
    /// 获取指定槽位的完整文件路径。
    /// </summary>
    /// <param name="slotIndex">槽位索引（从 0 开始）</param>
    public string GetSaveFilePath(int slotIndex)
    {
        // 格式：SaveData/save_slot0.sav
        string fileName = $"{filePrefix}_slot{slotIndex}.{fileExtension}";
        return System.IO.Path.Combine(GetSaveFolderPath(), fileName);
    }
}
