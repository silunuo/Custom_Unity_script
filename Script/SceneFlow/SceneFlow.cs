// ============================================================================
// SceneFlow.cs — 通用场景切换管理器
//
// 功能：
//   1. 单例模式，跨场景持久（DontDestroyOnLoad）
//   2. 提供同步 / 异步场景切换入口
//   3. 提供切场景开始、进度、完成事件
//   4. 暴露当前加载状态和当前进度
//   5. 加载中重复触发时会直接拦截，避免并发切场景
//
// 快速上手：
//   1. 在启动场景创建空物体，命名为 SceneFlow
//   2. 挂载 SceneFlow 组件
//   3. 代码里通过 SceneFlow.Instance 调用 LoadScene / LoadSceneAsync
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 通用场景切换管理器（单例）。
/// 负责统一管理场景加载状态、进度通知和切场景事件。
/// </summary>
public class SceneFlow : MonoBehaviour
{
    // ================================================================
    //  单例
    // ================================================================

    /// <summary>全局唯一实例</summary>
    public static SceneFlow Instance { get; private set; }

    // ================================================================
    //  事件
    // ================================================================

    /// <summary>场景加载开始时触发，参数为 (场景名, 加载模式)</summary>
    public event Action<string, LoadSceneMode> OnLoadStarted;

    /// <summary>场景加载进度变化时触发，参数为 (场景名, 进度 0~1)</summary>
    public event Action<string, float> OnLoadProgress;

    /// <summary>场景加载完成时触发，参数为 (场景名, 加载模式)</summary>
    public event Action<string, LoadSceneMode> OnLoadCompleted;

    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 调试 ===")]
    [Tooltip("是否在控制台输出场景切换日志")]
    [SerializeField] private bool enableDebugLog = true;

    // ================================================================
    //  运行时状态
    // ================================================================

    /// <summary>当前是否正在加载场景</summary>
    public bool IsLoading { get; private set; }

    /// <summary>当前加载进度（0~1）</summary>
    public float CurrentProgress { get; private set; }

    // 当前异步加载协程引用
    private Coroutine _loadCoroutine;

    // ================================================================
    //  Unity 生命周期
    // ================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ================================================================
    //  场景切换 API
    // ================================================================

    /// <summary>
    /// 同步加载指定场景。
    /// 适合简单流程切换，事件顺序固定为：开始 -> 进度 0 -> 进度 1 -> 完成。
    /// </summary>
    /// <param name="sceneName">目标场景名</param>
    /// <param name="mode">加载模式</param>
    public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (!TryBeginLoad(sceneName, mode)) return;

        try
        {
            SceneManager.LoadScene(sceneName, mode);
            PublishProgress(sceneName, 1f);
            CompleteLoad(sceneName, mode);
        }
        catch (Exception ex)
        {
            FailLoad(sceneName, $"同步加载失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 异步加载指定场景。
    /// 适合加载页、进度条、切场景期间播放转场动画等场景。
    /// </summary>
    /// <param name="sceneName">目标场景名</param>
    /// <param name="mode">加载模式</param>
    public void LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (!TryBeginLoad(sceneName, mode)) return;

        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
        }

        _loadCoroutine = StartCoroutine(LoadSceneAsyncCoroutine(sceneName, mode));
    }

    // ================================================================
    //  异步加载
    // ================================================================

    /// <summary>异步加载协程</summary>
    private IEnumerator LoadSceneAsyncCoroutine(string sceneName, LoadSceneMode mode)
    {
        AsyncOperation operation = null;

        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName, mode);
        }
        catch (Exception ex)
        {
            FailLoad(sceneName, $"异步加载失败：{ex.Message}");
            yield break;
        }

        if (operation == null)
        {
            FailLoad(sceneName, "异步加载返回了空操作对象");
            yield break;
        }

        while (!operation.isDone)
        {
            // Unity 异步加载进度通常停在 0.9，这里统一换算到 0~1
            float progress = operation.progress >= 0.9f
                ? 1f
                : Mathf.Clamp01(operation.progress / 0.9f);

            PublishProgress(sceneName, progress);
            yield return null;
        }

        PublishProgress(sceneName, 1f);
        CompleteLoad(sceneName, mode);
    }

    // ================================================================
    //  内部辅助
    // ================================================================

    /// <summary>
    /// 尝试开始一次场景加载。
    /// 会处理参数校验、并发保护和初始事件分发。
    /// </summary>
    private bool TryBeginLoad(string sceneName, LoadSceneMode mode)
    {
        if (IsLoading)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning($"[SceneFlow] 正在加载场景，忽略新的请求：{sceneName}");
            }
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneFlow] 场景名为空，无法开始加载");
            return false;
        }

        IsLoading = true;
        CurrentProgress = 0f;

        OnLoadStarted?.Invoke(sceneName, mode);
        PublishProgress(sceneName, 0f);

        if (enableDebugLog)
        {
            Debug.Log($"[SceneFlow] 开始加载场景 '{sceneName}'（模式：{mode}）");
        }

        return true;
    }

    /// <summary>更新当前进度并分发进度事件</summary>
    private void PublishProgress(string sceneName, float progress)
    {
        CurrentProgress = Mathf.Clamp01(progress);
        OnLoadProgress?.Invoke(sceneName, CurrentProgress);
    }

    /// <summary>收尾一次成功的场景加载</summary>
    private void CompleteLoad(string sceneName, LoadSceneMode mode)
    {
        IsLoading = false;
        _loadCoroutine = null;
        CurrentProgress = 1f;

        OnLoadCompleted?.Invoke(sceneName, mode);

        if (enableDebugLog)
        {
            Debug.Log($"[SceneFlow] 场景 '{sceneName}' 加载完成");
        }
    }

    /// <summary>收尾一次失败的场景加载</summary>
    private void FailLoad(string sceneName, string reason)
    {
        IsLoading = false;
        _loadCoroutine = null;
        CurrentProgress = 0f;

        Debug.LogError($"[SceneFlow] 场景 '{sceneName}' 加载失败：{reason}");
    }
}
