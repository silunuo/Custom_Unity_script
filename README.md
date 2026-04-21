# Custom_Unity_script

一个给独立开发者用的 Unity 通用脚本仓库，主打“复制即用”。  
每个模块尽量做到：

- 直接拷进项目就能接
- 有单独 README 讲清接法
- 保持小而实用，不一上来堆太重

当前仓库以基础设施和常用管理器为主，先解决这些高频问题：

- 音频管理
- 对象池
- 存档
- 事件分发
- 状态机
- 场景切换

---

## 仓库结构

```text
Script/
├── audio/          通用音频模块
├── ObjectP/        通用对象池模块
├── SaveData/       通用存档模块
├── EventBus/       轻量事件总线
├── StateMachine/   通用状态机骨架
├── SceneFlow/      场景切换管理器
└── Example/        示例或实验脚本
```

---

## 模块总览

| 模块 | 解决什么问题 | 适合什么项目 | 复制路径 | 接入入口 |
|------|--------------|--------------|----------|----------|
| AudioManager | 统一管理 SFX / BGM、音量、暂停恢复 | 2D / 3D、小型到中型项目 | `Script/audio/` | `AudioManager.cs` |
| ObjectPool | 解决高频创建销毁带来的 GC 和卡顿 | 子弹、特效、敌人、飘字 | `Script/ObjectP/` | `PoolManager.cs`、`PoolConfig.cs` |
| SaveManager | 提供多槽位、自动存档、加密、版本迁移 | 单机游戏、流程型项目 | `Script/SaveData/` | `SaveManager.cs`、`SaveSettings.cs` |
| EventBus | 解耦系统间通知，不用字符串事件名 | UI、流程、战斗事件广播 | `Script/EventBus/` | `EventBus.cs` |
| StateMachine | 给角色、敌人、流程控制提供状态切换骨架 | AI、角色行为、UI 状态流程 | `Script/StateMachine/` | `StateMachine.cs`、`IState.cs` |
| SceneFlow | 统一同步/异步切场景、进度和回调 | 有加载页、转场、流程切换的项目 | `Script/SceneFlow/` | `SceneFlow.cs` |

---

## 模块文档入口

| 模块 | 文档 |
|------|------|
| AudioManager | `Script/audio/Audio.README.md` |
| ObjectPool | `Script/ObjectP/ObjectPool.README.md` |
| SaveManager | `Script/SaveData/Save.README.md` |
| EventBus | `Script/EventBus/EventBus.README.md` |
| StateMachine | `Script/StateMachine/StateMachine.README.md` |
| SceneFlow | `Script/SceneFlow/SceneFlow.README.md` |

---

## 怎么用

### 方式一：只拿单个模块

适合你只想解决一个问题。

例子：

- 只想接音频，就拷 `Script/audio/`
- 只想接对象池，就拷 `Script/ObjectP/`
- 只想接事件总线，就拷 `Script/EventBus/`

### 方式二：组合多个模块

适合小项目快速搭底子。

一套常见组合：

- `audio`：管理 BGM / SFX
- `ObjectP`：管理子弹、特效
- `SaveData`：管理存档
- `SceneFlow`：做切场景和加载页
- `EventBus`：给模块间发通知

---

## 示例和实验区

`Script/Example/` 下面放的是示例脚本或还没完全泛化的内容。

当前示例：

- `Script/Example/OrbitAnimation.cs`：一个径向展开动画脚本，先当示例保留，不放进主推通用模块列表

---

## 当前建议

如果你是第一次拿这个仓库，推荐先看这几个：

1. `ObjectPool`
2. `AudioManager`
3. `SaveManager`
4. `EventBus`
5. `SceneFlow`

---

## 下一步候选

后面准备继续补这些方向：

- `TimerKit`：延迟调用、循环任务、冷却器
- `Localization`：轻量本地化键值读取
- `Addressables` 轻封装：异步加载、缓存、释放

---

## 说明

- 目标 Unity 版本先按 `2021.3 LTS+`
- 先不做 UPM 和示例工程，继续走“复制即用”
- 现阶段优先做通用基础设施，不做太重的编辑器工具
