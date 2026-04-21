// ============================================================================
// EventBus.cs — 轻量全局事件总线
//
// 功能：
//   1. 使用泛型事件对象分发消息，不依赖字符串事件名
//   2. 支持订阅、取消订阅、发布、清空全部监听
//   3. 重复订阅同一个处理函数时自动忽略，避免重复触发
//   4. 单个监听抛异常时不会打断其他监听
//
// 使用方法：
//   1. 定义事件数据类型（class / struct 均可）
//   2. 在需要接收消息的地方调用 Subscribe<T>()
//   3. 在需要派发消息的地方调用 Publish<T>()
//
// 约束：
//   1. 仅按主线程使用场景设计
//   2. 不做跨线程同步
//   3. 不做消息缓存和粘性事件
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 轻量全局事件总线。
/// 适合 UI 通知、角色状态广播、系统间松耦合通信。
/// </summary>
public static class EventBus
{
    // ================================================================
    //  运行时数据
    // ================================================================

    // 事件类型 -> 对应的监听委托
    private static readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

    // ================================================================
    //  订阅
    // ================================================================

    /// <summary>
    /// 订阅指定类型的事件。
    /// 同一个处理函数重复订阅时会被忽略，避免重复触发。
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    /// <param name="handler">事件处理函数</param>
    public static void Subscribe<T>(Action<T> handler)
    {
        if (handler == null) return;

        Type eventType = typeof(T);

        if (_handlers.TryGetValue(eventType, out Delegate existing))
        {
            // 防止同一个处理函数被重复加入
            foreach (Delegate callback in existing.GetInvocationList())
            {
                if (callback.Equals(handler))
                {
                    return;
                }
            }

            _handlers[eventType] = Delegate.Combine(existing, handler);
            return;
        }

        _handlers[eventType] = handler;
    }

    /// <summary>
    /// 取消订阅指定类型的事件。
    /// 处理函数不存在时静默忽略。
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    /// <param name="handler">事件处理函数</param>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (handler == null) return;

        Type eventType = typeof(T);
        if (!_handlers.TryGetValue(eventType, out Delegate existing)) return;

        Delegate updated = Delegate.Remove(existing, handler);

        if (updated == null)
        {
            _handlers.Remove(eventType);
        }
        else
        {
            _handlers[eventType] = updated;
        }
    }

    // ================================================================
    //  发布
    // ================================================================

    /// <summary>
    /// 发布一个事件对象。
    /// 会按订阅顺序依次调用所有监听，单个监听报错时会记录日志并继续后续监听。
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    /// <param name="evt">事件对象</param>
    public static void Publish<T>(T evt)
    {
        Type eventType = typeof(T);
        if (!_handlers.TryGetValue(eventType, out Delegate existing)) return;

        Delegate[] callbacks = existing.GetInvocationList();

        for (int i = 0; i < callbacks.Length; i++)
        {
            Action<T> handler = callbacks[i] as Action<T>;
            if (handler == null) continue;

            try
            {
                handler.Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventBus] 事件 '{eventType.Name}' 的监听执行失败：{ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    // ================================================================
    //  清理
    // ================================================================

    /// <summary>
    /// 清空全部事件监听。
    /// 适合切场景、重开游戏、单元测试重置等场景。
    /// </summary>
    public static void ClearAll()
    {
        _handlers.Clear();
    }
}
