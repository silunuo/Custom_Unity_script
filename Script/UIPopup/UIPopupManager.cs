// ============================================================================
// UIPopupManager.cs — 弹窗栈管理器
//
// 功能：
//   1. 自动扫描 popupRoot 下全部 UIPopup
//   2. 统一管理弹窗打开、关闭、堆叠
//   3. 维护顶层弹窗交互和遮罩状态
//   4. 过渡期间拦住重复操作
//
// 行为：
//   1. 新弹窗打开时，旧顶层弹窗保留显示但禁用交互
//   2. 关闭顶层后，上一层弹窗恢复交互
//   3. 遮罩点击只处理顶层弹窗
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景内弹窗栈管理器。
/// 适合确认框、设置框、二次确认弹窗等模态 UI 流程。
/// </summary>
public class UIPopupManager : MonoBehaviour
{
    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 弹窗容器 ===")]
    [Tooltip("弹窗根节点。留空时默认使用当前物体")]
    [SerializeField] private Transform popupRoot;

    [Tooltip("模态遮罩图片。留空则不处理遮罩点击")]
    [SerializeField] private Image modalBlocker;

    [Header("=== 调试 ===")]
    [Tooltip("是否输出弹窗栈日志")]
    [SerializeField] private bool enableDebugLog = true;

    // ================================================================
    //  运行时数据
    // ================================================================

    private readonly Dictionary<string, UIPopup> _popups = new Dictionary<string, UIPopup>();
    private readonly List<UIPopup> _stack = new List<UIPopup>();

    private Coroutine _transitionCoroutine;
    private Button _blockerButton;

    /// <summary>当前顶层弹窗，没有时为 null</summary>
    public UIPopup CurrentPopup => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

    /// <summary>当前是否存在打开中的弹窗</summary>
    public bool HasOpenPopup => _stack.Count > 0;

    /// <summary>当前是否处于过渡中</summary>
    public bool IsBusy { get; private set; }

    // ================================================================
    //  生命周期
    // ================================================================

    private void Awake()
    {
        if (popupRoot == null)
        {
            popupRoot = transform;
        }

        CachePopups();
        HideAllPopupsOnInit();
        SetupModalBlocker();
    }

    // ================================================================
    //  弹窗栈 API
    // ================================================================

    /// <summary>
    /// 打开一个弹窗。
    /// </summary>
    /// <param name="popupID">目标弹窗 ID</param>
    public void Open(string popupID)
    {
        Open<UIPopup>(popupID, null);
    }

    /// <summary>
    /// 打开一个弹窗，并在显示前注入数据。
    /// </summary>
    /// <typeparam name="TPopup">目标弹窗类型</typeparam>
    /// <param name="popupID">目标弹窗 ID</param>
    /// <param name="beforeShow">显示前回调，可用于写标题、正文、按钮文案</param>
    public TPopup Open<TPopup>(string popupID, Action<TPopup> beforeShow = null) where TPopup : UIPopup
    {
        if (IsBusy) return null;
        if (!TryGetPopup(popupID, out UIPopup popup)) return null;
        if (CurrentPopup == popup) return popup as TPopup;

        TPopup typedPopup = popup as TPopup;
        if (typedPopup == null)
        {
            Debug.LogError($"[UIPopupManager] 弹窗 '{popupID}' 不是 {typeof(TPopup).Name} 类型");
            return null;
        }

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
        }

