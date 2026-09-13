using UnityEngine;

/// <summary>
/// 同阵营挤位：佣兵/英雄仍软挡路；怪物允许重叠，头顶显示 ×N。
/// 怪物占位宽度按 opaque 像素表，不用整图 bounds。
/// </summary>
public static class UnitCrowd
{
    const float SpriteScale = 0.92f;
    const float DefaultHalfWidth = 0.42f;
    public const float MonsterFallbackHalfWidth = 0.22f;
    const float OverlapPad = 0.08f;
    const float MinSeparation = 0.72f;
    /// <summary>玩家与佣兵之间额外间隙倍率（半宽×该值×2）；越小站得越近。</summary>
    const float HeroMercHalfBodyGapMul = 0.22f;
    /// <summary>同排（近战前排 / 远程后排）相邻佣兵步进。</summary>
    const float MercSameLineStep = 0.48f;
    /// <summary>相对玩家的第一档前后距（半宽之外再加这点）。</summary>
    const float MercLineBasePad = 0.12f;
    /// <summary>两怪 footprint 重叠达较小者宽度的该比例才显示 ×N</summary>
    const float StackOverlapRatio = 0.8f;
    static float _nextStackRefresh;

    public static float GetHalfWidth(UnitBase u)
    {
        if (u == null) return DefaultHalfWidth;
        if (u is Monster mon)
            return mon.GetOpaqueFootprintHalfWidth();
        // 友军走路动画会改 sprite bounds；每帧估宽会导致佣兵站位 X 抖动（入场一顿一顿）
        return DefaultHalfWidth;
    }

    /// <summary>
    /// 沿 moveDir 检测前方同阵营单位（仅用于挤位/诊断；战斗移动不再因此停步）。
    /// </summary>
    public static bool IsBlockedByFrontAlly(UnitBase self, float moveDir)
    {
        if (self == null || self.isDead) return false;
        if (Mathf.Abs(moveDir) < 0.01f) return false;

        var bm = BattleManager.Instance;
        if (bm == null) return false;

        float myX = UnitBase.GetCombatX(self);
        float myHalf = GetHalfWidth(self);
        float dir = Mathf.Sign(moveDir);

        if (self.isAlly)
        {
            if (CheckList(bm.allyUnits, self, myX, myHalf, dir)) return true;
            if (bm.hero != null && bm.hero != self && !bm.hero.isDead)
            {
                if (Blocks(self, bm.hero, myX, myHalf, dir)) return true;
            }
        }
        else
        {
            if (CheckList(bm.monsters, self, myX, myHalf, dir)) return true;
        }
        return false;
    }

    /// <summary>
    /// 敌方怪物前进时：仅英雄/玩家挡路才停；不因前排其它怪物挡路而原地等（未进射程仍继续靠近目标）。
    /// </summary>
    public static bool IsBlockedByFrontHero(UnitBase self, float moveDir)
    {
        if (self == null || self.isDead || self.isAlly) return false;
        if (Mathf.Abs(moveDir) < 0.01f) return false;
        var bm = BattleManager.Instance;
        if (bm?.hero == null || bm.hero.isDead) return false;

        float myX = UnitBase.GetCombatX(self);
        float myHalf = GetHalfWidth(self);
        float dir = Mathf.Sign(moveDir);
        return Blocks(self, bm.hero, myX, myHalf, dir);
    }

    /// <summary>怪物允许重叠，不再沿 X 推开。</summary>
    public static void ResolveOverlap(UnitBase self)
    {
        if (self == null || self.isDead || self is Monster) return;
        var bm = BattleManager.Instance;
        if (bm == null) return;

        float myX = UnitBase.GetCombatX(self);
        float myHalf = GetHalfWidth(self);
        float push = 0f;

        void Consider(UnitBase other)
        {
            if (other == null || other == self || other.isDead) return;
            float ox = UnitBase.GetCombatX(other);
            float dx = myX - ox;
            float minSep = Mathf.Max(MinSeparation, myHalf + GetHalfWidth(other) + OverlapPad);
            float abs = Mathf.Abs(dx);
            if (abs >= minSep) return;

            float need = (minSep - abs) * 0.55f;
            if (abs < 0.04f)
                need = minSep * 0.55f;
            int tie = self.GetInstanceID() >= other.GetInstanceID() ? 1 : -1;
            float sign = abs < 0.04f ? tie : Mathf.Sign(dx);
            if (sign == 0f) sign = tie;
            push += sign * need;
        }

        if (self.isAlly)
        {
            if (bm.allyUnits != null)
            {
                for (int i = 0; i < bm.allyUnits.Count; i++)
                    Consider(bm.allyUnits[i]);
            }
            Consider(bm.hero);
        }
        else if (bm.monsters != null)
        {
            for (int i = 0; i < bm.monsters.Count; i++)
                Consider(bm.monsters[i]);
        }

        if (Mathf.Abs(push) < 0.01f) return;
        push = Mathf.Clamp(push, -0.35f, 0.35f);
        Vector3 p = self.transform.position;
        GameConfig.SetWorldPosition(self.transform, new Vector3(p.x + push, self.FootY, p.z));
    }

