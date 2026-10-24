using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-18 由 CharacterUI.cs 纯位移拆出（无逻辑改动）
// 职责：立绘与看板（玩家立绘刷新、看板选择、废弃的携带技能、背包变更事件订阅）
public partial class CharacterUI
{

    void RefreshPlayerPortrait()
    {
        if (portraitImage == null)
            portraitImage = transform.Find("Content/Stage/Portrait")?.GetComponent<Image>();
        if (portraitImage == null) return;

        // 清掉历史 SpumPreview
        var junk = portraitImage.transform.Find("SpumPreview");
        if (junk != null)
            Destroy(junk.gameObject);

        MercPortraitSprites.ClearCache();
        string boardId = GetCurrentBoardId(); // "player" 或 佣兵 HireId
        // 看板显示佣兵立绘（Content/Stage/Portrait 主人摆的就是立绘位）：
        // 优先 GetStand → Resources/Icons/MercStand（编辑器直读 Art/UI/Icons/佣兵立绘）；
        // 取不到才用头像 GetHead 兜底，都没有则保留预制体里主人摆好的图。
        // 2026-09-26 还原：此前曾用 GetHead 优先，导致立绘被头像替换。
        var stand = MercPortraitSprites.GetStand(boardId);
        var sp = stand ?? MercPortraitSprites.GetHead(boardId);
        if (sp != null)
            SetPortrait(sp, _portraitFlipped);
        else
            portraitImage.enabled = true;
    }

    /// <summary>当前看板 id：默认玩家自己（"player"），否则已选佣兵 HireId。</summary>
    public string GetCurrentBoardId()
    {
        return _boardId;
    }

    /// <summary>设为看板并立即刷新立绘；id 为空或 "player" 表示恢复默认（玩家自己）。</summary>
    public void ApplyBoardSelection(string id)
    {
        _boardId = string.IsNullOrEmpty(id) ? "player" : id;
        RefreshPlayerPortrait();
    }

    // 【2026-10-10 主人拍板】看板选择不再落 PlayerPrefs（残留会导致回城后角色界面一直显示上次选的
    // 佣兵立绘，主人要求「回城后的角色界面应该默认是主角立绘」）。改为只存内存：
    // 会话内在城镇里切换看板保留，**每次回城**由 GameSceneManager.ResetBoardToPlayer 重置回主角。
    static string _boardId = "player";

    /// <summary>回城时调用：看板回主角立绘。实例未建好时只重置静态值，打开页面时自然生效。</summary>
    public static void ResetBoardToPlayer()
    {
        _boardId = "player";
        if (Instance != null)
            Instance.RefreshPlayerPortrait();
    }

    // 2026-09-25 主人明确 LeftSkillButton 已改名且暂时不要了，携带技能展示彻底废弃。
    // 保留方法签名以免其它引用处（如技能选择回调）编译报错，但不再动任何节点、不再 SetActive。
    void RefreshCarriedSkill()
    {
        return;
    }

    static Sprite LoadPlayerSkillIcon(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        var sp = Resources.Load<Sprite>("Icons/SkillIcon/" + skillId);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>("Icons/SkillIcon/" + skillId);
        if (all != null && all.Length > 0) return all[0];
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            string artPath = "Assets/Art/UI/Icons/玩家SkillIcon/" + skillId + ".png";
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
            if (sp != null) return sp;
        }
#endif
        return null;
    }

    /// <summary>
    /// 技能图画在 LeftSkillButton 自身 Image 上。
    /// 2026-09-25 适配主人新层级：预制体 LeftButton/Skill/Icon 是主人新加的真按钮（挂了 Button），
    /// 因此这里禁止 Find("Icon") 后 Destroy —— 那会把主人的按钮删掉；
    /// 也不再 SetActive 任何子节点 Image（属于代码改美术节点状态，作用于 LeftButton 时更无意义）。
    /// </summary>
    void EnsureCarriedSkillIcon()
    {
        if (leftSkillButton == null) return;

        carriedSkillIcon = leftSkillButton.targetGraphic as Image
                           ?? leftSkillButton.GetComponent<Image>();
    }

    void WireBagEvents()
    {
        if (_bagEventsWired) return;
        if (BattleBagSystem.Instance == null) return;
        BattleBagSystem.Instance.OnBackpackChanged += OnBagOrCostumeChanged;
        BattleBagSystem.Instance.OnCostumeChanged += OnBagOrCostumeChanged;
        _bagEventsWired = true;
    }

    void OnBagOrCostumeChanged()
    {
        if (!gameObject.activeInHierarchy) return;
        RefreshBag();
        RefreshAttrs();
        TownHeroCostumePreview.EnsureOn(this)?.EnsureOffscreenCostume();
    }

}
