using UnityEngine;

/// <summary>
/// 城镇入口 · 大地图 V1 的静态配置（2026-11-22 主人拍板第一版只做「简单派遣，让闲置佣兵用起来」）。
/// <para>设计案见 Docs/城镇入口与裂隙远征_设计案_v7.0_2026-11-18.md 与 Docs/裂隙领地_SLG玩法设计案_2026-11-17.md；
/// 本文件只落 V1 最小集：一张大地图 + 城堡周边点位 + 3 闲置佣兵派遣 + 纯数值结算。
/// 农田/城墙/工坊等城堡建筑、守护石、友好度、行军耗时、广告加速一律不在 V1 范围。</para>
/// <para>🔴 解绑口径：本系统独立，不接任何旧玩法；图片全部走 Resources.Load 路径（铁律 16，56 字符 guid 一律不碰）。</para>
/// </summary>
public static class SlgDefs
{
    /// <summary>解锁门槛 = 第 1 章第 10 关（0-based stageIndex0=9，key "1_9"）。主人 2026-11-22 拍板。</summary>
    public const int UnlockChapter = 1;
    public const int UnlockStageIndex0 = 9;

    /// <summary>出发固定 3 名闲置佣兵（设计案铁律，V1 保留）。</summary>
    public const int SquadSize = 3;

    /// <summary>【2026-11-22 主人拍板】地图整体再大一倍：地图内容尺寸 + 全部坐标 ×2。
    /// 🔴 等比缩放：NearDistMax、行军距离分母同步 ×2 ⇒ 远近档判定与行军时长完全不变（只放大可视世界，不动玩法数值）。</summary>
    public const float WorldScale = 2f;

    /// <summary>出发消耗（主人："只要粮食和金币够就可以出征"）。金币走 ResourceWallet.Gold 唯一出口，粮草走 slgFood。</summary>
    public const int DispatchFoodCost = 20;    // 每队粮草
    public const int DispatchGoldCost = 200;   // 每队金币
    /// <summary>首次进入大地图补初始粮草（幂等，靠 slgFoodGranted）：否则粮草初始 0 → 出发要粮草 → 永远出不了征。</summary>
    public const int InitialFoodGrant = 200;

    /// <summary>左下角主城图：点击返回游戏主界面（主人 2026-11-22 拍板）。</summary>
    public const string HomeCityPath = ArtDir + "主城";

    /// <summary>点位冷却（秒）：打完即消失，冷却结束原地刷新。V1 简单值。</summary>
    public const int PointCooldownSec = 1800;    // 30 分钟

    // ── 美术路径（相对 Assets/Resources，不带扩展名）──
    public const string ArtDir = "UI/SLG/";
    public const string MapBgPath = ArtDir + "复古像素风奇幻大陆地图 拷贝";
    public const string CastlePath = ArtDir + "玩家";
    public const string MineBigPath = ArtDir + "大矿";
    public const string MineSmallPath = ArtDir + "小矿";
    public const string MonsterPath = ArtDir + "野怪";
    public const string CityPath = ArtDir + "城市";
    public const string FortPath = ArtDir + "城寨";
    public const string CampPath = ArtDir + "营地";
    public const string NamePlatePath = ArtDir + "名牌";
    /// <summary>关卡界面大地图入口图标（现成资源，替换 prefab 里悬空的 guid）。</summary>
    /// <summary>主界面入口图标（玩家主城城堡图，主人 2026-11-22「加原来那个城堡的icon」）。</summary>
    public const string EntryIconPath = "UI/SLG/玩家";
    /// <summary>天空王国城堡地图界面底图（主界面入口 → 本图 → 点城门 → 大地图）。</summary>
    public const string KingdomMapPath = "UI/SLG/天空王国城堡地图";

    /// <summary>点位类型。</summary>
    public enum PointType { Mine, Monster, City }

    /// <summary>
    /// 一个点位定义。Pos 为相对地图中心的逻辑坐标（720 宽竖屏内），由生成器摆到城堡周边；
    /// Continent = 需要通关的章数（大陆先后顺序：通几章就开几块），V1 只配大陆 1。
    /// </summary>
    public struct PointDef
    {
        public string Id;
        public PointType Type;
        public string Name;
        public string IconPath;
        public Vector2 Pos;
        public int Continent;
        /// <summary>守军战力（纯数值结算用，V1 固定值）。</summary>
        public int GuardPower;
        /// <summary>满额奖励（大胜 100%、小胜 70%、惨胜 40%、失败 0）。</summary>
        public int Merit;
        public int Ore;
        public int Stones;
        public int Food;
    }

