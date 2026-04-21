# 📣 EventBus — Unity 轻量事件总线

> 一套复制进去就能用的轻量消息分发脚本。  
> 适合 UI 通知、角色死亡广播、存档完成提示、系统间解耦通信。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `EventBus.cs` | 全局事件总线，负责订阅、取消订阅、发布、清空监听 |

---

## 🚀 快速上手（3 分钟接入）

### 第一步：导入文件

将 `EventBus.cs` 放进项目里，比如：

```text
Assets/Scripts/EventBus/
```

### 第二步：定义事件类型

```csharp
public struct UIButtonClickedEvent
{
    public string buttonID;
}
```

### 第三步：订阅事件

```csharp
private void OnEnable()
{
    EventBus.Subscribe<UIButtonClickedEvent>(OnUIButtonClicked);
}

private void OnDisable()
{
    EventBus.Unsubscribe<UIButtonClickedEvent>(OnUIButtonClicked);
}

private void OnUIButtonClicked(UIButtonClickedEvent evt)
{
    Debug.Log($"按钮点击：{evt.buttonID}");
}
```

### 第四步：发布事件

```csharp
public void OnClickStartButton()
{
    EventBus.Publish(new UIButtonClickedEvent
    {
        buttonID = "Start"
    });
}
```

---

## 📋 API 参考

| 方法 | 签名 | 说明 |
|------|------|------|
| `Subscribe<T>` | `void Subscribe<T>(Action<T> handler)` | 订阅指定类型事件 |
| `Unsubscribe<T>` | `void Unsubscribe<T>(Action<T> handler)` | 取消订阅指定类型事件 |
| `Publish<T>` | `void Publish<T>(T evt)` | 发布一个事件对象 |
| `ClearAll` | `void ClearAll()` | 清空全部事件监听 |

### 行为约定

| 场景 | 行为 |
|------|------|
| 同一个 handler 重复订阅 | 自动忽略，不会重复触发 |
| 取消未订阅的 handler | 静默忽略 |
| 单个监听抛异常 | 记录错误日志，其他监听继续执行 |
| 不同事件类型 | 互相隔离，不会串线 |

---

## 🔧 常见使用场景

### UI 点击通知

```csharp
public struct UIButtonClickedEvent
{
    public string buttonID;
}

public class MenuButton : MonoBehaviour
{
    [SerializeField] private string buttonID;

    public void OnClick()
    {
        EventBus.Publish(new UIButtonClickedEvent { buttonID = buttonID });
    }
}
```

### 角色死亡通知

```csharp
public struct PlayerDiedEvent
{
    public int remainLives;
}

public class PlayerHealth : MonoBehaviour
{
    public void Die(int remainLives)
    {
        EventBus.Publish(new PlayerDiedEvent
        {
            remainLives = remainLives
        });
    }
}
```

### 存档完成通知

```csharp
public struct SaveCompletedEvent
{
    public int slotIndex;
    public bool success;
}

public class SaveToast : MonoBehaviour
{
    private void OnEnable()
    {
        EventBus.Subscribe<SaveCompletedEvent>(OnSaveCompleted);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SaveCompletedEvent>(OnSaveCompleted);
    }

    private void OnSaveCompleted(SaveCompletedEvent evt)
    {
        if (evt.success)
        {
            Debug.Log($"槽位 {evt.slotIndex} 存档成功");
        }
    }
}
```

---

## ⚠️ 使用限制

- 按主线程使用场景设计，不处理跨线程同步。
- 不做消息缓存，晚订阅的对象收不到之前发过的消息。
- 不做粘性事件，不保存最后一条事件。
- 事件对象建议用 `struct` 或轻量 `class`，别塞大对象图。

---

## ❓ FAQ

**Q：为什么不用字符串事件名？**  
A：字符串容易拼错，也不方便带结构化数据。泛型事件对象更稳，IDE 也更好提示。

**Q：为什么重复订阅会被忽略？**  
A：Unity 里常见写法是 `OnEnable` 订阅、`OnDisable` 取消。如果重复注册没拦住，问题很难查。

**Q：可以跨场景用吗？**  
A：可以。`EventBus` 是静态类，不依赖场景物体。

**Q：什么时候该调用 `ClearAll()`？**  
A：重开游戏、切主流程、或做测试重置时可以手动清空。

