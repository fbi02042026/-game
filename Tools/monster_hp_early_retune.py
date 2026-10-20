# -*- coding: utf-8 -*-
"""
前期小怪血量下调 30%（2026-10-20 主人要求：让前期小怪的血降 30%）。

范围：主线前期两章（monsterChapter = 1 森林 / 2 亡灵）的**非 Boss 小怪**，
      baseHp 一列 × 0.7；Boss 行（isBoss=1）与第 3 章及以后全部不动。

目的：缩短开局两章的战斗时长，降低前期流失 → 提升次留/在线时长。

用法：python Tools/monster_hp_early_retune.py          # 只出报告，不写盘
      python Tools/monster_hp_early_retune.py --apply  # 写 csv（bytes 用 sync_table_bytes.py 同步）
"""
import os
import sys
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Data", "Source", "Tables", "monster_stats.csv")
BACKUP_DIR = os.path.join(ROOT, ".workbuddy", "backup", "20261020-monster-hp-early")

# 前期 = 主线前两章（主线顺序 1→2→5→6→7→8，支线 3/4 后期才解锁）
EARLY_CHAPTERS = {1, 2}
HP_MUL = 0.7

COL_CHAPTER = 1
COL_IS_BOSS = 5
COL_HP = 7


def is_data(line):
    s = line.strip()
    return bool(s) and not s.startswith("#") and not s.startswith("id,")


def main():
    apply = "--apply" in sys.argv
    with open(SRC, "rb") as f:
        raw = f.read()
    lines = raw.decode("utf-8-sig").split("\n")

    out = []
    changed = 0
    for line in lines:
        if not is_data(line):
            out.append(line)
            continue
        eol = "\r" if line.endswith("\r") else ""
        c = line.rstrip("\r").split(",")
        ch = int(c[COL_CHAPTER])
        if c[COL_IS_BOSS].strip() == "1" or ch not in EARLY_CHAPTERS:
            out.append(line)
            continue
        old = float(c[COL_HP])
        new = round(old * HP_MUL, 1)
        fmt = ("%d" % new) if abs(new - int(new)) < 1e-6 else ("%.1f" % new)
        print("  %-12s %s  baseHp %s -> %s" % (c[0], c[3], c[COL_HP], fmt))
        c[COL_HP] = fmt
        changed += 1
        out.append(",".join(c) + eol)

    print("\n将改动 %d 行（仅第 1/2 章非 Boss 小怪，Boss 与后续章节不动）" % changed)
    if not apply:
        print("（只出报告，未写盘。加 --apply 才写。）")
        return

    os.makedirs(BACKUP_DIR, exist_ok=True)
    shutil.copy2(SRC, os.path.join(BACKUP_DIR, "monster_stats.csv"))
    print("已备份 -> " + BACKUP_DIR)

    text = "\n".join(out)
    with open(SRC, "wb") as f:
        f.write("\ufeff".encode("utf-8") + text.encode("utf-8"))
    print("已写盘：monster_stats.csv（记得跑 Tools/sync_table_bytes.py monster_stats 同步 .bytes）")


if __name__ == "__main__":
    main()
