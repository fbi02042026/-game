using UnityEngine;

/// <summary>
/// 全局游戏配置，所有常量放这里，数值调优直接改这里
/// </summary>
public static partial class GameConfig
{
    [Header("分辨率配置")]
    public const float DESIGN_WIDTH = 720f;
    public const float DESIGN_HEIGHT = 1280f;
    /// <summary>CanvasScaler.matchWidthOrHeight 默认；实际以 BattleViewportFit 按屏比例覆盖</summary>
    public const float UI_MATCH = 1f;
    public const float PIXEL_PER_UNIT = 100f; // 数值表：100像素=1世界单位

    /// <summary>组织全称。主界面标题、图鉴条目等统一用这个，不要再写「冒险者公会」。</summary>
    public const string GUILD_NAME = "皇家冒险者公会";

    /// <summary>战斗地面单位可在站立线上下偏移的半高（对称参考；实际钳制用 MIN/MAX）。</summary>
    public const float BATTLE_LANE_HALF = 0.855f;
    /// <summary>站立线上方可行走半高（相对 HALF 再缩约 10%）。</summary>
    public const float BATTLE_LANE_MAX = BATTLE_LANE_HALF * 0.9025f;
    /// <summary>站立线下方可行走半高（相对 HALF 再缩约 45%，取负）。</summary>
    public const float BATTLE_LANE_MIN = -BATTLE_LANE_HALF * 0.54675f;
    /// <summary>可行走区域整体 Y 下移量（世界单位，正值=往下挪）。主人要"往下挪一点点"，调这个值即可二次微调。</summary>
    public const float BATTLE_LANE_Y_DROP = 0.12f;
    public const float BATTLE_LANE_MOVE_SPEED = 1.35f;
    /// <summary>摇杆左右移速倍率（相对 GetCombatMoveSpeed）。</summary>
    public const float HERO_MANUAL_MOVE_X_MUL = 1.8f;
    /// <summary>松摇杆后短暂停自动追怪，避免与手动抢方向。</summary>
    public const float HERO_MANUAL_RELEASE_HOLD = 0.25f;
    /// <summary>近战出手允许的车道 Y 误差：走到目标水平对面，上下可略偏，避免错位砍刀光发飘。</summary>
    public const float MELEE_LANE_ALIGN_TOL = 0.22f;

    /// <summary>追敌车道对齐容错开关：true=不严格站到与目标同一条水平线（留随机偏移）；false=完全回退原行为。</summary>
    public const bool ENABLE_LANE_ALIGN_TOLERANCE = true;

    /// <summary>酒馆功能开关（2026-09-26 主人拍板：佣兵功能待调好再开，点击酒馆先显示"未开放"）。调好后改成 true。</summary>
    public const bool TAVERN_ENABLED = false;
    /// <summary>车道对齐容错带（世界单位，Y 方向）：站位最多保留这么多上下偏移，不再收敛到严丝合缝。
    /// 取值依据：车道全高 MAX-MIN≈1.24，角色身高约 1.2 单位，0.12≈身高 10%（肉眼能看出错位、又不散）；
    /// 且恒小于近战出手闸门 MELEE_LANE_ALIGN_TOL(0.22)，绝不会挡住普攻。</summary>
    public const float LANE_ALIGN_TOLERANCE = 0.12f;
    /// <summary>容错偏移幅度下限比例：实际偏移 = TOLERANCE × Random(该值, 1)，避免每场都偏得一模一样。</summary>
    public const float LANE_ALIGN_TOLERANCE_MIN_RATIO = 0.45f;
    /// <summary>左右容错（世界单位）：近战站位比攻击射程再近这么一点（恒更靠内，进距普攻判定不受影响）。
    /// 剑射程 0.96，取 0.08 ≈ 8%；设 0 即取消左右偏移。注意：仅作用于近战站位距离，远程英雄不适用。</summary>
    public const float LANE_ALIGN_TOLERANCE_X = 0.08f;
    /// <summary>车道对齐速度倍率（乘在换道速度上）。1=不改手感；调小更缓更"稳"，调大更快"贴脸"。</summary>
    public const float LANE_ALIGN_SPEED_MUL = 1f;

