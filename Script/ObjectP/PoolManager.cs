// ============================================================================
// PoolManager.cs — 全局对象池管理器
//
// 功能：
//   1. 单例模式，跨场景持久（DontDestroyOnLoad）
//   2. 统一管理所有对象池，通过 poolID 字符串或 PoolConfig 访问
//   3. 提供全局 Spawn / Despawn API，自动路由到正确的池
//   4. 运行时动态创建/销毁池
//   5. 自动收缩（定期清理多余空闲对象）
//   6. 场景切换时可选自动回收
//
// 快速上手：
//   1. 创建 PoolConfig ScriptableObject（Assets → Create → ObjectPool → Pool Config）
//   2. 场景中创建空物体，挂载 PoolManager
//   3. 将 PoolConfig 拖入 Inspector 的预注册列表
//   4. 代码中调用 PoolManager.Instance.Spawn("Bullet", pos, rot)
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局对象池管理器（单例）。
/// 管理所有类型的对象池，提供统一的取出/归还接口。
/// </summary>
public class PoolManager : MonoBehaviour
{
    // ================================================================
    //  单例
    // ================================================================

    /// <summary>全局唯一实例</summary>
    public static PoolManager Instance { get; private set; }

    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 预注册池配置 ===")]
    [Tooltip("在 Inspector 中拖入需要预注册的 PoolConfig 资产。场景加载时自动创建这些池")]
    [SerializeField] private List<PoolConfig> preregisteredPools = new List<PoolConfig>();

    [Header("=== 全局设置 ===")]
    [Tooltip("场景切换时是否自动回收所有活跃对象（推荐开启）")]
    [SerializeField] private bool despawnAllOnSceneChange = true;

    [Tooltip("是否在控制台输出池操作日志")]
    [SerializeField] private bool enableDebugLog = true;

    // ================================================================
    //  运行时数据
    // ================================================================

    // 所有已注册的对象池（Key = poolID）
    private readonly Dictionary<string, ObjectPool> _pools = new Dictionary<string, ObjectPool>();

    // 对象 → 所属池的反向映射（用于 Despawn 时自动找到正确的池）
    private readonly Dictionary<GameObject, ObjectPool> _objectToPool
        = new Dictionary<GameObject, ObjectPool>();

    // 自动收缩协程引用
    private Coroutine _shrinkCoroutine;

    // ================================================================
    //  Unity 生命周期
    // ================================================================

