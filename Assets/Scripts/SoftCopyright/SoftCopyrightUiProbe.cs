using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>软著自动截图：读取当前 UI 状态（Play 模式轮询用）。</summary>
public static class SoftCopyrightUiProbe
{
    public static bool IsBootScene => GameSceneGate.IsBoot;
    public static bool IsTownScene => GameSceneGate.IsTown;
    public static bool IsBattleScene => GameSceneGate.IsBattle;

    public static LoginUI Login => Object.FindObjectOfType<LoginUI>(true);
    public static HealthNoticeUI HealthNotice => HealthNoticeUI.Instance;

    public static bool IsHealthNoticeVisible =>
        HealthNotice != null && HealthNotice.IsVisible
        && TextHas(HealthNotice.TitleText, "健康游戏忠告");
    public static DialogueUI Dialogue => DialogueUI.Instance;
    public static MainNavTab? CurrentNavTab => MainBottomNav.Instance != null ? MainBottomNav.Instance.Current : (MainNavTab?)null;

    public static bool TextHas(string text, string keyword)
    {
        return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(keyword) && text.Contains(keyword);
    }

    public static bool IsLoadingVisible => BattleLoadingOverlay.IsShowing;

    public static string LoadingTip => BattleLoadingOverlay.CurrentTip ?? "";

    /// <summary>片头视频 / 进城镇黑幕 / Loading 盖住大厅时不要截。</summary>
    public static bool IsTownCoveredByIntro =>
        OpeningIntroOverlay.IsPlaying
        || TownIntroVeil.IsBlocking
        || IsLoadingVisible;

    public static bool IsGuildHallVisible =>
        IsTownScene
        && !IsTownCoveredByIntro
        && GuildHallUI.IsChromeVisible
        && CurrentNavTab == MainNavTab.Guild
        && (AdventureUI.Instance == null || !AdventureUI.Instance.gameObject.activeSelf)
        && (AdventureLog == null || !AdventureLog.IsPageVisible)
        && (StoryDirector.Instance == null || !StoryDirector.Instance.IsPlaying)
        && (Dialogue == null || !Dialogue.IsVisible);

    public static bool IsAdventurePageVisible =>
        AdventureUI.Instance != null && AdventureUI.Instance.gameObject.activeSelf;

    public static bool IsCharacterPageVisible =>
        CharacterUI.Instance != null && CharacterUI.Instance.gameObject.activeSelf;

    public static bool IsTavernPageVisible =>
        TavernUI.Instance != null && TavernUI.Instance.gameObject.activeSelf;

    public static TutorialHintUI Hint => TutorialHintUI.Instance;

    public static bool HintContains(string keyword) =>
        Hint != null && Hint.IsVisible && TextHas(Hint.CurrentHint, keyword);

    public static bool DialogueContains(string keyword) =>
        Dialogue != null && Dialogue.IsVisible && TextHas(Dialogue.CurrentLine, keyword);

    public static bool HeadTalkContains(string keyword)
    {
        var talk = BattleHeadTalkUI.Instance;
        return talk != null && talk.IsShowing && TextHas(talk.CurrentLine, keyword);
    }

    public static EquipDropPopupUI EquipDrop => EquipDropPopupUI.Instance;

    public static RestStagePopupUI RestPopup => RestStagePopupUI.Instance;

    public static NextStageRouletteUI Roulette => NextStageRouletteUI.Instance;

    public static OfflineRewardPopup OfflinePopup => OfflineRewardPopup.Instance;

    public static TavernRosterPanel TavernRecruit => TavernRosterPanel.Instance;

    public static bool IsTalentVisible =>
        TalentUI.Instance != null && TalentUI.Instance.gameObject.activeSelf;

    public static AdventureLogUI AdventureLog => AdventureLogUI.Instance;

    public static bool IsAdventureLogVisible =>
        AdventureLog != null && AdventureLog.IsPageVisible;

    public static bool IsAdventureLogMonsterTab =>
        IsAdventureLogVisible
        && AdventureLog.CurrentTabName == "怪物"
        && TextHas(AdventureLog.BodyText, "【Boss】");

    /// <summary>第一章裂缝：场上同时有普通怪和 Boss。</summary>
    public static bool IsChapterBattleWithMobAndBoss
    {
        get
        {
            if (!IsBattleScene) return false;
            bool mob = false;
            bool boss = false;
            var monsters = Object.FindObjectsOfType<Monster>();
            for (int i = 0; i < monsters.Length; i++)
            {
                var m = monsters[i];
                if (m == null || m.isDead || !m.gameObject.activeInHierarchy) continue;
                if (m.config != null && m.config.isBoss) boss = true;
                else mob = true;
                if (mob && boss) return true;
            }
            return false;
        }
    }
}
