# -*- coding: utf-8 -*-
"""生成关卡怪物五表源 CSV（UTF-8 无 BOM）。运行：python Tools/generate_stage_monster_tables.py"""
import io
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TABLES = os.path.join(ROOT, "Assets", "Data", "Source", "Tables")
MONSTERS_DIR = os.path.join(ROOT, "Assets", "Resources", "Config", "Monsters")

THEMES = {
    1: "undead", 2: "jungle", 3: "sea", 4: "forest",
    5: "field", 6: "cave", 7: "devil", 8: "ice",
}

ATTACK_NOTES = {}


def load_attack_notes():
    path = os.path.join(TABLES, "monster_attack_style.csv")
    if not os.path.isfile(path):
        return
    with io.open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or line.startswith("monsterChapter"):
                continue
            parts = line.split(",")
            if len(parts) < 4:
                continue
            ch, idx, note = int(parts[0]), int(parts[1]), parts[3]
            ATTACK_NOTES[(ch, idx)] = note


def parse_monster_yaml(path):
    with io.open(path, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
    data = {}
    for key in (
        "id", "monsterName", "minWave", "isBoss", "unlockClearCount",
        "baseHp", "baseAttack", "baseAttackSpeed", "attackRange",
        "baseMoveSpeed", "baseGoldDrop", "expDrop", "spriteIndex", "spriteScale",
    ):
        m = re.search(r"^\s*" + key + r":\s*(.+)$", text, re.M)
        if not m:
            continue
        val = m.group(1).strip().strip('"')
        if key in ("minWave", "unlockClearCount", "baseGoldDrop", "expDrop", "spriteIndex"):
            data[key] = int(float(val)) if val else 0
        elif key in ("isBoss",):
            data[key] = 1 if val in ("1", "true", "True") else 0
        elif key in ("baseHp", "baseAttack", "baseAttackSpeed", "attackRange", "baseMoveSpeed", "spriteScale"):
            data[key] = float(val) if val else 0.0
        else:
            data[key] = val
    return data


def load_existing_assets():
    out = {}
    if not os.path.isdir(MONSTERS_DIR):
        return out
    for name in os.listdir(MONSTERS_DIR):
        if not name.endswith(".asset"):
            continue
        d = parse_monster_yaml(os.path.join(MONSTERS_DIR, name))
        if d.get("id"):
            out[d["id"]] = d
    return out


def default_unlock(sprite_index, is_boss):
    if is_boss or sprite_index >= 11:
        return 0
    if sprite_index <= 5:
        return 0
    if sprite_index <= 8:
        return 2
    return 4


def default_min_wave(sprite_index, is_boss):
    if is_boss:
        return 9
    if sprite_index <= 2:
        return 0
    if sprite_index <= 4:
        return 1
    return 2


def default_stats(ch, idx, is_boss):
    if is_boss:
        return dict(
            baseHp=3000.0, baseAttack=45.0, baseAttackSpeed=1.0,
            attackRange=4.0, baseMoveSpeed=1.8, baseGoldDrop=200, expDrop=100,
        )
    scale = 1.0 + (ch - 1) * 0.12
    return dict(
        baseHp=round(50 * scale + idx * 2, 1),
        baseAttack=round(5 * scale + idx * 0.5, 1),
        baseAttackSpeed=1.5,
        attackRange=1.5,
        baseMoveSpeed=2.2,
        baseGoldDrop=10 + ch,
        expDrop=5 + ch,
    )


def make_id(ch, idx):
    return "%s_%d%02d" % (THEMES[ch], ch, idx)


def write_monster_stats():
    load_attack_notes()
    assets = load_existing_assets()
    lines = [
        "# monster_stats：怪物数值与出场（monsterChapter=素材章 1~8）",
        "id,monsterChapter,spriteIndex,name,minWave,isBoss,unlockClearCount,baseHp,baseAtk,baseAtkSpeed,attackRange,moveSpeed,baseGold,exp,spriteScale,note",
    ]
    for ch in range(1, 9):
        for idx in range(1, 13):
            mid = make_id(ch, idx)
            is_boss = 1 if idx >= 11 else 0
            row = assets.get(mid)
            note = ATTACK_NOTES.get((ch, idx), "")
            name = note.replace("-", " ") if note else mid
            if row:
                name = row.get("monsterName") or name
                if isinstance(name, bytes):
                    name = name.decode("utf-8", errors="replace")
            stats = default_stats(ch, idx, is_boss)
            if row:
                stats["baseHp"] = row.get("baseHp", stats["baseHp"])
                stats["baseAttack"] = row.get("baseAttack", stats["baseAttack"])
                stats["baseAttackSpeed"] = row.get("baseAttackSpeed", stats["baseAttackSpeed"])
                stats["attackRange"] = row.get("attackRange", stats["attackRange"])
                stats["baseMoveSpeed"] = row.get("baseMoveSpeed", stats["baseMoveSpeed"])
                stats["baseGoldDrop"] = row.get("baseGoldDrop", stats["baseGoldDrop"])
                stats["expDrop"] = row.get("expDrop", stats["expDrop"])
            min_wave = row.get("minWave", default_min_wave(idx, is_boss)) if row else default_min_wave(idx, is_boss)
            unlock = row.get("unlockClearCount", default_unlock(idx, is_boss)) if row else default_unlock(idx, is_boss)
            is_boss_v = row.get("isBoss", is_boss) if row else is_boss
            scale = row.get("spriteScale", 1.0) if row else 1.0
            lines.append(
                "{id},{ch},{idx},{name},{minWave},{isBoss},{unlock},{hp},{atk},{asp},{ar},{ms},{gold},{exp},{scale},{note}".format(
                    id=mid, ch=ch, idx=idx, name=name,
                    minWave=min_wave, isBoss=is_boss_v, unlock=unlock,
                    hp=stats["baseHp"], atk=stats["baseAttack"],
                    asp=stats["baseAttackSpeed"], ar=stats["attackRange"],
                    ms=stats["baseMoveSpeed"], gold=stats["baseGoldDrop"],
                    exp=stats["expDrop"], scale=scale, note=note,
                )
            )
    path = os.path.join(TABLES, "monster_stats.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("wrote", path, "rows", len(lines) - 2)


def write_chapter_theme_map():
    rows = [
        "# gameChapter=游戏章；monsterChapter=素材章",
        "gameChapter,monsterChapter,folderName,mapName,bgFolder",
        "1,4,4 Forest,\u66ae\u5f71\u68ee\u6797,4 Forest",
        "2,1,1 Undead,\u5e7d\u51a5\u5893\u56ed,1 Undead",
        "3,2,2 Jungle,\u7fe0\u73b9\u79d8\u5883,2 Jungle",
        "4,3,3 Sea,\u6df1\u84dd\u9057\u8ff9\u6d77\u57df,3 Sea",
        "5,5,5 Field,\u6668\u66e6\u539f\u91ce,5 Field",
        "6,6,6 Cave,\u5de8\u5ca9\u6df1\u7a9f,6 Cave",
        "7,7,7 Devil,\u8d64\u7130\u70bc\u72f1,7 Devil",
        "8,8,8 Ice,\u6c38\u971c\u96ea\u5883,8 Ice",
    ]
    path = os.path.join(TABLES, "chapter_theme_map.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print("wrote", path)


def write_unlock_tier():
    rows = [
        "# clearCountMin\u8fbe\u5230\u540e\u53ef\u7528\u7cbe\u7075\u4e0a\u9650",
        "clearCountMin,spriteIndexMax,stageIndexBonus,note",
        "0,5,2,\u9996\u6b21\u901a\u5173",
        "2,8,2,",
        "4,10,2,",
    ]
    path = os.path.join(TABLES, "monster_unlock_tier.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print("wrote", path)


def write_stage_spawn():
    rows = [
        "# monsterTotal=0 \u8868\u793a\u8d70 GameConfig \u516c\u5f0f\uff1b* \u4e3a\u901a\u914d",
        "gameChapter,stageIndex,stageType,monsterTotal,waveCountMin,waveCountMax,eliteScaleMul,note",
        "1,0,Normal,18,4,6,1.0,\u7b2c\u4e00\u7ae0\u9996\u5173",
        "*,*,Normal,0,3,6,1.0,\u9ed8\u8ba4\u666e\u901a\u5173\u516c\u5f0f",
        "*,*,Elite,0,3,6,1.5,\u7cbe\u82f1\u5173\u516c\u5f0f",
        "*,*,Boss,0,3,7,1.0,Boss\u5c0f\u602a\u516c\u5f0f",
    ]
    path = os.path.join(TABLES, "stage_spawn.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print("wrote", path)


def write_tutorial_battle():
    rows = [
        "# order=\u6267\u884c\u987a\u5e8f\uff1baction=normal|flank|around",
        "order,action,count,spriteMelee,spriteRanged,ambush,mercId,mercHpRatio,aheadDist,stunned,note",
        "1,normal,2,2,1,0,,,,,\u9996\u6ce2",
        "2,normal,2,2,1,0,,,,,\u7b2c\u4e8c\u5c0f\u6ce2",
        "3,flank,5,2,1,1,,,,,\u5b9d\u7bb1\u57cb\u4f0f",
        "4,around,3,2,1,0,dunbing101,0.35,5.5,1,\u56f4\u6bb1\u8001\u76fe",
        "5,normal,4,2,1,0,,,,,\u7ec4\u961f\u540e",
        "6,flank,3,2,1,1,,,,,\u6e05\u573a\u540e\u4fa7\u7ffc",
    ]
    path = os.path.join(TABLES, "tutorial_battle.csv")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print("wrote", path)


def cook_bytes():
    out_dir = os.path.join(ROOT, "Assets", "Resources", "Data", "Tables")
    os.makedirs(out_dir, exist_ok=True)
    names = [
        "monster_stats", "chapter_theme_map", "monster_unlock_tier",
        "stage_spawn", "tutorial_battle",
    ]
    for name in names:
        src = os.path.join(TABLES, name + ".csv")
        if not os.path.isfile(src):
            continue
        with io.open(src, "r", encoding="utf-8") as f:
            text = f.read()
        dst = os.path.join(out_dir, name + ".bytes")
        with io.open(dst, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print("cooked", dst)


def main():
    os.makedirs(TABLES, exist_ok=True)
    write_monster_stats()
    write_chapter_theme_map()
    write_unlock_tier()
    write_stage_spawn()
    write_tutorial_battle()
    cook_bytes()


if __name__ == "__main__":
    main()
