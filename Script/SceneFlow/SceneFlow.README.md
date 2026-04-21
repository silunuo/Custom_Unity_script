# 🎬 SceneFlow — Unity 通用场景切换管理器

> 一套复制进去就能用的场景切换脚本。  
> 解决同步切场景、异步加载进度、统一事件回调、加载中重复点击拦截这些常见问题。

---

## 📁 文件清单

| 文件 | 职责 |
|------|------|
| `SceneFlow.cs` | 场景切换管理器（单例），负责同步/异步加载和事件分发 |

---

## 🚀 快速上手（5 分钟接入）

### 第一步：导入文件

将 `SceneFlow.cs` 放进项目里，比如：

```text
Assets/Scripts/SceneFlow/
```

### 第二步：场景配置

1. 在启动场景创建空物体，命名为 `SceneFlow`
2. 挂载 `SceneFlow` 组件
3. 按需勾住日志输出

### 第三步：代码调用

```csharp
// 同步切场景
SceneFlow.Instance.LoadScene("MainMenu");

// 异步切场景
SceneFlow.Instance.LoadSceneAsync("Battle");
```

---

## 📋 API 参考

| 成员 | 签名 | 说明 |
|------|------|------|
| `LoadScene` | `void LoadScene(string sceneName, LoadSceneMode mode = Single)` | 同步加载场景 |
| `LoadSceneAsync` | `void LoadSceneAsync(string sceneName, LoadSceneMode mode = Single)` | 异步加载场景 |
| `IsLoading` | `bool IsLoading { get; }` | 当前是否正在加载 |
| `CurrentProgress` | `float CurrentProgress { get; }` | 当前加载进度（0~1） |
| `OnLoadStarted` | `Action<string, LoadSceneMode>` | 加载开始事件 |
| `OnLoadProgress` | `Action<string, float>` | 进度更新事件 |
| `OnLoadCompleted` | `Action<string, LoadSceneMode>` | 加载完成事件 |

### 事件顺序

#### 同步加载

```text
OnLoadStarted -> OnLoadProgress(0) -> OnLoadProgress(1) -> OnLoadCompleted
```

#### 异步加载

```text
OnLoadStarted -> OnLoadProgress(0) -> ... -> OnLoadProgress(1) -> OnLoadCompleted
```

### 行为约定

| 场景 | 行为 |
|------|------|
| 正在加载时再次请求加载 | 直接拦截并输出警告 |
| 场景名为空 | 输出错误并忽略 |
| 同步加载成功 | 进度直接从 0 跳到 1 |
| 异步加载进度 | 统一换算到 0~1 |

---

## 🔧 常见使用场景

### 加载页进度条

```csharp
using UnityEngine;
using UnityEngine.UI;

public class LoadingPanel : MonoBehaviour
{
    [SerializeField] private Slider progressBar;
    [SerializeField] private GameObject root;

    private void OnEnable()
    {
        SceneFlow.Instance.OnLoadStarted += OnLoadStarted;
        SceneFlow.Instance.OnLoadProgress += OnLoadProgress;
        SceneFlow.Instance.OnLoadCompleted += OnLoadCompleted;
    }

    private void OnDisable()
    {
        SceneFlow.Instance.OnLoadStarted -= OnLoadStarted;
        SceneFlow.Instance.OnLoadProgress -= OnLoadProgress;
        SceneFlow.Instance.OnLoadCompleted -= OnLoadCompleted;
    }

    private void OnLoadStarted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        root.SetActive(true);
        progressBar.value = 0f;
    }

    private void OnLoadProgress(string sceneName, float progress)
    {
        progressBar.value = progress;
    }

    private void OnLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        root.SetActive(false);
    }
}
```

### 战斗场景切换时联动 BGM / 存档

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleEntrance : MonoBehaviour
{
    private void OnEnable()
    {
        SceneFlow.Instance.OnLoadStarted += HandleLoadStarted;
        SceneFlow.Instance.OnLoadCompleted += HandleLoadCompleted;
    }

    private void OnDisable()
    {
        SceneFlow.Instance.OnLoadStarted -= HandleLoadStarted;
        SceneFlow.Instance.OnLoadCompleted -= HandleLoadCompleted;
    }

    public void EnterBattle()
    {
        // 切场景前先保存当前流程
        GameSaveManager.Instance.Save(0);
        SceneFlow.Instance.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
    }

    private void HandleLoadStarted(string sceneName, LoadSceneMode mode)
    {
        if (sceneName == "BattleScene")
        {
            AudioManager.Instance.StopBGM();
        }
    }

    private void HandleLoadCompleted(string sceneName, LoadSceneMode mode)
    {
        if (sceneName == "BattleScene")
        {
            AudioManager.Instance.PlayBGM("Battle");
        }
    }
}
```

### Additive 加载子场景

```csharp
SceneFlow.Instance.LoadSceneAsync("UIOverlay", LoadSceneMode.Additive);
```

---

## ⚠️ 使用建议

- `SceneFlow` 适合放在启动场景，并让它跨场景常驻。
- 如果你有加载页 UI，推荐只监听事件，不要把 UI 逻辑写进 `SceneFlow.cs`。
- 想做更复杂的切场景动画，可以在 `OnLoadStarted` 和 `OnLoadCompleted` 里接自己的转场系统。
- 当前版本不处理跨线程加载控制，也不接 Addressables。

---

## ❓ FAQ

**Q：为什么不内置加载界面？**  
A：不同项目的 UI 差别很大，管理器只给进度和事件更通用。

**Q：异步进度为什么是 0 到 1？**  
A：Unity 原生 `AsyncOperation.progress` 常停在 `0.9`，这里已经帮你统一换算好了。

**Q：加载中再点一次按钮会怎样？**  
A：会被拦下，不会并发切场景。

**Q：支持 `LoadSceneMode.Additive` 吗？**  
A：支持，直接把 `mode` 参数改成 `Additive` 就行。

