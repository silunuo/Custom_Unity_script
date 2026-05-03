// ============================================================================
// ToastManager.cs — 轻提示管理器
//
// 功能：
//   1. 管理提示消息队列
//   2. 保证同一时间只显示一条 Toast
//   3. 支持手动打断当前提示
//   4. 当前提示结束后自动播放下一条
//
// 约定：
//   1. 新消息按 FIFO 顺序排队
//   2. HideCurrent 只打断当前显示，不清空后续队列
//   3. 默认显示时长走 defaultDuration，传入 >= 0 时用自定义值
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景内轻提示管理器。
/// 适合顶部通知、保存成功提示、操作反馈提示等轻量信息展示。
/// </summary>
public class ToastManager : MonoBehaviour
{
    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 容器和模板 ===")]
    [Tooltip("Toast 根节点。留空时默认使用当前物体")]
    [SerializeField] private Transform toastRoot;

    [Tooltip("Toast 模板。运行时会实例化一份并重复复用")]
    [SerializeField] private ToastItem toastPrefab;

    [Header("=== 默认参数 ===")]
    [Tooltip("默认显示时长（秒）")]
    [Min(0f)]
    [SerializeField] private float defaultDuration = 1.5f;

    [Header("=== 调试 ===")]
    [Tooltip("是否输出 Toast 日志")]
    [SerializeField] private bool enableDebugLog = true;

    // ================================================================
    //  运行时数据
    // ================================================================

    private readonly Queue<ToastRequest> _queue = new Queue<ToastRequest>();

    private ToastItem _runtimeItem;
    private Coroutine _processCoroutine;

    /// <summary>当前是否正在显示 Toast</summary>
    public bool IsShowing { get; private set; }

    // ================================================================
    //  生命周期
    // ================================================================

    private void Awake()
    {
        if (toastRoot == null)
        {
            toastRoot = transform;
        }
    }

    // ================================================================
    //  对外 API
    // ================================================================

    /// <summary>
    /// 显示一条提示。
    /// 若当前已有提示在播，会进入队列等待。
    /// </summary>
    /// <param name="message">提示文本</param>
    /// <param name="duration">显示时长。小于 0 时使用默认值</param>
    public void Show(string message, float duration = -1f)
    {
        float actualDuration = duration >= 0f ? duration : defaultDuration;
        _queue.Enqueue(new ToastRequest(message ?? string.Empty, Mathf.Max(0f, actualDuration)));

        if (_processCoroutine == null)
        {
            _processCoroutine = StartCoroutine(ProcessQueueCoroutine());
        }
    }

    /// <summary>
    /// 打断当前正在显示的提示。
    /// 只会关闭当前提示，不清空后续队列。
    /// </summary>
    public void HideCurrent()
    {
        if (!IsShowing) return;

        if (_processCoroutine != null)
        {
            StopCoroutine(_processCoroutine);
        }

        _processCoroutine = StartCoroutine(HideCurrentAndContinueCoroutine());
    }

    /// <summary>
    /// 清空尚未显示的提示队列。
    /// 当前已显示的提示不会被立刻打断。
    /// </summary>
    public void ClearQueue()
    {
        _queue.Clear();

        if (enableDebugLog)
        {
            Debug.Log("[ToastManager] 已清空后续队列");
        }
    }

    // ================================================================
    //  队列流程
    // ================================================================

    /// <summary>顺序播放队列中的 Toast</summary>
    private IEnumerator ProcessQueueCoroutine()
    {
        if (!EnsureRuntimeItem())
        {
            _processCoroutine = null;
            yield break;
        }

        while (_queue.Count > 0)
        {
            ToastRequest request = _queue.Dequeue();
            IsShowing = true;

            _runtimeItem.SetMessage(request.Message);
            yield return _runtimeItem.PlayShow();

            if (enableDebugLog)
            {
                Debug.Log($"[ToastManager] Show -> {request.Message}");
            }

            yield return WaitForDuration(request.Duration);
            yield return _runtimeItem.PlayHide();

            IsShowing = false;
        }

        _processCoroutine = null;
    }

    /// <summary>打断当前提示后继续播放后续队列</summary>
    private IEnumerator HideCurrentAndContinueCoroutine()
    {
        if (_runtimeItem != null && _runtimeItem.gameObject.activeSelf)
        {
            yield return _runtimeItem.PlayHide();
        }

        IsShowing = false;

        if (_queue.Count > 0)
        {
            _processCoroutine = StartCoroutine(ProcessQueueCoroutine());
        }
        else
        {
            _processCoroutine = null;
        }
    }

    // ================================================================
    //  内部辅助
    // ================================================================

    /// <summary>确保运行时 Toast 实例存在</summary>
    private bool EnsureRuntimeItem()
    {
        if (_runtimeItem != null) return true;

        if (toastPrefab == null)
        {
            Debug.LogError("[ToastManager] 未设置 toastPrefab");
            return false;
        }

        _runtimeItem = Instantiate(toastPrefab, toastRoot);
        _runtimeItem.gameObject.name = toastPrefab.gameObject.name;
        _runtimeItem.HideImmediate();
        return true;
    }

    /// <summary>按秒等待，使用 unscaled 时间方便暂停场景也能继续显示</summary>
    private IEnumerator WaitForDuration(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>单条 Toast 的队列数据</summary>
    private readonly struct ToastRequest
    {
        public readonly string Message;
        public readonly float Duration;

        public ToastRequest(string message, float duration)
        {
            Message = message;
            Duration = duration;
        }
    }
}
