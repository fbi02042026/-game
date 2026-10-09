# -*- coding: utf-8 -*-
"""
装备模板 baseAttr 重调（2026-10-09 主人拍板）

要解决的问题：
  1. 防具模板（头盔/胸甲/盾牌…）的 baseAttr 大面积填成 attrType 5 = Attack value 5，
     导致防具加攻击不加防御。现在按【槽位】改回该有的主属性（头盔/胸甲→防御，鞋→移速，披风→生命…）。
  2. 物理 / 魔法区分：weaponAttackType == Magic(1) 的武器走 MagicAttack(22)，否则走 Attack(5)。
  3. 属性条数跟着稀有度走（主人原话）：普通 1 条 / 稀有 2 条 / 传奇 2~3 条（+ 武器再挂 globalBonus 当被动）。
     Rarity 枚举 1 普通 2 优 3 稀有 4 史诗 5 传奇 → 条数 1 / 2 / 2 / 3 / 3。
  4. minLevel 按稀有度分档（当前代码未读该字段，纯预埋，等主人拍板再接门禁）。

用法：python Tools/equip_template_retune.py          # 只出报告，不写盘
      python Tools/equip_template_retune.py --apply  # 备份后写盘
"""
import os
import re
import sys
import shutil

ROOT = r"Y:\PixelAdventureTown"
EQ_DIR = os.path.join(ROOT, "Assets", "Resources", "Config", "Equips")
BACKUP_DIR = os.path.join(ROOT, ".workbuddy", "backup", "20261009-equip-templates")

# AttrType 枚举序号
A_MAXHP, A_ATTACK, A_ATKSPD, A_CRITRATE, A_MOVESPD, A_DEFENSE = 4, 5, 6, 7, 8, 10
A_LIFESTEAL = 11
A_MAGIC_ATK, A_MAGIC_DEF = 22, 23
A_FIRE_DMG, A_CRITDMG, A_POISON = 13, 20, 24

ST_HEAD, ST_CHEST, ST_HANDS, ST_FEET, ST_CAPE, ST_MAIN, ST_OFF = 0, 1, 2, 3, 4, 5, 6

# 初始武器（player_job_base_stats 的主手/副手模板ID）压一档，避免开局直接膨胀
STARTER_IDS = {
    "equip_training_sword", "equip_sword_1", "equip_shield_1", "weapon_twilight_staff",
    "equip_f_sr_hammer", "equip_new_weapon_06", "equip_new_weapon_12", "equip_magic_weapon_07",
}

# 稀有度 → (属性条数, 数值倍率, 等级门槛)
# 2026-10-09 主人拍板：卡进度靠「怪物伤害 / 数量」，**不做等级硬门禁** → minLevel 一律写 1（字段本身也无人读取）。
RARITY_PLAN = {
    1: (1, 1.00, 1),
    2: (2, 1.20, 1),
    3: (2, 1.45, 1),
    4: (3, 1.75, 1),
    5: (3, 2.40, 1),
}

# 2026-10-09 主人拍板：**传奇只给武器**（各职业武器 + 被动），其它部位最高史诗。
# 各职业武器关键字 → 用来保底「每个职业至少有一件传奇武器」。
JOB_WEAPON_GROUPS = {
    "剑盾": ("sword",),
    "重武": ("hammer", "axe", "spear"),
    "狂战/游侠": ("new_weapon",),
    "法师/牧师": ("magic_weapon", "staff", "twilight"),
}

