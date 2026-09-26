using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 服务登记器（2026-09-26 建立，对应 Docs/模块化与防复发方案_2026-09-26.md Phase 9.1 / 规则 R3）。
///
/// 【它要解决的复发机制】
/// 项目里有 59 个 `public static Xxx Instance` 单例、51 个文件用 DontDestroyOnLoad、
/// 204 个 public static 可变字段。它们跨场景不重置，于是「第一次进战斗是对的、第二次被上次残留覆盖」，
/// 主观感受就是「一会儿好一会儿坏」= 复发。而 Core/Singleton.cs 还有「找不到就 new 一个」的兜底，
/// 实例可能凭空产生、时序不确定。
///
/// 【本类不做什么】
/// · 不替换现有 59 个单例 —— 它们正在工作，一个都不动（铁律：没问题的不许改）。
/// · 不要求任何现有系统改写法。
///
/// 【本类做什么：只堵增量】
/// 新写的系统请这样用：
///     // 装配侧（AutoGameInitializer / 场景 bootstrap，显式登记）
///     ServiceRegistry.Register&lt;IMercService&gt;(new MercService());
///     // 使用侧（任何时候）
///     var svc = ServiceRegistry.Get&lt;IMercService&gt;();
/// 拿不到就是拿不到（返回 null 并可按需报错），**绝不凭空 new 一个**。
///
/// 已处理的坑：登记的是 UnityEngine.Object（MonoBehaviour/ScriptableObject）时，
/// 场景销毁后引用会变成「非 null 的僵尸对象」，`is T` 仍然成立但 `== null` 为真 ——
/// 这里显式识别并剔除，避免跨场景残留（这正是 Get 里那段 Unity Object 判空的用意）。
/// </summary>
public static class ServiceRegistry
{
    static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

    /// <summary>登记一个服务。重复登记会覆盖（后者生效）。</summary>
    public static void Register<T>(T service) where T : class
    {
        if (service == null)
        {
            Debug.LogError("[ServiceRegistry] 试图登记 null 服务：" + typeof(T).Name);
            return;
        }
        _services[typeof(T)] = service;
    }

    /// <summary>注销一个服务（系统销毁 / 退出场景时调用）。</summary>
    public static void Unregister<T>() where T : class
    {
        _services.Remove(typeof(T));
    }

    /// <summary>
    /// 取服务。拿不到返回 null（不 new、不报错、不抛异常）—— 由调用方决定怎么处理。
    /// 若登记的是已销毁的 UnityEngine.Object，会自动剔除并返回 null。
    /// </summary>
    public static T Get<T>() where T : class
    {
        if (!_services.TryGetValue(typeof(T), out var s))
            return null;

        // 注意两个判空含义不同，别写反：
        //   `s is UnityEngine.Object`  → 类型检查（不受 == 重载影响），判断它是不是引擎对象；
        //   `uo == null`               → Unity 的 == 重载，判断它是否**已被销毁**（僵尸引用）。
        // 若写成 `if (uo != null) { if (uo == null) ... }`，已销毁对象会让外层判断直接为 false，
        // 僵尸引用反而被当成有效服务返回 —— 正是本类要防的跨场景残留。
        if (s is UnityEngine.Object uo)
        {
            if (uo == null)
            {
                _services.Remove(typeof(T));
                return null;
            }
            return uo as T;
        }
        return s as T;
    }

    /// <summary>是否有可用服务（已登记且未失效）。</summary>
    public static bool Has<T>() where T : class
    {
        return Get<T>() != null;
    }

    /// <summary>清空全部登记。切场景 / 退出战斗 / 单元测试 setUp 时调用，杜绝跨局残留。</summary>
    public static void Clear()
    {
        _services.Clear();
    }

    /// <summary>当前登记数量（诊断用）。</summary>
    public static int Count => _services.Count;
}
