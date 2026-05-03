// ============================================================================
// UIPage.cs — 通用页面基类
//
// 适合：
//   1. 主菜单
//   2. 设置页
//   3. 背包页
//   4. 任意全屏或半屏的页面式 UI
//
// 生命周期：
//   1. OnOpen：首次被页面栈打开时调用
//   2. OnClose：被关闭并移出历史时调用
//   3. OnPause：当前页被新页面覆盖时调用
//   4. OnResume：从历史栈返回时调用
// ============================================================================

using System.Collections;
using UnityEngine;

/// <summary>
/// 通用页面基类。
/// 页面栈通过它统一控制显示、隐藏和过渡。
/// </summary>
public class UIPage : MonoBehaviour
{
    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 页面信息 ===")]
    [Tooltip("页面唯一 ID。留空时默认使用物体名")]
    [SerializeField] private string pageID = "";

    [Header("=== 组件引用 ===")]
    [Tooltip("用于控制透明度和交互的 CanvasGroup。留空时会自动查找或补一个")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("页面过渡组件。留空时尝试自动查找同物体上的 UITransition")]
    [SerializeField] private UITransition transition;

    // ================================================================
    //  运行时状态
    // ================================================================

    /// <summary>页面有效 ID</summary>
    public string PageID => string.IsNullOrEmpty(pageID) ? gameObject.name : pageID;

    /// <summary>当前是否可见</summary>
    public bool IsVisible { get; private set; }

    /// <summary>页面使用的 CanvasGroup</summary>
    public CanvasGroup CanvasGroup => canvasGroup;

    /// <summary>页面使用的过渡组件</summary>
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
    /// 首次被页面栈打开时调用。
    /// 适合做页面初始化、数据刷新、按钮状态同步。
    /// </summary>
    protected virtual void OnOpen()
    {
    }

    /// <summary>
    /// 页面被彻底关闭时调用。
    /// 适合做临时状态清理和外部解绑。
    /// </summary>
    protected virtual void OnClose()
    {
    }

    /// <summary>
    /// 当前页面被别的页面覆盖时调用。
    /// 适合暂停输入、暂停刷新、保存中间状态。
    /// </summary>
    protected virtual void OnPause()
    {
    }

    /// <summary>
    /// 页面从历史栈重新回到前台时调用。
    /// 适合恢复输入、刷新动态数据。
    /// </summary>
    protected virtual void OnResume()
    {
    }

    // ================================================================
    //  页面栈内部调用
    // ================================================================

    /// <summary>首次打开页面并播放进入过渡</summary>
    internal IEnumerator PlayOpen()
    {
        EnsureReferences();
        gameObject.SetActive(true);

        OnOpen();
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

    /// <summary>恢复页面并播放进入过渡</summary>
    internal IEnumerator PlayResume()
    {
        EnsureReferences();
        gameObject.SetActive(true);

        OnResume();
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

    /// <summary>暂停页面并播放退出过渡</summary>
    internal IEnumerator PlayPause()
    {
        EnsureReferences();

        OnPause();
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

    /// <summary>关闭页面并播放退出过渡</summary>
    internal IEnumerator PlayClose()
    {
        EnsureReferences();

        OnClose();
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

    /// <summary>立即显示页面，常用于启动页初始化</summary>
    internal void ShowImmediateAsOpen()
    {
        EnsureReferences();
        gameObject.SetActive(true);
        OnOpen();
        IsVisible = true;
        SnapToShown();
    }

    /// <summary>立即显示页面，常用于从历史栈恢复</summary>
    internal void ShowImmediateAsResume()
    {
        EnsureReferences();
        gameObject.SetActive(true);
        OnResume();
        IsVisible = true;
        SnapToShown();
    }

    /// <summary>立即隐藏页面，常用于初始化和强制清空</summary>
    internal void HideImmediateAsClose(bool invokeClose)
    {
        EnsureReferences();

        if (invokeClose)
        {
            OnClose();
        }

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
