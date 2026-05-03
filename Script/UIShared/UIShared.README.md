# 🧱 UIShared — 前台流程共享支撑

> 这一组不是单独拿来用的完整模块，主要给 `UIPage`、`UIPopup`、`Toast` 复用。  
> 里面放的是 UI 根入口和统一过渡接口。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `UIRoot.cs` | 场景内前台流程总入口，统一处理 `Esc` |
| `UITransition.cs` | 页面 / 弹窗 / Toast 共用的过渡接口 |
| `CanvasGroupFadeTransition.cs` | 默认淡入淡出实现 |

---

## 🚀 快速上手

### 第一步：把文件拷进项目

建议目录：

```text
Assets/Scripts/UIShared/
```

### 第二步：场景里建一个 UIRoot

1. 创建空物体，命名为 `UIRoot`
2. 挂载 `UIRoot.cs`
3. 把 `UIPageStack`、`UIPopupManager`、`ToastManager` 拖进去

### 第三步：给页面 / 弹窗 / Toast 根节点补过渡

如果你想用默认淡入淡出：

1. 在目标 UI 根节点挂 `CanvasGroup`
2. 再挂 `CanvasGroupFadeTransition`
3. 把对应基类里的 `transition` 字段拖过去

---

## 📋 API 参考

### UIRoot

| 成员 | 说明 |
|------|------|
| `PageStack` | 页面栈引用 |
| `PopupManager` | 弹窗管理器引用 |
| `ToastManager` | 轻提示管理器引用 |

### Esc 处理顺序

```text
顶层弹窗可关 -> 关闭顶层弹窗
顶层弹窗不能关 / 没弹窗 -> 页面回退
都处理不了 -> 忽略
```

### UITransition

| 方法 | 说明 |
|------|------|
| `PlayEnter(CanvasGroup group)` | 播放进入过渡 |
| `PlayExit(CanvasGroup group)` | 播放退出过渡 |
| `SnapToShown(CanvasGroup group)` | 立即切到显示状态 |
| `SnapToHidden(CanvasGroup group)` | 立即切到隐藏状态 |

### CanvasGroupFadeTransition

| 字段 | 说明 |
|------|------|
| `enterDuration` | 进入时长 |
| `enterCurve` | 进入曲线 |
| `exitDuration` | 退出时长 |
| `exitCurve` | 退出曲线 |
| `ignoreTimeScale` | 是否忽略 `Time.timeScale` |

---

## 🔧 常见用法

### 给页面挂默认过渡

```text
PageRoot
├── CanvasGroup
├── CanvasGroupFadeTransition
└── 你的页面脚本（继承 UIPage）
```

### 给弹窗挂默认过渡

```text
PopupRoot
├── CanvasGroup
├── CanvasGroupFadeTransition
└── 你的弹窗脚本（继承 UIPopup）
```

---

## ❓ FAQ

**Q：`UIShared` 能单独拿来用吗？**  
A：可以拷，但通常要和 `UIPage`、`UIPopup`、`Toast` 一起用才完整。

**Q：为什么用 `CanvasGroup` 做默认过渡？**  
A：够轻，也够通用。大多数菜单、弹窗、提示都能直接吃这一套。

**Q：复杂动画怎么办？**  
A：自己继承 `UITransition`，把默认的淡入淡出替掉就行。

