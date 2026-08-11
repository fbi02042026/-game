using UnityEngine;

/// <summary>
/// Boot 入口：初始化持久对象与存档，再进入主界面（Town / GuildHall）。
/// 流程：Boot → 主界面 → 点「冒险」进战斗。
/// </summary>
public class BootManager : MonoBehaviour
{
    [Header("加载延迟")]
    public float loadDelay = 0.5f;

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
    }

    void Start()
    {
        if (!GameSceneGate.IsBoot) return;
        Invoke(nameof(GotoMainHub), loadDelay);
    }

    void GotoMainHub()
    {
        GamePerf.Log("[Boot] 进入主界面 Town");
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.GoMainHub();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Town");
    }
}
