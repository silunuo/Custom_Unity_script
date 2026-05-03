# 🍞 Toast — 轻提示模块

> 一套给“保存成功”“获得道具”“网络异常”这类短提示用的轻量队列管理。  
> 重点解决同一时间只显示一条、后续消息排队、手动打断当前提示。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `ToastItem.cs` | 单条提示基类，负责文本和过渡 |
| `ToastManager.cs` | 提示队列管理器，负责排队、显示、打断 |

---

## 🚀 快速上手（5 分钟）

### 第一步：拷文件

建议目录：

```text
Assets/Scripts/Toast/
```

### 第二步：准备一个 Toast 根节点

1. 创建空物体，命名为 `ToastManager`
2. 挂载 `ToastManager.cs`
3. 准备一个用于显示提示的 `ToastItem` 预制体或模板物体
4. 把模板拖给 `toastPrefab`
5. 把真正承载提示的容器拖给 `toastRoot`

### 第三步：准备 Toast 模板

建议结构：

```text
ToastItem
├── CanvasGroup
├── CanvasGroupFadeTransition（可选）
└── TMP_Text
```

根节点挂 `ToastItem.cs`，并把 `TMP_Text` 拖给 `messageLabel`。

### 第四步：代码调用

```csharp
toastManager.Show("保存成功");
toastManager.Show("获得稀有道具", 2.5f);
toastManager.HideCurrent();
```

---

## 📋 API 参考

### ToastItem

| 成员 | 说明 |
|------|------|
| `SetMessage(string message)` | 设置提示文案 |
| `IsVisible` | 当前是否可见 |
| `MessageLabel` | 当前文本组件 |
| `CanvasGroup` | 当前 CanvasGroup |
| `Transition` | 当前过渡组件 |

### ToastManager

| 成员 | 签名 | 说明 |
|------|------|------|
| `Show` | `void Show(string message, float duration = -1f)` | 显示一条提示 |
| `HideCurrent` | `void HideCurrent()` | 打断当前提示 |
| `ClearQueue` | `void ClearQueue()` | 清空后续队列 |
| `IsShowing` | `bool IsShowing { get; }` | 当前是否正在显示 |

### 行为约定

| 场景 | 行为 |
|------|------|
| 连续 `Show()` | 新消息按 FIFO 进入队列 |
| 同时显示数量 | 永远只有 1 条 |
| `duration = -1` | 使用 `defaultDuration` |
| `HideCurrent()` | 只打断当前，不清空后面的队列 |
| `ClearQueue()` | 只清空后续，当前显示的不动 |

---

## 🔧 最小例子

### 奖励提示

```csharp
using UnityEngine;

public class RewardToastDemo : MonoBehaviour
{
    [SerializeField] private ToastManager toastManager;

    public void ShowReward()
    {
        toastManager.Show("获得 100 金币");
        toastManager.Show("获得 稀有钥匙", 2f);
    }
}
```

### 存档反馈

```csharp
public void OnSaveFinished(bool success)
{
    toastManager.Show(success ? "存档成功" : "存档失败");
}
```

### 手动打断当前提示

```csharp
public void SkipCurrentToast()
{
    toastManager.HideCurrent();
}
```

---

## ⚠️ 使用建议

- 提示文本尽量短，别把 Toast 当弹窗用。
- `toastPrefab` 建议做成单独预制体，后面换皮方便。
- 现在默认用 `Time.unscaledDeltaTime` 计时，暂停菜单里也能继续播提示。
- 如果你要更复杂的进出场动画，直接换掉 `ToastItem` 上的过渡组件。

---

## ❓ FAQ

**Q：为什么只显示一条？**  
A：Toast 的作用就是轻提示，叠太多读不清。

**Q：为什么后面要排队？**  
A：高频反馈很多的时候，排队比同时乱飞稳得多。

**Q：想做不同颜色、不同图标怎么办？**  
A：扩展 `ToastItem`，给它加图标、背景和样式字段，再在 `Show` 前设置内容。

