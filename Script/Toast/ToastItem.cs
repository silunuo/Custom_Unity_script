// ============================================================================
// ToastItem.cs — 单条轻提示基类
//
// 用于承载一条消息的文本、CanvasGroup 和过渡组件。
// 通常作为 ToastManager 的运行时实例或预制体根节点。
// ============================================================================

using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 单条轻提示基类。
/// 负责显示文本和执行进入 / 退出过渡。
/// </summary>
public class ToastItem : MonoBehaviour
{
    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 组件引用 ===")]
    [Tooltip("显示提示文本的 TMP_Text")]
    [SerializeField] private TMP_Text messageLabel;

    [Tooltip("用于控制透明度和交互的 CanvasGroup。留空时会自动查找或补一个")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("提示过渡组件。留空时尝试自动查找同物体上的 UITransition")]
    [SerializeField] private UITransition transition;

    // ================================================================
    //  运行时状态
    // ================================================================

    /// <summary>当前是否可见</summary>
    public bool IsVisible { get; private set; }

    /// <summary>当前文本组件引用</summary>
    public TMP_Text MessageLabel => messageLabel;

    /// <summary>当前 CanvasGroup 引用</summary>
    public CanvasGroup CanvasGroup => canvasGroup;

    /// <summary>当前过渡组件引用</summary>
    public UITransition Transition => transition;

    // ================================================================
    //  Unity 回调
    // ================================================================

    protected virtual void Reset()
    {
        messageLabel = GetComponentInChildren<TMP_Text>(true);
        canvasGroup = GetComponent<CanvasGroup>();
        transition = GetComponent<UITransition>();
    }

    protected virtual void Awake()
    {
        EnsureReferences();
    }

    // ================================================================
    //  对外 API
    // ================================================================

    /// <summary>
    /// 设置当前提示文案。
    /// </summary>
    /// <param name="message">提示文本</param>
    public virtual void SetMessage(string message)
    {
        EnsureReferences();

        if (messageLabel != null)
        {
            messageLabel.text = message ?? string.Empty;
        }
    }

    // ================================================================
    //  Toast 管理器内部调用
    // ================================================================

    /// <summary>播放显示流程</summary>
    internal IEnumerator PlayShow()
    {
        EnsureReferences();
        gameObject.SetActive(true);
        IsVisible = true;

        if (transition != null)
        {
            yield return transition.PlayEnter(canvasGroup);
        }
        else
        {
            SnapToShown();
        }
    }

    /// <summary>播放隐藏流程</summary>
    internal IEnumerator PlayHide()
    {
        EnsureReferences();
        IsVisible = false;

        if (transition != null)
        {
            yield return transition.PlayExit(canvasGroup);
        }
        else
        {
            SnapToHidden();
        }

        gameObject.SetActive(false);
    }

    /// <summary>立即隐藏</summary>
    internal void HideImmediate()
    {
        EnsureReferences();
        IsVisible = false;
        SnapToHidden();
        gameObject.SetActive(false);
    }

    // ================================================================
    //  内部辅助
    // ================================================================

    /// <summary>确保关键引用存在</summary>
    private void EnsureReferences()
    {
        if (messageLabel == null)
        {
            messageLabel = GetComponentInChildren<TMP_Text>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (transition == null)
        {
            transition = GetComponent<UITransition>();
        }
    }

    /// <summary>立即切到显示状态</summary>
    private void SnapToShown()
    {
        if (transition != null)
        {
            transition.SnapToShown(canvasGroup);
            return;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>立即切到隐藏状态</summary>
    private void SnapToHidden()
    {
        if (transition != null)
        {
            transition.SnapToHidden(canvasGroup);
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
