# -*- coding: utf-8 -*-
"""生成二期五表源 CSV（UTF-8 无 BOM）。运行：python Tools/generate_phase2_tables.py"""
import io
import os
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TABLES = os.path.join(ROOT, "Assets", "Data", "Source", "Tables")
BYTES = os.path.join(ROOT, "Assets", "Resources", "Data", "Tables")


def write_battle_quest():
    boss_objectives = [
        "\u51fb\u8d25 Boss \u68ee\u4e4b\u5b88\u62a4\u8005",
        "\u51fb\u8d25 Boss \u5893\u56ed\u5b88\u536b",
        "\u51fb\u8d25 Boss \u96e8\u6797\u5de8\u86db",
        "\u51fb\u8d25 Boss \u6d77\u5996\u87f9",
        "\u51fb\u8d25 Boss \u65f6\u4e4b\u98ce\u8f66\u7cbe\u7075",
        "\u51fb\u8d25 Boss \u6676\u77f3\u5de8\u50cf",
        "\u51fb\u8d25 Boss \u88c2\u9699\u5316\u8eab \u00b7 \u5c0f\u7f8e",
        "\u51fb\u8d25 Boss \u88c2\u7f1d\u610f\u5fd7",
    ]
    boss_gold = [200, 300, 400, 500, 600, 700, 800, 2000]
    lines = [
        "# gameChapter/stageType/isGoldDungeon: * \u4e3a\u901a\u914d\uff1bclearGold=0 \u8d70\u516c\u5f0f",
        "gameChapter,stageType,isGoldDungeon,objective,clearGold,normalBase,normalChapterAdd,eliteGoldMul,note",
    ]
    for i in range(8):
        ch = i + 1
        lines.append("{},{},0,{},{},,,,\u7b2c{}章Boss".format(
            ch, "Boss", boss_objectives[i], boss_gold[i], ch))
    lines.append("*,Normal,0,\u51fb\u8d25\u6240\u6709\u654c\u4eba,0,25,10,,\u666e\u901a\u5173\u516c\u5f0f")
    lines.append("*,Elite,0,\u51fb\u8d25\u6240\u6709\u654c\u4eba,0,25,10,1.5,\u7cbe\u82f1+50%")
    lines.append("*,*,1,\u6e05\u5272\u91d1\u5e01\u526f\u672c\u654c\u4eba,0,,,,\u91d1\u6570\u8d70GameConfig")
    path = os.path.join(TABLES, "battle_quest.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("wrote", path)


def write_stage_roller_weights():
    rows = [
        "# key-value \u5168\u5c40\u53c2\u6570",
        "key,value,note",
        "bossWindow,3,\u9996\u9886\u6700\u65e9\u5012\u6570\u7b2c\u51e0\u5173",
        "maxRestPerChapter,2,",
        "bossWeightBase,0.22,",
        "bossWeightStep,0.24,",
        "restWeightBase,0.10,",
        "restWeightPerStageIndex,0.035,",
        "restFirstChapterMultiplier,1.6,",
        "eliteWeightBase,0.15,",
        "eliteWeightPerStageIndex,0.05,",
        "normalWeightFloor,0.2,",
        "normalWeightComplement,1.0,",
    ]
    path = os.path.join(TABLES, "stage_roller_weights.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print("wrote", path)


def write_sprite_pick_weight():
    rows = [
        "# stageIndex=0\u8d77\uff1bspriteIndex=0\u8868\u793a\u8be5\u6863\u5176\u4f59\u7f16\u53f7",
        "stageIndexMin,stageIndexMax,spriteIndex,weight,formula,minWeight,note",
        "0,0,1,5,,,\u7b2c1\u5173\u5f3a\u504f1\u53f7",
        "0,0,0,1,,,\u7b2c1\u5173\u5176\u4f59",
        "1,2,1,3,,,\u7b2c2-3\u5173",
        "1,2,2,2,,,",
        "1,2,0,1,,,\u7b2c2-3\u5173\u5176\u4f59",
        "3,999,0,0,spriteIndex*0.5,1.0,\u7b2c4\u5173\u8d77",
    ]
    path = os.path.join(TABLES, "sprite_pick_weight.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print("wrote", path)


def write_wave_slot():
    rows = [
        "# \u65e0\u6570\u636e\u884c\u65f6\u5168\u8d70\u4ee3\u7801\u5947\u5076\u8fdc\u7a0b\u903b\u8f91",
        "gameChapter,stageIndex,stageType,waveIndex,slotIndex,spriteIndex,styleFilter,allowDuplicate,note",
    ]
    path = os.path.join(TABLES, "wave_slot.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print("wrote", path)


def write_chapter_branch():
    lines = [
        "# gameChapter=* \u901a\u914d\uff1bedgeKind=main|skip",
        "gameChapter,fromIndex,toIndex,edgeKind,priority,note",
    ]
    for i in range(9):
        lines.append("*,{},{},main,0,".format(i, i + 1))
    path = os.path.join(TABLES, "chapter_branch.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("wrote", path)


def write_chapter_branch_rules():
    rows = [
        "# \u968f\u673a\u8df3\u5173\u8fb9\u53c2\u6570",
        "gameChapter,branchCountMin,branchCountMax,branchPoolFrom,branchPoolTo,skipDistance,note",
        "*,1,2,1,5,2,\u968f\u673a1-2\u6761\u8df3\u5173\u8fb9",
    ]
    path = os.path.join(TABLES, "chapter_branch_rules.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print("wrote", path)


def cook_bytes():
    os.makedirs(BYTES, exist_ok=True)
    names = [
        "battle_quest", "stage_roller_weights", "sprite_pick_weight",
        "wave_slot", "chapter_branch", "chapter_branch_rules",
    ]
    for name in names:
        src = os.path.join(TABLES, name + ".csv")
        if not os.path.isfile(src):
            continue
        with io.open(src, "r", encoding="utf-8") as f:
            text = f.read()
        dst = os.path.join(BYTES, name + ".bytes")
        with io.open(dst, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print("cooked", dst)


def main():
    os.makedirs(TABLES, exist_ok=True)
    write_battle_quest()
    write_stage_roller_weights()
    write_sprite_pick_weight()
    write_wave_slot()
    write_chapter_branch()
    write_chapter_branch_rules()
    cook_bytes()


if __name__ == "__main__":
    main()
