# -*- coding: utf-8 -*-
"""
Walk Unity prefab YAML, resolve Image owner GameObject path, list orphan sprites.
Then rebind using name/path heuristics + explicit maps.
"""
from __future__ import annotations

import json
import os
import re
from collections import defaultdict

ROOT = r"Y:\PixelAdventureTown\Assets"
CACHE = r"Y:\PixelAdventureTown\Tools\_guid_cache.json"


def load_cache():
    data = json.load(open(CACHE, encoding="utf-8"))
    return data["guid_to_path"], data["path_to_guid"]


def parse_prefab(path: str):
    """Return list of {go_id, name, parent_id, sprite_guid|None, sprite_line_idx, has_image}"""
    text = open(path, encoding="utf-8").read()
    lines = text.splitlines()

    # Map fileID -> go info
    gos = {}  # id -> {name, components[]}
    transforms = {}  # transform_id -> {go, father, children}
    images = {}  # image_id -> {go, sprite_guid, line_idx, line}

    i = 0
    while i < len(lines):
        m = re.match(r"--- !u!(\d+) &(-?\d+)", lines[i])
        if not m:
            i += 1
            continue
        typ, fid = int(m.group(1)), m.group(2)
        # collect block until next ---
        j = i + 1
        while j < len(lines) and not lines[j].startswith("--- "):
            j += 1
        block = lines[i:j]
        blob = "\n".join(block)

        if typ == 1:  # GameObject
            nm = re.search(r"^  m_Name: (.*)$", blob, re.M)
            name = nm.group(1).strip() if nm else ""
            if name.startswith('"') and name.endswith('"'):
                name = bytes(name[1:-1], "utf-8").decode("unicode_escape")
            comps = re.findall(r"- component: \{fileID: (-?\d+)\}", blob)
            gos[fid] = {"name": name, "components": comps}
        elif typ == 224:  # RectTransform
            go_m = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", blob)
            father = re.search(r"m_Father: \{fileID: (-?\d+)\}", blob)
            children = re.findall(r"- \{fileID: (-?\d+)\}", blob)
            transforms[fid] = {
                "go": go_m.group(1) if go_m else None,
                "father": father.group(1) if father else "0",
                "children": children,
            }
        elif typ == 114:  # MonoBehaviour — skip
            pass
        elif typ == 222:  # CanvasRenderer
            pass
        elif typ == 114:
            pass

        # Image is !u!114 sometimes? No — Image is MonoBehaviour 114.
        # Actually Unity UI Image is:
        # --- !u!114 &id
        # MonoBehaviour:
        #   m_Script: {fileID: 11500000, guid: IMAGE_SCRIPT...}
        #   m_Sprite: ...
        if typ == 114 and "m_Sprite:" in blob:
            go_m = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", blob)
            sp = re.search(r"m_Sprite: \{([^}]*)\}", blob)
            sprite_guid = None
            if sp:
                gm = re.search(r"guid: ([0-9a-fA-F]{32})", sp.group(1))
                if gm:
                    sprite_guid = gm.group(1)
            # find line index of m_Sprite within file
            sprite_line = None
            for k in range(i, j):
                if "m_Sprite:" in lines[k]:
                    sprite_line = k
                    break
            images[fid] = {
                "go": go_m.group(1) if go_m else None,
                "sprite_guid": sprite_guid,
                "line": sprite_line,
                "raw": sp.group(0) if sp else None,
            }

        i = j

    # transform_id -> go_id reverse via go components
    go_to_transform = {}
    for tid, t in transforms.items():
        if t["go"]:
            go_to_transform[t["go"]] = tid

    def path_of(go_id: str) -> str:
        parts = []
        seen = set()
        cur = go_id
        while cur and cur != "0" and cur not in seen:
            seen.add(cur)
            go = gos.get(cur)
            if go:
                parts.append(go["name"] or "?")
            tid = go_to_transform.get(cur)
            if not tid:
                break
            father_tid = transforms[tid]["father"]
            if father_tid == "0":
                break
            # father transform -> go
            cur = transforms.get(father_tid, {}).get("go")
        return "/".join(reversed(parts))

    rows = []
    for iid, im in images.items():
        go = im["go"]
        name = gos.get(go, {}).get("name", "?") if go else "?"
        p = path_of(go) if go else name
        rows.append(
            {
                "path": p,
                "name": name,
                "guid": im["sprite_guid"],
                "line": im["line"],
                "raw": im["raw"],
            }
        )
    return rows, lines


def main_audit(prefab_rel: str):
    guid_to_path, _ = load_cache()
    abs_p = os.path.join(ROOT, prefab_rel.replace("/", os.sep))
    rows, _ = parse_prefab(abs_p)
    print("====", prefab_rel)
    for r in rows:
        g = r["guid"]
        if g is None:
            status = "EMPTY"
        elif g in guid_to_path:
            status = "OK"
        else:
            status = "BAD"
        if status != "OK":
            print(f"  {status:5} {r['path']}")
            if g:
                print(f"         guid={g}")


if __name__ == "__main__":
    import sys

    prefs = sys.argv[1:] or [
        "Resources/Prefabs/Dialogue/DialogueUI.prefab",
        "Resources/Prefabs/Town/GuildHallUI.prefab",
        "Resources/Prefabs/Battle/EquipDropPopup.prefab",
    ]
    for p in prefs:
        main_audit(p)
