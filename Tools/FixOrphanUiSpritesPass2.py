# -*- coding: utf-8 -*-
"""第二轮：冒险页 + 战斗 UI 剩余孤儿 Sprite GUID。"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(r"Y:\PixelAdventureTown\Assets")


def find_by_suffix(folder: Path, suffix: str) -> str | None:
    if not folder.is_dir():
        return None
    for p in folder.rglob("*.png"):
        name = p.name.lstrip("\x7f")
        if name == suffix or name.endswith(suffix):
            return p.relative_to(ROOT).as_posix()
    return None


def build_path_to_guid() -> dict[str, str]:
    m = {}
    for p in (ROOT / "Art" / "UI").rglob("*.png"):
        meta = Path(str(p) + ".meta")
        if not meta.is_file():
            continue
        g = re.search(r"^guid:\s*([0-9a-fA-F]{32})", meta.read_text(encoding="utf-8-sig"), re.M)
        if not g:
            continue
        rel = p.relative_to(ROOT).as_posix()
        m[rel] = g.group(1)
        m[p.name.lstrip("\x7f").lower()] = g.group(1)
    return m


def main():
    ptg = build_path_to_guid()
    adv = ROOT / "Art" / "UI" / "Adventure"
    battle = ROOT / "Art" / "UI" / "battle"
    common = ROOT / "Art" / "UI" / "Common"
    stages = ROOT / "Art" / "UI" / "StageIcons"
    frames = ROOT / "Art" / "UI" / "装备格子"

    def A(s):
        return find_by_suffix(adv, s)

    def B(s):
        return find_by_suffix(battle, s)

    def C(s):
        return find_by_suffix(common, s)

    def St(s):
        return find_by_suffix(stages, s)

    orphan: dict[str, str] = {}

    def add(guid: str, art: str | None, label: str):
        if not art:
            print("WARN", label)
            return
        g = ptg.get(art) or ptg.get(Path(art).name.lower())
        if not g:
            print("WARN guid", art, label)
            return
        orphan[guid] = g
        print(f"MAP {label}: {art}")

    # AdventureUI
    add("2bacb4f389ec7a240af320a123d09eee", A("_未选中.png") or A("_冒险标签.png"), "ModeBtn")
    add("f0f611959f3e14044bc085e2acca0034", A("_选中.png"), "ModeBtn选中")
    add("05abf95dd13897b4bacd0037a02991f7", A("_敌人图标.png") or A("敌人图标框.png"), "EnemyIcon")
    add("9ebe53df5321adb419f038693d56dadf", A("敌人图标框.png") or find_by_suffix(frames, "frame_common.png"), "DropIconKuang")
    add("a7f740c34d3affc47ab89c52c3d68e4a", A("_宝箱奖励.png"), "boxbg")
    add("b3d521f8e1defc846b7f8765c48fd212", A("_普通.png"), "Diff普通")
    add("1c9c8ef2033288c47ac1765134059f10", A("_困难.png"), "Diff困难")
    add("d658ea91a3318f84eba163b3d7127eb3", A("_噩梦.png"), "Diff噩梦")
    add("333aba60068579b4ab6d9d116a2816bc", A("_背景-拷贝.png") or A("_图层-8.png"), "Overlay")
    add("f4c10f8b47e432042a2e20d60eab5f4c", A("_主线.png"), "ModeIcon0")
    add("b957111ea03cc9849b0a1d965bc4f4d2", A("_背景-拷贝.png"), "MapBg")
    add("da09a09e1c3066941a22b322d8bb7b8e", A("_章节.png"), "TitleBar")
    add("e73e474a00db87f40b9029f83711d91e", A("_左箭头.png"), "PrevBtn")
    add("1b32181be1066884e86d40d16a3021a3", A("_右箭头.png"), "NextBtn")

    # more adventure from audit - get remaining paths
    # Battle remaining
    add("682123ec8e9bf0e4db2c88b7539295a0", B("血条.png"), "HPBarFill")
    add("3075800ba2f500247bf43e82cc5b3722", B("资源底框.png"), "TopDisplays")
    add("45b13c3eb4ce8dc4d85f7d106aecb4de", B("关卡.png") or C("星.png"), "EnchantIcon")
    add("357d5a7b7389e924a8b593a0ffc79df2", B("设置.png") or C("问题.png"), "DecomposeIcon")
    add("6d278d7623abd2f4d9538dec94529a9f", B("大边框.png") or B("边框.png"), "BackpackPanel")
    add("10c0b7edcdfce5e47a4685baa1b5d3ef", B("设置.png") or C("设置.png"), "BattleSettings")
    add("74f9d0a064cacf14bbfa38953acc0090", B("任务底框.png"), "QuestPanel")
    add("f3cd7a02c0571c544a32e501c991102a", B("难度.png"), "DifficultyIcon")
    # map nodes 1/2/3 — stage stones
    add("3ff8272ae3a954f418b6437e01637bc1", B("关卡.png") or St("_森林.png"), "map1")
    add("8f110a596fb4da445a14356173e9f8b2", B("关卡.png"), "map2")
    add("f8db56b87d4480b4cb3b182f16c24002", B("关卡.png"), "map3")

    print("map size", len(orphan))

    # Also path-based for Adventure mode icons ModeBtn_1.. etc via second audit after
    targets = list((ROOT / "Resources" / "Prefabs").rglob("*.prefab"))
    targets += list((ROOT / "Scenes").rglob("*.unity"))
    touched = 0
    reps = 0
    for p in targets:
        text = p.read_text(encoding="utf-8")
        orig = text
        for old, new in orphan.items():
            if old in text:
                reps += text.count(old)
                text = text.replace(old, new)
        if text != orig:
            p.write_text(text, encoding="utf-8", newline="\n")
            touched += 1
            print("wrote", p.relative_to(ROOT))
    print(f"DONE files={touched} reps={reps}")

    # dump leftover Adventure BAD for manual
    Path(r"Y:\PixelAdventureTown\Tools\orphan-sprite-rebind-pass2.json").write_text(
        json.dumps(orphan, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
