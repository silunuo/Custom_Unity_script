# 🔄 ObjectPool — Unity 通用对象池系统

> 一套零 GC 的 GameObject 复用方案。支持多池管理、溢出策略、自动回收、自动收缩。
> 适用于子弹、敌人、特效、伤害数字、拾取物等任何需要频繁创建销毁的对象。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `PoolConfig.cs` | ScriptableObject 配置（预制体、容量、溢出策略） |
| `IPoolable.cs` | 可池化接口（OnSpawn/OnDespawn 回调） |
| `ObjectPool.cs` | 单类型对象池核心逻辑 |
| `PoolManager.cs` | 全局管理器（单例，统一 API） |
| `AutoRecycle.cs` | 自动回收组件（延时/离屏/粒子结束） |

---

## 🚀 快速上手（3 分钟接入）

### 第一步：场景配置

1. 创建空物体 → 命名为 `PoolManager` → 挂载 `PoolManager` 组件

### 第二步：创建池配置

1. Project 窗口右键 → `Create → ObjectPool → Pool Config`
2. 拖入预制体，设置 poolID、初始数量等
3. 将 PoolConfig 拖入 PoolManager 的 `Preregistered Pools` 列表

### 第三步：使用

```csharp
// 取出
GameObject bullet = PoolManager.Instance.Spawn("Bullet", firePoint.position, firePoint.rotation);

// 归还
PoolManager.Instance.Despawn(bullet);

// 延迟归还（3 秒后自动回池）
PoolManager.Instance.Despawn(bullet, 3f);
```

---

## 📋 完整 API 参考

### PoolManager — 全局管理

| 方法 | 签名 | 说明 |
|------|------|------|
| `Spawn` | `GameObject Spawn(string poolID, Vector3 pos, Quaternion rot, Transform parent = null)` | 从池取出 |
| `Spawn<T>` | `T Spawn<T>(string poolID, Vector3 pos, Quaternion rot)` | 取出并获取组件 |
| `Despawn` | `bool Despawn(GameObject obj)` | 归还（自动识别所属池） |
| `Despawn` | `void Despawn(GameObject obj, float delay)` | 延迟归还 |
| `DespawnAll` | `void DespawnAll(string poolID)` | 回收指定池全部 |
| `DespawnAllPools` | `void DespawnAllPools()` | 回收所有池全部 |
| `CreatePool` | `ObjectPool CreatePool(PoolConfig config)` | 从配置创建池 |
| `CreatePool` | `ObjectPool CreatePool(string id, GameObject prefab, int init, int max)` | 运行时动态创建 |
| `DestroyPool` | `void DestroyPool(string poolID)` | 销毁池及所有对象 |
| `GetPool` | `ObjectPool GetPool(string poolID)` | 获取池引用 |
| `HasPool` | `bool HasPool(string poolID)` | 池是否存在 |
| `LogStats` | `void LogStats()` | 输出统计日志 |

### ObjectPool — 单池操作

| 方法/属性 | 说明 |
|-----------|------|
| `Spawn(pos, rot, parent)` | 取出对象 |
| `Despawn(obj)` | 归还对象 |
| `DespawnAll()` | 回收所有活跃对象 |
| `Prewarm(count)` | 追加预热 |
| `Shrink(keepCount)` | 收缩空闲对象 |
| `Clear()` | 销毁池所有对象 |
| `CountActive` | 活跃数量 |
| `CountInactive` | 空闲数量 |
| `CountTotal` | 总数量 |

---

## 🔧 常见使用场景

### 子弹系统

```csharp
public class Weapon : MonoBehaviour
{
    [SerializeField] private string bulletPoolID = "Bullet";
    [SerializeField] private Transform firePoint;

    void Fire()
    {
        var bullet = PoolManager.Instance.Spawn<Bullet>(
            bulletPoolID, firePoint.position, firePoint.rotation);
        bullet.SetDirection(firePoint.up);
    }
}

public class Bullet : MonoBehaviour, IPoolable
{
    private Rigidbody2D rb;
    [SerializeField] private float speed = 20f;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    public void OnSpawn() { rb.linearVelocity = Vector2.zero; }
    public void OnDespawn() { rb.linearVelocity = Vector2.zero; }

    public void SetDirection(Vector2 dir) { rb.linearVelocity = dir * speed; }

    void OnTriggerEnter2D(Collider2D other)
    {
        PoolManager.Instance.Despawn(gameObject);
    }
}
```

