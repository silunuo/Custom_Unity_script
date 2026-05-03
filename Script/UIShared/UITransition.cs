// ============================================================================
// UITransition.cs — 通用 UI 过渡接口
//
// 给页面、弹窗、Toast 统一提供进入 / 退出过渡能力。
// 默认按组件方式挂在同一个 UI 根节点上，方便直接在 Inspector 中拖引用。
//
// 使用方式：
//   1. 让具体过渡类继承 UITransition
//   2. 在 PlayEnter / PlayExit 里实现协程动画
//   3. 在 SnapToShown / SnapToHidden 里实现立即切换状态
// ============================================================================

using System.Collections;
using UnityEngine;

/// <summary>
/// 通用 UI 过渡接口。
/// 用于统一页面、弹窗、Toast 的显示和隐藏动画。
/// </summary>
public abstract class UITransition : MonoBehaviour
{
    /// <summary>
    /// 播放进入过渡。
    /// 调用方会在显示 UI 时启动这个协程。
    /// </summary>
    /// <param name="group">目标 CanvasGroup</param>
    public abstract IEnumerator PlayEnter(CanvasGroup group);

    /// <summary>
    /// 播放退出过渡。
    /// 调用方会在隐藏 UI 时启动这个协程。
    /// </summary>
    /// <param name="group">目标 CanvasGroup</param>
    public abstract IEnumerator PlayExit(CanvasGroup group);

    /// <summary>
    /// 立即切到显示状态，不播放动画。
    /// 常用于初始化和强制重置。
    /// </summary>
    /// <param name="group">目标 CanvasGroup</param>
    public abstract void SnapToShown(CanvasGroup group);

    /// <summary>
    /// 立即切到隐藏状态，不播放动画。
    /// 常用于初始化和强制重置。
    /// </summary>
    /// <param name="group">目标 CanvasGroup</param>
    public abstract void SnapToHidden(CanvasGroup group);
}
