using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 英雄 SPUM 武器挂点规则（冻结约定，新增武器类型都走这里）：
///
/// 1. wanjia 冻结：ArmR = 攻击手/主手（R_Weapon）；ArmL = 副手（L_Weapon）。盾仅副手（L_Shield）。
/// 2. 运行时 Build() 得到 HandRig：
///    - AttackSlot / AttackDir   → 普攻那只手（剑/弓/杖/双手武器）
///    - SecondarySlot / SecondaryDir → 另一只手（盾、副手剑/斧等）
/// 3. 穿戴槽位与 SPUM Dir 解耦：ResolveWearSlot() 按武器职责映射到 Attack/Secondary 槽。
/// 4. 正反：SPUM 武器精灵按挂点手型绘制，放对手会「拿反」——保证 Dir 正确即可；
///    换装后恢复该挂点预制体默认 flipX，避免运行时污染。
/// </summary>
public static class HeroWeaponRig
{
    public const string DirRight = "Right";
    public const string DirLeft = "Left";

    public enum WeaponWearRole
    {
        Attack,
        Secondary,
        TwoHand
    }

    public struct HandRig
    {
        public EquipSlotType AttackSlot;
        public EquipSlotType SecondarySlot;
        public string AttackDir;
        public string SecondaryDir;
        public bool AttackHandFlipX;
        public bool SecondaryHandFlipX;

        public bool IsValid => !string.IsNullOrEmpty(AttackDir);
    }

    /// <summary>旧接口：固定 MainHand→Right。请改用 DirForSlot(slot, rig)。</summary>
    public static string DirForSlot(EquipSlotType slot)
        => slot == EquipSlotType.MainHand ? DirRight : DirLeft;

    public static EquipSlotType SlotForDir(string dir)
        => dir == DirRight ? EquipSlotType.MainHand : EquipSlotType.OffHand;

    public static HandRig Build(SPUM_Prefabs spum, SPUM_MatchingList[] lists)
    {
        string attackDir = DetectAttackSpumDir(spum, lists);
        string secondaryDir = attackDir == DirLeft ? DirRight : DirLeft;
        var rig = new HandRig
        {
            AttackDir = attackDir,
            SecondaryDir = secondaryDir,
            AttackSlot = SlotForDir(attackDir),
            SecondarySlot = SlotForDir(secondaryDir),
        };
        CaptureHandFlipDefaults(lists, rig.AttackDir, rig.SecondaryDir, out rig.AttackHandFlipX, out rig.SecondaryHandFlipX);
        return rig;
    }

    public static string DirForSlot(EquipSlotType slot, in HandRig rig)
    {
        if (slot == rig.AttackSlot) return rig.AttackDir;
        if (slot == rig.SecondarySlot) return rig.SecondaryDir;
        return DirForSlot(slot);
    }

    public static bool DefaultFlipXForDir(in HandRig rig, string dir)
    {
        if (dir == rig.AttackDir) return rig.AttackHandFlipX;
        if (dir == rig.SecondaryDir) return rig.SecondaryHandFlipX;
        return false;
    }

    /// <summary>扫描 SPUM 预制体：攻击手 = 默认挂了「非盾」武器的那只手。</summary>
    public static EquipSlotType DetectAttackWeaponSlot(SPUM_Prefabs spum, SPUM_MatchingList[] lists)
        => Build(spum, lists).AttackSlot;

    public static WeaponWearRole ClassifyWeapon(EquipInstance equip)
    {
        if (equip == null) return WeaponWearRole.Secondary;
        if (equip.weaponType == WeaponType.TwoHand) return WeaponWearRole.TwoHand;
        if (WeaponLoadoutRules.IsShield(equip)) return WeaponWearRole.Secondary;
        if (WeaponLoadoutRules.IsOffHandWeapon(equip)) return WeaponWearRole.Secondary;
        if (equip.weaponType != WeaponType.None) return WeaponWearRole.Attack;
        return WeaponWearRole.Secondary;
    }

    /// <summary>穿戴映射：看 weaponHand + HandRig，双手仅攻击手。</summary>
    public static EquipSlotType ResolveWearSlot(EquipInstance equip, in HandRig rig)
        => WeaponLoadoutRules.ResolveWearSlot(equip, rig);

    public static void ApplyWeaponPresentation(SpriteRenderer renderer, string spumDir, in HandRig rig)
    {
        if (renderer == null) return;
        renderer.flipX = DefaultFlipXForDir(rig, spumDir);
    }