    /// <summary>
    /// 机器人城随机名字池（10 个，零 IP 挂靠——主人 2026-11-21 宠物命名红线的同款口径）。
    /// </summary>
    public static readonly string[] CityNames =
    {
        "铁砧堡", "灰鸦城", "石桥关", "霜锋寨", "赤岩城",
        "苍梧镇", "断刃堡", "鸣沙城", "黑槐镇", "云渡口"
    };

    /// <summary>点位坐标统一乘地图缩放（定义必须在 Points 之前，静态初始化顺序）。</summary>
    static Vector2 Scale(float x, float y) => new Vector2(x * WorldScale, y * WorldScale);

    /// <summary>
    /// V1 点位表：大陆 1（通关第 1 章后开）。城堡居中偏下，点位散布周边。
    /// 后续扩大陆只在这里加行、Continent 填 2/3…，不用改系统代码。
    /// </summary>
    public static readonly PointDef[] Points =
    {
        new PointDef { Id="p1_mine_big",   Type=PointType.Mine,    Name="富矿脉",   IconPath=MineBigPath,   Pos=Scale(-160f,  250f), Continent=1, GuardPower=120, Merit=0,  Ore=60, Stones=0,  Food=0  },
        new PointDef { Id="p1_mine_s",     Type=PointType.Mine,    Name="碎矿坑",   IconPath=MineSmallPath, Pos=Scale( 170f,  300f), Continent=1, GuardPower=80,  Merit=0,  Ore=35, Stones=0,  Food=0  },
        new PointDef { Id="p1_monster_1",  Type=PointType.Monster, Name="林地游荡者", IconPath=MonsterPath, Pos=Scale(-190f,   20f), Continent=1, GuardPower=100, Merit=5,  Ore=0,  Stones=8,  Food=20 },
        new PointDef { Id="p1_monster_2",  Type=PointType.Monster, Name="荒野掠食者", IconPath=MonsterPath, Pos=Scale( 200f,   60f), Continent=1, GuardPower=150, Merit=8,  Ore=0,  Stones=12, Food=30 },
        new PointDef { Id="p1_city_1",     Type=PointType.City,    Name="铁砧堡",   IconPath=CityPath,      Pos=Scale(-150f, -150f), Continent=1, GuardPower=180, Merit=30, Ore=0,  Stones=15, Food=25 },
        new PointDef { Id="p1_city_2",     Type=PointType.City,    Name="灰鸦城",   IconPath=FortPath,      Pos=Scale( 165f, -180f), Continent=1, GuardPower=260, Merit=30, Ore=0,  Stones=20, Food=30 },
    };

    // ════════════════════════════════════════════════════════════════════════
    // 【2026-11-22 升级】行军 / 战斗演出 / 占领 / 资源条参数
    // 主人拍板：行军=真实计时+广告加速；战斗=头像演出、胜负按战力比先算好、血条可视化；
    //           资源条复制主界面 TopBar 换 icon（矿石/粮草图主人待补，按路径加载）。
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>玩家城堡在地图内容坐标（固定不变——主人拍板；随地图等比放大）。</summary>
    public static readonly Vector2 CastlePos = new Vector2(0f, -120f * WorldScale);

    /// <summary>近/远档阈值（逻辑像素）：基础 300 × WorldScale = 600，距城堡 ≤ 600 = 近，> 600 = 远。
    /// 🔴 与点位坐标同步放大 ⇒ 远近档划分完全不变（只放大可视世界，不动玩法数值）。</summary>
    public const float NearDistMax = 300f * WorldScale;
    public enum DistTier { Near, Far }
    /// <summary>按点位坐标判近/远档（掉落表筛选键）。</summary>
    public static DistTier DistTierFor(Vector2 pos)
        => Vector2.Distance(pos, CastlePos) <= NearDistMax ? DistTier.Near : DistTier.Far;

    /// <summary>行军基础时长（秒）= 最近点位 10 分钟。主人："最初 10 分钟到后期可能几小时"。</summary>
    public const int MarchBaseSec = 600;          // 10 分钟
    /// <summary>每多一块大陆，行军基础上加 30 分钟（大陆越远越久）。</summary>
    public const int MarchSecPerContinent = 1800; // 30 分钟
    /// <summary>距离每多 200 逻辑像素，行军 +5 分钟。</summary>
    public const int MarchSecPerDistance = 300;   // 5 分钟 / 200px
    /// <summary>行军时长封顶（秒）= 6 小时。</summary>
    public const int MarchSecMax = 6 * 3600;

