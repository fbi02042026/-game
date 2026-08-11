using UnityEngine;

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
