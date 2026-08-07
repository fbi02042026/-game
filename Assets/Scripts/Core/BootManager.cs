using UnityEngine;

/// <summary>
/// Boot场景入口：初始化跨场景持久对象，加载存档，自动跳转到Town
/// 挂在Boot场景唯一的空GameObject上
/// 注意：项目中存在自定义SceneManager类，必须使用完全限定名避免冲突
/// </summary>
public class BootManager : MonoBehaviour
{
    [Header("加载延迟")]
    public float loadDelay = 0.5f;

    void Awake()
    {
        Application.runInBackground = true;
        GamePerf.ApplyStartup();

        // 创建持久根节点（跨场景不销毁）
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
        Invoke(nameof(GotoTown), loadDelay);
    }

    void GotoTown()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Town");
    }
}
