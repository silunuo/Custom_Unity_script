// ============================================================================
// StateMachine.cs — 通用状态机骨架
//
// 功能：
//   1. 管理当前状态引用
//   2. 统一处理状态切换
//   3. 提供 Update / FixedUpdate 转发入口
//   4. 支持传入 null 清空当前状态
//
// 约定：
//   1. 切状态顺序固定为 OnExit -> OnEnter
//   2. 重复切到同一个状态实例时会直接忽略
//   3. 不绑定 MonoBehaviour，宿主脚本自己转发 Update / FixedUpdate
// ============================================================================

/// <summary>
/// 通用状态机。
/// 适合敌人 AI、角色行为、流程推进、UI 状态切换等场景。
/// </summary>
public class StateMachine
{
    // ================================================================
    //  状态
    // ================================================================

    /// <summary>
    /// 当前正在运行的状态。
    /// 没有状态时为 null。
    /// </summary>
    public IState CurrentState { get; private set; }

    // ================================================================
    //  状态切换
    // ================================================================

    /// <summary>
    /// 切换到新状态。
    /// 若 nextState 与当前状态是同一个实例，则本次切换会被忽略。
    /// 传入 null 时会退出当前状态并清空状态机。
    /// </summary>
    /// <param name="nextState">目标状态</param>
    public void ChangeState(IState nextState)
    {
        if (ReferenceEquals(CurrentState, nextState))
        {
            return;
        }

        if (CurrentState != null)
        {
            CurrentState.OnExit();
        }

        CurrentState = nextState;

        if (CurrentState != null)
        {
            CurrentState.OnEnter();
        }
    }

    // ================================================================
    //  帧更新转发
    // ================================================================

    /// <summary>
    /// 将宿主脚本的 Update 转发给当前状态。
    /// 当前没有状态时静默忽略。
    /// </summary>
    public void Update()
    {
        if (CurrentState == null) return;
        CurrentState.OnUpdate();
    }

    /// <summary>
    /// 将宿主脚本的 FixedUpdate 转发给当前状态。
    /// 当前没有状态时静默忽略。
    /// </summary>
    public void FixedUpdate()
    {
        if (CurrentState == null) return;
        CurrentState.OnFixedUpdate();
    }
}