# ---------------------------------------------------------------------------
# 【2026-10-09 主人拍板】传奇武器按【职业定位】逐件差异化。
#
# ⚠ 必须先知道的一条事实：武器模板 baseAttr 里的**主攻击值写多少都没用** ——
#    EquipInstance.ApplyTableDrivenWeaponAttack（EquipInstance.cs:137-173）会把第一条
#    攻击属性**硬覆盖**成 equip_attr_ranges 的 ATK 行 × 星级 × 稀有度倍率（且只认 "ATK" 行，
#    法师/牧师也一样读 ATK 行，只是落到 MagicAttack 上）。
#    → 所以「职业定位」能落地的只有三处：**副属性 / globalBonus / 被动**。
#    主攻击值的物理/魔法区分由 equip_attr_ranges 的 ATK / MAGIC_ATK 两行负责（掉落路径）。
#
# 职业定位（取自 player_job_base_stats）：
#   P001 剑盾 坦克：血厚防高、攻低间隔 0.85 → 副属性给防/血，globalBonus 给**防御**（不给攻击）
#   P002 重武 控制：血最厚、基础攻最高 8.0、间隔最慢 1.15 → 单次伤害最高，给暴伤
#   P003 狂战 爆发：血最薄、基础攻最低 3.8、间隔最快 0.65 → 攻速/暴击 + 吸血续命
#   P004 游侠 远程：血薄、射程 220、移速最快 60 → 攻速 + 移速，被动给**毒**（游侠专属已落地）
#   P005 法师 法系爆发：血最薄 225 → 魔攻给最高 + 元素增伤，靠高回报补生存
#   P006 牧师 法系支援：血 390 → 魔攻给最低，改给生命/魔防 + 吸血（牧师向那件）
# ---------------------------------------------------------------------------
LEGEND_JOB_PLAN = {
    # --- P001 剑盾卫士（坦克） ---
    "equip_axe_1": dict(
        job="P001剑盾·坦克", main=(A_ATTACK, 30.0, 0),
        subs=[(A_DEFENSE, 18.0, 0), (A_MAXHP, 160.0, 0)],
        gb=(A_DEFENSE, 24.0, 0), pas=(A_LIFESTEAL, 0.05, 0)),
    "equip_new_weapon_11": dict(
        job="P001剑盾·坦克", main=(A_ATTACK, 33.0, 0),
        subs=[(A_DEFENSE, 20.0, 0), (A_MAXHP, 180.0, 0)],
        gb=(A_DEFENSE, 27.0, 0), pas=(A_LIFESTEAL, 0.055, 0)),
    "equip_new_weapon_17": dict(
        job="P001剑盾·坦克", main=(A_ATTACK, 36.0, 0),
        subs=[(A_DEFENSE, 22.0, 0), (A_MAXHP, 200.0, 0)],
        gb=(A_DEFENSE, 30.0, 0), pas=(A_LIFESTEAL, 0.06, 0)),
    "equip_sword_6": dict(
        job="P001剑盾·坦克", main=(A_ATTACK, 39.0, 0),
        subs=[(A_DEFENSE, 24.0, 0), (A_MAXHP, 220.0, 0)],
        gb=(A_DEFENSE, 33.0, 0), pas=(A_LIFESTEAL, 0.065, 0)),
    # --- P002 重武者（控制 / 长枪，慢速高单次） ---
    "equip_new_weapon_09": dict(
        job="P002重武·控制", main=(A_ATTACK, 46.0, 0),
        subs=[(A_MAXHP, 130.0, 0), (A_DEFENSE, 10.0, 0)],
        gb=(A_ATTACK, 74.0, 0), pas=(A_CRITDMG, 0.25, 0)),
    "equip_new_weapon_13": dict(
        job="P002重武·控制", main=(A_ATTACK, 49.0, 0),
        subs=[(A_MAXHP, 140.0, 0), (A_DEFENSE, 11.0, 0)],
        gb=(A_ATTACK, 78.0, 0), pas=(A_CRITDMG, 0.28, 0)),
    "equip_new_weapon_14": dict(
        job="P002重武·控制", main=(A_ATTACK, 52.0, 0),
        subs=[(A_MAXHP, 150.0, 0), (A_DEFENSE, 12.0, 0)],
        gb=(A_ATTACK, 82.0, 0), pas=(A_CRITDMG, 0.30, 0)),
    "equip_new_weapon_16": dict(
        job="P002重武·控制", main=(A_ATTACK, 55.0, 0),
        subs=[(A_MAXHP, 160.0, 0), (A_DEFENSE, 13.0, 0)],
        gb=(A_ATTACK, 86.0, 0), pas=(A_CRITDMG, 0.32, 0)),
    # --- P003 狂战士（爆发 / 双手，最快攻击间隔 0.65） ---
    "equip_new_weapon_18": dict(
        job="P003狂战·爆发", main=(A_ATTACK, 42.0, 0),
        subs=[(A_ATKSPD, 0.10, 1), (A_CRITRATE, 0.07, 0)],
        gb=(A_ATTACK, 64.0, 0), pas=(A_LIFESTEAL, 0.07, 0)),
    "equip_new_weapon_19": dict(
        job="P003狂战·爆发", main=(A_ATTACK, 45.0, 0),
        subs=[(A_ATKSPD, 0.11, 1), (A_CRITRATE, 0.08, 0)],
        gb=(A_ATTACK, 68.0, 0), pas=(A_LIFESTEAL, 0.08, 0)),
    "equip_new_weapon_20": dict(
        job="P003狂战·爆发", main=(A_ATTACK, 48.0, 0),
        subs=[(A_ATKSPD, 0.12, 1), (A_CRITRATE, 0.09, 0)],
        gb=(A_ATTACK, 72.0, 0), pas=(A_LIFESTEAL, 0.09, 0)),
    # --- P004 游侠（远程弓，射程 220 / 移速 60） ---
    "equip_new_weapon_10": dict(
        job="P004游侠·远程", main=(A_ATTACK, 38.0, 0),
        subs=[(A_ATKSPD, 0.09, 1), (A_MOVESPD, 0.07, 1)],
        gb=(A_ATTACK, 58.0, 0), pas=(A_POISON, 0.10, 0)),
    "equip_new_weapon_15": dict(
        job="P004游侠·远程", main=(A_ATTACK, 42.0, 0),
        subs=[(A_ATKSPD, 0.10, 1), (A_MOVESPD, 0.08, 1)],
        gb=(A_ATTACK, 64.0, 0), pas=(A_POISON, 0.12, 0)),
    # --- P005 法师（法系爆发，血最薄 225 → 伤害给最高） ---
    "equip_magic_weapon_09": dict(
        job="P005法师·爆发", main=(A_MAGIC_ATK, 60.0, 0),
        subs=[(A_FIRE_DMG, 0.18, 1), (A_CRITRATE, 0.06, 0)],
        gb=(A_MAGIC_ATK, 90.0, 0), pas=(A_FIRE_DMG, 0.15, 1)),
    "equip_magic_weapon_11": dict(
        job="P005法师·持续", main=(A_MAGIC_ATK, 55.0, 0),
        subs=[(A_FIRE_DMG, 0.15, 1), (A_ATKSPD, 0.08, 1)],
        gb=(A_MAGIC_ATK, 82.0, 0), pas=(A_CRITDMG, 0.35, 0)),
    # --- P006 牧师（法系支援，血 390 → 魔攻给最低，改补生存） ---
    "equip_magic_weapon_16": dict(
        job="P006牧师·支援", main=(A_MAGIC_ATK, 46.0, 0),
        subs=[(A_MAXHP, 220.0, 0), (A_MAGIC_DEF, 14.0, 0)],
        gb=(A_MAGIC_ATK, 66.0, 0), pas=(A_LIFESTEAL, 0.06, 0)),
}