    /// <summary>像素 → 世界单位（对齐数值表「攻击范围(像素)」）</summary>
    public static float PixelsToUnits(float pixels) => pixels / PIXEL_PER_UNIT;

    /// <summary>
    /// 归一化攻击距离：数值表用像素（通常≥64）；旧配置若已是世界单位（通常&lt;10）则原样返回。
    /// </summary>
    public static float NormalizeAttackRange(float raw)
    {
        if (raw > 10f) return PixelsToUnits(raw);
        return raw;
    }

    /// <summary>是否远程攻击射程（弓）</summary>
    public static bool IsRangedAttackRange(float rangeWorld)
    {
        return rangeWorld >= RangeBow - 0.15f;
    }

    /// <summary>
    /// 战斗索敌范围（世界单位）：与攻击射程无关，约为当前屏幕可见宽度 + 少量边距。
    /// 不要用过大 pad，否则会锁到镜头外的怪并开打。
    /// </summary>
    public const float COMBAT_DETECT_SCREEN_PAD = 0.35f;
    public const float COMBAT_DETECT_FALLBACK = 8f;
    /// <summary>镜头外多少世界单位仍算「在屏上」（索敌/出手允许的边距）。</summary>
    public const float COMBAT_VIEWPORT_MARGIN = 0.35f;

    static float _combatDetectCached = -1f;
    static int _combatDetectCachedFrame = -1;

    public static float GetCombatDetectRange()
    {
        if (_combatDetectCachedFrame == Time.frameCount && _combatDetectCached > 0f)
            return _combatDetectCached;

        float range = COMBAT_DETECT_FALLBACK;
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float screenW = cam.orthographicSize * cam.aspect * 2f;
            range = screenW + COMBAT_DETECT_SCREEN_PAD;
        }

