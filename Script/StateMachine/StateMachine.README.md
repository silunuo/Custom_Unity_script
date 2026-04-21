# 🔁 StateMachine — Unity 通用状态机骨架

> 一套纯 C# 的轻量状态机模板。  
> 适合敌人 AI、角色行为、流程控制、UI 状态切换这类“当前只能处于一个状态”的场景。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `IState.cs` | 状态生命周期接口 |
| `StateMachine.cs` | 状态切换和 Update 转发骨架 |

---

## 🚀 快速上手（5 分钟接入）

### 第一步：导入文件

将 `IState.cs` 和 `StateMachine.cs` 放进项目里，比如：

```text
Assets/Scripts/StateMachine/
```

### 第二步：定义状态类

```csharp
public class IdleState : IState
{
    public void OnEnter() { Debug.Log("进入待机"); }
    public void OnUpdate() { }
    public void OnFixedUpdate() { }
    public void OnExit() { Debug.Log("离开待机"); }
}
```

### 第三步：在宿主脚本里持有状态机

```csharp
public class EnemyController : MonoBehaviour
{
    private StateMachine _stateMachine;
    private IdleState _idleState;

    private void Awake()
    {
        _stateMachine = new StateMachine();
        _idleState = new IdleState();
    }

    private void Start()
    {
        _stateMachine.ChangeState(_idleState);
    }

    private void Update()
    {
        _stateMachine.Update();
    }

    private void FixedUpdate()
    {
        _stateMachine.FixedUpdate();
    }
}
```

---

## 📋 API 参考

### IState

| 方法 | 说明 |
|------|------|
| `OnEnter()` | 切入状态时调用 |
| `OnUpdate()` | 宿主脚本每帧转发调用 |
| `OnFixedUpdate()` | 宿主脚本物理帧转发调用 |
| `OnExit()` | 离开状态时调用 |

### StateMachine

| 成员 | 签名 | 说明 |
|------|------|------|
| `CurrentState` | `IState CurrentState { get; }` | 当前状态，没有时为 null |
| `ChangeState` | `void ChangeState(IState nextState)` | 切换到新状态 |
| `Update` | `void Update()` | 转发宿主脚本的 Update |
| `FixedUpdate` | `void FixedUpdate()` | 转发宿主脚本的 FixedUpdate |

### 行为约定

| 场景 | 行为 |
|------|------|
| 普通切状态 | 固定顺序：`OnExit -> OnEnter` |
| 重复切到同一个状态实例 | 直接忽略 |
| `ChangeState(null)` | 退出当前状态并清空状态机 |
| 当前没有状态时调用 `Update/FixedUpdate` | 静默忽略 |

---

## 🔧 常见使用场景

### 敌人巡逻 / 追击 / 攻击

```csharp
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField] private Transform target;

    private StateMachine _stateMachine;
    private PatrolState _patrolState;
    private ChaseState _chaseState;
    private AttackState _attackState;

    private void Awake()
    {
        _stateMachine = new StateMachine();
        _patrolState = new PatrolState(this);
        _chaseState = new ChaseState(this);
        _attackState = new AttackState(this);
    }

    private void Start()
    {
        _stateMachine.ChangeState(_patrolState);
    }

    private void Update()
    {
        _stateMachine.Update();

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance < 1.5f)
        {
            _stateMachine.ChangeState(_attackState);
        }
        else if (distance < 6f)
        {
            _stateMachine.ChangeState(_chaseState);
        }
        else
        {
            _stateMachine.ChangeState(_patrolState);
        }
    }

    private void FixedUpdate()
    {
        _stateMachine.FixedUpdate();
    }
}

public class PatrolState : IState
{
    private readonly EnemyController _owner;

    public PatrolState(EnemyController owner)
    {
        _owner = owner;
    }

    public void OnEnter()
    {
        Debug.Log("进入巡逻");
    }

    public void OnUpdate()
    {
    }

    public void OnFixedUpdate()
    {
    }

    public void OnExit()
    {
        Debug.Log("离开巡逻");
    }
}

public class ChaseState : IState
{
    private readonly EnemyController _owner;

    public ChaseState(EnemyController owner)
    {
        _owner = owner;
    }

    public void OnEnter()
    {
        Debug.Log("进入追击");
    }

    public void OnUpdate()
    {
    }

    public void OnFixedUpdate()
    {
    }

    public void OnExit()
    {
        Debug.Log("离开追击");
    }
}

public class AttackState : IState
{
    private readonly EnemyController _owner;

    public AttackState(EnemyController owner)
    {
        _owner = owner;
    }

    public void OnEnter()
    {
        Debug.Log("进入攻击");
    }

    public void OnUpdate()
    {
    }

    public void OnFixedUpdate()
    {
    }

    public void OnExit()
    {
        Debug.Log("离开攻击");
    }
}
```

### UI 流程切换

```csharp
public class UIPanelController : MonoBehaviour
{
    private StateMachine _stateMachine = new StateMachine();

    public void OpenMainMenu(IState mainMenuState)
    {
        _stateMachine.ChangeState(mainMenuState);
    }

    public void CloseAllPanels()
    {
        _stateMachine.ChangeState(null);
    }
}
```

---

## ⚠️ 使用建议

- 这套状态机只管“切换”和“转发”，状态数据、引用、条件判断由你自己组织。
- 如果状态很多，推荐把切换条件放在宿主控制器里，不要散在每个状态里。
- 一个状态实例可以重复复用，适合减少频繁 new。
- 如果状态依赖 `MonoBehaviour`、动画器、导航组件，就在构造函数里把宿主引用传进去。

---

## ❓ FAQ

**Q：为什么状态机不继承 `MonoBehaviour`？**  
A：这样更轻，也更通用。角色、敌人、流程控制都能复用，宿主自己转发 `Update` 就行。

**Q：为什么重复切同一个状态实例会被忽略？**  
A：这样能避免同一帧里重复触发 `OnExit / OnEnter`，状态抖动会少很多。

**Q：可以切到 `null` 吗？**  
A：可以。`ChangeState(null)` 会先执行当前状态的 `OnExit()`，然后把状态机清空。

**Q：状态里能不能自己切状态？**  
A：可以，只要你把 `StateMachine` 引用传给状态类。但大多数时候，把切换条件放在宿主控制器里更好查。

