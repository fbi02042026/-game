# -*- coding: utf-8 -*-
"""
战力换算体检（只读分析，不改任何游戏数据）
复刻：PowerCalc.cs / RunLoadout.TotalPower / AttrSystem.ApplyDerivedAttributes
输出：每个装备词条的战力当量 + 各部位属性池的「战力=0」权重占比
运行：python Tools/power_audit.py
"""
import csv, io, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
T = os.path.join(ROOT, "Assets", "Data", "Source", "Tables")

# ===== PowerCalc.cs 常量（唯一真源，改动必须同步那里）=====
POWER_PER_DPS = 4.42267
POWER_PER_HP = 0.15
POWER_PER_DEF = 2.5
POWER_BASE = 130.50
BASE_STR = 5
BASE_INT = 5


def rows(fn):
    raw = open(os.path.join(T, fn), "rb").read().decode("utf-8-sig")
    return [r for r in csv.reader(io.StringIO(raw)) if r and not r[0].startswith("#")]


def load_jobs():
    out = {}
    for r in rows("player_job_base_stats.csv")[1:]:
        if len(r) < 15:
            continue
        out[r[0]] = dict(
            name=r[1], hp=float(r[2]), atk=float(r[3]), df=float(r[4]),
            cr=float(r[6].rstrip("%")) / 100.0, interval=float(r[9]),
            mdf=float(r[16]), magic=(r[15].strip() == "magic"))
    return out


def load_pools():
    out = {}
    for r in rows("equip_slot_pools.csv")[1:]:
        if len(r) < 7:
            continue
        out[r[0]] = dict(name=r[1], main=r[2], rand=r[4])
    return out


def load_ranges():
    out = {}
    for r in rows("equip_attr_ranges.csv")[1:]:
        if len(r) < 9:
            continue
        # 普通/稀有/传奇 三档取中位，单位统一成数值
        def mid(a, b):
            def num(s):
                s = s.strip()
                for suf in ("%/秒", "%", "秒", "点", "身位"):
                    if s.endswith(suf):
                        s = s[:-len(suf)]
                        break
                return float(s)
            return (num(a) + num(b)) / 2.0
        pct = ("%" in r[2]) if len(r) > 2 else False
        pct = ("%" in r[9]) or ("%" in r[3]) if len(r) > 9 else pct
        out[r[0]] = dict(name=r[1], c=mid(r[3], r[4]), r=mid(r[5], r[6]), l=mid(r[7], r[8]), pct=pct)
    return out


def parse_pool(s):
    out = []
    for part in s.split("|"):
        part = part.strip()
        if not part or ":" not in part:
            continue
        k, v = part.rsplit(":", 1)
        out.append((k.strip(), int(v)))
    return out


CN2ID = {"生命": "HP", "攻击": "ATK", "防御": "DEF", "移速": "MS", "减伤": "DMG_RED",
         "暴击率": "CRIT_RATE", "暴击伤害": "CRIT_DMG", "攻速": "ATK_SPD", "射程": "RANGE",
         "元素伤害": "ELE_DMG", "闪避": "DODGE", "生命偷取": "LIFE_STEAL", "魔攻": "MAGIC_ATK",
         "魔防": "MAGIC_DEF", "中毒": "POISON"}


