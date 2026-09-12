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
            if (_applicationIsQuitting || !Application.isPlaying)
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
