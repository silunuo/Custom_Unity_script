// ============================================================================
// AutoRecycle.cs — 自动回收组件
//
// 挂载到需要自动归还的预制体上。支持三种自动回收模式：
//   1. 延时回收：取出后经过指定秒数自动归还
//   2. 离屏回收：对象离开摄像机视野后自动归还
//   3. 粒子结束回收：ParticleSystem 播放完毕后自动归还
//
// 可同时启用多种模式，任一条件满足即触发回收。
// 与 IPoolable 配合使用时，OnDespawn 会在回收前正常调用。
// ============================================================================

using UnityEngine;

/// <summary>
/// 自动回收组件。挂载到池化的预制体上，实现自动归还到池中。
/// 同时实现 IPoolable 接口以在取出时重置内部计时器。
/// </summary>
public class AutoRecycle : MonoBehaviour, IPoolable
{
    // ===== 延时回收 =====

    [Header("延时回收")]
    [Tooltip("是否启用延时回收")]
    public bool enableTimer = true;

    [Tooltip("取出后经过多少秒自动归还")]
    [Range(0.1f, 60f)]
    public float lifetime = 3f;

    // ===== 离屏回收 =====

    [Header("离屏回收")]
    [Tooltip("是否启用离屏回收（对象离开所有摄像机视野后归还）")]
    public bool enableOffscreen = false;

    [Tooltip("离屏后等待多久再归还（秒）。防止物体短暂出屏就被回收")]
    [Range(0f, 5f)]
    public float offscreenDelay = 0.5f;

    // ===== 粒子结束回收 =====

    [Header("粒子结束回收")]
    [Tooltip("是否在 ParticleSystem 播放结束后自动归还")]
    public bool enableParticleAutoRecycle = false;

    // ===== 内部状态 =====

    private float _timer;                // 延时计时器
    private float _offscreenTimer;       // 离屏计时器
    private bool _isVisible;             // 当前是否在屏幕内
    private ParticleSystem _particle;    // 缓存的粒子系统引用

    // ================================================================
    //  IPoolable 接口
    // ================================================================

    /// <summary>
    /// 从池中取出时调用：重置所有计时器。
    /// </summary>
    public void OnSpawn()
    {
        _timer = 0f;
        _offscreenTimer = 0f;
        _isVisible = true; // 假设取出时在屏幕内

        // 缓存粒子系统引用（首次或引用丢失时）
        if (enableParticleAutoRecycle && _particle == null)
        {
            _particle = GetComponent<ParticleSystem>();
        }

        // 重新播放粒子
        if (_particle != null)
        {
            _particle.Clear();
            _particle.Play();
        }
    }

    /// <summary>
    /// 归还到池中时调用：停止粒子。
    /// </summary>
    public void OnDespawn()
    {
        if (_particle != null)
        {
            _particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    // ================================================================
    //  Unity 生命周期
    // ================================================================

    private void Update()
    {
        // 延时回收检查
        if (enableTimer)
        {
            _timer += Time.deltaTime;
            if (_timer >= lifetime)
            {
                ReturnToPool();
                return; // 已回收，跳过后续检查
            }
        }

        // 离屏回收检查
        if (enableOffscreen && !_isVisible)
        {
            _offscreenTimer += Time.deltaTime;
            if (_offscreenTimer >= offscreenDelay)
            {
                ReturnToPool();
                return;
            }
        }

        // 粒子结束回收检查
        if (enableParticleAutoRecycle && _particle != null)
        {
            // 粒子已停止且没有存活粒子
            if (!_particle.isPlaying && _particle.particleCount == 0)
            {
                ReturnToPool();
                return;
            }
        }
    }

    // ================================================================
    //  可见性检测（由 Unity 渲染系统自动调用）
    // ================================================================

    /// <summary>对象进入任意摄像机视野时调用</summary>
    private void OnBecameVisible()
    {
        _isVisible = true;
        _offscreenTimer = 0f; // 重置离屏计时
    }

    /// <summary>对象离开所有摄像机视野时调用</summary>
    private void OnBecameInvisible()
    {
        _isVisible = false;
    }

    // ================================================================
    //  回收
    // ================================================================

    /// <summary>将自身归还到对象池</summary>
    private void ReturnToPool()
    {
        // 优先通过 PoolManager 归还
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Despawn(gameObject);
        }
        else
        {
            // PoolManager 不存在（可能已被销毁），直接销毁自身
            Destroy(gameObject);
        }
    }
}
