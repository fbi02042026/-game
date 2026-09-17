# -*- coding: utf-8 -*-
"""
怪物数值体检（只读审计，不改任何文件）

把 monster_stats.csv + monster_attack_style.csv + chapter_stat_scale.csv
按 Monster.Init / UnitBase 里的**真实公式**还原成"进战斗后的实际数值"，
用来查：近战 vs 远程、章内层级、跨章曲线、精英兜底值冲突。

公式来源（2026-09-17 核对）：
  Monster.cs:673-680
    waveMul = 1 + stageIdx * 0.05
    scale   = chapterScale * guildScale(1+0.02*lv) * diffScale
    普通 MaxHp = baseHp * scale * waveMul * MONSTER_HP_GLOBAL_MUL(1.0)
    普通 Atk   = baseAtk * scale * waveMul * MONSTER_DAMAGE_MULTIPLIER(1.0)
    精英 MaxHp = 105 * (tank1.1 / glass0.75) * (chapterScale * ELITE_TTK_HP_MUL1.15) * waveMul
    精英 Atk   = 30   * (tank0.92 / glass1.35) * scale * waveMul
    Boss  MaxHp= max(表值, 800) * (chapterScale * BOSS_TTK_HP_MUL1.35) * waveMul
"""
import csv, io, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Data", "Source", "Tables")

MUL_HP = 1.0
MUL_ATK = 1.0
ELITE_HP = 105.0
ELITE_ATK = 30.0
ELITE_TTK = 1.15
BOSS_TTK = 1.35
TANK_HP, TANK_ATK = 1.1, 0.92
GLASS_HP, GLASS_ATK = 0.75, 1.35
WAVE_GROWTH = 0.05


def load(path):
    rows = []
    with io.open(path, encoding="utf-8-sig") as f:
        for line in f:
            if line.lstrip().startswith("#"):
                continue
            line = line.strip()
            if line:
                rows.append(next(csv.reader([line])))
    head, body = rows[0], [r for r in rows[1:] if len(r) >= 3]
    return head, body


_, ms = load(os.path.join(SRC, "monster_stats.csv"))
_, st = load(os.path.join(SRC, "monster_attack_style.csv"))
_, cs = load(os.path.join(SRC, "chapter_stat_scale.csv"))

chapter_scale = {int(r[0]): float(r[1]) for r in cs}
style_map = {}
for r in st:
    style_map[(int(r[0]), int(r[1]))] = r[2].strip()


def is_ranged(s):
    return s in ("Ranged", "Bow", "Orb", "Magic")


mons = []
for r in ms:
    ch, idx = int(r[1]), int(r[2])
    mons.append(dict(
        id=r[0], ch=ch, idx=idx, name=r[3], minWave=int(r[4]),
        boss=r[5] == "1", hp=float(r[7]), atk=float(r[8]),
        style=style_map.get((ch, idx), "?"), scale=float(r[14]),
    ))


def eff(m, stage, elite=None):
    """返回该怪在指定关卡的实际 HP / ATK"""
    cs_ = chapter_scale[m["ch"]]
    wave = 1 + WAVE_GROWTH * stage
    sc = cs_ * wave
    if m["boss"]:
        hp = max(m["hp"], 800.0) * (cs_ * BOSS_TTK) * wave
        atk = max(m["atk"], 55.0) * sc * wave
    elif elite == "tank":
        hp = ELITE_HP * TANK_HP * (cs_ * ELITE_TTK) * wave
        atk = ELITE_ATK * TANK_ATK * sc * wave
    elif elite == "glass":
        hp = ELITE_HP * GLASS_HP * (cs_ * ELITE_TTK) * wave
        atk = ELITE_ATK * GLASS_ATK * sc * wave
    else:
        hp = m["hp"] * sc * wave * MUL_HP
        atk = m["atk"] * sc * wave * MUL_ATK
    return hp, atk


print("=" * 96)
print("一、章表 × 攻击方式：进战斗实际血量（第 1 关，stageIdx=0）")
print("=" * 96)
for ch in range(1, 9):
    rows = [m for m in mons if m["ch"] == ch and not m["boss"]]
    print("\n【第%d章】章节倍率 %.1f" % (ch, chapter_scale[ch]))
    print("  %-12s %-6s %-8s %-9s %-9s %-7s %s" %
          ("id", "精灵", "方式", "实战HP", "实战ATK", "射程", "namespace"))
    for m in rows:
        hp, atk = eff(m, 0)
        print("  %-12s %-6s %-8s %-9.1f %-9.1f %s" %
              (m["id"], m["idx"], m["style"], hp, atk,
               "远程" if is_ranged(m["style"]) else "近战"))
    me = [eff(m, 0)[0] for m in rows if not is_ranged(m["style"])]
    ra = [eff(m, 0)[0] for m in rows if is_ranged(m["style"])]
    if me and ra:
        print("  >> 近战均值 %.1f  远程均值 %.1f  近/远 = %.2f" %
              (sum(me) / len(me), sum(ra) / len(ra),
               (sum(me) / len(me)) / (sum(ra) / len(ra))))