    static bool CheckList(System.Collections.Generic.List<UnitBase> list, UnitBase self,
        float myX, float myHalf, float dir)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
        {
            var other = list[i];
            if (other == null || other == self || other.isDead) continue;
            if (Blocks(self, other, myX, myHalf, dir)) return true;
        }
        return false;
    }

    static bool Blocks(UnitBase self, UnitBase other, float myX, float myHalf, float dir)
    {
        float ox = UnitBase.GetCombatX(other);
        float ahead = (ox - myX) * dir;
        if (ahead < -0.02f) return false;

        float otherHalf = GetHalfWidth(other);
        float gap = OverlapPad + HeroMercExtraGap(self, other);
        float stopDist = Mathf.Max(MinSeparation, myHalf + otherHalf + gap);
        return ahead <= stopDist;
    }

    static float HeroMercExtraGap(UnitBase self, UnitBase other)
    {
        bool heroMerc = (self is Hero && other is Mercenary) || (self is Mercenary && other is Hero);
        if (!heroMerc) return 0f;
        return Mathf.Max(GetHalfWidth(self), GetHalfWidth(other)) * HeroMercHalfBodyGapMul * 2f;
    }

    /// <summary>远程（弓/法/奶）站玩家身后；近战站玩家身前（靠怪）。可更近。</summary>
    public static float GetMercDesiredCombatX(Hero hero, Mercenary merc, int partyIndex)
    {
        if (hero == null) return 0f;
        float heroX = UnitBase.GetCombatX(hero);
        float heroHalf = GetHalfWidth(hero);
        float mercHalf = merc != null ? GetHalfWidth(merc) : DefaultHalfWidth;
        float gap = heroHalf * HeroMercHalfBodyGapMul * 2f;
        float first = heroHalf + gap + mercHalf + MercLineBasePad;

        bool ranged = IsRangedFormation(merc);
        int lineSlot = GetMercLineSlot(merc, ranged, partyIndex);
        float offset = first + lineSlot * MercSameLineStep;
        return ranged ? heroX - offset : heroX + offset;
    }

    /// <summary>弓/杖类远程站后排；其余近战前排。</summary>
    public static bool IsRangedFormation(Mercenary merc)
    {
        if (merc == null) return false;
        return !Mercenary.IsMeleeRoleId(merc.mercId);
    }

    /// <summary>在同排（前/后）里的序号：按活跃列表顺序，不含对面排。</summary>
    public static int GetMercLineSlot(Mercenary merc, bool rangedLine, int fallbackIndex)
    {
        var mm = MercenaryManager.Instance;
        var list = mm != null ? mm.GetActiveMercs() : null;
        if (list == null || list.Count == 0)
            return Mathf.Max(0, fallbackIndex);

        int slot = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.isDead) continue;
            if (IsRangedFormation(m) != rangedLine) continue;
            if (m == merc) return slot;
            slot++;
        }
        return Mathf.Max(0, fallbackIndex);
    }

    static float EstimateSpriteWidth(UnitBase u)
    {
        var srs = u.GetComponentsInChildren<SpriteRenderer>(true);
        float maxW = 0f;
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            string n = sr.gameObject.name;
            if (IsIgnoredSpriteName(n)) continue;
            float w = sr.bounds.size.x;
            if (w > maxW) maxW = w;
        }
        if (maxW < 0.05f) return DefaultHalfWidth * 2f;
        return maxW;
    }

    static bool IsIgnoredSpriteName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        return n.Contains("shadow") || n.Contains("阴影") || n.Contains("hpbar")
            || n.Contains("bar_bg") || n.Contains("damage") || n.Contains("stackcount")
            || n.Contains("mercname");
    }

    // 簇统计的复用缓冲：原实现每 0.12s new 一次 List/int[]/Dictionary + 两个局部函数闭包，
    // 手机上 GC 抖动明显。这里全部静态化，热路径零分配。
    static readonly System.Collections.Generic.List<Monster> _stackAlive = new System.Collections.Generic.List<Monster>(48);
    static readonly System.Collections.Generic.Dictionary<int, int> _clusterSize = new System.Collections.Generic.Dictionary<int, int>(48);
    static readonly System.Collections.Generic.Dictionary<int, int> _clusterLeader = new System.Collections.Generic.Dictionary<int, int>(48);
    static int[] _ufParent = new int[64];

    /// <summary>统计真正重叠的怪物簇，仅在 N≥2 时显示 ×N（开关见 GameConfig.SHOW_MONSTER_STACK_LABEL）。</summary>
    public static void TickMonsterOverlapStacks()
    {
        if (!GameConfig.SHOW_MONSTER_STACK_LABEL) return;
        if (Time.time < _nextStackRefresh) return;
        _nextStackRefresh = Time.time + 0.12f;

        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return;

        _stackAlive.Clear();
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            if (bm.monsters[i] is Monster m && m != null && !m.isDead)
                _stackAlive.Add(m);
        }

        int n = _stackAlive.Count;
        if (n == 0) return;

        if (_ufParent == null || _ufParent.Length < n)
            _ufParent = new int[Mathf.NextPowerOfTwo(n)];
        for (int i = 0; i < n; i++) _ufParent[i] = i;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (MonsterFootprintsOverlap(_stackAlive[i], _stackAlive[j]))
                {
                    int ra = FindSet(_ufParent, i);
                    int rb = FindSet(_ufParent, j);
                    if (ra != rb) _ufParent[rb] = ra;
                }
            }
        }

        _clusterSize.Clear();
        _clusterLeader.Clear();
        for (int i = 0; i < n; i++)
        {
            int r = FindSet(_ufParent, i);
            _clusterSize.TryGetValue(r, out int c);
            _clusterSize[r] = c + 1;

            if (!_clusterLeader.TryGetValue(r, out int leaderIdx))
                _clusterLeader[r] = i;
            else if (UnitBase.GetCombatX(_stackAlive[i]) < UnitBase.GetCombatX(_stackAlive[leaderIdx]))
                _clusterLeader[r] = i;
        }

        for (int i = 0; i < n; i++)
        {
            int r = FindSet(_ufParent, i);
            int size = _clusterSize[r];
            if (size >= 2 && _clusterLeader[r] == i)
                _stackAlive[i].SetOverlapStackCount(size);
            else
                _stackAlive[i].SetOverlapStackCount(1);
        }
    }

    static int FindSet(int[] parent, int x)
    {
        while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
        return x;
    }

    static bool MonsterFootprintsOverlap(Monster a, Monster b)
    {
        if (a == null || b == null || a == b) return false;

        float ax = UnitBase.GetCombatX(a);
        float bx = UnitBase.GetCombatX(b);
        float dx = Mathf.Abs(ax - bx);

        float aHalf = GetStackOverlapHalfWidth(a);
        float bHalf = GetStackOverlapHalfWidth(b);
        float overlapLen = Mathf.Max(0f, aHalf + bHalf - dx);
        if (overlapLen <= 0f) return false;

        float minWidth = Mathf.Min(aHalf + aHalf, bHalf + bHalf);
        if (overlapLen < minWidth * StackOverlapRatio) return false;

        // 车道（Y）必须同时重叠：只比 X 会把上下两排错开的怪算成一簇，
        // 这正是「明明没重叠还亮 ×N」的根因。
        float dy = Mathf.Abs(a.FootY - b.FootY);
        float yLimit = Mathf.Min(GetStackOverlapHalfHeight(a), GetStackOverlapHalfHeight(b))
                       * 2f * StackOverlapRatio;
        return dy < yLimit;
    }

    /// <summary>躯干竖直半高（sprite 含大量头部/透明留白，取半高再折半）。</summary>
    static float GetStackOverlapHalfHeight(Monster m)
    {
        var sr = m != null ? m.sr : null;
        if (sr != null && sr.sprite != null)
            return Mathf.Max(0.25f, sr.bounds.size.y * 0.25f);
        return 0.3f;
    }

    static float GetStackOverlapHalfWidth(Monster m)
    {
        if (m == null) return MonsterFallbackHalfWidth;
        float opaque = m.GetOpaqueFootprintHalfWidth();
        var sr = m.sr;
        if (sr != null && sr.sprite != null)
        {
            float visualHalf = sr.bounds.size.x * 0.5f;
            return Mathf.Max(opaque, visualHalf * 0.42f, MonsterFallbackHalfWidth);
        }
        return Mathf.Max(opaque, MonsterFallbackHalfWidth);
    }

    /// <summary>怪物不再挂按整图缩放的碰撞体（允许重叠）。</summary>
    public static void EnsureTriggerCollider(UnitBase u)
    {
        if (u == null || u is Monster) return;
        var box = u.GetComponent<BoxCollider2D>();
        if (box == null) box = u.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        float w = EstimateSpriteWidth(u) * SpriteScale;
        float h = Mathf.Max(0.4f, w * 1.2f);
        box.size = new Vector2(w, h);
        box.offset = new Vector2(0f, h * 0.35f);
    }
}
