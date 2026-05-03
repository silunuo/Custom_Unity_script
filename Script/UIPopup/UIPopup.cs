// ============================================================================
// UIPopup.cs — 通用弹窗基类
//
// 适合：
//   1. 确认框
//   2. 提示框
//   3. 选择框
//   4. 模态设置面板
//
// 行为：
//   1. OnShow：弹窗显示时调用
//   2. OnHide：弹窗关闭时调用
//   3. 顶层弹窗可配置是否响应 Esc 和遮罩点击
// ============================================================================

using System.Collections;
using UnityEngine;

/// <summary>
/// 通用弹窗基类。
/// 弹窗管理器通过它统一控制显示、关闭和交互状态。
/// </summary>
public class UIPopup : MonoBehaviour
{
    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 弹窗信息 ===")]
    [Tooltip("弹窗唯一 ID。留空时默认使用物体名")]
    [SerializeField] private string popupID = "";

    [Header("=== 交互配置 ===")]
    [Tooltip("顶层弹窗按 Esc 时是否允许关闭")]
    [SerializeField] private bool closeOnEsc = true;

    [Tooltip("点击遮罩时是否允许关闭")]
    [SerializeField] private bool closeOnMaskClick = true;

    [Header("=== 组件引用 ===")]
    [Tooltip("用于控制透明度和交互的 CanvasGroup。留空时会自动查找或补一个")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("弹窗过渡组件。留空时尝试自动查找同物体上的 UITransition")]
    [SerializeField] private UITransition transition;

    // ================================================================
    //  运行时状态
    // ================================================================

    /// <summary>弹窗有效 ID</summary>
    public string PopupID => string.IsNullOrEmpty(popupID) ? gameObject.name : popupID;

    /// <summary>顶层弹窗按 Esc 时是否允许关闭</summary>
    public bool CloseOnEsc => closeOnEsc;

    /// <summary>点击遮罩时是否允许关闭</summary>
    public bool CloseOnMaskClick => closeOnMaskClick;

    /// <summary>当前是否可见</summary>
    public bool IsVisible { get; private set; }

    /// <summary>弹窗使用的 CanvasGroup</summary>
    public CanvasGroup CanvasGroup => canvasGroup;

    /// <summary>弹窗使用的过渡组件</summary>
    public UITransition Transition => transition;

    // ================================================================
    //  Unity 回调
    // ================================================================

    protected virtual void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        transition = GetComponent<UITransition>();
    }

    protected virtual void Awake()
    {
        EnsureReferences();
    }

    // ================================================================
    //  生命周期钩子
    // ================================================================

    /// <summary>
    /// 弹窗显示时调用。
    /// 适合设置标题、正文、按钮状态等。
    /// </summary>
    protected virtual void OnShow()
    {
    }

    /// <summary>
    /// 弹窗关闭时调用。
    /// 适合清理临时状态和解绑外部监听。
    /// </summary>
    protected virtual void OnHide()
    {
    }

    // ================================================================
    //  弹窗管理器内部调用
    // ================================================================

    /// <summary>播放显示流程</summary>
    internal IEnumerator PlayShow()
    {
        EnsureReferences();
        gameObject.SetActive(true);

        OnShow();
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

    /// <summary>播放关闭流程</summary>
    internal IEnumerator PlayHide()
    {
        EnsureReferences();

        OnHide();
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

    /// <summary>立即显示弹窗</summary>
    internal void ShowImmediate()
    {
        EnsureReferences();
        gameObject.SetActive(true);
        OnShow();
        IsVisible = true;
        SnapToShown();
    }

    /// <summary>立即隐藏弹窗</summary>
    internal void HideImmediate(bool invokeHide)
    {
        EnsureReferences();

        if (invokeHide)
        {
            OnHide();
        }

        IsVisible = false;
        SnapToHidden();
        gameObject.SetActive(false);
    }

    /// <summary>设置当前弹窗是否可交互</summary>
    internal void SetInteractable(bool interactable)
    {
        EnsureReferences();
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    // ================================================================
    //  内部辅助
    // ================================================================

    /// <summary>确保关键引用存在</summary>
    private void EnsureReferences()
    {
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
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
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
