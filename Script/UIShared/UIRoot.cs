// ============================================================================
// UIRoot.cs — 场景内 UI 流程总入口
//
// 功能：
//   1. 统一持有页面栈、弹窗管理器、Toast 管理器引用
//   2. 在 Update 里集中处理 Esc 返回逻辑
//   3. 约定前台流程优先级：弹窗 > 页面
//
// Esc 处理顺序：
//   1. 先看顶层弹窗能不能关
//   2. 关不了再尝试页面回退
//   3. 都处理不了就静默忽略
// ============================================================================

using UnityEngine;

/// <summary>
/// 场景内 UI 根节点。
/// 建议挂在 UIRoot 物体上，作为页面、弹窗、Toast 的总入口。
/// </summary>
public class UIRoot : MonoBehaviour
{
    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 子系统引用 ===")]
    [Tooltip("页面栈管理器")]
    [SerializeField] private UIPageStack pageStack;

    [Tooltip("弹窗管理器")]
    [SerializeField] private UIPopupManager popupManager;

    [Tooltip("Toast 管理器")]
    [SerializeField] private ToastManager toastManager;

    [Header("=== 输入 ===")]
    [Tooltip("是否启用 Esc 返回逻辑")]
    [SerializeField] private bool enableEscHandling = true;

    // ================================================================
    //  对外只读引用
    // ================================================================

    /// <summary>页面栈管理器引用</summary>
    public UIPageStack PageStack => pageStack;

    /// <summary>弹窗管理器引用</summary>
    public UIPopupManager PopupManager => popupManager;

    /// <summary>Toast 管理器引用</summary>
    public ToastManager ToastManager => toastManager;

    // ================================================================
    //  生命周期
    // ================================================================

    private void Update()
    {
        if (!enableEscHandling) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // 优先处理顶层弹窗
        if (popupManager != null && popupManager.CurrentPopup != null)
        {
            if (popupManager.IsBusy) return;

            if (popupManager.CurrentPopup.CloseOnEsc)
            {
                if (popupManager.CloseTop())
                {
                    return;
                }
            }
        }

        // 没有可关弹窗时再尝试页面回退
        if (pageStack != null && !pageStack.IsBusy)
        {
            pageStack.Back();
        }
    }
}
