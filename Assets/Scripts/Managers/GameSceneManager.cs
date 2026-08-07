using UnityEngine;

/// <summary>
/// 场景管理器：跨场景单例，挂在PersistentRoot上
/// 负责 Boot → Town → Battle 的场景切换
/// 注意：项目中存在自定义SceneManager类，必须使用完全限定名避免冲突
/// </summary>
public class GameSceneManager : Singleton<GameSceneManager>
{
    public const string BOOT_SCENE = "Boot";
    public const string TOWN_SCENE = "Town";
    public const string BATTLE_SCENE = "Battle";

    protected override void Awake()
    {
        base.Awake();
        // Singleton基类会自动 DontDestroyOnLoad
    }

    /// <summary>
    /// 加载城镇场景（主菜单）
    /// </summary>
    public void LoadTownScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(TOWN_SCENE);
    }

    /// <summary>
    /// 加载战斗场景
    /// </summary>
    public void LoadBattleScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(BATTLE_SCENE);
    }

    /// <summary>
    /// 重新加载当前场景
    /// </summary>
    public void ReloadCurrentScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// 返回城镇（战斗结束/死亡后调用）
    /// </summary>
    public void ReturnToTown()
    {
        LoadTownScene();
    }
}
