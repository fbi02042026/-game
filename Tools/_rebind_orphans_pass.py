# -*- coding: utf-8 -*-
"""全量重绑关键路径里的悬空 Sprite/Texture guid（不改任何 .meta 的 guid）。"""
from pathlib import Path
import re
from collections import defaultdict

ROOT = Path(r"Y:/PixelAdventureTown")
ASSETS = ROOT / "Assets"
PKG = ROOT / "Library" / "PackageCache"

GUID_LINE = re.compile(r"^guid:\s*(\S+)", re.M)
REF = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
SPRITE_REF = re.compile(
    r"(m_Sprite|icon|iconSprite|sprite|frontSprite|midSprite|backSprite|"
    r"portrait|headIcon|bgSprite|background):\s*\{fileID:\s*21300000,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*3\}"
)

def load_known():
    known = {}  # guid -> relative path without .meta
    for base in (ASSETS, PKG if PKG.exists() else None):
        if base is None:
            continue
        for meta in base.rglob("*.meta"):
            try:
                t = meta.read_text(encoding="utf-8", errors="ignore")
            except Exception:
                continue
            m = GUID_LINE.search(t)
            if not m:
                continue
            g = m.group(1).strip()
            if not re.fullmatch(r"[0-9a-fA-F]{32}", g):
                # keep base64 too as known so we don't try to "fix" them
                known[g] = str(meta)[:-5]
                continue
            known[g] = str(meta)[:-5]
    return known

def index_sprites():
    """stem / relative key -> guid for png/jpg under Art & Resources."""
    by_stem = defaultdict(list)  # stem lower -> [(guid, relpath)]
    by_rel = {}
    for folder in [ASSETS / "Art", ASSETS / "Resources"]:
        if not folder.exists():
            continue
        for meta in folder.rglob("*.meta"):
            asset = Path(str(meta)[:-5])
            if asset.suffix.lower() not in {".png", ".jpg", ".jpeg", ".psd", ".tga"}:
                continue
            try:
                t = meta.read_text(encoding="utf-8", errors="ignore")
            except Exception:
                continue
            m = GUID_LINE.search(t)
            if not m:
                continue
            g = m.group(1).strip()
            if not re.fullmatch(r"[0-9a-fA-F]{32}", g):
                continue
            rel = asset.relative_to(ASSETS).as_posix()
            by_rel[rel.lower()] = g
            by_stem[asset.stem.lower()].append((g, rel))
    return by_stem, by_rel

def pick_stem(stem, by_stem, prefer_substrings=None):
    cands = by_stem.get(stem.lower(), [])
    if not cands:
        return None
    if prefer_substrings:
        for pref in prefer_substrings:
            for g, rel in cands:
                if pref.lower() in rel.lower():
                    return g, rel
    # prefer Resources then Art/UI
    for g, rel in cands:
        if "Resources/" in rel or "Resources\\" in rel:
            return g, rel
    for g, rel in cands:
        if "Art/UI" in rel or "Art\\UI" in rel:
            return g, rel
    return cands[0]