        _transitionCoroutine = StartCoroutine(OpenRoutine(typedPopup, () => beforeShow?.Invoke(typedPopup)));
        return typedPopup;
    }

    /// <summary>
    /// 关闭顶层弹窗。
    /// 没有弹窗时返回 false。
    /// </summary>
    public bool CloseTop()
    {
        if (IsBusy) return false;
        if (CurrentPopup == null) return false;

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
        }

        _transitionCoroutine = StartCoroutine(CloseTopRoutine());
        return true;
    }

    /// <summary>
    /// 关闭指定 ID 的弹窗。
    /// 若目标正好是顶层弹窗，会走完整关闭流程。
    /// 若目标是下层弹窗，则立即移出栈。
    /// </summary>
    /// <param name="popupID">目标弹窗 ID</param>
    public bool Close(string popupID)
    {
        if (IsBusy) return false;

        int index = FindOpenPopupIndex(popupID);
        if (index < 0) return false;

        if (index == _stack.Count - 1)
        {
            return CloseTop();
        }

        UIPopup popup = _stack[index];
        _stack.RemoveAt(index);
        popup.HideImmediate(true);
        RefreshPopupStates();

        if (enableDebugLog)
        {
            Debug.Log($"[UIPopupManager] Close -> {popup.PopupID}");
        }

        return true;
    }

    /// <summary>
    /// 关闭全部弹窗并清空栈。
    /// </summary>
    public void CloseAll()
    {
        if (IsBusy) return;

        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            _stack[i].HideImmediate(true);
        }

        _stack.Clear();
        RefreshPopupStates();

        if (enableDebugLog)
        {
            Debug.Log("[UIPopupManager] 已关闭全部弹窗");
        }
    }

    // ================================================================
    //  协程流程
    // ================================================================

    /// <summary>打开弹窗流程</summary>
    private IEnumerator OpenRoutine(UIPopup popup, Action beforeShow)
    {
        IsBusy = true;

        if (CurrentPopup != null)
        {
            CurrentPopup.SetInteractable(false);
        }

        _stack.Remove(popup);
        _stack.Add(popup);

        popup.transform.SetAsLastSibling();
        UpdateModalBlocker();

        beforeShow?.Invoke();
        yield return popup.PlayShow();

        RefreshPopupStates();

        if (enableDebugLog)
        {
            Debug.Log($"[UIPopupManager] Open -> {popup.PopupID}");
        }

        IsBusy = false;
        _transitionCoroutine = null;
    }

    /// <summary>关闭顶层弹窗流程</summary>
    private IEnumerator CloseTopRoutine()
    {
        IsBusy = true;

        UIPopup current = CurrentPopup;
        _stack.RemoveAt(_stack.Count - 1);

        if (current != null)
        {
            yield return current.PlayHide();
        }

        RefreshPopupStates();

        if (enableDebugLog && current != null)
        {
            Debug.Log($"[UIPopupManager] CloseTop -> {current.PopupID}");
        }

        IsBusy = false;
        _transitionCoroutine = null;
    }

    // ================================================================
    //  初始化
    // ================================================================

    /// <summary>扫描 popupRoot 下全部弹窗并建立缓存</summary>
    private void CachePopups()
    {
        _popups.Clear();

        UIPopup[] popups = popupRoot.GetComponentsInChildren<UIPopup>(true);
        for (int i = 0; i < popups.Length; i++)
        {
            UIPopup popup = popups[i];
            string id = popup.PopupID;

            if (_popups.ContainsKey(id))
            {
                Debug.LogError($"[UIPopupManager] 检测到重复弹窗 ID：{id}");
                continue;
            }

            _popups.Add(id, popup);
        }
    }

    /// <summary>初始化时先隐藏全部弹窗</summary>
    private void HideAllPopupsOnInit()
    {
        foreach (UIPopup popup in _popups.Values)
        {
            popup.HideImmediate(false);
        }
    }

    /// <summary>初始化遮罩点击处理</summary>
    private void SetupModalBlocker()
    {
        if (modalBlocker == null) return;

        modalBlocker.gameObject.SetActive(false);
        modalBlocker.raycastTarget = true;

        _blockerButton = modalBlocker.GetComponent<Button>();
        if (_blockerButton == null)
        {
            _blockerButton = modalBlocker.gameObject.AddComponent<Button>();
        }

        _blockerButton.transition = Selectable.Transition.None;
        _blockerButton.onClick.RemoveListener(OnModalBlockerClicked);
        _blockerButton.onClick.AddListener(OnModalBlockerClicked);
    }

    // ================================================================
    //  内部辅助
    // ================================================================

    /// <summary>按当前栈状态刷新交互和遮罩</summary>
    private void RefreshPopupStates()
    {
        for (int i = 0; i < _stack.Count; i++)
        {
            bool isTop = i == _stack.Count - 1;
            _stack[i].SetInteractable(isTop);
        }

        UpdateModalBlocker();
    }

    /// <summary>刷新遮罩显隐和层级</summary>
    private void UpdateModalBlocker()
    {
        if (modalBlocker == null) return;

        bool shouldShow = CurrentPopup != null;
        modalBlocker.gameObject.SetActive(shouldShow);

        if (!shouldShow) return;

        CurrentPopup.transform.SetAsLastSibling();

        int topIndex = CurrentPopup.transform.GetSiblingIndex();
        int blockerIndex = Mathf.Max(0, topIndex - 1);
        modalBlocker.transform.SetSiblingIndex(blockerIndex);
    }

    /// <summary>处理遮罩点击</summary>
    private void OnModalBlockerClicked()
    {
        if (IsBusy) return;
        if (CurrentPopup == null) return;
        if (!CurrentPopup.CloseOnMaskClick) return;

        CloseTop();
    }

    /// <summary>按 ID 查找缓存弹窗</summary>
    private bool TryGetPopup(string popupID, out UIPopup popup)
    {
        if (string.IsNullOrEmpty(popupID))
        {
            Debug.LogError("[UIPopupManager] popupID 为空");
            popup = null;
            return false;
        }

        if (_popups.TryGetValue(popupID, out popup))
        {
            return true;
        }

        Debug.LogError($"[UIPopupManager] 弹窗 '{popupID}' 不存在");
        return false;
    }

    /// <summary>在打开栈里查找弹窗索引</summary>
    private int FindOpenPopupIndex(string popupID)
    {
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            if (_stack[i].PopupID == popupID)
            {
                return i;
            }
        }

        return -1;
    }
}
