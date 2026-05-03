# 🪟 UIPopup — 弹窗栈模块

> 一套给确认框、模态设置框、二次提示框用的轻量弹窗管理。  
> 重点解决顶层交互、遮罩点击、Esc 关闭和弹窗堆叠。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `UIPopup.cs` | 弹窗基类，统一弹窗生命周期和交互配置 |
| `UIPopupManager.cs` | 弹窗栈管理器，负责打开、关闭、顶层恢复交互 |

---

## 🚀 快速上手（5 分钟）

### 第一步：拷文件

建议目录：

```text
Assets/Scripts/UIPopup/
```

### 第二步：建弹窗根节点

1. 创建空物体，命名为 `UIPopupManager`
2. 挂载 `UIPopupManager.cs`
3. 把真正的弹窗容器拖给 `popupRoot`
4. 如果要支持遮罩点击，准备一个全屏 `Image` 拖给 `modalBlocker`

### 第三步：每个弹窗挂 `UIPopup`

弹窗根节点建议这样放：

```text
ConfirmPopup
├── CanvasGroup
├── CanvasGroupFadeTransition（可选）
└── 继承 UIPopup 的弹窗脚本（可选）
```

### 第四步：代码调用

```csharp
popupManager.Open("ConfirmPopup");
popupManager.CloseTop();
popupManager.CloseAll();
```

---

## 📋 API 参考

### UIPopup

| 成员 | 说明 |
|------|------|
| `PopupID` | 弹窗有效 ID，留空时默认用物体名 |
| `CloseOnEsc` | 顶层弹窗按 Esc 时是否允许关闭 |
| `CloseOnMaskClick` | 点击遮罩时是否允许关闭 |
| `IsVisible` | 当前是否可见 |
| `CanvasGroup` | 当前弹窗用的 CanvasGroup |
| `Transition` | 当前弹窗用的过渡组件 |

### UIPopup 生命周期

| 钩子 | 说明 |
|------|------|
| `OnShow()` | 弹窗显示时调用 |
| `OnHide()` | 弹窗关闭时调用 |

### UIPopupManager

| 成员 | 签名 | 说明 |
|------|------|------|
| `Open` | `void Open(string popupID)` | 打开一个弹窗 |
| `Open<TPopup>` | `TPopup Open<TPopup>(string popupID, Action<TPopup> beforeShow = null)` | 打开并在显示前写数据 |
| `CloseTop` | `bool CloseTop()` | 关闭顶层弹窗 |
| `Close` | `bool Close(string popupID)` | 关闭指定弹窗 |
| `CloseAll` | `void CloseAll()` | 关闭全部弹窗 |
| `CurrentPopup` | `UIPopup CurrentPopup { get; }` | 当前顶层弹窗 |
| `HasOpenPopup` | `bool HasOpenPopup { get; }` | 当前是否有打开中的弹窗 |
| `IsBusy` | `bool IsBusy { get; }` | 当前是否正在过渡 |

### 行为约定

| 场景 | 行为 |
|------|------|
| 打开弹窗 A 后再开 B | A 保留显示，但不可交互；B 成为顶层 |
| `CloseTop()` | 关闭当前顶层，上一层恢复交互 |
| 遮罩点击 | 只处理顶层弹窗，且要 `CloseOnMaskClick = true` |
| Esc 关闭 | 只处理顶层弹窗，且要 `CloseOnEsc = true` |

---

## 🔧 最小例子

### 确认弹窗

```csharp
using TMPro;
using UnityEngine;

public class ConfirmPopup : UIPopup
{
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;

    public void SetContent(string title, string body)
    {
        titleLabel.text = title;
        bodyLabel.text = body;
    }
}
```

### 打开前写标题和正文

```csharp
using UnityEngine;

public class DeleteSaveButton : MonoBehaviour
{
    [SerializeField] private UIPopupManager popupManager;

    public void AskDelete()
    {
        popupManager.Open<ConfirmPopup>("ConfirmPopup", popup =>
        {
            popup.SetContent("删除存档", "这一步删了就回不来了。");
        });
    }
}
```

### 手动关闭顶层弹窗

```csharp
public void OnClickClose()
{
    popupManager.CloseTop();
}
```

---

## ⚠️ 使用建议

- 遮罩 `Image` 最好单独做一个全屏物体，挂在弹窗层里。
- 顶层弹窗的关闭按钮、Esc、遮罩点击最好只保留一套明确逻辑，别互相打架。
- `beforeShow` 很适合在打开前塞文案、按钮回调、当前上下文数据。
- 弹窗 ID 最好手填，别全靠物体名。

---

## ❓ FAQ

**Q：为什么下层弹窗不直接隐藏？**  
A：保留显示更像常见桌面和游戏里的模态层结构，也方便做多层确认。

**Q：`Close(string popupID)` 能关下层弹窗吗？**  
A：能。顶层会走完整关闭流程；下层会直接移出栈并隐藏。

**Q：为什么遮罩点击只关顶层？**  
A：这样逻辑最稳，不会出现点一下把整串弹窗都冲掉。

