# -*- coding: utf-8 -*-
"""
怪物数值重建【候选稿】（只输出到 Tools/*.csv 预览，**不写入正式表**）

原则：保持每章的**平均强度不变**（玩家感受到的"第 N 章有多难"不动），
      只重排章内结构，解决两个病：
        ① 同章内 1~4 号 与 5~10 号 是两套互不相干的基数（章内落差最大 7.5 倍）
        ② 近战 / 远程的血量关系是**随机**的，出现近战比远程脆 2~3 倍的实例

规则（只要改这里就能整体调手感的档位）：
  tierMul（出场层级）   idx 1~3 = 1.15（章主力，最先出场）
                        idx 5~10 = 0.95（第 3 关起补位）
                        idx 4     = 2.20（第 10 关才露面的章末精锐）
  roleMul（战斗定位）   血量：Melee 1.30 / Bow 0.90 / Ranged 0.80
                        攻击：Melee 1.00 / Bow 1.05 / Ranged 1.15
  每章按各自"本次权重的加权和"归一化 → 该章平均值与现状**完全相等**（强度不漂移）
"""
import csv, io, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Data", "Source", "Tables")
OUT = os.path.join(ROOT, "Tools", "monster_balance_proposal.csv")

HP_TIER = {1: 1.15, 2: 1.15, 3: 1.15, 4: 2.20}
HP_ROLE = {"Melee": 1.30, "Bow": 0.90, "Ranged": 0.80}
ATK_TIER = {1: 1.05, 2: 1.05, 3: 1.05, 4: 1.30}
ATK_ROLE = {"Melee": 1.00, "Bow": 1.05, "Ranged": 1.15}


def tier(idx):
    """1~3 = 章主力；4 = 章末精锐；5~10 = 补位怪"""
    if idx <= 3:
        return idx
    if idx == 4:
        return 4
    return 5


HP_TIER[5] = 0.95
ATK_TIER[5] = 0.95


def load(path):
    rows = []
    with io.open(path, encoding="utf-8-sig") as f:
        for line in f:
            if line.lstrip().startswith("#"):
                continue
            line = line.strip()
            if line:
                rows.append(next(csv.reader([line])))
    return rows[0], [r for r in rows[1:] if len(r) >= 3]


_, ms = load(os.path.join(SRC, "monster_stats.csv"))
_, st = load(os.path.join(SRC, "monster_attack_style.csv"))
style_map = {(int(r[0]), int(r[1])): r[2].strip() for r in st}

mons = []
for r in ms:
    ch, idx = int(r[1]), int(r[2])
    mons.append(dict(id=r[0], raw=r, ch=ch, idx=idx, name=r[3], minWave=int(r[4]),
                     boss=r[5] == "1", hp=float(r[7]), atk=float(r[8]),
                     style=style_map.get((ch, idx), "Melee")))

norm = [m for m in mons if not m["boss"]]

print("=" * 88)
print("重建前后对照（base 值，未乘章节倍率；每章平均值保持不变）")
print("=" * 88)
print("%-5s %-10s %-10s %-10s %-10s %-8s" %
      ("章", "现状均值血", "新均值血", "现状均值攻", "新均值攻", "章内血落差(现→新)"))
new_hp, new_atk = {}, {}
for ch in range(1, 9):
    rows = [m for m in norm if m["ch"] == ch]
    mean_hp = sum(m["hp"] for m in rows) / len(rows)
    mean_atk = sum(m["atk"] for m in rows) / len(rows)

    w = [HP_TIER[tier(m["idx"])] * HP_ROLE.get(m["style"], 1.0) for m in rows]
    wa = [ATK_TIER[tier(m["idx"])] * ATK_ROLE.get(m["style"], 1.0) for m in rows]
    s, sa = sum(w) / len(w), sum(wa) / len(wa)
    for m, wi, wai in zip(rows, w, wa):
        new_hp[m["id"]] = round(mean_hp * wi / s, 1)
        new_atk[m["id"]] = round(mean_atk * wai / sa, 1)

    old = max(m["hp"] for m in rows) / min(m["hp"] for m in rows)
    nw = max(new_hp[m["id"]] for m in rows) / min(new_hp[m["id"]] for m in rows)
    print("%-5d %-10.1f %-10.1f %-10.1f %-10.1f %8s" %
          (ch, mean_hp, sum(new_hp[m["id"]] for m in rows) / len(rows),
           mean_atk, sum(new_atk[m["id"]] for m in rows) / len(rows),
           "%.1fx -> %.1fx" % (old, nw)))