# 传奇必带的一条「被动」→ 写进模板 skillPassives。
# 走 EquipStatRollup.AppendSkillPassives（EquipStatRollup.cs:27/81），**全槽位都结算**，
# 且不经过 TryResolveCombatAttr 白名单 → 生命偷取 / 减伤% 这类没在掉落池里落地的属性也能用。
def passive_of(slot, is_magic, is_shield):
    if slot == ST_HEAD:
        return (A_DEFENSE, 0.05, 1)
    if slot == ST_CHEST:
        return (A_DEFENSE, 0.08, 1)
    if slot == ST_HANDS:
        return (A_ATKSPD, 0.08, 1)
    if slot == ST_FEET:
        return (A_MOVESPD, 0.06, 1)
    if slot == ST_CAPE:
        return (A_MAXHP, 80.0, 0)
    if slot == ST_MAIN:
        return (A_FIRE_DMG, 0.12, 1) if is_magic else (11, 0.04, 0)   # 11 = LifeSteal
    if is_shield:
        return (A_DEFENSE, 0.06, 1)
    return (A_FIRE_DMG, 0.08, 1) if is_magic else (A_ATKSPD, 0.06, 1)


def num(v):
    f = float(v)
    if abs(f - int(f)) < 1e-6:
        return str(int(f))
    s = ("%.3f" % f).rstrip("0").rstrip(".")
    return s