        _combatDetectCached = range;
        _combatDetectCachedFrame = Time.frameCount;
        return range;
    }

    /// <summary>单位是否在战斗镜头内（含少量边距）。屏外目标不得被索敌/开打。</summary>
    public static bool IsInCombatViewport(UnitBase unit, float marginWorld = COMBAT_VIEWPORT_MARGIN)
    {
        if (unit == null) return false;
        Transform tf = unit.transform;
        if (unit is Monster mon)
            tf = mon.GetBodyTransform();
        return IsInCombatViewport(tf != null ? tf.position : unit.transform.position, marginWorld);
    }

    public static bool IsInCombatViewport(Vector3 worldPos, float marginWorld = COMBAT_VIEWPORT_MARGIN)
    {
        var cam = Camera.main;
        if (cam == null) return true;
        if (!cam.orthographic)
        {
            Vector3 v = cam.WorldToViewportPoint(worldPos);
            return v.z > 0f && v.x >= -0.02f && v.x <= 1.02f && v.y >= -0.02f && v.y <= 1.02f;
        }
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;
        return worldPos.x >= c.x - halfW - marginWorld
            && worldPos.x <= c.x + halfW + marginWorld
            && worldPos.y >= c.y - halfH - marginWorld
            && worldPos.y <= c.y + halfH + marginWorld;
    }

    /// <summary>
    /// 主手/副手武器射程：一律以 WeaponKind 表为准。
    /// 历史模板大量写死 96 像素，若优先读模板会把弓也锁成近战距。
    /// </summary>
    public static float ResolveWeaponAttackRange(EquipTemplate tpl)
    {
        if (tpl == null) return BASE_ATTACK_RANGE;
        if (tpl.slotType == EquipSlotType.MainHand || tpl.slotType == EquipSlotType.OffHand)
            return WeaponCombatTable.GetAttackRangeWorld(WeaponCombatTable.ResolveKind(tpl));

        float raw = tpl.attackRange;
        if (raw > 10f)
            return NormalizeAttackRange(raw);
        if (raw > 0.1f)
            return raw;
        return BASE_ATTACK_RANGE;
    }

    /// <summary>从装备模板解析基础攻速（次/秒）</summary>
    public static float ResolveWeaponAttackSpeed(EquipTemplate tpl)
    {
        return WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.ResolveKind(tpl));
    }

    [Header("战斗排序")]
    public const string BATTLE_SORTING_LAYER = "Default";
    /// <summary>map 战斗背景 Canvas（用户约定：BattleUI=0，map=10，单位=15，特效=50）</summary>
    public const int SORT_MAPROOT = 10;
    /// <summary>BattleUI 根 Canvas（顶栏/背包/角色栏）</summary>
    public const int SORT_BATTLE_UI = 0;
    /// <summary>人物/怪物/血条</summary>
    public const int SORT_UNIT = 15;
    /// <summary>攻击特效</summary>
    public const int SORT_VFX = 50;

    /// <summary>
    /// 默认解锁的背包行数。本期扩容到 4 行，默认全开 3 行，第 4 行由背包扩容天赋 R_BAG 解锁。
    /// </summary>
    public const int BACKPACK_DEFAULT_ROWS = 3;
    /// <summary>
    /// 战斗内背包固定只开放的行数（2026-09-21 主人定）。战斗内场地有限，只给前 2 行 = 8 格，
    /// 第 3 行锁上；城镇 / 角色页不受此限制（仍走 <see cref="GetUnlockedBackpackRows"/> 的 3~4 行）。
    /// 注意：这是**显示与可用行数的上限**，逻辑网格仍按 BACKPACK_HEIGHT_MAX 分配。
    /// </summary>
    public const int BATTLE_BACKPACK_ROWS = 2;

    /// <summary>
    /// 当前存档实际可用的背包行数（钳到 [1, BACKPACK_HEIGHT_MAX]）。
    /// <para>真值来源优先级：</para>
    /// 1) SaveData.backpackRows —— **权威来源**，由 TalentSystem 在 R_BAG 解锁 / 洗点时写回
    ///    （解锁置 4，洗点清掉 R_BAG 后回落 3，见 TalentSystem.SyncBackpackRows）；
    /// 2) 天赋字典兜底（TalentDefs.CountBagRowUnlocks），仅用于旧存档
    ///    「天赋已点但字段还没落盘」时的兼容。
    /// <para>2026-09-21：删除旧天赋 id 常量 TALENT_BACKPACK_ROW4（"backpack_row4"）及其死分支
    /// —— 真天赋 id 早已是 R_BAG，旧 id 永远匹配不到。</para>
    /// </summary>
    public static int GetUnlockedBackpackRows(SaveData data)
    {
        int rows = BACKPACK_DEFAULT_ROWS;
        if (data != null)
        {
            rows = data.backpackRows;
            if (data.talents != null)
                rows = Mathf.Max(rows, BACKPACK_DEFAULT_ROWS + TalentDefs.CountBagRowUnlocks(data.talents));
        }
        return Mathf.Clamp(rows, 1, BACKPACK_HEIGHT_MAX);
    }

    /// <summary>战斗内实际可用的背包行数：在存档真值之上再压 <see cref="BATTLE_BACKPACK_ROWS"/> 的上限。</summary>
    public static int GetBattleBackpackRows(SaveData data) =>
        Mathf.Clamp(Mathf.Min(GetUnlockedBackpackRows(data), BATTLE_BACKPACK_ROWS), 1, BACKPACK_HEIGHT_MAX);

    /// <summary>调试/UI 容量显示用：直接读当前存档解锁的背包行数（无需传参）。</summary>
    public static int UnlockedBackpackRows() => GetUnlockedBackpackRows(SaveSystem.Instance?.Data);

    /// <summary>投普通/精英/Boss 怪物根缩放（子节点已归一为 1）</summary>
    public static float RollMonsterRootScale(bool isElite, bool isBoss)
    {
        float visual = Random.Range(MONSTER_SCALE_MIN, MONSTER_SCALE_MAX);
        if (isBoss) visual *= BOSS_SCALE_MULTIPLIER;
        else if (isElite) visual *= ELITE_SCALE_MULTIPLIER;
        return visual;
    }

    public static float GetUnitScale(bool isElite = false, bool isBoss = false)
    {
        return RollMonsterRootScale(isElite, isBoss);
    }

    /// <summary>解析佣兵档位：npc / junior(1xx) / advanced(2xx)</summary>
    public static MercTier GetMercTier(string mercId)
    {
        if (string.IsNullOrEmpty(mercId)) return MercTier.Junior;
        if (mercId.StartsWith("npc_", System.StringComparison.OrdinalIgnoreCase))
            return MercTier.Npc;
        // 取末尾连续数字：dunbing201 → 201
        int i = mercId.Length - 1;
        while (i >= 0 && char.IsDigit(mercId[i])) i--;
        string num = mercId.Substring(i + 1);
        if (num.Length > 0 && num[0] == '2') return MercTier.Advanced;
        return MercTier.Junior;
    }

    public static bool IsMercAvailable(string mercId, SaveData data)
    {
        MercTier tier = GetMercTier(mercId);
        if (tier == MercTier.Npc) return false; // 剧情 NPC，不进入可雇佣/出战池
        if (tier == MercTier.Advanced)
        {
            int guild = data != null ? data.guildLevel : 1;
            return guild >= ADVANCED_MERC_GUILD_LEVEL;
        }
        return true;
    }

    /// <summary>把单位挂到场景 unit 节点下（保持世界坐标），找不到则不改父级</summary>
    public static void AttachToUnitRoot(Transform t)
    {
        if (t == null) return;
        Transform root = BattleManager.Instance != null ? BattleManager.Instance.unitRoot : null;
        if (root == null)
        {
            GameObject go = GameObject.Find("unit");
            if (go == null) go = GameObject.Find("Unit");
            if (go != null) root = go.transform;
        }
        if (root == null || t.parent == root) return;
        t.SetParent(root, true);
    }

    /// <summary>
    /// 单位前后遮挡：只改 SortingGroup 的 sortingOrder（随世界 Y）。
    /// 禁止改 SPUM/角色子 Sprite 的 sortingOrder、sortingLayer——部件层级全留预制体。
    /// </summary>
    public static void ApplyUnitSorting(Transform root)
    {
        if (root == null) return;
        ApplyUnitSorting(root, root.position.y);
    }

    public static void ApplyUnitSorting(Transform root, float worldY)
    {
        if (root == null) return;
        // Y 越低越靠镜头前
        int order = SORT_UNIT + Mathf.RoundToInt(-worldY * 40f);

        // 优先用已有 SortingGroup（SPUM 常挂在 UnitRoot），禁止再往根上叠一层把部件搞乱
        var sg = root.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = root.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>();
        if (sg == null)
            sg = root.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();

        sg.sortingLayerName = BATTLE_SORTING_LAYER;
        sg.sortingOrder = order;
    }
}

/// <summary>佣兵/角色档位</summary>
public enum MercTier
{
    Junior,   // 1xx 初级
    Advanced, // 2xx 高级
    Npc       // npc_* 剧情
}

/// <summary>关卡类型</summary>
public enum StageType
{
    Normal, // 普通关：普通怪，基础奖励
    Elite, // 精英关：精英怪，更多装备/高概率蓝紫装
    Merchant, // 商人关：可以用金币买装备/道具/回血
    Enchant, // 附魔关：给已有装备加随机附魔词条
    Curse, // 诅咒关：三选一buff，每个buff带一个debuff，高风险高收益
    Rest, // 恢复关：回血/分解装备得材料，越往后给的强化材料越多
    Boss, // BOSS关：每章最后一关，必掉紫/橙装，解锁下一章
    Forge // 锻造关：打造/强化装备；与附魔关每章只会出现一种
}

/// <summary>通关宝箱品质：木/银/金</summary>
public enum ClearBoxTier
{
    Mu = 0,
    Yin = 1,
    Jin = 2
}
