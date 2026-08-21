using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 暂停界面：战斗中的暂停菜单
/// 功能：继续游戏、设置、撤离、显示超时提示
/// </summary>
public class PausePanel : MonoBehaviour
{
    [Header("按钮")]
    public Button resumeButton;      // 继续游戏
    public Button settingsButton;    // 设置
    public Button evacuateButton;    // 撤离（结束本局）
    public Button quitButton;        // 返回主菜单

    [Header("提示文字")]
    public Text timeoutHint;         // 超时提示 "30分钟后自动撤离（与死亡同规则）"
    public Text offlineHint;         // 离线提示 "离线后角色停留在当前关卡继续战斗"

    [Header("倒计时")]
    public Text countdownText;       // 暂停倒计时显示

    // 暂停超时时间（秒）
    private const float PAUSE_TIMEOUT_SECONDS = 30f * 60f;

    // 暂停开始时间
    private float _pauseStartTime;

    // 是否处于暂停状态
    private bool _isPaused = false;

    void Awake()
    {
        // 绑定按钮事件
        if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
        if (evacuateButton != null) evacuateButton.onClick.AddListener(OnEvacuate);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitToMenu);
        GameFonts.ApplyToHierarchy(transform);
    }

    void Update()
    {
        if (!_isPaused) return;

        // 更新倒计时
        float elapsed = Time.realtimeSinceStartup - _pauseStartTime;
        float remaining = PAUSE_TIMEOUT_SECONDS - elapsed;

        if (countdownText != null)
        {
            if (remaining > 0)
            {
                TimeSpan ts = TimeSpan.FromSeconds(remaining);
                countdownText.text = $"自动撤离倒计时: {ts.Minutes:D2}:{ts.Seconds:D2}";
                countdownText.color = remaining < 300f ? Color.red : Color.white; // 最后5分钟变红
            }
            else
            {
                countdownText.text = "已超时，即将撤离...";
                // 触发自动撤离
                OnEvacuate();
            }
        }
    }

    /// <summary>
    /// 打开暂停界面
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        _isPaused = true;
        _pauseStartTime = Time.realtimeSinceStartup;

        // 冻结游戏时间（使用realtime避免受Time.timeScale影响）
        Time.timeScale = 0f;

        // 更新提示文字
        if (timeoutHint != null)
        {
            timeoutHint.text = "暂停超过30分钟将自动撤离\n（与死亡同规则：选1件遗产）";
        }
        if (offlineHint != null)
        {
            offlineHint.text = "直接退出游戏后，角色将停留在当前关卡\n下次上线可继续战斗";
        }
    }

    /// <summary>
    /// 关闭暂停界面，继续游戏
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        _isPaused = false;
        Time.timeScale = 1f;
    }

    /// <summary>
    /// 继续游戏
    /// </summary>
    void OnResume()
    {
        Hide();
        BattleUI.Instance?.CloseAllPanels();
    }

    /// <summary>
    /// 打开设置
    /// </summary>
    void OnSettings()
    {
        // 设置面板在BattleUI中管理
        BattleUI.Instance?.OnOpenSettings();
    }

    /// <summary>
    /// 撤离：结束当前战斗，回城镇冒险页
    /// </summary>
    void OnEvacuate()
    {
        Hide();
        BattleStateSaver.Instance?.SaveBattleState();
        TownHubController.PendingOpenAdventure = true;
        BattleManager.Instance?.TriggerEvacuation();
        Debug.Log("[PausePanel] 玩家主动撤离 → 冒险页");
    }

    /// <summary>
    /// 返回主菜单
    /// 保存战斗状态，回到城镇
    /// </summary>
    void OnQuitToMenu()
    {
        Hide();

        // 保存战斗状态（下次可以恢复）
        BattleStateSaver.Instance?.SaveBattleState();

        // 回到城镇场景
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadTownScene();
        }

        Debug.Log("[PausePanel] 返回主菜单，战斗状态已保存");
    }
}
