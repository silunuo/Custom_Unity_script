# Custom_Unity_script

已完成

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

## 示例和实验区

`Script/Example/` 

当前示例：

- `Script/Example/OrbitAnimation.cs`：一个径向展开动画脚本

---
