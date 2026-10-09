# -*- coding: utf-8 -*-
"""
怪物攻击重调（2026-10-09 主人拍板：攻击可以往上提，但不要太高，因为怪多）。

背景：monster_stats.csv 的 baseAtk 是「章内基准」，最终攻击还要乘
      chapter_stat_scale(章) x stage_stat_scale(关) x MONSTER_DAMAGE_MULTIPLIER(2)。
      实测各章小怪「最终攻击」（第 1 关第 0 波）严重不单调：
        主线 1 -> 2 -> 5 -> 6 -> 7 -> 8 = 15.5 -> 9.1 -> 14.6 -> 25.6 -> 34.1 -> 45.7
        ↑ 第 1 章到第 2 章反而掉了 41%
        支线 3 / 4 = 15.9 / 27.6，比解锁它们的主线第 8 章(45.7)还弱，与
        chapter_stat_scale.csv 里「支线需通关第 8 章才开放，故给高倍率」的注释矛盾。

做法：给每个章算一个「章内统一倍率」，只乘 baseAtk / baseMagAtk 两列，
      保留怪与怪之间的相对强弱（血厚/血薄的手感不动），Boss 行不动。
      目标 = 该章小怪最终攻击（第 1 关第 0 波）落到 TARGET_EFF 指定的水位。

用法：python Tools/monster_atk_retune.py          # 只出报告，不写盘
      python Tools/monster_atk_retune.py --apply  # 写 csv + 同步 .bytes
"""
import os
import sys
import shutil

ROOT = r"Y:\PixelAdventureTown"
SRC = os.path.join(ROOT, "Assets", "Data", "Source", "Tables", "monster_stats.csv")
DST = os.path.join(ROOT, "Assets", "Resources", "Data", "Tables", "monster_stats.bytes")
BACKUP_DIR = os.path.join(ROOT, ".workbuddy", "backup", "20261009-monster-atk")

# 章节属性倍率（与 chapter_stat_scale.csv 1:1）
CHAPTER_SCALE = {1: 0.88, 2: 1.10, 3: 1.55, 4: 1.72, 5: 1.24, 6: 1.50, 7: 1.72, 8: 2.12}
MONSTER_DAMAGE_MULTIPLIER = 2.0

# 目标：各章小怪「最终攻击」（第 1 关 / 第 0 波，未计玩家防御）
# 主线 1->2->5->6->7->8 单调上升；支线 3/4 压在第 8 章之上。
TARGET_EFF = {1: 17.0, 2: 19.0, 3: 50.0, 4: 54.0, 5: 24.0, 6: 29.0, 7: 35.0, 8: 46.0}

COL_CHAPTER = 1
COL_IS_BOSS = 5
COL_ATK = 8
COL_MAG_ATK = 15


def load_rows():
    with open(SRC, "rb") as f:
        raw = f.read()
    text = raw.decode("utf-8-sig")
    lines = text.split("\n")
    return raw, lines


def is_data(line):
    s = line.strip()
    return bool(s) and not s.startswith("#") and not s.startswith("id,")


def chapter_avg_atk(lines):
    acc = {}
    for line in lines:
        if not is_data(line):
            continue
        c = line.rstrip("\r").split(",")
        ch = int(c[COL_CHAPTER])
        if c[COL_IS_BOSS].strip() == "1":
            continue
        acc.setdefault(ch, []).append(float(c[COL_ATK]))
    return {k: sum(v) / len(v) for k, v in acc.items()}


def main():
    apply = "--apply" in sys.argv
    raw, lines = load_rows()
    avg = chapter_avg_atk(lines)

    mul = {}
    print("章节  现基准均值  现最终攻击  目标最终攻击  倍率")
    for ch in sorted(avg):
        cur_eff = avg[ch] * CHAPTER_SCALE[ch] * MONSTER_DAMAGE_MULTIPLIER
        m = TARGET_EFF[ch] / cur_eff
        mul[ch] = m
        print("  %d   %7.2f   %9.1f   %11.1f   %.3f" % (ch, avg[ch], cur_eff, TARGET_EFF[ch], m))

    out = []
    changed = 0
    for line in lines:
        if not is_data(line):
            out.append(line)
            continue
        eol = "\r" if line.endswith("\r") else ""
        c = line.rstrip("\r").split(",")
        ch = int(c[COL_CHAPTER])
        if c[COL_IS_BOSS].strip() == "1":
            out.append(line)
            continue
        old = float(c[COL_ATK])
        new = round(old * mul[ch], 1)
        fmt = ("%d" % new) if abs(new - int(new)) < 1e-6 else ("%.1f" % new)
        c[COL_ATK] = fmt
        if len(c) > COL_MAG_ATK and c[COL_MAG_ATK].strip() != "":
            c[COL_MAG_ATK] = fmt
        changed += 1
        out.append(",".join(c) + eol)

    print("\n将改动 %d 行（Boss 行不动）" % changed)
    if not apply:
        print("（只出报告，未写盘。加 --apply 才写。）")
        return

    os.makedirs(BACKUP_DIR, exist_ok=True)
    shutil.copy2(SRC, os.path.join(BACKUP_DIR, "monster_stats.csv"))
    shutil.copy2(DST, os.path.join(BACKUP_DIR, "monster_stats.bytes"))
    print("已备份 -> " + BACKUP_DIR)

    text = "\n".join(out)
    with open(SRC, "wb") as f:
        f.write("\ufeff".encode("utf-8") + text.encode("utf-8"))
    with open(DST, "wb") as f:
        f.write(text.encode("utf-8"))
    print("已写盘：csv + bytes 同步完成")


if __name__ == "__main__":
    main()
