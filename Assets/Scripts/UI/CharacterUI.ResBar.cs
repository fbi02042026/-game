using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-24 主人拍板：角色界面顶部显示 金币/钻石/天赋石 资源条。
// 实现沿用 2026-10-24 MercAscendUI 已验收的「克隆主界面 TopBar」口径（TownSharedChrome 唯一克隆口，
// 禁止各页自建资源条）：只留 金币Panel/钻石Panel 两列，再克隆金币列做 天赋石 列（换图标、藏加号）；
// 三列挪到页首右上空白区（「苍穹的尤恩」木牌右边、右侧功能钮上方），整列等比 0.78 缩放。
// 数字真源唯一 = ResourceWallet，格式唯一出口 = GuildHallUI.FormatResource（不许抄第二份）。
// 页面开着时天赋/邮件等花销只回调 GuildHallUI.RefreshAllHudStatic、到不了本页，
// 故 Update 轮询三值比对、变了才刷（与 GuildHallUI 体力 HUD 轮询同口径）；显隐/切页走 RefreshAll。
public partial class CharacterUI
{
    /// <summary>天赋石图标（Resources 路径已核实存在，与冒险日志/邮件奖励格同图）。</summary>
    const string TalentIconPath = "Art/UI/Icons/Common/icon_talent_stone";

    /// <summary>三列整体缩放：主界面顶条列宽 260（视觉约 180），右上空白区仅约 420 宽，
    /// 等比缩到 0.78 才放得下三列且不压木牌；只动克隆列自身 localScale，不碰任何预制体几何。</summary>
    const float ResCellScale = 0.78f;

    Transform _resBar;         // 克隆出来的资源条（CharacterUI 根下，随页面显隐）
    Text _resGoldText;         // 克隆条 GoldPanel/GoldText
    Text _resDiaText;          // 克隆条 钻石Panel/钻石Text
    Text _resTalentText;       // 克隆列 TalentPanel/GoldText（克隆自金币列，节点同名）
    long _lastGold, _lastDia, _lastTalent;
    bool _resNumbersDirty = true;   // 首帧强制刷一次