print()
print("=" * 88)
print("近战 vs 远程重建后：**同层级内**最脆近战 / 最肉远程")
print("（精灵 4 是第 10 关才出的章末精锐，单独一档，不参与同层比较）")
print("=" * 88)
print("%-5s %-14s %-10s %-14s %-10s %s" % ("章", "最脆近战", "新HP", "最肉远程", "新HP", "近/远"))
for ch in range(1, 9):
    rows = [m for m in norm if m["ch"] == ch and m["idx"] != 4]
    ml = min((m for m in rows if m["style"] == "Melee"), key=lambda x: new_hp[x["id"]])
    ra = max((m for m in rows if m["style"] != "Melee"), key=lambda x: new_hp[x["id"]])
    print("%-5d %-14s %-10.1f %-14s %-10.1f %.2fx" %
          (ch, ml["id"], new_hp[ml["id"]], ra["id"], new_hp[ra["id"]],
           new_hp[ml["id"]] / new_hp[ra["id"]]))

print()
print("=" * 88)
print("同一关能同刷出现的怪之间的血量落差（精灵 1~3 + 5~10）")
print("=" * 88)
print("%-5s %-22s %-22s" % ("章", "现状 最高/最低", "重建后 最高/最低"))
for ch in range(1, 9):
    rows = [m for m in norm if m["ch"] == ch and m["idx"] != 4]
    o_max = max(m["hp"] for m in rows) / min(m["hp"] for m in rows)
    n_max = max(new_hp[m["id"]] for m in rows) / min(new_hp[m["id"]] for m in rows)
    print("%-5d %-22s %-22s" % (ch, "%.2fx" % o_max, "%.2fx" % n_max))

print()
print("=" * 88)
print("样例（第 6 章洞穴，base 值）")
print("=" * 88)
print("%-12s %-6s %-8s %-10s %-10s %-10s %-10s" %
      ("id", "精灵", "方式", "旧血", "新血", "旧攻", "新攻"))
for m in [m for m in norm if m["ch"] == 6]:
    print("%-12s %-6d %-8s %-10.1f %-10.1f %-10.1f %-10.1f" %
          (m["id"], m["idx"], m["style"], m["hp"], new_hp[m["id"]],
           m["atk"], new_atk[m["id"]]))

# 目标是将来可能直接替换 Assets/Data/Source/Tables/monster_stats.csv，
# 该表工程内统一 CRLF（core.autocrlf=true），这里跟着写 CRLF，避免将来 diff 出现换行噪音。
# head = 表头（load 返回的 body 是**数据行**，别再误当成全表）
head, _ = load(os.path.join(SRC, "monster_stats.csv"))
COMMENTS = [
    "# monster_stats：怪物数值与出场（monsterChapter=素材章 1~8）",
]
with io.open(OUT, "w", encoding="utf-8-sig", newline="") as f:
    for c in COMMENTS:
        f.write(c + "\r\n")
    f.write(",".join(head) + "\r\n")
    for m in mons:
        r = list(m["raw"])
        if not m["boss"]:
            r[7] = "%.1f" % new_hp[m["id"]]
            r[8] = "%.1f" % new_atk[m["id"]]
        f.write(",".join(r) + "\r\n")
print("\n候选全表已写出（未生效）：" + OUT)
