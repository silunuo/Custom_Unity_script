// ============================================================================
// ObjectPool.cs — 单类型对象池
//
// 管理同一种预制体的对象复用。内部使用 Stack 存储空闲对象，
// Queue 追踪活跃对象（用于 RecycleOldest 策略）。
//
// 通常不直接使用此类，而是通过 PoolManager 统一管理。
// 如果只需要一个简单的池，也可以独立使用。
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单类型对象池。管理一种预制体的创建、复用、回收。
/// </summary>
public class ObjectPool
{
    // ===== 配置 =====

    /// <summary>此池的配置信息</summary>
    public PoolConfig Config { get; private set; }

    /// <summary>此池的唯一 ID</summary>
    public string PoolID { get; private set; }

    // ===== 统计（只读） =====

    /// <summary>当前空闲对象数量（池中待取出的）</summary>
    public int CountInactive => _inactiveStack.Count;

    /// <summary>当前活跃对象数量（已取出正在使用的）</summary>
    public int CountActive => _activeSet.Count;

    /// <summary>已创建的对象总数（活跃 + 空闲）</summary>
    public int CountTotal => CountActive + CountInactive;

    // ===== 内部数据结构 =====

    // 空闲对象栈（后进先出，最近归还的优先复用，缓存更热）
    private readonly Stack<GameObject> _inactiveStack = new Stack<GameObject>();

    // 活跃对象集合（用于快速判断一个对象是否属于此池）
    private readonly HashSet<GameObject> _activeSet = new HashSet<GameObject>();

    // 活跃对象队列（用于 RecycleOldest 策略，先进先出）
    private readonly Queue<GameObject> _activeQueue = new Queue<GameObject>();

    // 对象 → IPoolable 缓存（避免每次 GetComponents 的 GC）
    private readonly Dictionary<GameObject, IPoolable[]> _poolableCache
        = new Dictionary<GameObject, IPoolable[]>();

    // Hierarchy 中的分组父物体
    private Transform _parentTransform;

    // 预制体引用
    private GameObject _prefab;

    // ================================================================
    //  构造与初始化
    // ================================================================

    /// <summary>
    /// 创建一个对象池。
    /// </summary>
    /// <param name="config">池配置</param>
    /// <param name="rootTransform">PoolManager 的根 Transform（可选，用于 Hierarchy 分组）</param>
    public ObjectPool(PoolConfig config, Transform rootTransform = null)
    {
        Config = config;
        PoolID = config.GetPoolID();
        _prefab = config.prefab;

        // 创建 Hierarchy 分组父物体
        if (config.groupInHierarchy && rootTransform != null)
        {
            GameObject parentGO = new GameObject($"[Pool] {PoolID}");
            parentGO.transform.SetParent(rootTransform);
            _parentTransform = parentGO.transform;
        }

        // 预创建初始对象
        Prewarm(config.initialSize);
    }

    /// <summary>
    /// 预热：批量创建指定数量的对象并放入池中。
    /// 适合在 Loading 界面或场景初始化时调用，避免运行时卡顿。
    /// </summary>
    /// <param name="count">预创建数量</param>
    public void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // 如果设置了 maxSize 且已达上限，停止预创建
            if (Config.maxSize > 0 && CountTotal >= Config.maxSize) break;