def sub_pool(slot, is_magic, is_shield):
    """副属性池（按顺序取第 2、3 条）。格式：(attrType, value, isPercent)"""
    if slot == ST_HEAD:
        return [(A_MAXHP, 30.0, 0), (A_MAGIC_DEF, 3.0, 0)]
    if slot == ST_CHEST:
        return [(A_MAXHP, 45.0, 0), (A_MAGIC_DEF, 4.0, 0)]
    if slot == ST_HANDS:
        return [(A_ATKSPD, 0.04, 1), (A_MAXHP, 25.0, 0)]
    if slot == ST_FEET:
        return [(A_MAXHP, 25.0, 0), (A_DEFENSE, 3.0, 0)]
    if slot == ST_CAPE:
        return [(A_DEFENSE, 3.0, 0), (A_MAGIC_DEF, 3.0, 0)]
    if slot == ST_MAIN:
        return [(A_ATKSPD, 0.05, 1), (A_FIRE_DMG, 6.0, 0)] if is_magic \
            else [(A_ATKSPD, 0.05, 1), (A_DEFENSE, 4.0, 0)]
    # ST_OFF
    if is_shield:
        return [(A_MAXHP, 30.0, 0), (A_MAGIC_DEF, 4.0, 0)]
    return [(A_ATKSPD, 0.04, 1), (A_MAXHP, 20.0, 0)]


def main_attr(slot, is_magic, is_shield):
    if slot == ST_HEAD:
        return (A_DEFENSE, 6.0, 0)
    if slot == ST_CHEST:
        return (A_DEFENSE, 9.0, 0)
    if slot == ST_HANDS:
        return (A_MAGIC_ATK, 4.0, 0) if is_magic else (A_ATTACK, 4.0, 0)
    if slot == ST_FEET:
        return (A_MOVESPD, 0.05, 1)
    if slot == ST_CAPE:
        return (A_MAXHP, 50.0, 0)
    if slot == ST_MAIN:
        return (A_MAGIC_ATK, 12.0, 0) if is_magic else (A_ATTACK, 12.0, 0)
    if is_shield:
        return (A_DEFENSE, 7.0, 0)
    return (A_MAGIC_ATK, 5.0, 0) if is_magic else (A_ATTACK, 5.0, 0)


def parse(path):
    t = open(path, encoding="utf-8").read()
    d = {"crlf": "\r\n" in t}
    t = t.replace("\r\n", "\n")

    def gi(key, default=0):
        m = re.search(r"^  %s: (-?\d+)" % key, t, re.M)
        return int(m.group(1)) if m else default

    m = re.search(r"^  templateId: (\S+)", t, re.M)
    d["templateId"] = m.group(1) if m else os.path.basename(path).replace(".asset", "")
    d["baseRarity"] = gi("baseRarity", 1)
    d["slotType"] = gi("slotType", 0)
    d["weaponAttackType"] = gi("weaponAttackType", 0)
    d["weaponType"] = gi("weaponType", 0)
    d["isAnchor"] = gi("isAnchor", 0)
    d["text"] = t
    return d


def render_base_attr(entries):
    lines = ["  baseAttr:"]
    for at, v, p in entries:
        lines.append("  - attrType: %d" % at)
        lines.append("    value: %s" % num(v))
        lines.append("    isPercent: %d" % p)
    return "\n".join(lines)


# 各职业保底一件传奇武器（由 main() 按 JOB_WEAPON_GROUPS 算出，保证每个职业都有顶级武器）
LEGEND_FLOOR = set()


