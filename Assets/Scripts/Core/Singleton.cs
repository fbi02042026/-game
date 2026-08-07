using UnityEngine;

/// <summary>
/// 全局单例基类，继承这个的类全局唯一
/// </summary>
/// <typeparam name="T"></typeparam>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static object _lock = new object();

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                try
                {
                    lock (_lock)
                    {
                        if (_instance == null) // double-check
                        {
                            _instance = FindObjectOfType<T>();
                            if (_instance == null)
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
                    // 构造期间调用 FindObjectOfType 会抛异常，返回 null 而非崩溃
                    // 调用方应在 Awake/Start 中访问 Instance，而非字段初始化器
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
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }
}