def per_point(attr, job, A):
    """1 点/1% 该属性折多少战力（按指定职业与当前攻击 A）。返回 (当量, 说明)"""
    S = 1.0 / job["interval"]
    cr = job["cr"]
    atk = job["atk"] if not job["magic"] else 1.0  # 占位，见下
    if attr == "ATK":
        if job["magic"]:
            return 0.0, "法系：物理攻击不进 DPS"
        return POWER_PER_DPS * (1 + cr) * S, "ΔA×k×(1+Cr)×S"
    if attr == "MAGIC_ATK":
        if not job["magic"]:
            return 0.0, "物理系：魔攻不进 DPS"
        return POWER_PER_DPS * (1 + cr) * S, "ΔA×k×(1+Cr)×S"
    if attr == "HP":
        return POWER_PER_HP, "k_hp"
    if attr == "DEF":
        return POWER_PER_DEF, "k_def"
    if attr == "MAGIC_DEF":
        return POWER_PER_DEF, "k_def"
    if attr == "CRIT_RATE":      # 每 1 个百分点
        return POWER_PER_DPS * A * S / 100.0, "k×A×S×ΔCr"
    if attr == "ATK_SPD":        # 每 1 个百分点（乘算）
        return POWER_PER_DPS * A * (1 + cr) * S / 100.0, "k×A×(1+Cr)×S×Δ%"
    if attr in ("DMG_RED", "DODGE"):
        # 2026-10-26：折等效防御（受击期望乘区 EHP×1/(1-r) 的一阶等效），与 PlayerPowerSegment ③同式
        df = job["df"] + job["mdf"]
        return POWER_PER_DEF * (df + 100) ** 2 / 100 * 0.01, "等效防御 (def+100)²/100×1%"
    if attr == "LIFE_STEAL":
        # 等效生命 = DPS×ratio×T(T=45s假设)，与 PlayerPowerSegment ④同式；A=40 参考
        S = 1.0 / job["interval"]
        return POWER_PER_HP * 40.0 * (1 + cr) * S * 45 * 0.01, "等效生命 dps×45s×1%"
    if attr == "CRIT_DMG":
        # 期望乘区 1+Cr×(暴伤-1) 对暴伤的导数 = Cr，A=40 参考
        S = 1.0 / job["interval"]
        return POWER_PER_DPS * 40.0 * S * job["cr"] * 0.01, "期望dps ×cr×1%"
    if attr == "ELE_DMG":
        # 火/冰附加 = 最终伤害×(点值×0.01)，取大系；A=40 参考
        S = 1.0 / job["interval"]
        return POWER_PER_DPS * 40.0 * (1 + cr) * S * 0.01, "最终伤害 ×1%/点"
    if attr in ("STRENGTH", "INTELLIGENCE"):
        # 2026-10-26 起玩家侧补了对称派生：力量成长段×2→Attack（AttrSystem.cs）、智力成长段×2→MagicAttack。
        # 装备词条全是成长段 → 全量 ×2。非本职业向的词条按职业白名单根本不会掉，记 0。
        if attr == "STRENGTH" and not job["magic"]:
            return POWER_PER_DPS * 2.0 * (1 + cr) * S, "成长段×2折 Attack"
        if attr == "INTELLIGENCE" and job["magic"]:
            return POWER_PER_DPS * 2.0 * (1 + cr) * S, "成长段×2折 MagicAttack"
        return 0.0, "非本职业向（不掉该词条）"
    if attr == "VITALITY":
        return 10.0 * POWER_PER_HP + 1.0 * POWER_PER_DEF, "派生 +10血 +1防"
    return 0.0, "战力口径未计"


