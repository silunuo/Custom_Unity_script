// ============================================================================
// SaveData.cs — 存档数据基类
//
// 所有游戏存档数据的基类。使用时创建子类并添加游戏特有的字段。
//
// 使用示例：
//   [System.Serializable]
//   public class MyGameData : SaveData
//   {
//       public int playerLevel = 1;
//       public float playerHP = 100f;
//       public List<string> inventory = new List<string>();
//       public Vector3Serializable playerPosition;
//   }
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存档数据基类。
/// 包含所有存档共有的元数据，游戏特有数据通过继承添加。
/// </summary>
[Serializable]
public class SaveData
{
    // ===== 元数据（SaveManager 自动维护，无需手动设置） =====

    /// <summary>存档数据版本号，用于版本迁移</summary>
    public int version = 1;

    /// <summary>存档创建时间（UTC，ISO 8601 格式字符串）</summary>
    public string createdAt;

    /// <summary>最后保存时间（UTC，ISO 8601 格式字符串）</summary>
    public string updatedAt;

    /// <summary>累计游戏时长（秒）</summary>
    public double playTimeSeconds;

    /// <summary>存档显示名称（可选，用于 UI 展示，如 "第三章 - Lv.25"）</summary>
    public string displayName = "";

    // ===== 分布式存档支持 =====

    /// <summary>
    /// 通用键值对存储区。
    /// ISaveable 组件通过各自的 SaveID 将数据序列化为 JSON 字符串存入此字典，
    /// 实现各模块独立管理自己的存档数据，互不干扰。
    /// 不使用 ISaveable 时可忽略此字段。
    /// </summary>
    public Dictionary<string, string> moduleData = new Dictionary<string, string>();

    // ===== 元数据辅助方法 =====

    /// <summary>
    /// 标记为新建存档（设置创建时间和更新时间）。
    /// 由 SaveManager 在首次保存时自动调用。
    /// </summary>
    public void MarkAsNew()
    {
        string now = DateTime.UtcNow.ToString("o"); // ISO 8601 格式
        createdAt = now;
        updatedAt = now;
    }

    /// <summary>
    /// 标记为已更新（刷新更新时间）。
    /// 由 SaveManager 在每次保存时自动调用。
    /// </summary>
    public void MarkAsUpdated()
    {
        updatedAt = DateTime.UtcNow.ToString("o");
    }

    /// <summary>
    /// 获取格式化的游戏时长字符串（如 "02:35:17"）。
    /// 适合在存档选择界面显示。
    /// </summary>
    public string GetFormattedPlayTime()
    {
        TimeSpan ts = TimeSpan.FromSeconds(playTimeSeconds);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", ts.Hours, ts.Minutes, ts.Seconds);
    }

    /// <summary>
    /// 获取格式化的最后保存时间（本地时间）。
    /// 适合在存档选择界面显示。
    /// </summary>
    public string GetFormattedSaveTime()
    {
        if (DateTime.TryParse(updatedAt, out DateTime dt))
        {
            return dt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        }
        return "未知";
    }
}

// ============================================================================
// 序列化辅助结构体
// Unity 的 Vector3/Vector2/Color 无法被 JSON 正确序列化，
// 以下结构体提供隐式转换，使用时几乎无感。
//
// 用法：Vector3Serializable pos = transform.position;  // 自动转换
//       transform.position = pos;                       // 自动转回
// ============================================================================

/// <summary>Vector3 的可序列化版本。</summary>
[Serializable]
public struct Vector3Serializable
{
    public float x, y, z;

    public Vector3Serializable(float x, float y, float z)
    { this.x = x; this.y = y; this.z = z; }

    public Vector3Serializable(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public Vector3 ToVector3() => new Vector3(x, y, z);

    // 隐式转换运算符
    public static implicit operator Vector3(Vector3Serializable s) => s.ToVector3();
    public static implicit operator Vector3Serializable(Vector3 v) => new Vector3Serializable(v);
}

/// <summary>Vector2 的可序列化版本。</summary>
[Serializable]
public struct Vector2Serializable
{
    public float x, y;

    public Vector2Serializable(float x, float y) { this.x = x; this.y = y; }
    public Vector2Serializable(Vector2 v) { x = v.x; y = v.y; }
    public Vector2 ToVector2() => new Vector2(x, y);

    public static implicit operator Vector2(Vector2Serializable s) => s.ToVector2();
    public static implicit operator Vector2Serializable(Vector2 v) => new Vector2Serializable(v);
}

/// <summary>Color 的可序列化版本。</summary>
[Serializable]
public struct ColorSerializable
{
    public float r, g, b, a;

    public ColorSerializable(float r, float g, float b, float a = 1f)
    { this.r = r; this.g = g; this.b = b; this.a = a; }

    public ColorSerializable(Color c) { r = c.r; g = c.g; b = c.b; a = c.a; }
    public Color ToColor() => new Color(r, g, b, a);

    public static implicit operator Color(ColorSerializable s) => s.ToColor();
    public static implicit operator ColorSerializable(Color c) => new ColorSerializable(c);
}