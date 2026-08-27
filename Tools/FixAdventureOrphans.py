# -*- coding: utf-8 -*-
"""AdventureUI remaining orphan sprite rebind."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(r"Y:\PixelAdventureTown\Assets")


def guid_of(p: Path) -> str:
    meta = Path(str(p) + ".meta")
    m = re.search(r"^guid:\s*([0-9a-fA-F]{32})", meta.read_text(encoding="utf-8-sig"), re.M)
    if not m:
        raise RuntimeError(p)
    return m.group(1)


def find(folder: Path, suf: str) -> str | None:
    if not folder.is_dir():
        return None
    for p in folder.rglob("*.png"):
        n = p.name.lstrip("\x7f")
        if n == suf or n.endswith(suf):
            return p.relative_to(ROOT).as_posix()
    return None


def main():
    adv = ROOT / "Art" / "UI" / "Adventure"
    stages = ROOT / "Art" / "UI" / "StageIcons"
    ptg: dict[str, str] = {}
    for folder in (adv, stages):
        for p in folder.rglob("*.png"):
            ptg[p.relative_to(ROOT).as_posix()] = guid_of(p)

    orphan: dict[str, str] = {}

    def add(guid: str, art: str | None, label: str):
        if not art or art not in ptg:
            print("WARN", label, art)
            return
        orphan[guid] = ptg[art]
        print("MAP", label, art)

    order = [
        "_森林.png",
        "_雨林.png",
        "_海岸.png",
        "_亡灵.png",
        "_田野.png",
        "_洞穴.png",
        "_熔岩.png",
        "_冰川.png",
    ]
    node_guids = [
        "37e227987c2e7cc4789b06ebaa1f6aca",
        "1a15c20485f2ef947803ea42bd5242c5",
        "087a545a49e745043b0abe17d908e4a9",
        "8cbac1b6be16432409cdc6be1e3213f2",
        "96fff5adabe72744aaab919251daceb7",
        "29b0c4525da9c9e4daa5f91da58779a3",
        "e766fef543875c04b884c2c253fa9110",
        "90cb5ddf9e61f2b448165f30076dcdce",
    ]
    for i, g in enumerate(node_guids):
        add(g, find(stages, order[i]), f"Node_{i+1}")

    add("88025d76ffa4d9c4d859b17544194762", find(adv, "_图层-13.png") or find(adv, "_背景-拷贝.png"), "DetailPanel")
    add("d24a2d63c74e8614c9317241f4900672", find(adv, "敌人图标框.png"), "DropIcon2")
    add("a16588c1964bcf549a707e9b5ca26570", find(adv, "敌人图标框.png"), "DropIcon3")
    add("090e1c7b819f3324893f067082340234", find(adv, "_体力加号.png"), "AddChances")
    add("2dab355472013a84ba7761670484a8e3", find(adv, "_开始冒险.png"), "StartBtn")
    add("726ddbf69acf3c54f96b0b8031d24f55", find(adv, "_扫荡.png"), "SweepBtn")
    add("ad73c923cc22a2d4bb632baa09f87c24", find(adv, "_每日.png"), "Mode1")
    add("fd85150c53cae7646920b2495f294591", find(adv, "_迷宫.png"), "Mode2")
    add("647bb7604575af34cbcd1000278c1fb7", find(adv, "_活动.png"), "Mode3")
    add("70d9751dd91f2ed4f9eabf648fccc672", find(adv, "_BOSS.png"), "Mode4")
    add("b9564f032c43e1d428259339d75569fe", find(adv, "_图层-8.png"), "TopBg")

    pref = ROOT / "Resources" / "Prefabs" / "Town" / "AdventureUI.prefab"
    text = pref.read_text(encoding="utf-8")
    n = 0
    for o, nw in orphan.items():
        if o in text:
            n += text.count(o)
            text = text.replace(o, nw)
    pref.write_text(text, encoding="utf-8", newline="\n")
    print(f"replaced {n} in AdventureUI, map={len(orphan)}")


if __name__ == "__main__":
    main()
