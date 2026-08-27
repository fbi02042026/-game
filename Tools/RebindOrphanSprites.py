# -*- coding: utf-8 -*-
"""Rebind orphan sprite GUIDs in UI prefabs to current Art assets by node name / heuristics."""
import os
import re
import json

ROOT = r"Y:\PixelAdventureTown\Assets"
CACHE = r"Y:\PixelAdventureTown\Tools\_guid_cache.json"


def build_guid_cache():
    guid_to_path = {}
    path_to_guid = {}
    # Only Art + Resources — enough for UI sprites
    for sub in ("Art", "Resources"):
        base = os.path.join(ROOT, sub)
        for dp, _, fs in os.walk(base):
            for f in fs:
                if not f.endswith(".meta"):
                    continue
                asset = os.path.join(dp, f[:-5])
                if not os.path.isfile(asset):
                    continue
                # only images / fonts-ish for sprites
                ext = os.path.splitext(asset)[1].lower()
                if ext not in (".png", ".jpg", ".jpeg", ".psd", ".tga", ".bmp"):
                    continue
                try:
                    t = open(os.path.join(dp, f), encoding="utf-8-sig").read(400)
                except OSError:
                    continue
                m = re.search(r"^guid:\s*(\S+)", t, re.M)
                if not m:
                    continue
                g = m.group(1)
                if not re.fullmatch(r"[0-9a-fA-F]{32}", g):
                    continue
                guid_to_path[g] = asset
                rel = os.path.relpath(asset, ROOT).replace("\\", "/")
                path_to_guid[rel] = g
                path_to_guid[os.path.basename(asset).lower()] = g
    with open(CACHE, "w", encoding="utf-8") as f:
        json.dump({"guid_to_path": guid_to_path, "path_to_guid": path_to_guid}, f)
    print("cached", len(guid_to_path), "sprites ->", CACHE)
    return guid_to_path, path_to_guid


def load_cache():
    if os.path.isfile(CACHE):
        data = json.load(open(CACHE, encoding="utf-8"))
        return data["guid_to_path"], data["path_to_guid"]
    return build_guid_cache()


# Prefab-relative rebinds: (prefab_relpath, node_name) -> art path relative to Assets/
# node_name is the GameObject that owns the Image (closest m_Name before m_Sprite)
MANUAL = {
    # Dialogue
    ("Resources/Prefabs/Dialogue/DialogueUI.prefab", "DialogueBox"): "Art/UI/Story/ui_dialogue_panel.png",
    ("Resources/Prefabs/Dialogue/DialogueUI.prefab", "LeftNamePlate"): "Art/UI/Story/ui_nameplate_npc.png",
    ("Resources/Prefabs/Dialogue/DialogueUI.prefab", "RightNamePlate"): "Art/UI/Story/ui_nameplate_player.png",
    ("Resources/Prefabs/Dialogue/DialogueUI.prefab", "NextArrow"): "Art/UI/Story/ui_tap.png",
    ("Resources/Prefabs/Dialogue/DialogueUI.prefab", "Choice_0"): "Art/UI/Story/ui_choice.png",
    ("Resources/Prefabs/Dialogue/DialogueUI.prefab", "Choice_1"): "Art/UI/Story/ui_choice.png",
    ("Resources/Prefabs/Dialogue/DialogueUI.prefab", "Choice_2"): "Art/UI/Story/ui_choice.png",
    # portraits are runtime-filled; keep or clear — leave as-is if we can't map
}


def sprite_ref(guid: str) -> str:
    return f"{{fileID: 21300000, guid: {guid}, type: 3}}"


def rebind_prefab(prefab_abs, prefab_rel, path_to_guid, guid_to_path):
    txt = open(prefab_abs, encoding="utf-8").read()
    lines = txt.splitlines(keepends=True)
    last_names = []
    changes = 0
    out = []
    for i, line in enumerate(lines):
        if line.strip().startswith("m_Name:"):
            last_names.append(line.split(":", 1)[1].strip())
        if "m_Sprite:" in line:
            m = re.search(r"guid:\s*([0-9a-fA-F]{32})", line)
            name = last_names[-1] if last_names else ""
            need = False
            if m:
                g = m.group(1)
                if g not in guid_to_path:
                    need = True
            else:
                # fileID 0 empty — only rebind if we have a manual map
                key = (prefab_rel, name)
                need = key in MANUAL

            if need:
                key = (prefab_rel.replace("\\", "/"), name)
                art = MANUAL.get(key)
                if art:
                    ng = path_to_guid.get(art) or path_to_guid.get(art.replace("\\", "/"))
                    if ng:
                        new_line = re.sub(
                            r"m_Sprite:\s*\{[^}]*\}",
                            f"m_Sprite: {sprite_ref(ng)}",
                            line,
                        )
                        if new_line != line:
                            changes += 1
                            line = new_line
                            print(f"  FIX {name} -> {art} ({ng})")
                    else:
                        print(f"  MISS art {art} for {name}")
                else:
                    # report unmapped
                    gshow = m.group(1) if m else "0"
                    print(f"  UNMAPPED {name} guid={gshow}")
        out.append(line)

    if changes:
        open(prefab_abs, "w", encoding="utf-8", newline="\n").write("".join(out))
    return changes


def main():
    guid_to_path, path_to_guid = load_cache()
    targets = [
        "Resources/Prefabs/Dialogue/DialogueUI.prefab",
        "Resources/Prefabs/Battle/EquipDropPopup.prefab",
        "Resources/Prefabs/Town/GuildHallUI.prefab",
        "Resources/Prefabs/Battle/BattleUI.prefab",
    ]
    total = 0
    for rel in targets:
        abs_p = os.path.join(ROOT, rel.replace("/", os.sep))
        if not os.path.isfile(abs_p):
            print("skip missing", rel)
            continue
        print("====", rel)
        total += rebind_prefab(abs_p, rel, path_to_guid, guid_to_path)
    print("total changes", total)


if __name__ == "__main__":
    build_guid_cache()
    main()