    public static bool IsPrimaryWeaponRenderer(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.gameObject == null) return false;
        string n = renderer.gameObject.name;
        return n.IndexOf("L_Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0
               || n.IndexOf("R_Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsShieldRenderer(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.gameObject == null) return false;
        string n = renderer.gameObject.name;
        return n.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsShieldSpumName(string spumName)
    {
        if (string.IsNullOrEmpty(spumName)) return false;
        return spumName.ToLowerInvariant().Contains("shield");
    }

    public static bool TryGetPrimaryWeaponRenderer(SPUM_MatchingList[] lists, string spumDir, out SpriteRenderer renderer)
    {
        renderer = null;
        if (lists == null) return false;
        string want = spumDir == DirRight ? "R_Weapon" : "L_Weapon";
        for (int i = 0; i < lists.Length; i++)
        {
            var tables = lists[i]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me?.renderer == null || me.PartType != "Weapons" || me.Dir != spumDir) continue;
                if (!IsPrimaryWeaponRenderer(me.renderer)) continue;
                renderer = me.renderer;
                return true;
            }
        }
        return false;
    }

    static string DetectAttackSpumDir(SPUM_Prefabs spum, SPUM_MatchingList[] lists)
    {
        // 冻结：ArmR=主手/攻击(Right)，ArmL=副手(Left)；盾只挂副手
        if (HasNamedNode(spum, lists, "ArmR")) return DirRight;
        if (HasNamedNode(spum, lists, "ArmL") && !HasNamedNode(spum, lists, "ArmR")) return DirLeft;

        string leftAttack = null, rightAttack = null;

        void Consider(string dir, string path)
        {
            if (string.IsNullOrEmpty(path) || IsShieldPath(path)) return;
            if (!LooksLikeWeaponPath(path)) return;
            if (dir == DirLeft) leftAttack = path;
            else if (dir == DirRight) rightAttack = path;
        }

        if (spum?.ImageElement != null)
        {
            for (int i = 0; i < spum.ImageElement.Count; i++)
            {
                var ie = spum.ImageElement[i];
                if (ie == null || ie.PartType != "Weapons") continue;
                Consider(ie.Dir, ie.ItemPath);
            }
        }

        if (lists != null)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                var ml = lists[i];
                if (ml?.matchingTables == null) continue;
                for (int j = 0; j < ml.matchingTables.Count; j++)
                {
                    var me = ml.matchingTables[j];
                    if (me == null || me.PartType != "Weapons") continue;
                    Consider(me.Dir, me.ItemPath);
                }
            }
        }

        if (!string.IsNullOrEmpty(rightAttack) && string.IsNullOrEmpty(leftAttack)) return DirRight;
        if (!string.IsNullOrEmpty(leftAttack) && string.IsNullOrEmpty(rightAttack)) return DirLeft;
        if (!string.IsNullOrEmpty(rightAttack)) return DirRight;

        if (HasWeaponRendererNamed(lists, "R_Weapon")) return DirRight;
        if (HasWeaponRendererNamed(lists, "L_Weapon")) return DirLeft;
        return DirRight;
    }

    static bool HasNamedNode(SPUM_Prefabs spum, SPUM_MatchingList[] lists, string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName)) return false;
        if (spum != null)
        {
            var trs = spum.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                if (trs[i] != null && trs[i].name.Equals(nodeName, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        if (lists != null)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                if (lists[i] == null) continue;
                var trs = lists[i].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < trs.Length; t++)
                {
                    if (trs[t] != null && trs[t].name.Equals(nodeName, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        return false;
    }

    static void CaptureHandFlipDefaults(SPUM_MatchingList[] lists, string attackDir, string secondaryDir,
        out bool attackFlip, out bool secondaryFlip)
    {
        attackFlip = false;
        secondaryFlip = false;
        if (lists == null) return;
        for (int i = 0; i < lists.Length; i++)
        {
            var tables = lists[i]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me == null || me.PartType != "Weapons" || me.renderer == null) continue;
                if (me.Dir == attackDir) attackFlip = me.renderer.flipX;
                else if (me.Dir == secondaryDir) secondaryFlip = me.renderer.flipX;
            }
        }
    }

    static bool HasWeaponRendererNamed(SPUM_MatchingList[] lists, string nodeName)
    {
        if (lists == null || string.IsNullOrEmpty(nodeName)) return false;
        for (int i = 0; i < lists.Length; i++)
        {
            var tables = lists[i]?.matchingTables;
            if (tables == null) continue;
            for (int t = 0; t < tables.Count; t++)
            {
                var me = tables[t];
                if (me == null || me.PartType != "Weapons" || me.renderer == null) continue;
                var go = me.renderer.gameObject;
                if (go != null && go.name.IndexOf(nodeName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    public static bool IsShieldEquip(EquipInstance equip)
        => WeaponLoadoutRules.IsShield(equip);

    static bool IsShieldPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string low = path.ToLowerInvariant();
        return low.Contains("shield") || low.Contains("3_shield") || low.Contains("7_shield");
    }

    static bool LooksLikeWeaponPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string low = path.ToLowerInvariant();
        if (IsShieldPath(low)) return false;
        return low.Contains("sword") || low.Contains("axe") || low.Contains("spear")
               || low.Contains("hammer") || low.Contains("wand") || low.Contains("bow")
               || low.Contains("staff") || low.Contains("6_weapons");
    }
}
