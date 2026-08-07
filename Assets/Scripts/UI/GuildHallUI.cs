using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险者公会大厅 UI 根组件（挂 GuildHallUI 预制体上，便于后续接逻辑）
/// </summary>
public class GuildHallUI : MonoBehaviour
{
    [Header("顶部")]
    public Text goldText;
    public Button goldPlusButton;

    [Header("左侧")]
    public Button mailButton;
    public Button noticeButton;
    public Button activityButton;

    [Header("右侧")]
    public Button rankButton;
    public Button shopButton;
    public Button settingsButton;

    [Header("场景热点")]
    public Button noticeBoardButton;
    public Button licenseHallButton;
    public Button armoryButton;
    public Button receptionistButton;

    [Header("底部导航")]
    public Button navGuildButton;
    public Button navCharacterButton;
    public Button navAdventureButton;
    public Button navTavernButton;
    public Button navLogButton;

    void Awake()
    {
        // 统一 Canvas：Camera / 720×1280 / Match Height
        UICanvasSetup.ApplyOn(gameObject, Camera.main);
        GameFonts.ApplyToHierarchy(transform);
        WireDefaultClicks();
        RefreshGold();
    }

    void WireDefaultClicks()
    {
        if (navAdventureButton != null)
            navAdventureButton.onClick.AddListener(() => GameSceneManager.Instance?.LoadBattleScene());
        if (navGuildButton != null)
            navGuildButton.onClick.AddListener(() => Debug.Log("[GuildHall] 公会（当前页）"));
        if (navCharacterButton != null)
            navCharacterButton.onClick.AddListener(() => Debug.Log("[GuildHall] 角色（待实现）"));
        if (navTavernButton != null)
            navTavernButton.onClick.AddListener(() => Debug.Log("[GuildHall] 酒馆（待实现）"));
        if (navLogButton != null)
            navLogButton.onClick.AddListener(() => Debug.Log("[GuildHall] 冒险日志（待实现）"));
        if (shopButton != null)
            shopButton.onClick.AddListener(() => Debug.Log("[GuildHall] 商城（待实现）"));
        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => Debug.Log("[GuildHall] 设置（待实现）"));
        if (mailButton != null)
            mailButton.onClick.AddListener(() => Debug.Log("[GuildHall] 邮件（待实现）"));
        if (noticeButton != null)
            noticeButton.onClick.AddListener(() => Debug.Log("[GuildHall] 公告（待实现）"));
        if (activityButton != null)
            activityButton.onClick.AddListener(() => Debug.Log("[GuildHall] 活动（待实现）"));
        if (rankButton != null)
            rankButton.onClick.AddListener(() => Debug.Log("[GuildHall] 排行榜（待实现）"));
        if (noticeBoardButton != null)
            noticeBoardButton.onClick.AddListener(() => Debug.Log("[GuildHall] 公告板（待实现）"));
        if (licenseHallButton != null)
            licenseHallButton.onClick.AddListener(() => Debug.Log("[GuildHall] 执照大厅（待实现）"));
        if (armoryButton != null)
            armoryButton.onClick.AddListener(() => Debug.Log("[GuildHall] 武器库（待实现）"));
        if (receptionistButton != null)
            receptionistButton.onClick.AddListener(() => Debug.Log("[GuildHall] 看板娘（待实现）"));
    }

    public void RefreshGold()
    {
        if (goldText == null) return;
        long gold = SaveSystem.Instance?.Data?.totalGold ?? 0;
        goldText.text = gold.ToString("N0");
    }
}