    /// <summary>
    /// 确保角色页根下有一份「主界面 TopBar」克隆，只留金币+钻石两列并补一列天赋石。
    /// 克隆只做一次（页面不销毁，随根显隐复用）；缺列 LogError 不兜底（AGENTS.md §4）。
    /// 位置：条本身仍是主界面那条整宽贴顶条（TopBarBg 本就隐藏、无底图不挡视线），
    /// 三列挪到条内右侧 —— 屏宽 720，木牌视觉右缘约 x=291，右侧功能钮顶约 y=180，
    /// 三列根坐标 x = 30/173/316（等比 0.78 后视觉恰落在 297~718，互不重叠、不压木牌）。
    /// </summary>
    void EnsureResBar()
    {
        if (_resBar != null) { RefreshResNumbers(); return; }

        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : transform.root;
        _resBar = TownSharedChrome.EnsureResourceBar(transform, hall, forceCloneTopBar: true);
        if (_resBar == null)
        {
            Debug.LogError("[CharacterUI] 顶条资源条克隆失败（找不到主界面 TopBar）——本页不显示金币/钻石/天赋石条");
            return;
        }
        // 不叫 TopBar：PreloadOnce 的 StripLocalChrome 会按名销毁「TopBar」子节点（2026-10-13 拍板本页去顶条）
        _resBar.name = "CharResBar";
        _resBar.SetAsLastSibling();

        // 只留金币/钻石两列，其余直子节点整列隐藏（体力/邮件/设置/公会名等，与 MercAscendUI 同口径，只切 active 不动几何）
        for (int i = 0; i < _resBar.childCount; i++)
        {
            var c = _resBar.GetChild(i);
            bool keep = c.name == "TopBarBg" || c.name == "GoldPanel" || c.name == "钻石Panel";
            if (!keep) c.gameObject.SetActive(false);
        }

        var goldPanel = FindDeep(_resBar, "GoldPanel");
        var diaPanel = FindDeep(_resBar, "钻石Panel");
        if (goldPanel == null || diaPanel == null)
        {
            Debug.LogError("[CharacterUI] 克隆顶条缺 GoldPanel/钻石Panel 列 —— 不兜底，本页不显示资源条");
            Destroy(_resBar.gameObject);
            _resBar = null;
            return;
        }

        // 天赋石列 = 克隆金币列：换 CoinIcon 图为天赋石、藏 PlusButton（天赋石无充值入口，金币/钻石加号照主界面保留）
        var talentPanel = Instantiate(goldPanel, _resBar, false);
        talentPanel.name = "TalentPanel";
        var tIcon = FindDeep(talentPanel, "CoinIcon")?.GetComponent<Image>();
        if (tIcon != null)
        {
            var sp = Resources.Load<Sprite>(TalentIconPath);
            if (sp != null) tIcon.sprite = sp;
            else Debug.LogError("[CharacterUI] 天赋石图标取不到：" + TalentIconPath + " —— 克隆列维持金币图");
        }
        else
            Debug.LogError("[CharacterUI] 克隆金币列里没有 CoinIcon —— 天赋石图标不换（不兜底）");
        var tPlus = FindDeep(talentPanel, "PlusButton");
        if (tPlus != null) tPlus.gameObject.SetActive(false);
        else Debug.LogError("[CharacterUI] 克隆金币列里没有 PlusButton —— 天赋石列加号未隐藏（不兜底）");

        // 三列横排到条内右侧（从左到右：天赋石 → 钻石 → 金币，与主界面「钻左金右」习惯一致）
        LayoutResCell(talentPanel, 30f);
        LayoutResCell(diaPanel, 173f);
        LayoutResCell(goldPanel, 316f);

        // 数字 Text 各自收进 Panel 局部找 —— 主界面顶条「体力Panel」里的数字 Text 也叫 GoldText，
        // 全局深搜会串绑到被藏的体力格上（与 MercAscendUI / GuildHallUI.AutoBindMissingRefs 同口径）
        var goldT = FindDeep(goldPanel, "GoldText");
        _resGoldText = goldT != null ? goldT.GetComponent<Text>() : null;
        if (_resGoldText == null) Debug.LogError("[CharacterUI] GoldPanel 里没有 GoldText —— 金币数字将不显示（不兜底）");
        var diaT = FindDeep(diaPanel, "钻石Text");
        _resDiaText = diaT != null ? diaT.GetComponent<Text>() : null;
        if (_resDiaText == null) Debug.LogError("[CharacterUI] 钻石Panel 里没有 钻石Text —— 钻石数字将不显示（不兜底）");
        var talT = FindDeep(talentPanel, "GoldText");
        _resTalentText = talT != null ? talT.GetComponent<Text>() : null;
        if (_resTalentText == null) Debug.LogError("[CharacterUI] TalentPanel 里没有 GoldText —— 天赋石数字将不显示（不兜底）");

        _resNumbersDirty = true;
        RefreshResNumbers();
    }

    /// <summary>克隆列摆位：只动克隆体自身（锚点居中 / 等比缩放 / 横坐标），不碰任何预制体几何（铁律 32）。</summary>
    static void LayoutResCell(Transform cell, float x)
    {
        if (cell == null) return;
        var rt = cell as RectTransform;
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one * ResCellScale;
        rt.anchoredPosition = new Vector2(x, 0f);
    }

    /// <summary>克隆条数字刷新：真源唯一 = ResourceWallet，格式唯一出口 = GuildHallUI.FormatResource（不许抄第二份）。</summary>
    void RefreshResNumbers()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        long g = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        long d = ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond);
        long t = ResourceWallet.Get(data, ResourceWallet.ResourceType.TalentPoint);
        if (!_resNumbersDirty && g == _lastGold && d == _lastDia && t == _lastTalent) return;
        _lastGold = g; _lastDia = d; _lastTalent = t;
        _resNumbersDirty = false;
        if (_resGoldText != null)
            _resGoldText.text = GuildHallUI.FormatResource(g);
        if (_resDiaText != null)
            _resDiaText.text = GuildHallUI.FormatResource(d);
        if (_resTalentText != null)
            _resTalentText.text = GuildHallUI.FormatResource(t);
    }

    /// <summary>
    /// 页面开着时的资源变化兜转：天赋购买/邮件领取等只回调 GuildHallUI.RefreshAllHudStatic、
    /// 到不了本页克隆条，这里每帧比对三值、变了才刷（无分配、无射线，开销可忽略）。
    /// </summary>
    void Update()
    {
        if (_resBar == null) return;
        RefreshResNumbers();
    }
}
