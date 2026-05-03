// ============================================================================
// CanvasGroupFadeTransition.cs — 基于 CanvasGroup 的淡入淡出过渡
//
// 功能：
//   1. 使用 CanvasGroup.alpha 做基础淡入淡出
//   2. 支持进入和退出分别配置时长、曲线
//   3. 支持忽略 Time.timeScale，方便暂停菜单也能正常播过渡
//
// 适合：
//   1. 页面切换
//   2. 模态弹窗
//   3. 轻提示淡入淡出
// ============================================================================

using System.Collections;
using UnityEngine;

/// <summary>
/// 默认的 CanvasGroup 淡入淡出过渡。
/// 不做复杂位移和缩放，只提供一层最小默认效果。
/// </summary>
public class CanvasGroupFadeTransition : UITransition
{
    // ================================================================
    //  Inspector 配置
    // ================================================================

    [Header("=== 进入过渡 ===")]
    [Tooltip("进入过渡时长（秒）")]
    [Min(0f)]
    [SerializeField] private float enterDuration = 0.2f;

    [Tooltip("进入过渡曲线")]
    [SerializeField] private AnimationCurve enterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("=== 退出过渡 ===")]
    [Tooltip("退出过渡时长（秒）")]
    [Min(0f)]
    [SerializeField] private float exitDuration = 0.15f;

    [Tooltip("退出过渡曲线")]
    [SerializeField] private AnimationCurve exitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("=== 时间 ===")]
    [Tooltip("是否忽略 Time.timeScale。暂停菜单建议开启")]
    [SerializeField] private bool ignoreTimeScale = true;

    // ================================================================
    //  进入 / 退出
    // ================================================================

    /// <summary>
    /// 播放淡入过渡。
    /// </summary>
    public override IEnumerator PlayEnter(CanvasGroup group)
    {
        if (group == null) yield break;

        if (enterDuration <= 0f)
        {
            SnapToShown(group);
            yield break;
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        float timer = 0f;
        while (timer < enterDuration)
        {
            timer += GetDeltaTime();
            float t = Mathf.Clamp01(timer / enterDuration);
            group.alpha = enterCurve.Evaluate(t);
            yield return null;
        }

        SnapToShown(group);
    }

    /// <summary>
    /// 播放淡出过渡。
    /// </summary>
    public override IEnumerator PlayExit(CanvasGroup group)
    {
        if (group == null) yield break;

        if (exitDuration <= 0f)
        {
            SnapToHidden(group);
            yield break;
        }

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        float timer = 0f;
        while (timer < exitDuration)
        {
            timer += GetDeltaTime();
            float t = Mathf.Clamp01(timer / exitDuration);
            group.alpha = 1f - exitCurve.Evaluate(t);
            yield return null;
        }

        SnapToHidden(group);
    }

    // ================================================================
    //  立即切换
    // ================================================================

    /// <summary>
    /// 立即切到显示状态。
    /// </summary>
    public override void SnapToShown(CanvasGroup group)
    {
        if (group == null) return;

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    /// <summary>
    /// 立即切到隐藏状态。
    /// </summary>
    public override void SnapToHidden(CanvasGroup group)
    {
        if (group == null) return;

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    // ================================================================
    //  内部辅助
    // ================================================================

    /// <summary>按配置返回当前应该使用的 deltaTime</summary>
    private float GetDeltaTime()
    {
        return ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