### 特效系统

```csharp
// 预制体上挂 AutoRecycle（enableParticleAutoRecycle = true）
// 粒子播完自动归还，无需手动管理
public class VFXHelper
{
    public static void PlayAt(string vfxID, Vector3 position)
    {
        PoolManager.Instance.Spawn(vfxID, position, Quaternion.identity);
        // AutoRecycle 会在粒子播完后自动处理归还
    }
}
```

### 伤害数字（飘字）

```csharp
public class DamageNumber : MonoBehaviour, IPoolable
{
    private TextMeshPro tmp;
    private float timer;

    public void OnSpawn() { timer = 0f; tmp.alpha = 1f; }
    public void OnDespawn() { }

    public void Setup(int damage, Color color)
    {
        tmp.text = damage.ToString();
        tmp.color = color;
    }

    void Update()
    {
        transform.Translate(Vector3.up * Time.deltaTime * 2f);
        timer += Time.deltaTime;
        tmp.alpha = 1f - (timer / 1f);

        if (timer >= 1f) PoolManager.Instance.Despawn(gameObject);
    }
}
```

### 运行时动态注册

```csharp
// 加载 DLC 或 Mod 时动态创建池
void LoadEnemyMod(GameObject enemyPrefab)
{
    PoolManager.Instance.CreatePool("ModEnemy_Goblin", enemyPrefab,
        initialSize: 5, maxSize: 20);
}
```

---

## ⚙️ PoolConfig 配置说明

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `poolID` | "" | 唯一标识（空 = 用预制体名） |
| `prefab` | — | 要池化的预制体 |
| `initialSize` | 10 | 初始预创建数量 |
| `maxSize` | 0 | 最大容量（0 = 无限） |
| `expandBatchSize` | 5 | 扩展时批量创建数 |
| `overflowStrategy` | Expand | 池满策略 |
| `enableAutoShrink` | false | 是否自动收缩 |
| `shrinkInterval` | 60s | 收缩检查间隔 |
| `shrinkKeepCount` | 5 | 收缩时保留最少空闲数 |
| `groupInHierarchy` | true | Hierarchy 分组 |

### 溢出策略选择

| 策略 | 适用场景 | 说明 |
|------|----------|------|
| `Expand` | 通用默认 | 池满时继续创建新对象 |
| `RecycleOldest` | 子弹、粒子 | 强制回收最早的活跃对象 |
| `ReturnNull` | 需要精确控制 | 返回 null，调用方自行处理 |

### 容量推荐

| 对象类型 | initialSize | maxSize | 策略 |
|----------|-------------|---------|------|
| 子弹 | 20 | 50 | RecycleOldest |
| 敌人 | 10 | 30 | Expand |
| 特效/粒子 | 15 | 40 | RecycleOldest |
| 伤害数字 | 10 | 30 | RecycleOldest |
| 拾取物 | 5 | 0 | Expand |

---

## ❓ FAQ

**Q：不实现 IPoolable 可以用池吗？**
A：完全可以。IPoolable 只是一个可选的回调接口。不实现的对象正常 Spawn/Despawn，只是不会收到通知。

**Q：Despawn 时需要指定池 ID 吗？**
A：不需要。PoolManager 内部维护了对象到池的反向映射，Despawn 时自动识别。

**Q：场景切换时池中的对象会怎样？**
A：PoolManager 挂了 DontDestroyOnLoad，池本身不会被销毁。默认配置下场景卸载时会自动回收所有活跃对象。

**Q：可以同时用 AutoRecycle 和手动 Despawn 吗？**
A：可以。AutoRecycle 在归还前会检查对象是否仍然活跃，不会重复归还。

**Q：对象池和 Addressables 怎么配合？**
A：异步加载完 Prefab 后，用 `CreatePool(id, loadedPrefab, ...)` 动态注册即可。

**Q：多线程安全吗？**
A：不是。Unity 的 GameObject 操作必须在主线程执行，对象池也遵循这个约束。

---

## 📜 版本历史

| 版本 | 说明 |
|------|------|
| v1.0 | 初始版本：多池管理、三种溢出策略、IPoolable 回调、AutoRecycle、自动收缩、场景切换回收 |
