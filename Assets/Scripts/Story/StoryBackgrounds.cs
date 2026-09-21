using UnityEngine;

/// <summary>剧情场景背景 ID。资源在 Resources/Story/Backgrounds。</summary>
public static class StoryBackgrounds
{
    public const string GuildOffice = "guild_office";
    public const string GuildHall = "guild_hall";

    // —— 第 8 章四结局背景（2026-09-21 接入）——
    // 图早就画好了（576×1024，存在 Art/UI/Story/end_*.png），但一直没进运行时目录、
    // 也没有任何代码引用，等于结局演出只能看到战斗背景。本轮补上 Resources 副本 + 这里登记 ID。
    /// <summary>Happy《回来的是两个人》——裂隙出口，天刚亮。</summary>
    public const string EndHappy = "end_happy";
    /// <summary>BadHome《回来的不是她》——公会医务室清晨，一张空病床。</summary>
    public const string EndBadHome = "end_badhome";
    /// <summary>BadGate《留下的是她》——裂隙最深处，沉座已合拢成一块完整晶体。</summary>
    public const string EndBadGate = "end_badgate";
    /// <summary>True《最后一个箱子》——公会旧装备仓库，一只开着盖的补给箱。</summary>
    public const string EndTrue = "end_true";

    // —— 第 3/4/5/6 章剧情背景（2026-09-21 接入）——
    /// <summary>N3 · 梅莉莎的营地（生者之径）。</summary>
    public const string MelissaCamp = "melissa_camp";
    /// <summary>N4 · 风车（计时之径）。</summary>
    public const string Windmill = "windmill";
    /// <summary>N6 · 沉座（巨岩深窟）。</summary>
    public const string ThroneSeat = "throne_seat";
    // ⚠ `briefing_tent`（简报帐篷）已作废：那个提示词是照过时策划写的，
    //   而 N5 实际台词写的是「搁浅的补给船」—— 两者对不上。改由 `WreckShip` 接手。
    /// <summary>N5 · 搁浅的补给船（三路汇合）。</summary>
    public const string WreckShip = "wreck_ship";
    /// <summary>N2 · 墓园最里侧七块无名碑（亡者之径），第八块是空的。</summary>
    public const string GraveSeven = "grave_seven";
    /// <summary>N7 · 裂隙最深处无底深渊边缘的悬空石台（三选一前）。</summary>
    public const string AbyssChoice = "abyss_choice";

    public static string DisplayName(string id)
    {
        if (id == GuildOffice) return "会长办公室";
        if (id == GuildHall) return "公会大厅";
        // ⚠ 换背景会走「地点揭示」黑场，名字取自这里；返回空字符串会显示一片空白，四结局不能漏。
        if (id == EndHappy) return "裂隙出口";
        if (id == EndBadHome) return "公会医务室";
        if (id == EndBadGate) return "沉座";
        if (id == EndTrue) return "装备科仓库";
        if (id == MelissaCamp) return "梅莉莎的营地";
        if (id == Windmill) return "风车";
        if (id == ThroneSeat) return "沉座";
        if (id == WreckShip) return "搁浅的补给船";
        if (id == GraveSeven) return "无名碑";
        if (id == AbyssChoice) return "深渊边缘";
        return "";
    }

    public static Sprite Get(string id)
    {
        var sp = StoryAssetLoader.Load(StoryAssetLoader.Backgrounds, id);
        if (sp != null) return sp;

        // 缺背景就顶一张占位图，避免整屏黑。
        var ph = PlaceholderArt.Background(id);
        if (ph != null) PlaceholderArt.ReportMissing("背景", id);
        return ph;
    }
}
