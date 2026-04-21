// ============================================================================
// IState.cs — 通用状态接口
//
// 约定一组最基础的状态生命周期回调：
//   1. OnEnter：切入状态时调用
//   2. OnUpdate：宿主脚本每帧转发调用
//   3. OnFixedUpdate：宿主脚本物理帧转发调用
//   4. OnExit：离开状态时调用
//
// 配合 StateMachine 使用时，切状态顺序固定为：
//   OnExit -> OnEnter
// ============================================================================

/// <summary>
/// 通用状态接口。
/// 适合角色、敌人、流程控制、UI 页面流程等状态切换场景。
/// </summary>
public interface IState
{
    /// <summary>
    /// 切入当前状态时调用。
    /// 适合做初始化、播放动画、重置计时器等。
    /// </summary>
    void OnEnter();

    /// <summary>
    /// 宿主脚本每帧转发调用。
    /// 适合做输入处理、AI 逻辑、计时推进等。
    /// </summary>
    void OnUpdate();

    /// <summary>
    /// 宿主脚本每个物理帧转发调用。
    /// 适合做 Rigidbody 移动、物理检测等。
    /// </summary>
    void OnFixedUpdate();

    /// <summary>
    /// 离开当前状态时调用。
    /// 适合做清理、停协程、移除临时效果等。
    /// </summary>
    void OnExit();
}
