using UnityEngine;

/// <summary>
/// 同阵营单位挤位：后面的单位撞到前面的就停，避免叠成一坨继续往前拱。
/// 碰撞半径按精灵可视宽度估算（不依赖物理碰撞体）。
/// </summary>
public static class UnitCrowd
{
    const float SpriteScale = 0.92f;
    const float DefaultHalfWidth = 0.42f;
    const float OverlapPad = 0.08f;
    const float MinSeparation = 0.95f;

    public static float GetHalfWidth(UnitBase u)
    {
        if (u == null) return DefaultHalfWidth;
        float w = EstimateSpriteWidth(u);
        return Mathf.Max(DefaultHalfWidth, w * 0.5f * SpriteScale);
    }

    /// <summary>
    /// 沿 moveDir（-1/1）前进时，前方是否已有同阵营存活单位挡住。
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

    /// <summary>同阵营重叠时沿 X 轻推分开（进战/围殴叠怪用）。</summary>
    public static void ResolveOverlap(UnitBase self)
    {
        if (self == null || self.isDead) return;
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
        GameConfig.SetWorldPosition(self.transform, new Vector3(p.x + push, UnitBase.GROUND_Y, p.z));
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
        float stopDist = Mathf.Max(MinSeparation, myHalf + otherHalf + OverlapPad);
        return ahead <= stopDist;
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
            || n.Contains("bar_bg") || n.Contains("damage");
    }

    /// <summary>可选：给单位挂上按精灵缩放的 BoxCollider2D（触发器用，AI 仍走软挡路）。</summary>
    public static void EnsureTriggerCollider(UnitBase u)
    {
        if (u == null) return;
        var box = u.GetComponent<BoxCollider2D>();
        if (box == null) box = u.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        float w = EstimateSpriteWidth(u) * SpriteScale;
        float h = Mathf.Max(0.4f, w * 1.2f);
        box.size = new Vector2(w, h);
        box.offset = new Vector2(0f, h * 0.35f);
    }
}
