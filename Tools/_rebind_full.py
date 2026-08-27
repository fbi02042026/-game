# -*- coding: utf-8 -*-
"""一次性重绑：装备图标 / 怪物图鉴 / Battle 场景关键引用。不改任何 .meta guid。"""
from pathlib import Path
import re

ASSETS = Path(r"Y:/PixelAdventureTown/Assets")
GUID_RE = re.compile(r"^guid:\s*(\S+)", re.M)


def guid_of(path: Path) -> str | None:
    meta = Path(str(path) + ".meta") if path.suffix != ".meta" else path
    if not meta.exists() and path.suffix != ".meta":
        meta = Path(str(path) + ".meta")
    if not meta.exists():
        return None
    m = GUID_RE.search(meta.read_text(encoding="utf-8", errors="ignore"))
    return m.group(1).strip() if m else None


def stem_index(folder: Path, exts):
    """lower stem -> guid, prefer exact file"""
    idx = {}
    if not folder.exists():
        return idx
    for p in folder.rglob("*"):
        if not p.is_file() or p.suffix.lower() not in exts:
            continue
        g = guid_of(p)
        if g and re.fullmatch(r"[0-9a-fA-F]{32}", g):
            idx[p.stem.lower()] = (g, p)
            # also without spaces/underscores variants
            idx[p.stem.lower().replace(" ", "_")] = (g, p)
    return idx


log = []
n = 0

# ---------- 1) Equip icons by iconFileName ----------
equip_icons = stem_index(ASSETS / "Art/UI/Icons/EquipIcons", {".png"})
# also Resources copy if any
equip_icons.update(stem_index(ASSETS / "Resources/UI/EquipIcons", {".png"}))
equip_dir = ASSETS / "Resources/Config/Equips"
for asset in sorted(equip_dir.glob("*.asset")):
    text = asset.read_text(encoding="utf-8", errors="ignore")
    m_name = re.search(r"^  iconFileName:\s*(\S+)\s*$", text, re.M)
    m_icon = re.search(
        r"^(  icon: \{fileID: 21300000, guid: )([0-9a-fA-F]{32})(, type: 3\})$",
        text,
        re.M,
    )
    if not m_name or not m_icon:
        continue
    name = m_name.group(1).strip().strip('"')
    old = m_icon.group(2)
    hit = equip_icons.get(name.lower())
    if not hit:
        log.append(f"EQUIP_MISS {asset.name} iconFileName={name}")
        continue
    new_g = hit[0]
    if old == new_g:
        continue
    text2 = text[: m_icon.start(2)] + new_g + text[m_icon.end(2) :]
    asset.write_text(text2, encoding="utf-8", newline="\n")
    n += 1
    log.append(f"EQUIP {asset.name}: {old} -> {new_g} ({name})")

# ---------- 2) MonsterSpriteRegistry rebuild from folders ----------
msr_path = ASSETS / "Resources/Config/MonsterSpriteRegistry.asset"
folders = [
    ("chapter1_Undead", "1 Undead"),
    ("chapter2_Jungle", "2 Jungle"),
    ("chapter3_Sea", "3 Sea"),
    ("chapter4_Forest", "4 Forest"),
    ("chapter5_Field", "5 Field"),
    ("chapter6_Cave", "6 Cave"),
    ("chapter7_Devil", "7 Devil"),
    ("chapter8_Ice", "8 Ice"),
]
# Keep script guid from existing file
old_msr = msr_path.read_text(encoding="utf-8", errors="ignore")
script_m = re.search(r"m_Script: \{fileID: 11500000, guid: ([0-9a-fA-F]{32}), type: 3\}", old_msr)
script_guid = script_m.group(1) if script_m else "f94f4551558b48f59d49ea1222f864b7"

lines = [
    "%YAML 1.1",
    "%TAG !u! tag:yousandi.cn,2023:",
    "--- !u!114 &11400000",
    "MonoBehaviour:",
    "  m_ObjectHideFlags: 0",
    "  m_CorrespondingSourceObject: {fileID: 0}",
    "  m_PrefabInstance: {fileID: 0}",
    "  m_PrefabAsset: {fileID: 0}",
    "  m_GameObject: {fileID: 0}",
    "  m_Enabled: 1",
    "  m_EditorHideFlags: 0",
    f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}",
    "  m_Name: MonsterSpriteRegistry",
    "  m_EditorClassIdentifier: ",
]
base = ASSETS / "Resources/Config/MonsterSpriteRegistry"
for field, folder in folders:
    lines.append(f"  {field}:")
    d = base / folder
    pngs = sorted([p for p in d.glob("*.png")], key=lambda p: p.name.lower())
    if not pngs:
        lines.append("  - {fileID: 0}")
        log.append(f"MONSTER_EMPTY {folder}")
        continue
    for p in pngs:
        g = guid_of(p)
        if not g:
            continue
        lines.append(f"  - {{fileID: 21300000, guid: {g}, type: 3}}")
        n += 1
    log.append(f"MONSTER {folder}: {len(pngs)} sprites")
