using System.Collections;
using UnityEngine;

/// <summary>
/// 场景流程（三场景）：
/// · Boot — 持久化系统、进 Town
/// · Town — 公会/酒馆/角色/日志等全部在此，切页不 LoadScene
/// · Battle — 仅战斗；异步加载 + Loading 遮罩
/// </summary>
public class GameSceneManager : Singleton<GameSceneManager>
{
    public const string BOOT_SCENE = "Boot";
    public const string TOWN_SCENE = "Town";
    public const string BATTLE_SCENE = "Battle";

    bool _loadingBattle;
    bool _loadingTown;

    protected override void Awake()
    {
        base.Awake();
    }

    public void LoadTownScene() => LoadTownSceneAsync();

    public void GoMainHub() => LoadTownSceneAsync();

    /// <summary>Battle → Town，异步避免主线程假死</summary>
    public void ReturnToTown()
    {
        LoadTownSceneAsync();
    }

    void LoadTownSceneAsync()
    {
        if (_loadingTown) return;
        _loadingBattle = false;
        if (!isActiveAndEnabled)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(TOWN_SCENE);
            return;
        }
        StartCoroutine(LoadTownAsync());
    }

    IEnumerator LoadTownAsync()
    {
        _loadingTown = true;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != TOWN_SCENE)
            BattleLoadingOverlay.Show("返回城镇…");

        yield return null;
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(TOWN_SCENE);
        if (op == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(TOWN_SCENE);
            _loadingTown = false;
            BattleLoadingOverlay.Hide();
            yield break;
        }
        yield return CoTrackLoadProgress(op);
        _loadingTown = false;
        BattleLoadingOverlay.Hide();
    }

    /// <summary>仅 Town → Battle 切场景；Town 内底栏切页不走这里</summary>
    public void LoadBattleScene()
    {
        if (_loadingBattle) return;
        if (!isActiveAndEnabled)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(BATTLE_SCENE);
            return;
        }
        StartCoroutine(LoadBattleAsync());
    }

    System.Collections.IEnumerator LoadBattleAsync()
    {
        _loadingBattle = true;
        BattleLoadingOverlay.Show("进入冒险…");
        yield return null;

        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(BATTLE_SCENE);
        if (op == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(BATTLE_SCENE);
            _loadingBattle = false;
            BattleLoadingOverlay.Hide();
            yield break;
        }
        yield return CoTrackLoadProgress(op);
        _loadingBattle = false;
        BattleLoadingOverlay.Hide();
    }

    /// <summary>Unity AsyncOperation 进度常卡在 0.9，映射到 0~100% 显示</summary>
    static IEnumerator CoTrackLoadProgress(AsyncOperation op)
    {
        if (op == null) yield break;
        while (!op.isDone)
        {
            float p = Mathf.Clamp01(op.progress / 0.9f);
            BattleLoadingOverlay.SetProgress(p);
            yield return null;
        }
        BattleLoadingOverlay.SetProgress(1f);
        yield return null;
    }

    public void EnterAdventure() => LoadBattleScene();

    public void ReloadCurrentScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
