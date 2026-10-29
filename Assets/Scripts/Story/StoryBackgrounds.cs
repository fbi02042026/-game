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

    // —— 支线岛剧情背景（2026-10-24 主人供图接入）——
    // 岛 id 对齐 SideIslandDefs（1=迷雾屿 2=沉船湾 3=翡翠秘境 …8=黑曜祭坛，岛名草稿主人可全改）。
    // 八张图 2026-10-24 已全部到位（island_01..08.jpg，主人分两批供图）。
    // 命名口径：路径加载（Resources.Load），不进 prefab 引用，56 字符 guid 规矩不受影响（铁律 30）。
    public const string Island01 = "island_01";
    public const string Island02 = "island_02";
    public const string Island03 = "island_03";
    public const string Island04 = "island_04";
    public const string Island05 = "island_05";
    public const string Island06 = "island_06";
    public const string Island07 = "island_07";
    public const string Island08 = "island_08";

    /// <summary>
    /// 酒馆内景（2026-10-29 酒馆闲话剧情接入）。复用 2026-10-15 主人拍板的酒馆底图
    /// （与 <see cref="InformantQuizDirector"/>.TavernBackgroundPath 同一张，800×1424 满屏高）。
    /// ⚠ 不把图复制进 Story/Backgrounds：同一张图存两份会触发团结对重复内容签发 56 字符
    /// guid 覆盖 meta 的坑（AGENTS.md 前车之鉴），故 Get() 里按 Resources 路径直读。
    /// </summary>
    public const string Tavern = "tavern";
    /// <summary>酒馆底图的 Resources 路径（真源与 InformantQuizDirector.TavernBackgroundPath 同值，改一边记得另一边）。</summary>
    public const string TavernPath = "UI/Art/Tavern/酒馆_0007_图层-4";

    /// <summary>快节奏场景：地名大字显示时间 ×此系数（2026-10-14 主人拍板：会长办公室/公会大厅的地名大字时间减 30%）。</summary>
    public const float FastTextTimeScale = 0.7f;

    /// <summary>该背景切换时的地名大字是否走快节奏（会长办公室 / 公会大厅）。判定真源唯一在此。</summary>
    public static bool IsFastTextScene(string id)
    {
        return id == GuildOffice || id == GuildHall;
    }

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
        // —— 支线岛（岛名真源 SideIslandDefs 草稿；黑幕大字显示这里，四字为主）——
        if (id == Island01) return "迷雾屿";
        if (id == Island02) return "沉船湾";
        if (id == Island03) return "翡翠秘境";
        if (id == Island04) return "晨曦草原";
        if (id == Island05) return "搁浅地";
        if (id == Island06) return "王座间";
        if (id == Island07) return "深渊口";
        if (id == Island08) return "黑曜祭坛";
        if (id == Tavern) return "酒馆";
        return "";
    }

    public static Sprite Get(string id)
    {
        // 酒馆底图不在 Story/Backgrounds 目录：按路径直读（见 Tavern 字段注释，不复制资源）。
        if (id == Tavern)
        {
            var tavernSp = Resources.Load<Sprite>(TavernPath);
            if (tavernSp == null)
                Debug.LogError("[StoryBackgrounds] 酒馆背景缺失：Resources/" + TavernPath);
            return tavernSp;
        }

        var sp = StoryAssetLoader.Load(StoryAssetLoader.Backgrounds, id);
        if (sp != null) return sp;

        // 缺背景就顶一张占位图，避免整屏黑。
        var ph = PlaceholderArt.Background(id);
        if (ph != null) PlaceholderArt.ReportMissing("背景", id);
        return ph;
    }
}
