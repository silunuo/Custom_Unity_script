# 📚 UIPage — 页面栈模块

> 一套给菜单页、设置页、背包页这类页面式 UI 用的轻量栈管理。  
> 重点解决页面打开、替换、回退和历史保留。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `UIPage.cs` | 页面基类，统一页面生命周期 |
| `UIPageStack.cs` | 页面栈管理器，负责打开、替换、回退 |

---

## 🚀 快速上手（5 分钟）

### 第一步：拷文件

建议目录：

```text
Assets/Scripts/UIPage/
```

### 第二步：建页面栈根节点

1. 创建空物体，命名为 `UIPageStack`
2. 挂载 `UIPageStack.cs`
3. 把真正的页面容器拖给 `pageRoot`
4. 需要默认启动页的话，填 `startPageID`

### 第三步：每个页面挂 `UIPage`

每个页面根节点建议这样放：

```text
MainMenuPage
├── CanvasGroup
├── CanvasGroupFadeTransition（可选）
└── 继承 UIPage 的页面脚本（可选）
```

如果只是最简单页面，直接挂 `UIPage` 也能跑。

### 第四步：代码调用

```csharp
pageStack.Open("Settings");
pageStack.Replace("SaveSelect");
pageStack.Back();
```

---

## 📋 API 参考

### UIPage

| 成员 | 说明 |
|------|------|
| `PageID` | 页面有效 ID，留空时默认用物体名 |
| `IsVisible` | 当前页面是否可见 |
| `CanvasGroup` | 当前页面用的 CanvasGroup |
| `Transition` | 当前页面用的过渡组件 |

### UIPage 生命周期

| 钩子 | 说明 |
|------|------|
| `OnOpen()` | 页面首次被打开时调用 |
| `OnClose()` | 页面被关闭并移出历史时调用 |
| `OnPause()` | 当前页被别的页面覆盖时调用 |
| `OnResume()` | 从历史栈返回时调用 |

### UIPageStack

| 成员 | 签名 | 说明 |
|------|------|------|
| `Open` | `void Open(string pageID)` | 打开新页面，保留当前页历史 |
| `Replace` | `void Replace(string pageID)` | 替换当前页，不保留当前页历史 |
| `Back` | `bool Back()` | 回到上一页 |
| `CloseAll` | `void CloseAll()` | 关闭全部页面并清空历史 |
| `CurrentPage` | `UIPage CurrentPage { get; }` | 当前页 |
| `CanGoBack` | `bool CanGoBack { get; }` | 当前是否还能后退 |
| `IsBusy` | `bool IsBusy { get; }` | 当前是否正在过渡 |

### 行为约定

| 场景 | 行为 |
|------|------|
| `Open(A)` 后再 `Open(B)` | A 走 `OnPause`，B 走 `OnOpen` |
| `Replace(B)` | 当前页走 `OnClose` 并移出历史 |
| `Back()` | 当前页走 `OnClose`，上一个页面走 `OnResume` |
| 重复打开当前页 | 直接忽略 |
| 根页回退 | 返回 `false` |

---

## 🔧 最小例子

### 简单设置页

```csharp
using TMPro;
using UnityEngine;

public class SettingsPage : UIPage
{
    [SerializeField] private TMP_Text titleLabel;

    protected override void OnOpen()
    {
        titleLabel.text = "设置";
    }

    protected override void OnResume()
    {
        Debug.Log("设置页重新回到前台");
    }
}
```

### 调用页面切换

```csharp
using UnityEngine;

public class MainMenuButtons : MonoBehaviour
{
    [SerializeField] private UIPageStack pageStack;

    public void OpenSettings()
    {
        pageStack.Open("Settings");
    }

    public void OpenSaveSelect()
    {
        pageStack.Replace("SaveSelect");
    }

    public void Back()
    {
        pageStack.Back();
    }
}
```

---

## ⚠️ 使用建议

- 页面 ID 最好手动填，别全靠物体名，后面重构 safer。
- 页面根节点都挂 `CanvasGroup`，这样默认淡入淡出和交互控制都顺。
- `Open` 适合“进子页”，`Replace` 适合“当前页直接换流程”。
- 过渡期间 `IsBusy = true`，外面按钮最好顺手做一次防连点。

---

## ❓ FAQ

**Q：页面脚本一定要继承 `UIPage` 吗？**  
A：如果要让页面栈管生命周期，最好继承。最简单页面也能直接挂 `UIPage` 本体。

**Q：`Open` 和 `Replace` 怎么选？**  
A：要保留返回链就用 `Open`；不想保留当前页历史就用 `Replace`。

**Q：为什么 `Back()` 时当前页走的是 `OnClose()`？**  
A：因为它已经离开当前历史顶层了，下一次要再回来，通常应该重新 `Open`。