    /// <summary>行军飞行视觉动画时长（秒）——只是"从城堡飞向目标"的演出，不代表真实行军耗时。</summary>
    public const float MarchFlyAnimSec = 2.2f;

    /// <summary>战斗演出时长：前期 10 秒，每多一块大陆 +2 秒，封顶 20 秒（主人："前期10秒后期最多20秒"）。</summary>
    public static int BattleShowSecFor(int continent)
        => Mathf.Clamp(10 + (continent - 1) * 2, 10, 20);

    /// <summary>占领倒计时（秒）：到达并打赢后，头像下方读条，读完 +X 跳字入账。</summary>
    public const int OccupyCountdownSec = 3;

    /// <summary>行军广告加速 placement（走 RewardedAdBridge 单一出口）。</summary>
    public const string AdPlacementMarchSpeed = "slg_march_speed";

    // 资源条 icon（复制 TopBar 换图；2026-11-22 主人已把矿石/粮草图放进 Common 目录）
    public const string OreIconPath = "UI/Art/Icons/Common/矿石";
    public const string FoodIconPath = "UI/Art/Icons/Common/粮食";
    public const string MeritIconPath = "UI/Art/Icons/Common/战功"; // 主人 2026-11-22 已放入 Common（56 字符 guid，路径加载不碰 guid）
    public const string StoneIconPath = "UI/Art/Icons/Common/icon_talent_stone"; // 保留常量；矿=石头，资源条已合并显示，不再用此图

    // 头像 / 血条（战斗演出）
    public const string HeadDir = "UI/Icons/MercHead/";      // H001~H024 + player
    public const string HpFillPath = "UI/Art/battle/血条";        // 我方绿
    public const string HpEnemyFillPath = "UI/Art/battle/敌人血条"; // 敌方红
    public const string HpBgPath = "UI/Art/battle/血条底";

    /// <summary>按点位坐标 + 所属大陆算真实行军时长（秒）。确定性，无随机。</summary>
    public static int MarchSecFor(Vector2 pos, int continent)
    {
        float dist = Vector2.Distance(pos, CastlePos);
        int sec = MarchBaseSec + Mathf.Max(0, continent - 1) * MarchSecPerContinent;
        // 🔴 距离基准 200 × WorldScale：坐标放大一倍后每档距离的实际像素同步翻倍 ⇒ 行军时长一秒不变。
        float step = 200f * WorldScale;
        sec += Mathf.RoundToInt(Mathf.Max(0f, dist - step) / step) * MarchSecPerDistance;
        return Mathf.Clamp(sec, MarchBaseSec, MarchSecMax);
    }

    /// <summary>守军头像号（3 个，avatar 2~25 → H001~H024）：按 pointId 确定性派生，无随机。</summary>
    public static int[] DefenderAvatars(string pointId)
    {
        int h = 17;
        if (!string.IsNullOrEmpty(pointId))
            for (int i = 0; i < pointId.Length; i++) h = h * 31 + pointId[i];
        h = Mathf.Abs(h);
        return new[] { 2 + h % 24, 2 + (h / 24) % 24, 2 + (h / 576) % 24 };
    }

    /// <summary>V1 简单战力估值：只用花名册基础属性，不碰主线 PowerCalc/ApplyCombatStats 公式。</summary>
    public static float EstimatePower(float atk, float hp, float def, float atkSpeed)
        => Mathf.Max(1f, atk * 2f + hp * 0.2f + def * 3f + atkSpeed * 10f);

    /// <summary>战力比分档奖励系数（设计案 §6.5，V1 沿用）。</summary>
    public static float RewardRatio(float powerRate)
    {
        if (powerRate >= 1.5f) return 1f;
        if (powerRate >= 1.0f) return 0.7f;
        if (powerRate >= 0.8f) return 0.4f;
        return 0f;
    }

    /// <summary>按 hireId 取点位显示用的佣兵名（缺真名不兜底，回退 id）。</summary>
    public static string MercName(string hireId)
        => MercRosterDefs.TryGetByHireId(hireId, out var d) ? d.Name : hireId;
}