def tier_of(d):
    """按模板序号定档位：_1/_001→普通，_2→优良，_3→稀有，_4~_8→史诗，≥9→传奇。
    2026-10-09 主人拍板：**传奇只给武器**，非武器部位最高史诗(4)。
    初始武器 / 锚点模板强制普通（它们带 overrideRarity，档位改了没意义，反而容易误判）。"""
    tid = d["templateId"]
    slot = d["slotType"]
    if tid in LEGEND_FLOOR and slot == ST_MAIN:
        return 5
    if tid in STARTER_IDS or d["isAnchor"] == 1:
        return d["baseRarity"] if tid == "weapon_twilight_staff" else 1
    m = re.search(r"(\d+)$", tid)
    n = int(m.group(1)) if m else 1
    if n <= 1:
        t = 1
    elif n == 2:
        t = 2
    elif n == 3:
        t = 3
    elif n <= 8:
        t = 4
    else:
        t = 5
    # 传奇只给武器：头盔/胸甲/手/鞋/披风/副手盾 一律封顶史诗
    if t == 5 and slot != ST_MAIN:
        t = 4
    return t


def build(d):
    tid = d["templateId"]
    slot = d["slotType"]
    is_magic = d["weaponAttackType"] == 1
    is_shield = slot == ST_OFF and "shield" in tid.lower()
    cnt, mul, min_lv = RARITY_PLAN.get(tier_of(d), (1, 1.0, 1))
    scale = mul * (0.55 if tid in STARTER_IDS else 1.0)

    entries = []
    a, v, p = main_attr(slot, is_magic, is_shield)
    entries.append((a, v * (1.0 if p else scale), p))
    subs = sub_pool(slot, is_magic, is_shield)
    for i in range(min(cnt - 1, len(subs))):
        a2, v2, p2 = subs[i]
        entries.append((a2, v2 * (1.0 if p2 else scale) * (0.8 if i else 1.0), p2))

    # 传奇武器额外挂 globalBonus（AttrSystem.cs:156 目前只对武器结算，防具不挂，避免「显示但不生效」）
    gb = None
    pas = None
    if tier_of(d) == 5:
        pas = passive_of(slot, is_magic, is_shield)
        if slot == ST_MAIN:
            # 主人要求「传奇攻击最少 50+，要拉出差距」→ 30 × 2.40(传奇倍率) ≈ 72 攻 / 魔攻
            gb = (A_MAGIC_ATK if is_magic else A_ATTACK, 30.0 * scale, 0)
        # 按职业定位逐件覆盖（含主属性 / 副属性 / globalBonus / 被动）
        plan = LEGEND_JOB_PLAN.get(tid)
        if plan is not None:
            entries = [plan["main"]] + list(plan["subs"])
            gb = plan["gb"]
            pas = plan["pas"]
    return entries, gb, min_lv, tier_of(d), pas


def rewrite(text, entries, gb, min_lv, rarity, pas):
    new_ba = render_base_attr(entries)
    text = re.sub(r"^  baseAttr:\n(?:  - .*\n|    .*\n)*", new_ba + "\n", text, count=1, flags=re.M)
    if gb is None:
        gb_txt = "  globalBonus:\n    attrType: 0\n    value: 0\n    isPercent: 0"
    else:
        gb_txt = "  globalBonus:\n    attrType: %d\n    value: %s\n    isPercent: %d" % (gb[0], num(gb[1]), gb[2])
    text = re.sub(r"^  globalBonus:\n    attrType: .*\n    value: .*\n    isPercent: .*",
                  gb_txt, text, count=1, flags=re.M)
    text = re.sub(r"^  minLevel: .*$", "  minLevel: %d" % min_lv, text, count=1, flags=re.M)
    text = re.sub(r"^  baseRarity: .*$", "  baseRarity: %d" % rarity, text, count=1, flags=re.M)
    if pas is not None:
        blk = "  skillPassives:\n  - attrType: %d\n    value: %s\n    isPercent: %d" % (pas[0], num(pas[1]), pas[2])
    else:
        blk = "  skillPassives: []"
    if re.search(r"^  skillPassives:", text, flags=re.M):
        text = re.sub(r"^  skillPassives:\n(?:  - .*\n|    .*\n)*", blk + "\n", text, count=1, flags=re.M)
        text = re.sub(r"^  skillPassives: \[\]$", blk, text, count=1, flags=re.M)
    else:
        text = re.sub(r"^  isLegendary: ", blk + "\n  isLegendary: ", text, count=1, flags=re.M)
    return text