print()
print("=" * 96)
print("二、同章内「谁最脆」——近战最低 vs 远程最高（第 1 关）")
print("=" * 96)
print("  %-6s %-14s %-14s %-14s %-14s %s" %
      ("章", "最脆近战", "HP", "最肉远程", "HP", "结论"))
for ch in range(1, 9):
    rows = [m for m in mons if m["ch"] == ch and not m["boss"]]
    ml = min((m for m in rows if not is_ranged(m["style"])), key=lambda x: eff(x, 0)[0])
    rh = max((m for m in rows if is_ranged(m["style"])), key=lambda x: eff(x, 0)[0])
    bad = eff(ml, 0)[0] < eff(rh, 0)[0]
    print("  %-6d %-14s %-14.1f %-14s %-14.1f %s" %
          (ch, ml["id"], eff(ml, 0)[0], rh["id"], eff(rh, 0)[0],
           "* 近战比远程还脆 *" if bad else ""))

print()
print("=" * 96)
print("三、跨章曲线（每章 6 只普通怪，第 5 关 stageIdx=4）")
print("=" * 96)
print("  %-4s %-12s %-12s %-12s %-12s" % ("章", "近战血均值", "远程血均值", "近战攻均值", "远程攻均值"))
for ch in range(1, 9):
    rows = [m for m in mons if m["ch"] == ch and not m["boss"]]
    def avg(lst, key):
        v = [eff(m, 4)[key] for m in lst]
        return sum(v) / len(v) if v else 0
    ml = [m for m in rows if not is_ranged(m["style"])]
    ra = [m for m in rows if is_ranged(m["style"])]
    print("  %-4d %-12.1f %-12.1f %-12.1f %-12.1f" %
          (ch, avg(ml, 0), avg(ra, 0), avg(ml, 1), avg(ra, 1)))

print()
print("=" * 96)
print("四、精英兜底值 MONSTER_ELITE_HP=105 与本章普通怪的冲突")
print("=" * 96)
print("  %-4s %-12s %-12s %-12s %s" % ("章", "精英血(坦克)", "精英血(玻璃)", "普通怪血上限", "问题"))
for ch in range(1, 9):
    rows = [m for m in mons if m["ch"] == ch and not m["boss"]]
    top = max(rows, key=lambda x: eff(x, 4)[0])
    fake = dict(id="ELITE", ch=ch, idx=0, name="", minWave=0, boss=False,
                hp=ELITE_HP, atk=ELITE_ATK, style="Melee", scale=1.0)
    tk, gl = eff(fake, 4, "tank")[0], eff(fake, 4, "glass")[0]
    topv = eff(top, 4)[0]
    flag = ""
    if tk < topv * 0.9:
        flag = "* 精英不如%s（%.1f）*" % (top["id"], topv)
    print("  %-4d %-12.1f %-12.1f %-12.1f %s" % (ch, tk, gl, topv, flag))

print()
print("=" * 96)
print("五、不同的玩家攻击下，普通怪需要几刀（HP/(ATK-DEF)，DEF=2）")
print("=" * 96)
# 参考装备曲线（说明性假设）：职业基础攻 15 + 武器 ATK 按章递增
ref = {1: 25, 2: 38, 3: 50, 4: 62, 5: 75, 6: 92, 7: 112, 8: 135}
print("  参考玩家攻击：" + "  ".join("ch%d=%d" % (c, a) for c, a in sorted(ref.items())))
print()
print("  %-4s %-12s %-8s %-10s %-10s %s" % ("章", "精灵", "方式", "实战HP", "参考ATK", "刀数"))
for ch in range(1, 9):
    rows = [m for m in mons if m["ch"] == ch and not m["boss"]]
    a = ref[ch]
    line = []
    for m in rows:
        hp = eff(m, 4)[0]
        line.append("%s(%s) %.0f/%d=%.1f刀" %
                    (m["id"], "远" if is_ranged(m["style"]) else "近", hp, a, hp / max(1, a - 2)))
    print("  第%d章: %s" % (ch, " | ".join(line)))
