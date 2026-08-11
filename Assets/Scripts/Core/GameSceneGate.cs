/// <summary>
/// 三场景流程门控：Boot / Town / Battle 各跑各的，其它场景不硬拉战斗初始化。
/// </summary>
public static class GameSceneGate
{
    public static string ActiveName => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    public static bool IsBoot => ActiveName == GameSceneManager.BOOT_SCENE;
    public static bool IsTown => ActiveName == GameSceneManager.TOWN_SCENE;
    public static bool IsBattle => ActiveName == GameSceneManager.BATTLE_SCENE;

    public static bool IsKnownGameplayScene => IsBoot || IsTown || IsBattle;
}