    private void Awake()
    {
        // 单例初始化
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 预注册所有配置的池
        foreach (var config in preregisteredPools)
        {
            if (config != null && config.prefab != null)
            {
                CreatePool(config);
            }
        }

        // 监听场景切换事件
        if (despawnAllOnSceneChange)
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        // 启动全局自动收缩
        _shrinkCoroutine = StartCoroutine(GlobalShrinkCoroutine());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Instance = null;
        }
    }

    /// <summary>场景卸载时回收所有活跃对象</summary>
    private void OnSceneUnloaded(Scene scene)
    {
        if (despawnAllOnSceneChange)
        {
            DespawnAllPools();

            if (enableDebugLog)
            {
                Debug.Log($"[PoolManager] 场景 '{scene.name}' 卸载，已回收所有池对象");
            }
        }
    }

    // ================================================================
    //  池管理 API
    // ================================================================

    /// <summary>
    /// 根据配置创建一个新的对象池。
    /// 如果该 poolID 已存在，会跳过创建并输出警告。
    /// </summary>
    /// <param name="config">池配置资产</param>
    /// <returns>创建的对象池（已存在时返回现有池）</returns>
    public ObjectPool CreatePool(PoolConfig config)
    {
        string id = config.GetPoolID();

        // 防止重复创建
        if (_pools.ContainsKey(id))
        {
            if (enableDebugLog)
            {
                Debug.LogWarning($"[PoolManager] 池 '{id}' 已存在，跳过创建");
            }
            return _pools[id];
        }

        // 创建池，传入 PoolManager 的 Transform 作为 Hierarchy 根
        ObjectPool pool = new ObjectPool(config, transform);
        _pools[id] = pool;

        if (enableDebugLog)
        {
            Debug.Log($"[PoolManager] 创建池 '{id}'（初始: {config.initialSize}, 最大: {config.maxSize}）");
        }

        return pool;
    }

    /// <summary>
    /// 运行时动态创建池（无需预先创建 PoolConfig 资产）。
    /// 适合程序化生成或模组系统动态注册的预制体。
    /// </summary>
    /// <param name="poolID">池 ID</param>
    /// <param name="prefab">预制体</param>
    /// <param name="initialSize">初始数量</param>
    /// <param name="maxSize">最大数量（0 = 无限）</param>
    /// <returns>创建的对象池</returns>
    public ObjectPool CreatePool(string poolID, GameObject prefab, int initialSize = 10, int maxSize = 0)
    {
        if (_pools.ContainsKey(poolID))
        {
            if (enableDebugLog) Debug.LogWarning($"[PoolManager] 池 '{poolID}' 已存在");
            return _pools[poolID];
        }

        // 创建运行时 PoolConfig
        PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
        config.poolID = poolID;
        config.prefab = prefab;
        config.initialSize = initialSize;
        config.maxSize = maxSize;

        return CreatePool(config);
    }

    /// <summary>
    /// 销毁指定的对象池及其所有对象。
    /// </summary>
    /// <param name="poolID">要销毁的池 ID</param>
    public void DestroyPool(string poolID)
    {
        if (!_pools.TryGetValue(poolID, out ObjectPool pool)) return;

        // 清除反向映射中属于此池的条目
        var keysToRemove = new List<GameObject>();
        foreach (var kvp in _objectToPool)
        {
            if (kvp.Value == pool) keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
        {
            _objectToPool.Remove(key);
        }

        // 销毁池
        pool.Clear();
        _pools.Remove(poolID);

        if (enableDebugLog)
        {
            Debug.Log($"[PoolManager] 已销毁池 '{poolID}'");
        }
    }

    /// <summary>
    /// 获取指定 ID 的对象池引用。
    /// </summary>
    public ObjectPool GetPool(string poolID)
    {
        _pools.TryGetValue(poolID, out ObjectPool pool);
        return pool;
    }

    /// <summary>
    /// 检查指定 ID 的池是否已注册。
    /// </summary>
    public bool HasPool(string poolID)
    {
        return _pools.ContainsKey(poolID);
    }

    // ================================================================
    //  核心 API — Spawn（取出）
    // ================================================================

    /// <summary>
    /// 从指定池中取出一个对象。
    /// </summary>
    /// <param name="poolID">池 ID</param>
    /// <param name="position">初始位置</param>
    /// <param name="rotation">初始旋转</param>
    /// <param name="parent">父物体（可选）</param>
    /// <returns>取出的 GameObject，池不存在或策略返回 null 时为 null</returns>
    public GameObject Spawn(string poolID, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!_pools.TryGetValue(poolID, out ObjectPool pool))
        {
            Debug.LogError($"[PoolManager] 池 '{poolID}' 不存在！请先创建或预注册。");
            return null;
        }

        GameObject obj = pool.Spawn(position, rotation, parent);

        // 建立反向映射（用于 Despawn 时自动识别）
        if (obj != null)
        {
            _objectToPool[obj] = pool;
        }

        return obj;
    }

    /// <summary>从指定池取出（简化版，默认位置和旋转）</summary>
    public GameObject Spawn(string poolID)
    {
        return Spawn(poolID, Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// 从指定池取出并自动获取组件（省去 GetComponent 调用）。
    /// </summary>
    /// <typeparam name="TComponent">要获取的组件类型</typeparam>
    /// <param name="poolID">池 ID</param>
    /// <param name="position">初始位置</param>
    /// <param name="rotation">初始旋转</param>
    /// <returns>取出对象上的指定组件，找不到时为 null</returns>
    public TComponent Spawn<TComponent>(string poolID, Vector3 position, Quaternion rotation)
        where TComponent : Component
    {
        GameObject obj = Spawn(poolID, position, rotation);
        return obj != null ? obj.GetComponent<TComponent>() : null;
    }

    // ================================================================
    //  核心 API — Despawn（归还）
    // ================================================================

    /// <summary>
    /// 将对象归还到其所属的池中。
    /// 通过反向映射自动识别对象属于哪个池，无需手动指定池 ID。
    /// </summary>
    /// <param name="obj">要归还的对象</param>
    /// <returns>是否成功归还</returns>
    public bool Despawn(GameObject obj)
    {
        if (obj == null) return false;

        // 通过反向映射找到所属池
        if (_objectToPool.TryGetValue(obj, out ObjectPool pool))
        {
            return pool.Despawn(obj);
        }

        Debug.LogWarning($"[PoolManager] 对象 '{obj.name}' 不属于任何已注册的池，直接销毁");
        Destroy(obj);
        return false;
    }

    /// <summary>
    /// 延迟归还对象（常用于子弹存活时间、特效持续时间等）。
    /// </summary>
    /// <param name="obj">要归还的对象</param>
    /// <param name="delay">延迟秒数</param>
    public void Despawn(GameObject obj, float delay)
    {
        if (obj == null) return;
        StartCoroutine(DespawnDelayedCoroutine(obj, delay));
    }

    /// <summary>延迟归还协程</summary>
    private IEnumerator DespawnDelayedCoroutine(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        // 延迟后对象可能已被销毁或已归还
        if (obj != null && obj.activeInHierarchy)
        {
            Despawn(obj);
        }
    }

    /// <summary>
    /// 回收所有池中的所有活跃对象。
    /// 适合场景切换或关卡重置。
    /// </summary>
    public void DespawnAllPools()
    {
        foreach (var pool in _pools.Values)
        {
            pool.DespawnAll();
        }
    }

    /// <summary>
    /// 回收指定池中的所有活跃对象。
    /// </summary>
    public void DespawnAll(string poolID)
    {
        if (_pools.TryGetValue(poolID, out ObjectPool pool))
        {
            pool.DespawnAll();
        }
    }

    // ================================================================
    //  统计 API
    // ================================================================

    /// <summary>
    /// 获取所有池的统计信息（调试用）。
    /// </summary>
    public string GetStatsReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== PoolManager Stats =====");

        foreach (var kvp in _pools)
        {
            var pool = kvp.Value;
            sb.AppendLine($"  [{kvp.Key}] Active: {pool.CountActive} | Inactive: {pool.CountInactive} | Total: {pool.CountTotal}");
        }

        sb.AppendLine($"  Tracked objects: {_objectToPool.Count}");
        return sb.ToString();
    }

    /// <summary>在控制台输出所有池的统计信息</summary>
    public void LogStats()
    {
        Debug.Log(GetStatsReport());
    }

    // ================================================================
    //  自动收缩
    // ================================================================

    /// <summary>
    /// 全局自动收缩协程：定期遍历所有池，收缩开启了 autoShrink 的池。
    /// </summary>
    private IEnumerator GlobalShrinkCoroutine()
    {
        // 每 30 秒检查一次
        WaitForSeconds checkInterval = new WaitForSeconds(30f);
        Dictionary<string, float> lastShrinkTime = new Dictionary<string, float>();

        while (true)
        {
            yield return checkInterval;

            float now = Time.realtimeSinceStartup;

            foreach (var kvp in _pools)
            {
                var pool = kvp.Value;
                var config = pool.Config;

                // 跳过未启用自动收缩的池
                if (!config.enableAutoShrink) continue;

                // 检查是否到达收缩间隔
                if (!lastShrinkTime.ContainsKey(kvp.Key))
                {
                    lastShrinkTime[kvp.Key] = now;
                    continue;
                }

                if (now - lastShrinkTime[kvp.Key] >= config.shrinkInterval)
                {
                    pool.Shrink();
                    lastShrinkTime[kvp.Key] = now;

                    if (enableDebugLog)
                    {
                        Debug.Log($"[PoolManager] 自动收缩池 '{kvp.Key}'（剩余空闲: {pool.CountInactive}）");
                    }
                }
            }
        }
    }
}
