# -*- coding: utf-8 -*-
"""List Image m_Sprite refs in prefabs that point to missing GUIDs (with nearby m_Name)."""
import os
import re
import sys

ROOT = r"Y:\PixelAdventureTown\Assets"


def load_guid_map():
    guid_map = {}
    for dp, _, fs in os.walk(ROOT):
        for f in fs:
            if not f.endswith(".meta"):
                continue
            p = os.path.join(dp, f)
            try:
                t = open(p, encoding="utf-8-sig").read(500)
            except OSError:
                continue
            m = re.search(r"^guid:\s*(\S+)", t, re.M)
            if m:
                guid_map[m.group(1)] = p[:-5]
    return guid_map


def analyze(pref, guid_map):
    txt = open(pref, encoding="utf-8").read()
    lines = txt.splitlines()
    last_names = []
    results = []
    for i, l in enumerate(lines):
        if l.strip().startswith("m_Name:"):
            name = l.split(":", 1)[1].strip()
            last_names.append((i, name))
        if "m_Sprite:" not in l:
            continue
        m = re.search(r"guid:\s*([0-9a-fA-F]{32})", l)
        if not m:
            nm = last_names[-1][1] if last_names else "?"
            results.append(("EMPTY", nm, "", ""))
            continue
        g = m.group(1)
        name = "?"
        for li, nm in reversed(last_names):
            if i - li <= 60 and nm:
                name = nm
                break
        if g in guid_map:
            results.append(("OK", name, g, guid_map[g]))
        else:
            results.append(("BAD", name, g, "MISSING"))
    return results


def main():
    guid_map = load_guid_map()
    prefs = sys.argv[1:]
    if not prefs:
        prefs = []
        for dp, _, fs in os.walk(os.path.join(ROOT, "Resources", "Prefabs")):
            for f in fs:
                if f.endswith(".prefab"):
                    prefs.append(os.path.join(dp, f))
        for dp, _, fs in os.walk(os.path.join(ROOT, "Scenes")):
            for f in fs:
                if f.endswith(".unity"):
                    prefs.append(os.path.join(dp, f))

    bad_guids = {}
    for pref in prefs:
        rows = analyze(pref, guid_map)
        bads = [r for r in rows if r[0] == "BAD"]
        if not bads:
            continue
        rel = os.path.relpath(pref, ROOT)
        print("====", rel, "bad", len(bads))
        for mark, name, g, path in bads:
            print(f"  {name}\t{g}")
            bad_guids.setdefault(g, []).append(f"{rel}::{name}")

    print("\n=== UNIQUE BAD GUIDS", len(bad_guids), "===")
    for g, locs in sorted(bad_guids.items(), key=lambda x: -len(x[1]))[:50]:
        print(len(locs), g, "->", locs[0])


if __name__ == "__main__":
    main()