def main():
    apply = "--apply" in sys.argv
    files = sorted(f for f in os.listdir(EQ_DIR) if f.endswith(".asset"))
    print("模板总数：%d" % len(files))

    # 先扫一遍：按职业武器关键字分组，每组取编号最大的一件保底传奇武器
    allp = [parse(os.path.join(EQ_DIR, fn)) for fn in files]
    for gname in sorted(JOB_WEAPON_GROUPS):
        kws = JOB_WEAPON_GROUPS[gname]
        best = None
        for d0 in allp:
            if d0["slotType"] != ST_MAIN:
                continue
            low = d0["templateId"].lower()
            if "offhand" in low:
                continue
            # 初始武器不参与保底（它们带 overrideRarity=Common，提到传奇只会破坏开局平衡）
            if d0["templateId"] in STARTER_IDS:
                continue
            if not any(k in low for k in kws):
                continue
            m = re.search(r"(\d+)$", d0["templateId"])
            n = int(m.group(1)) if m else 0
            if best is None or n > best[0]:
                best = (n, d0["templateId"])
        if best:
            LEGEND_FLOOR.add(best[1])
            print("  %-10s 保底传奇武器 → %s（编号 %d）" % (gname, best[1], best[0]))
    stat = {}
    for fn in files:
        d = parse(os.path.join(EQ_DIR, fn))
        entries, gb, min_lv, rar, pas = build(d)
        stat.setdefault(d["slotType"], [0, 0, 0, 0, 0, 0])
        stat[d["slotType"]][0] += 1
        stat[d["slotType"]][rar] += 1
        if not apply:
            print("  %-28s slot=%d rar=%d %s -> %s%s" % (
                d["templateId"], d["slotType"], rar,
                "魔法" if d["weaponAttackType"] == 1 else "物理",
                " / ".join("%s=%s%s" % (a, num(v), "%" if p else "") for a, v, p in entries),
                ("   【被动】%s=%s%s" % (pas[0], num(pas[1]), "%" if pas[2] else "")) if pas else ""))
    print("\n槽位统计（总数 / 普通 / 优良 / 稀有 / 史诗 / 传奇）：")
    for s in sorted(stat):
        r = stat[s]
        print("  slot=%d  %3d 个   普通%3d  优良%3d  稀有%3d  史诗%3d  传奇%3d" % (s, r[0], r[1], r[2], r[3], r[4], r[5]))
    if not apply:
        print("（只出报告，未写盘。加 --apply 才写。）")
        return

    # ⚠ 绝不删旧备份：原始件已由 _backup_equips_from_git.py 从 HEAD 导出到 20261009-equip-templates-orig/，
    #   重复跑 --apply 时只需跳过备份，不做任何删除。
    if os.path.isdir(BACKUP_DIR):
        print("备份已存在，跳过（原始件见 20261009-equip-templates-orig/）：" + BACKUP_DIR)
    else:
        shutil.copytree(EQ_DIR, BACKUP_DIR)
        print("已备份 -> " + BACKUP_DIR)
    n = 0
    for fn in files:
        p = os.path.join(EQ_DIR, fn)
        d = parse(p)
        entries, gb, min_lv, rar, pas = build(d)
        out = rewrite(d["text"], entries, gb, min_lv, rar, pas)
        if out != d["text"]:
            if d["crlf"]:
                out = out.replace("\n", "\r\n")
            open(p, "wb").write(out.encode("utf-8"))
            n += 1
    print("已写盘 %d 个模板" % n)


if __name__ == "__main__":
    main()
