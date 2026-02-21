// ============================================================================
// IPoolable.cs — 可池化对象接口
//
// 实现此接口的组件会在对象被取出和归还时收到回调通知，
// 用于执行初始化和清理逻辑，替代 Awake/OnDestroy 的职责。
//
// 不实现此接口的对象也可以正常使用对象池，
// 只是不会收到取出/归还的回调。
//
// 使用示例：
//   public class Bullet : MonoBehaviour, IPoolable
//   {
//       private Rigidbody2D rb;
//
//       public void OnSpawn()
//       {
//           // 从池中取出时调用：重置状态
//           rb.linearVelocity = Vector2.zero;
//           GetComponent<TrailRenderer>()?.Clear();
//       }
//
//       public void OnDespawn()
//       {
//           // 归还到池中时调用：清理残留状态
//           rb.linearVelocity = Vector2.zero;
//       }
//   }
// ============================================================================

/// <summary>
/// 可池化对象接口。
/// 挂载在预制体上的任意 MonoBehaviour 均可实现此接口以接收池事件。
/// PoolManager 会自动查找对象上所有实现了 IPoolable 的组件。
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// 对象从池中取出时调用（相当于"激活/重生"）。
    /// 在此重置位置、速度、生命值等运行时状态。
    /// 调用时机：SetActive(true) 之后、返回给调用方之前。
    /// </summary>
    void OnSpawn();

    /// <summary>
    /// 对象归还到池中时调用（相当于"停用/死亡"）。
    /// 在此清理残留状态：停止协程、重置动画、清除特效拖尾等。
    /// 调用时机：回调之后、SetActive(false) 之前。
    /// </summary>
    void OnDespawn();
}
