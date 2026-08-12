using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Boot 入口：初始化持久对象与存档，显示登录界面；登录后再进 Town。
/// 流程：Boot(登录) → Town(主界面) → 冒险 → Battle。
/// </summary>
public class BootManager : MonoBehaviour
{
    const string LoginPrefabPath = "Prefabs/Login/LoginUI";

    [Header("加载延迟")]
    public float loadDelay = 0.3f;

    LoginUI _login;
    bool _enteringTown;

    void Awake()
    {
        if (!GameSceneGate.IsBoot) return;

        Application.runInBackground = true;
        GamePerf.ApplyStartup();

        GameObject persistentRoot = GameObject.Find("PersistentRoot");
        if (persistentRoot == null)
        {
            persistentRoot = new GameObject("PersistentRoot");
            DontDestroyOnLoad(persistentRoot);

            persistentRoot.AddComponent<SaveSystem>();
            persistentRoot.AddComponent<ConfigManager>();
            persistentRoot.AddComponent<GameSceneManager>();

            GamePerf.Log("[Boot] PersistentRoot 已创建（跨场景保留）");
        }

        EnsureCamera();
        EnsureEventSystem();
    }

    void Start()
    {
        if (!GameSceneGate.IsBoot) return;
        Invoke(nameof(ShowLogin), loadDelay);
    }

    void ShowLogin()
    {
        if (_login != null) return;

        GameObject prefab = Resources.Load<GameObject>(LoginPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Boot] 未找到登录预制体 Resources/{LoginPrefabPath}，直接进 Town");
            EnterTown();
            return;
        }

        GameObject go = Instantiate(prefab);
        go.name = "LoginUI";
        UICanvasSetup.ApplyOn(go, Camera.main);

        _login = go.GetComponent<LoginUI>();
        if (_login == null)
            _login = go.AddComponent<LoginUI>();

        _login.BindEnterTown(EnterTown);
        GameFonts.ApplyToHierarchy(go.transform);
        GamePerf.Log("[Boot] 已显示登录界面");
    }

    public void EnterTown()
    {
        if (_enteringTown) return;
        _enteringTown = true;
        GamePerf.Log("[Boot] 登录完成 → 进入主界面 Town");

        if (_login != null)
        {
            Destroy(_login.gameObject);
            _login = null;
        }

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.GoMainHub();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Town");
    }

    static void EnsureCamera()
    {
        if (Camera.main != null) return;
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.2f);
        if (camGo.GetComponent<AudioListener>() == null)
            camGo.AddComponent<AudioListener>();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}