msr_path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

# ---------- 3) Battle.unity critical refs ----------
battle = ASSETS / "Scenes/Battle.unity"
bt = battle.read_text(encoding="utf-8", errors="ignore")
repl = {
    # Ground sprite
    "a9885f8ab5d89434180fb96c90f33fb6": guid_of(ASSETS / "Art/UI/background/ground_tex.png"),
    # box animator
    "94421aa8c700cab41bafa3021ca936aa": guid_of(ASSETS / "Art/Effects/Ani/box/box.controller"),
    # door01 material
    "b32da79c88f331743841fca7b5d00bda": guid_of(ASSETS / "Art/Effects/Material/door01.mat"),
    # open material (box)
    "fefbef7113a4d8147b24bc11684a7b6d": guid_of(ASSETS / "Art/Effects/Material/box01.mat"),
    # close / open sprites — use wood box as default scene prop
    "c10c79ddcb5ac3a4592c150f3df0d71b": guid_of(ASSETS / "Art/UI/box/mubox_close.png"),
    "c28d3c1e0de75bb43aaefbb52ab06a07": guid_of(ASSETS / "Art/UI/box/mubox_open.png"),
    # door01 sprite — use a door building art if available; else leave
}
# door01 sprite 51ea3127 — try find door png in town art
door_candidates = list((ASSETS / "Art").rglob("*Door 01.png"))
if door_candidates:
    repl["51ea312770f08f14888d2e099847bbf4"] = guid_of(door_candidates[0])
    log.append(f"DOOR_SPRITE -> {door_candidates[0].relative_to(ASSETS).as_posix()}")

for old, new in list(repl.items()):
    if not new:
        log.append(f"BATTLE_SKIP {old} (target missing)")
        del repl[old]
        continue
    if old not in bt:
        log.append(f"BATTLE_ABSENT {old}")
        continue
    cnt = bt.count(old)
    bt = bt.replace(old, new)
    n += cnt
    log.append(f"BATTLE {old} -> {new} x{cnt}")

battle.write_text(bt, encoding="utf-8", newline="\n")

# ---------- 4) BattleStageMap orphans if any ----------
for prefab_rel in [
    "Resources/Prefabs/Battle/BattleStageMap.prefab",
    "Resources/Prefabs/background/map.prefab",
]:
    pf = ASSETS / prefab_rel
    if not pf.exists():
        continue
    t = pf.read_text(encoding="utf-8", errors="ignore")
    ground = guid_of(ASSETS / "Art/UI/background/ground_tex.png")
    forest1 = guid_of(ASSETS / "Art/UI/background/1 Forest/1.png")
    # replace known orphans from report
    for old in ["c0d4fd61fe83b8f4996b8a0be8ab86c7", "f4a6573a73531b94ca5e167ea809e82d", "63b662dd66754a7b9d9c5a0ec3d3640e"]:
        if old in t and ground:
            # map layers: use forest/ground
            new = forest1 if "map" in prefab_rel.lower() or "StageMap" in prefab_rel else ground
            if "StageMap" in prefab_rel and forest1:
                new = forest1
            if "ground" in prefab_rel or old.endswith("c0d4"):
                new = ground or new
            t2 = t.replace(old, new)
            if t2 != t:
                n += t.count(old)
                log.append(f"PREFAB {prefab_rel}: {old} -> {new}")
                t = t2
    pf.write_text(t, encoding="utf-8", newline="\n")

# ---------- 5) VFX bow orphans ----------
bow_map = {
    "64968a7620fada84b8321913f5b8e0d1": None,  # ally bow fly
    "a1367395cf482f24c9733fa749bae6e8": None,  # enemy bow fly
}
# find bow sprites
for p in (ASSETS / "Art").rglob("*.png"):
    name = p.name.lower()
    if "bow" in name or "arrow" in name:
        g = guid_of(p)
        log.append(f"BOW_CAND {g} {p.relative_to(ASSETS).as_posix()}")

out = Path(r"Y:/PixelAdventureTown/Tools/_rebind_full_log.txt")
out.write_text(f"total_ops={n}\n\n" + "\n".join(log), encoding="utf-8")
print("done ops", n, "log", out)