            GameObject obj = CreateNewObject();
            obj.SetActive(false);
            _inactiveStack.Push(obj);
        }
    }

    // ================================================================
    //  核心 API — 取出
    // ================================================================

    /// <summary>
    /// 从池中取出一个对象。
    /// 如果池中有空闲对象则复用，否则根据溢出策略决定行为。
    /// </summary>
    /// <param name="position">初始位置</param>
    /// <param name="rotation">初始旋转</param>
    /// <param name="parent">父物体（null = 无父物体）</param>
    /// <returns>取出的 GameObject，策略为 ReturnNull 且池满时可能返回 null</returns>
    public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent = null)
    {
        GameObject obj = null;

        // 尝试从空闲栈中取出
        while (_inactiveStack.Count > 0)
        {
            obj = _inactiveStack.Pop();

            // 防御：对象可能已被外部销毁
            if (obj != null) break;
            obj = null;
        }

        // 空闲栈为空，根据策略处理
        if (obj == null)
        {
            switch (Config.overflowStrategy)
            {
                case OverflowStrategy.Expand:
                    // 批量扩展，但只取一个
                    for (int i = 0; i < Config.expandBatchSize - 1; i++)
                    {
                        if (Config.maxSize > 0 && CountTotal >= Config.maxSize) break;
                        GameObject extra = CreateNewObject();
                        extra.SetActive(false);
                        _inactiveStack.Push(extra);
                    }
                    obj = CreateNewObject();
                    break;

                case OverflowStrategy.RecycleOldest:
                    obj = ForceRecycleOldest();
                    if (obj == null)
                    {
                        // 如果连强制回收都拿不到（不应该发生），创建新的
                        obj = CreateNewObject();
                    }
                    break;

                case OverflowStrategy.ReturnNull:
                    return null;
            }
        }

        // 设置 Transform
        if (parent != null)
        {
            obj.transform.SetParent(parent);
        }
        else if (_parentTransform != null)
        {
            // 无指定父物体时脱离池的分组父物体
            obj.transform.SetParent(null);
        }

        obj.transform.SetPositionAndRotation(position, rotation);

        // 激活对象
        obj.SetActive(true);

        // 记录为活跃
        _activeSet.Add(obj);
        _activeQueue.Enqueue(obj);

        // 通知 IPoolable 组件
        NotifySpawn(obj);

        return obj;
    }

    /// <summary>
    /// 从池中取出一个对象（简化版，使用默认位置和旋转）。
    /// </summary>
    public GameObject Spawn()
    {
        return Spawn(Vector3.zero, Quaternion.identity);
    }

    // ================================================================
    //  核心 API — 归还
    // ================================================================

    /// <summary>
    /// 将对象归还到池中。
    /// 对象会被停用（SetActive(false)）并放回空闲栈等待复用。
    /// </summary>
    /// <param name="obj">要归还的对象</param>
    /// <returns>是否成功归还（对象不属于此池时返回 false）</returns>
    public bool Despawn(GameObject obj)
    {
        if (obj == null) return false;

        // 检查是否属于此池
        if (!_activeSet.Contains(obj))
        {
            Debug.LogWarning($"[ObjectPool] 对象 '{obj.name}' 不属于池 '{PoolID}'，忽略归还请求");
            return false;
        }

        // 通知 IPoolable 组件
        NotifyDespawn(obj);

        // 停用并归还
        obj.SetActive(false);

        // 归回分组父物体下（保持 Hierarchy 整洁）
        if (_parentTransform != null)
        {
            obj.transform.SetParent(_parentTransform);
        }

        // 从活跃集合移除
        _activeSet.Remove(obj);
        // 注意：_activeQueue 中可能还有引用，但 Despawn 后会在后续操作中被跳过

        // 放回空闲栈
        _inactiveStack.Push(obj);

        return true;
    }

    /// <summary>
    /// 强制回收所有活跃对象。
    /// 适合场景切换、关卡重置等需要一次性清理的场景。
    /// </summary>
    public void DespawnAll()
    {
        // 复制一份避免迭代时修改集合
        var activeList = new List<GameObject>(_activeSet);
        foreach (var obj in activeList)
        {
            if (obj != null) Despawn(obj);
        }
    }

    // ================================================================
    //  容量管理
    // ================================================================

    /// <summary>
    /// 收缩池：销毁多余的空闲对象，释放内存。
    /// 保留至少 keepCount 个空闲对象。
    /// </summary>
    /// <param name="keepCount">保留的最少空闲数量（默认使用配置值）</param>
    public void Shrink(int keepCount = -1)
    {
        if (keepCount < 0) keepCount = Config.shrinkKeepCount;

        while (_inactiveStack.Count > keepCount)
        {
            GameObject obj = _inactiveStack.Pop();
            if (obj != null)
            {
                _poolableCache.Remove(obj);
                Object.Destroy(obj);
            }
        }
    }

    /// <summary>
    /// 销毁此池的所有对象并清空。
    /// 调用后此池不应再被使用。
    /// </summary>
    public void Clear()
    {
        DespawnAll();

        while (_inactiveStack.Count > 0)
        {
            GameObject obj = _inactiveStack.Pop();
            if (obj != null) Object.Destroy(obj);
        }

        _activeSet.Clear();
        _activeQueue.Clear();
        _poolableCache.Clear();

        if (_parentTransform != null)
        {
            Object.Destroy(_parentTransform.gameObject);
        }
    }

    // ================================================================
    //  内部方法
    // ================================================================

    /// <summary>创建一个新的池对象实例</summary>
    private GameObject CreateNewObject()
    {
        GameObject obj = Object.Instantiate(_prefab);
        obj.name = $"{PoolID}_{CountTotal}"; // 方便调试识别

        // 放到分组父物体下
        if (_parentTransform != null)
        {
            obj.transform.SetParent(_parentTransform);
        }

        // 缓存 IPoolable 组件引用
        IPoolable[] poolables = obj.GetComponentsInChildren<IPoolable>(true);
        if (poolables.Length > 0)
        {
            _poolableCache[obj] = poolables;
        }

        return obj;
    }

    /// <summary>强制回收最早取出的活跃对象（RecycleOldest 策略）</summary>
    private GameObject ForceRecycleOldest()
    {
        // 从队列中找到第一个仍在活跃的对象
        while (_activeQueue.Count > 0)
        {
            GameObject oldest = _activeQueue.Dequeue();

            // 跳过已被归还或已销毁的对象
            if (oldest == null || !_activeSet.Contains(oldest)) continue;

            // 强制回收
            NotifyDespawn(oldest);
            oldest.SetActive(false);
            _activeSet.Remove(oldest);

            return oldest;
        }

        return null; // 队列为空
    }

    /// <summary>通知对象上的所有 IPoolable 组件：已被取出</summary>
    private void NotifySpawn(GameObject obj)
    {
        if (_poolableCache.TryGetValue(obj, out IPoolable[] poolables))
        {
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnSpawn();
            }
        }
    }

    /// <summary>通知对象上的所有 IPoolable 组件：即将被归还</summary>
    private void NotifyDespawn(GameObject obj)
    {
        if (_poolableCache.TryGetValue(obj, out IPoolable[] poolables))
        {
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnDespawn();
            }
        }
    }
}
