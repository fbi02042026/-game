# -*- coding: utf-8 -*-
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(r"Y:\PixelAdventureTown\Assets")


def guid_of(p: Path) -> str:
    return re.search(
        r"^guid:\s*([0-9a-fA-F]{32})",
        Path(str(p) + ".meta").read_text(encoding="utf-8-sig"),
        re.M,
    ).group(1)


def find(folder: Path, suf: str) -> Path | None:
    if not folder.is_dir():
        return None
    for p in folder.rglob("*.png"):
        n = p.name.lstrip("\x7f")
        if n == suf or n.endswith(suf):
            return p
    return None


def main():
    tavern = ROOT / "Art/UI/Tavern"
    log = ROOT / "Art/UI/冒险日志"
    stage = ROOT / "Art/UI/关卡"
    common = ROOT / "Art/UI/Common"
    battle = ROOT / "Art/UI/battle"
    # loading art?
    loading_dirs = [
        ROOT / "Art/UI",
        ROOT / "Art/Effects",
        ROOT / "Art",
    ]

    orphan: dict[str, str] = {}

    def add(g: str, p: Path | None, label: str):
        if not p:
            print("WARN", label)
            return
        orphan[g] = guid_of(p)
        print("MAP", label, p.name)

    # Tavern cards — use tavern layer sprites
    layers = sorted([p for p in tavern.glob("*.png")], key=lambda x: x.name)
    tavern_guids = [
        ("6b85dc82c147c3e4fbfb878719b042a5", "Quest"),
        ("974eb4bcb830a1e4488a2a1a7321e0fa", "Trust"),
        ("ba92121038c8e6f469ca7f4a707195c8", "Recruit"),
        ("5c0bd3ca7290b88479aae8a3fa7be5a5", "Intel"),
    ]
    for i, (g, lab) in enumerate(tavern_guids):
        art = layers[i] if i < len(layers) else layers[-1]
        add(g, art, f"Tavern{lab}")
    add("e8ca17786574dfb448f1896b8f705f73", find(tavern, "_图层-1.png") or (layers[-1] if layers else None), "TavernScene")
    add("9226fd8b4d6091a4fa5698d665fe320d", find(tavern, "_图层-5.png") or (layers[0] if layers else None), "Tavern人物")

    # AdventureLog
    add("4e7e61d1248102543827e68c92c1396b", find(log, "图层 1.png") or find(log, "内容底.png"), "LogFrame")
    add("22ce756b838c7eb4c8c5fe75567d747f", find(log, "内容底.png"), "PaperBg")
    add("deffeb46b9fe8bd4eb59048f65cef9d2", find(log, "标签底.png"), "TabsImage")
    add("3b60a5e33ea00d440aecf03b967862bb", find(log, "默认.png") or find(log, "插图/主线.png"), "CardArt")
    add("adace6f90e8b9f84f954a1739a9c4acf", find(log, "装饰1.png"), "Emblem")
    add("8c826b5222de4de4a93c8c630be5ba52", find(log, "标头.png"), "BiaotouBg")
    add("d5bec8bb4d69b4f458e0d18a22950020", find(log, "字底.png") or find(common, "确定按钮.png"), "ClaimAch")
    add("4e945a5bbc324b94c9de734e4a2248a5", find(log, "图层 2.png") or find(log, "内容底.png"), "Paper")
    add("43b2f0296bf367946a075535213ce87c", find(log, "默认.png"), "ArtDi")
    # Tab icons — 插图
    tab_arts = [
        find(log, "插图/主线.png") or find(log, "主线-1.png"),
        find(log, "插图/支线.png") or find(log, "支线-1.png"),
        find(log, "插图/佣兵.png") or find(log, "佣兵-1.png"),
        find(log, "插图/怪物.png") or find(log, "怪物-1.png"),
        find(log, "插图/成就.png") or find(log, "成就-1.png"),
        find(log, "插图/世界.png") or find(log, "世界-1.png"),
    ]
    tab_guids = [
        "ab46f23c8899089468ee16827b4fe340",
        "cc14d8e3bdb0e7c4e99088de5e978bee",
        "c1817766074589f4fa9e4dbaf5dd3030",
        "bef58070f57509f4ab9cf82e34975d16",
        "12d72543aa22dbe498925372f66ad967",
        "96861a8d12d6819418e725ca96379bd8",
    ]
    for g, art in zip(tab_guids, tab_arts):
        add(g, art, "TabIcon")

    # BattleStageMap backdrop
    add("a0fac80ef2666064685d5200901f2482", find(stage, "背景.png"), "Backdrop")

    # Roulette stage icons
    roulette = [
        ("e7dc600685b8c9e4eba19858c3635593", "stage_normal.png"),
        ("99b6606d4264ef64fa9b35ed30acffd4", "stage_elite.png"),
        ("65bb99996e1220947bf4a56d620ff65d", "stage_boss.png"),
        ("3851167a8e5d0d44cb9621e115d6a657", "stage_rest.png"),
        ("1793ae584e10eef42855eeffd51ad191", "stage_enchant.png"),
    ]
    for g, name in roulette:
        add(g, find(stage, name), name)

    # Rest popup
    add("1ff6263506362994d9363f991b9525f3", find(common, "确定按钮.png"), "Continue")
    add("ce376f914b84bd345ae833d5ca6d3a00", find(stage, "恢复底框.png"), "IllustFrame")
    add("53ef94da656a3d54d865568470335da1", find(stage, "恢复体力.png") or find(battle, "大边框.png"), "RestPanel")

    # Loading — search logo
    logo = None
    bg = None
    for d in loading_dirs:
        for p in d.rglob("*.png"):
            n = p.name.lower()
            if logo is None and "logo" in n:
                logo = p
            if bg is None and ("loading" in n or "load" in n):
                bg = p
    # Login background as fallback for loading bg
    login = ROOT / "Art/UI/Login"
    if bg is None:
        bg = find(login, "背景") if login.exists() else None
        if bg is None and login.exists():
            pngs = list(login.glob("*.png"))
            bg = pngs[0] if pngs else None
    if logo is None:
        # Effects logo or Main
        for p in (ROOT / "Art").rglob("*logo*.png"):
            logo = p
            break
    add("c6ab257eeb0243f9abbf84559d03f17a", bg or find(battle, "底图.png"), "LoadingBg")
    add("84db53dcff6659041ba3b70f811f945a", logo or find(battle, "进度.png"), "LoadingLogo")

    targets = list((ROOT / "Resources/Prefabs").rglob("*.prefab"))
    touched = reps = 0
    for pref in targets:
        text = pref.read_text(encoding="utf-8")
        orig = text
        for o, n in orphan.items():
            if o in text:
                reps += text.count(o)
                text = text.replace(o, n)
        if text != orig:
            pref.write_text(text, encoding="utf-8", newline="\n")
            touched += 1
            print("wrote", pref.name)
    print(f"DONE files={touched} reps={reps}")


if __name__ == "__main__":
    main()
