using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景管理器
/// </summary>
public class SceneManager : Singleton<SceneManager>
{
    public const string TOWN_SCENE = "Town";
    public const string BATTLE_SCENE = "Battle";

    /// <summary>
    /// 加载城镇场景
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
}
