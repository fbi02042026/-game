using UnityEngine;

/// <summary>
/// 战斗必需单例标记：<see cref="Singleton{T}.Instance"/> 找不到时返回 null 并打 Error，禁止 getter 里 new 空物体。
/// Boot/城镇系统不要实现本接口（仍允许自动创建以免直接 Play 子场景崩）。
/// </summary>
public interface ICombatBoundSingleton { }

/// <summary>
/// 全局单例基类，继承这个的类全局唯一
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static object _lock = new object();
    private static bool _applicationIsQuitting;

    public static T Instance
    {
        get
        {
            // Application.isPlaying 在 MonoBehaviour 构造函数 / 实例字段初始化器里被访问会抛
            // "get_IsPlaying is not allowed to be called from a MonoBehaviour constructor"。
            // 那种时机本就只应返回已有实例、绝不能建物，故静默降级返回 _instance。
            bool inPlay;
            try
            {
                inPlay = Application.isPlaying;
            }
            catch (System.Exception)
            {
                return _instance;
            }

            if (_applicationIsQuitting || !inPlay)
                return _instance;

            if (_instance == null)
            {
                try
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = FindObjectOfType<T>();
                            // 场景卸载/退出 Play 时禁止在 getter 里新建，避免 OnDestroy 链上刷 GridBackpackSystem 等
                            if (_instance == null && Application.isPlaying && !_applicationIsQuitting)
                            {
                                if (typeof(ICombatBoundSingleton).IsAssignableFrom(typeof(T)))
                                {
                                    Debug.LogError("[Singleton] 战斗必需系统未挂载，拒绝自动创建空对象: "
                                        + typeof(T).Name
                                        + "。应由 AutoGameInitializer.EnsureGameRoot 装配。");
                                    return null;
                                }
                                GameObject go = new GameObject(typeof(T).Name);
                                _instance = go.AddComponent<T>();
                                DontDestroyOnLoad(go);
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Singleton] {typeof(T).Name}.Instance 在不当时机被访问: {e.Message}");
                    return null;
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 静默查询单例：仅在 <see cref="_instance"/> 为空时做一次 <see cref="FindObjectOfType{T}"/>；
    /// 不自动 new 空物体、不打印任何 Log/LogError、异常也静默降级返回当前值（不向外抛）。
    /// 找不到就返回 null。用于系统尚未装配时（如 BattleUI.Awake 早于 AutoGameInitializer）
    /// 查询战斗单例，避免刷 Error、也不误关交互。
    /// </summary>
    public static T InstanceQuiet
    {
        get
        {
            // Application.isPlaying 在 MonoBehaviour 构造期访问会抛，静默降级返回已有实例、绝不建物
            bool inPlay;
            try
            {
                inPlay = Application.isPlaying;
            }
            catch (System.Exception)
            {
                return _instance;
            }

            if (_applicationIsQuitting || !inPlay)
                return _instance;

            if (_instance == null)
            {
                try
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = FindOnly();
                    }
                }
                catch (System.Exception)
                {
                    return _instance;
                }
            }
            return _instance;
        }
    }

    /// <summary>仅做一次场景查找，失败返回 null（不 new、不报错）。</summary>
    static T FindOnly()
    {
        try
        {
            return FindObjectOfType<T>();
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // 只销毁重复组件：GameRoot 上挂着十几个系统，销毁整个物体会连带干掉战斗管理器
            Destroy(this);
            return;
        }
        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }
}