def main():
    print("loading known guids...")
    known = load_known()
    print("known", len(known))
    by_stem, by_rel = index_sprites()
    print("sprite stems", len(by_stem))

    # critical files
    roots = [
        ASSETS / "Resources" / "Config",
        ASSETS / "Resources" / "Prefabs",
        ASSETS / "Resources" / "VFX",
        ASSETS / "Scenes",
    ]
    exts = {".prefab", ".unity", ".asset", ".controller"}
    files = []
    for r in roots:
        if not r.exists():
            continue
        for f in r.rglob("*"):
            if f.is_file() and f.suffix.lower() in exts and f.stat().st_size < 20 * 1024 * 1024:
                files.append(f)
    print("files", len(files))

    # equip icon heuristics: equip_xxx.asset -> icon names
    equip_name_map = {
        "hands": ["equip_hands", "hands", "手套"],
        "cape": ["equip_cape", "cape", "披风"],
        "feet": ["equip_feet", "feet", "鞋"],
        "weapon": ["equip_weapon", "weapon", "武器"],
        "shield": ["equip_shield", "shield", "盾"],
        "head": ["equip_head", "helmet", "头盔"],
        "chest": ["equip_chest", "armor", "胸甲"],
        "cloth": ["equip_cloth", "cloth", "布甲"],
        "pant": ["equip_pant", "pants", "裤"],
        "helmet": ["equip_helmet", "helmet", "头盔"],
        "armor": ["equip_armor", "armor", "铠甲"],
        "sword": ["equip_sword", "sword", "剑"],
        "axe": ["equip_axe", "axe", "斧"],
        "bow": ["equip_bow", "bow", "弓"],
        "spear": ["equip_spear", "spear", "矛"],
        "foot": ["equip_foot", "feet", "鞋"],
        "woodshield": ["woodshield", "shield", "木盾"],
        "steelshield": ["steelshield", "shield", "钢盾"],
    }

    replacements = 0
    files_touched = 0
    unresolved = []
    log = []

    for f in files:
        try:
            text = f.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        orig = text
        # find orphan sprite refs
        for m in list(SPRITE_REF.finditer(text)):
            field, g = m.group(1), m.group(2)
            if g in known:
                continue
            # try resolve
            new_g = None
            how = ""
            stem_hint = f.stem.lower()
            # MonsterSpriteRegistry: leave for specialized pass
            if f.name == "MonsterSpriteRegistry.asset":
                continue
            # Equip assets
            if "Equips" in str(f) and f.suffix == ".asset":
                # try exact stem icon_equip_xxx or equip_xxx
                for cand in [stem_hint, "icon_" + stem_hint, stem_hint.replace("equip_", "icon_")]:
                    hit = pick_stem(cand, by_stem, ["Icons", "Equip", "equip", "UI"])
                    if hit:
                        new_g, how = hit[0], hit[1]
                        break
                if not new_g:
                    # category fallback
                    for key, stems in equip_name_map.items():
                        if key in stem_hint:
                            for s in stems:
                                hit = pick_stem(s, by_stem, ["Icons", "Equip", "UI"])
                                if hit:
                                    new_g, how = hit[0], f"fallback:{hit[1]}"
                                    break
                            break
            # Prefab/UI: try common loading/bg names from context around match - hard; skip generic
            if not new_g and f.suffix in {".prefab", ".unity"}:
                # don't random-rebind UI sprites without path hint
                unresolved.append((g, str(f.relative_to(ASSETS)), field))
                continue

            if new_g and new_g != g:
                old = m.group(0)
                new = old.replace(g, new_g)
                text = text.replace(old, new, 1)
                replacements += 1
                log.append(f"{f.relative_to(ASSETS)} | {field} {g} -> {new_g} ({how})")
            else:
                unresolved.append((g, str(f.relative_to(ASSETS)), field))

        if text != orig:
            f.write_text(text, encoding="utf-8", newline="\n")
            files_touched += 1

    # Specialized: MonsterSpriteRegistry — rebind by monsterId field nearby
    msr = ASSETS / "Resources" / "Config" / "MonsterSpriteRegistry.asset"
    if msr.exists():
        text = msr.read_text(encoding="utf-8", errors="ignore")
        orig = text
        # entries look like: monsterId: forest_401 \n sprite: {fileID: 21300000, guid: XXX
        entry_re = re.compile(
            r"(monsterId|id):\s*([A-Za-z0-9_]+).*?(sprite|icon):\s*\{fileID:\s*21300000,\s*guid:\s*([0-9a-fA-F]{32})",
            re.S,
        )
        # simpler line-based: find orphan guids and preceding monster id within 8 lines
        lines = text.splitlines(True)
        for i, line in enumerate(lines):
            gm = re.search(r"guid:\s*([0-9a-fA-F]{32})", line)
            if not gm:
                continue
            g = gm.group(1)
            if g in known:
                continue
            mid = None
            for j in range(max(0, i - 12), i):
                mm = re.search(r"(monsterId|id|key):\s*([A-Za-z0-9_]+)", lines[j])
                if mm:
                    mid = mm.group(2)
            if not mid:
                unresolved.append((g, "MonsterSpriteRegistry.asset", "sprite"))
                continue
            hit = pick_stem(mid, by_stem, ["Monster", "monster", "Art"])
            if not hit:
                # try without prefix
                hit = pick_stem(mid.split("_")[-1], by_stem, ["Monster", "monster"])
            if hit:
                lines[i] = line.replace(g, hit[0])
                replacements += 1
                log.append(f"MonsterSpriteRegistry | {mid} {g} -> {hit[0]} ({hit[1]})")
            else:
                unresolved.append((g, f"MonsterSpriteRegistry:{mid}", "sprite"))
        text2 = "".join(lines)
        if text2 != orig:
            msr.write_text(text2, encoding="utf-8", newline="\n")
            files_touched += 1

    # BattleStageMap / map prefab / VFX: try ground_tex / known battle arts
    special_prefabs = [
        ASSETS / "Resources" / "Prefabs" / "background" / "map.prefab",
        ASSETS / "Resources" / "Prefabs" / "Battle" / "BattleStageMap.prefab",
    ]
    for pf in special_prefabs:
        if not pf.exists():
            continue
        text = pf.read_text(encoding="utf-8", errors="ignore")
        orig = text
        for m in SPRITE_REF.finditer(orig):
            field, g = m.group(1), m.group(2)
            if g in known:
                continue
            # prefer ground_tex or Forest 1
            for stem in ["ground_tex", "1", "关卡"]:
                hit = pick_stem(stem, by_stem, ["background", "battle", "Battle"])
                if hit:
                    text = text.replace(g, hit[0])
                    replacements += 1
                    log.append(f"{pf.name} | {g} -> {hit[0]} ({hit[1]})")
                    break
        if text != orig:
            pf.write_text(text, encoding="utf-8", newline="\n")
            files_touched += 1

    report = ROOT / "Tools" / "_rebind_pass_log.txt"
    report.write_text(
        f"replacements={replacements}\nfiles_touched={files_touched}\nunresolved={len(unresolved)}\n\n"
        + "\n".join(log[:200])
        + "\n\n--- unresolved sample ---\n"
        + "\n".join(f"{g} @ {fp} ({fld})" for g, fp, fld in unresolved[:80]),
        encoding="utf-8",
    )
    print("replacements", replacements, "files_touched", files_touched, "unresolved", len(unresolved))
    print("log", report)

if __name__ == "__main__":
    main()
