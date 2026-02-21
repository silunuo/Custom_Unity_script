// ============================================================================
// PoolConfig.cs — 对象池配置（ScriptableObject）
//
// 在 Unity 中通过 Assets → Create → ObjectPool → Pool Config 创建。
// 每种需要池化的预制体对应一份配置资产。
//
// 推荐按用途命名：Pool_Bullet、Pool_Enemy_Slime、Pool_VFX_Explosion 等。
// ============================================================================

using UnityEngine;

/// <summary>
/// 单个对象池的配置信息。
/// 通过 ScriptableObject 实现数据驱动，方便在 Inspector 中调整参数。
/// </summary>
[CreateAssetMenu(fileName = "NewPoolConfig", menuName = "ObjectPool/Pool Config")]
public class PoolConfig : ScriptableObject
{
    // ===== 基本信息 =====

    [Header("基本信息")]
    [Tooltip("池的唯一标识符。代码中通过此 ID 获取/归还对象。留空则自动使用预制体名称")]
    public string poolID = "";

    [Tooltip("要池化的预制体")]
    public GameObject prefab;

    // ===== 容量 =====

    [Header("容量")]
    [Tooltip("初始预创建数量。场景加载时一次性生成，避免运行时卡顿")]
    [Range(0, 200)]
    public int initialSize = 10;

    [Tooltip("最大容量。0 = 无上限，池满时继续扩展；>0 时池满将触发回收策略")]
    [Range(0, 1000)]
    public int maxSize = 0;

    [Tooltip("每次扩展时批量创建的数量（减少频繁单个创建的开销）")]
    [Range(1, 50)]
    public int expandBatchSize = 5;

    // ===== 回收策略 =====

    [Header("回收策略")]
    [Tooltip("池满且无可用对象时的处理方式")]
    public OverflowStrategy overflowStrategy = OverflowStrategy.Expand;

    [Tooltip("是否启用自动收缩（定期销毁多余的空闲对象，释放内存）")]
    public bool enableAutoShrink = false;

    [Tooltip("自动收缩间隔（秒）。仅在 enableAutoShrink = true 时生效")]
    [Range(10f, 300f)]
    public float shrinkInterval = 60f;

    [Tooltip("收缩时保留的最少空闲对象数量（不会收缩到低于此值）")]
    [Range(0, 100)]
    public int shrinkKeepCount = 5;

    // ===== 调试 =====

    [Header("调试")]
    [Tooltip("是否在 Hierarchy 中为此池创建父物体进行分组（方便调试查看）")]
    public bool groupInHierarchy = true;

    /// <summary>
    /// 获取有效的池 ID（优先使用手动设置的 poolID，否则使用预制体名称）。
    /// </summary>
    public string GetPoolID()
    {
        if (!string.IsNullOrEmpty(poolID)) return poolID;
        if (prefab != null) return prefab.name;
        return "Unknown";
    }
}

/// <summary>
/// 池满时的溢出处理策略。
/// </summary>
public enum OverflowStrategy
{
    /// <summary>继续扩展池容量（创建新对象）。适合不想丢失任何请求的场景</summary>
    Expand,

    /// <summary>回收最早被取出的活跃对象（强制归还后重用）。适合子弹、粒子等</summary>
    RecycleOldest,

    /// <summary>本次请求返回 null，不创建新对象。调用方需自行处理 null</summary>
    ReturnNull
}
