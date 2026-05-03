// ============================================================================
// UIPageStack.cs — 页面栈管理器
//
// 功能：
//   1. 自动扫描 pageRoot 下全部 UIPage
//   2. 统一管理页面打开、替换、回退
//   3. 维护页面历史栈
//   4. 过渡期间拦住重复操作
//
// 行为：
//   1. Open：当前页暂停，新页打开，历史保留
//   2. Replace：当前页关闭，新页打开，历史不保留当前页
//   3. Back：当前页关闭，上一个页面恢复
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景内页面栈管理器。
/// 适合菜单、设置、背包、选择关卡这类页面式 UI 流程。
/// </summary>
public class UIPageStack : MonoBehaviour
{
    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 页面容器 ===")]
    [Tooltip("页面根节点。留空时默认使用当前物体")]
    [SerializeField] private Transform pageRoot;

    [Tooltip("启动时默认打开的页面 ID。留空则不自动打开")]
    [SerializeField] private string startPageID = "";

    [Header("=== 调试 ===")]
    [Tooltip("是否输出页面栈日志")]
    [SerializeField] private bool enableDebugLog = true;

    // ================================================================
    //  运行时数据
    // ================================================================

    private readonly Dictionary<string, UIPage> _pages = new Dictionary<string, UIPage>();
    private readonly List<UIPage> _history = new List<UIPage>();

    private Coroutine _transitionCoroutine;

    /// <summary>当前页，没有时为 null</summary>
    public UIPage CurrentPage => _history.Count > 0 ? _history[_history.Count - 1] : null;

    /// <summary>当前是否可以回退</summary>
    public bool CanGoBack => _history.Count > 1;

    /// <summary>当前是否处于过渡中</summary>
    public bool IsBusy { get; private set; }

    // ================================================================
    //  生命周期
    // ================================================================

    private void Awake()
    {
        if (pageRoot == null)
        {
            pageRoot = transform;
        }

        CachePages();
        HideAllPagesOnInit();
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(startPageID)) return;

        if (TryGetPage(startPageID, out UIPage startPage))
        {
            _history.Add(startPage);
            startPage.ShowImmediateAsOpen();

            if (enableDebugLog)
            {
                Debug.Log($"[UIPageStack] 启动页：{startPage.PageID}");
            }
        }
    }

    // ================================================================
    //  页面栈 API
    // ================================================================

    /// <summary>
    /// 打开一个新页面。
    /// 当前页会暂停并保留在历史栈里。
    /// </summary>
    /// <param name="pageID">目标页面 ID</param>
    public void Open(string pageID)
    {
        if (IsBusy) return;
        if (!TryGetPage(pageID, out UIPage nextPage)) return;
        if (CurrentPage == nextPage) return;

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
        }

        _transitionCoroutine = StartCoroutine(OpenRoutine(nextPage));
    }

    /// <summary>
    /// 用一个新页面替换当前页。
    /// 当前页会关闭并从历史栈移除。
    /// </summary>
    /// <param name="pageID">目标页面 ID</param>
    public void Replace(string pageID)
    {
        if (IsBusy) return;
        if (!TryGetPage(pageID, out UIPage nextPage)) return;
        if (CurrentPage == nextPage) return;

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
        }

        _transitionCoroutine = StartCoroutine(ReplaceRoutine(nextPage));
    }

    /// <summary>
    /// 返回上一个页面。
    /// 根页无法回退时返回 false。
    /// </summary>
    public bool Back()
    {
        if (IsBusy) return false;
        if (_history.Count <= 1) return false;

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
        }

        _transitionCoroutine = StartCoroutine(BackRoutine());
        return true;
    }

    /// <summary>
    /// 关闭全部页面并清空历史。
    /// </summary>
    public void CloseAll()
    {
        if (IsBusy) return;

        for (int i = _history.Count - 1; i >= 0; i--)
        {
            _history[i].HideImmediateAsClose(true);
        }

        _history.Clear();

        if (enableDebugLog)
        {
            Debug.Log("[UIPageStack] 已清空全部页面");
        }
    }

    // ================================================================
    //  协程流程
    // ================================================================

    /// <summary>打开新页面流程</summary>
    private IEnumerator OpenRoutine(UIPage nextPage)
    {
        IsBusy = true;

        UIPage previous = CurrentPage;
        if (previous != null)
        {
            yield return previous.PlayPause();
        }

        // 避免同一页面实例在历史里重复出现
        _history.Remove(nextPage);
        _history.Add(nextPage);

        yield return nextPage.PlayOpen();

        if (enableDebugLog)
        {
            Debug.Log($"[UIPageStack] Open -> {nextPage.PageID}");
        }

        IsBusy = false;
        _transitionCoroutine = null;
    }

    /// <summary>替换当前页面流程</summary>
    private IEnumerator ReplaceRoutine(UIPage nextPage)
    {
        IsBusy = true;

        UIPage previous = CurrentPage;
        if (previous != null)
        {
            _history.RemoveAt(_history.Count - 1);
            yield return previous.PlayClose();
        }

        _history.Remove(nextPage);
        _history.Add(nextPage);

        yield return nextPage.PlayOpen();

        if (enableDebugLog)
        {
            Debug.Log($"[UIPageStack] Replace -> {nextPage.PageID}");
        }

        IsBusy = false;
        _transitionCoroutine = null;
    }

    /// <summary>回退流程</summary>
    private IEnumerator BackRoutine()
    {
        IsBusy = true;

        UIPage current = CurrentPage;
        _history.RemoveAt(_history.Count - 1);

        if (current != null)
        {
            yield return current.PlayClose();
        }

        UIPage previous = CurrentPage;
        if (previous != null)
        {
            yield return previous.PlayResume();
        }

        if (enableDebugLog && previous != null)
        {
            Debug.Log($"[UIPageStack] Back -> {previous.PageID}");
        }

        IsBusy = false;
        _transitionCoroutine = null;
    }

    // ================================================================
    //  初始化
    // ================================================================

    /// <summary>扫描 pageRoot 下所有页面并建立缓存</summary>
    private void CachePages()
    {
        _pages.Clear();

        UIPage[] pages = pageRoot.GetComponentsInChildren<UIPage>(true);
        for (int i = 0; i < pages.Length; i++)
        {
            UIPage page = pages[i];
            string id = page.PageID;

            if (_pages.ContainsKey(id))
            {
                Debug.LogError($"[UIPageStack] 检测到重复页面 ID：{id}");
                continue;
            }

            _pages.Add(id, page);
        }
    }

    /// <summary>初始化时先把全部页面隐藏</summary>
    private void HideAllPagesOnInit()
    {
        foreach (UIPage page in _pages.Values)
        {
            page.HideImmediateAsClose(false);
        }
    }

    // ================================================================
    //  内部辅助
    // ================================================================

    /// <summary>按 ID 查找页面</summary>
    private bool TryGetPage(string pageID, out UIPage page)
    {
        if (string.IsNullOrEmpty(pageID))
        {
            Debug.LogError("[UIPageStack] pageID 为空");
            page = null;
            return false;
        }

        if (_pages.TryGetValue(pageID, out page))
        {
            return true;
        }

        Debug.LogError($"[UIPageStack] 页面 '{pageID}' 不存在");
        return false;
    }
}