def main():
    jobs = load_jobs()
    pools = load_pools()
    rng = load_ranges()

    print("=" * 118)
    print("A. 六职业开局战力（纯职业表，**不含初始武器** → 比 Docs 4.2 表含武器的数低一截，仅验公式形状）")
    print("=" * 118)
    print(f"{'职业':<10}{'攻击':>7}{'每秒':>7}{'暴击':>7}{'DPS':>9}{'旧表DPS':>10}{'战力(零装)':>12}")
    docDps = {"P001": 12.180, "P002": 19.163, "P003": 12.238, "P004": 10.771, "P005": 15.361, "P006": 10.118}
    for k, j in jobs.items():
        S = 1.0 / j["interval"]
        dps0 = j["atk"] * (1 + j["cr"]) * S
        pw = POWER_BASE + POWER_PER_DPS * dps0 + POWER_PER_HP * j["hp"] + POWER_PER_DEF * (j["df"] + j["mdf"])
        print(f"{j['name']:<10}{j['atk']:>7.1f}{S:>7.2f}{j['cr']*100:>6.1f}%{dps0:>9.2f}{docDps[k]:>10.3f}{round(pw):>12}")

    print()
    print("=" * 118)
    print("B. 每 1 点（1%）词条折多少战力 —— A=当前攻击（含武器/装备），取 40 为中前期参考")
    print("=" * 118)
    hdr = f"{'词条':<12}" + "".join(f"{j['name']:>11}" for j in jobs.values())
    print(hdr)
    order = ["ATK", "MAGIC_ATK", "CRIT_RATE", "ATK_SPD", "HP", "DEF", "MAGIC_DEF", "STRENGTH", "INTELLIGENCE", "VITALITY"]
    for a in order:
        line = f"{rng[a]['name'] if a in rng else a:<12}"
        for j in jobs.values():
            v, _ = per_point(a, j, 40.0)
            line += f"{v:>11.3f}"
        print(line)
    print()
    print("  注：CRIT_RATE / ATK_SPD 当量随 A 线性放大（A=100 时翻 2.5 倍）；ATK/HP/DEF 与 A 无关。")
    print("  STRENGTH=0 是本次截图报障的根因；INTELLIGENCE 只有法系非 0（2026-10-25 补的那一段）。")

    print()
    print("=" * 118)
    print("C. 各部位属性池里「战力当量 = 0」的权重占比（按 equip_slot_pools.csv 权重，已按职业白名单过滤）")
    print("=" * 118)
    PHYS = {"ATK", "STRENGTH"}
    MAGIC = {"MAGIC_ATK", "MAGIC_DEF", "INTELLIGENCE"}
    for view, jobKey in (("物理系(剑盾视角)", "P001"), ("法系(法师视角)", "P005")):
        j = jobs[jobKey]
        print(f"\n  【{view}】")
        print(f"  {'部位':<12}{'池':<7}{'可见权重':>9}{'0战力':>8}{'占比':>7}   0战力词条")
        for slot, p in pools.items():
            for kind in ("main", "rand"):
                items = []
                for cn, w in parse_pool(p[kind]):
                    aid = CN2ID.get(cn, cn)
                    if aid in PHYS and jobKey == "P005":
                        continue          # 法系掉不到物理词条
                    if aid in MAGIC and jobKey != "P005":
                        continue          # 物理系掉不到法系词条
                    items.append((cn, aid, w))
                tot = sum(w for _, _, w in items)
                if tot == 0:
                    continue
                zero, zlist = 0, []
                for cn, aid, w in items:
                    v, _ = per_point(aid, j, 40.0)
                    if v == 0.0:
                        zero += w
                        zlist.append(cn)
                print(f"  {p['name']:<12}{kind:<7}{tot:>9}{zero:>8}{zero/tot*100:>6.0f}%   {'、'.join(zlist) or '—'}")

    print()
    print("=" * 118)
    print("D. 同传奇档位、不同词条的战力价值（跨词条对比，暴露「同档位不同价」）")
    print("=" * 118)
    A = 40.0
    for jobKey in ("P001", "P002"):
        j = jobs[jobKey]
        print(f"  【{j['name']}，A={A}】")
        for aid in ["ATK", "CRIT_RATE", "ATK_SPD", "HP", "DEF", "STRENGTH", "INTELLIGENCE", "MS", "ELE_DMG", "CRIT_DMG", "DMG_RED", "DODGE", "LIFE_STEAL", "RANGE"]:
            if aid not in rng:
                continue
            r = rng[aid]
            v, why = per_point(aid, j, A)
            print(f"    {r['name']:<8}传奇 {r['l']:>6.2f}{'%' if r['pct'] else '点':<2} → {v*r['l']:>8.1f} 战力   （{why}）")
        print()


    print()
    print("=" * 118)
    print("E. 实战样例：普通★1 一条词条，弹窗「战力 +N」会显示多少（普通档中位）")
    print("=" * 118)
    samples = [("STRENGTH", "力量"), ("ATK", "攻击"), ("DEF", "防御"), ("HP", "生命"),
               ("CRIT_RATE", "暴击率"), ("ATK_SPD", "攻速"), ("ELE_DMG", "元素伤害"),
               ("DMG_RED", "减伤"), ("CRIT_DMG", "暴击伤害")]
    for jobKey in ("P001", "P002", "P005"):
        j = jobs[jobKey]
        A = 40.0
        print(f"\n  【{j['name']}，A={A}】")
        for aid, cn in samples:
            r = rng[aid]
            v, _ = per_point(aid, j, A)
            raw = r["c"]
            pw = v * raw
            print(f"    {cn:<6}{raw:>7.2f}{'%' if r['pct'] else '点':<2} → 战力 +{pw:>7.1f}")
        # 力量若补上「成长段 ×2 折 Attack」的对称派生
        v_atk, _ = per_point("ATK", j, A)
        print(f"    {'（2026-10-26 已落地：力量=攻击×2 当量）':<30}力量 5.00点 → 攻击 +10 → 战力 +{v_atk*10:>6.1f}")

    print()
    print("=" * 118)
    print("F. 敏感性：A（当前攻击）变化时，百分比类词条的战力当量")
    print("=" * 118)
    j = jobs["P001"]
    print(f"  {'A':>6}" + "".join(f"{x:>14}" for x in ("暴击率+9.5%", "攻速+16%", "攻击+36")))
    for A in (20, 40, 80, 160, 320):
        cr, _ = per_point("CRIT_RATE", j, A)
        sp, _ = per_point("ATK_SPD", j, A)
        at, _ = per_point("ATK", j, A)
        print(f"  {A:>6}" + f"{cr*9.5:>14.1f}" + f"{sp*16:>14.1f}" + f"{at*36:>14.1f}")
    print("  → 攻击词条恒定；暴击/攻速随 A 线性膨胀。A 到 160 时，一件传奇手套的攻速词条 = 两件传奇武器主属性。")


if __name__ == "__main__":
    main()
