# -*- coding: utf-8 -*-
import re, os, io, json

ROOT = r"E:\xiangsumaoxian"
PREFAB = os.path.join(ROOT, "Assets", "Resources", "Prefabs", "Battle", "BattleUI.prefab")
CS = os.path.join(ROOT, "Assets", "Scripts", "UI", "BattleUI.cs")

# 1) guid of BattleUI.cs
meta = io.open(CS + ".meta", encoding="utf-8", errors="ignore").read()
guid = re.search(r"guid:\s*([0-9a-fA-F]{32})", meta).group(1)

txt = io.open(PREFAB, encoding="utf-8", errors="ignore").read()
docs = re.split(r"(?m)^--- ", txt)

# 2) find the BattleUI MonoBehaviour doc
body = None
for d in docs:
    if "m_Script: {fileID: 11500000, guid: %s" % guid in d:
        body = d
        break

if body is None:
    print("!! BattleUI component NOT FOUND in prefab")
else:
    lines = body.split("\n")
    print("=== BattleUI component serialized fields ===")
    for i, ln in enumerate(lines):
        if "fileID:" in ln and not ln.strip().startswith("m_Script"):
            key = ln.split(":")[0].strip()
            fid = re.search(r"fileID:\s*(-?\d+)", ln)
            v = fid.group(1) if fid else "?"
            if v == "0":
                print("  [EMPTY] %s" % key)
            else:
                print("  [bound] %s -> &%s" % (key, v))
        elif re.match(r"^  [a-zA-Z_].*:\s*[-0-9]", ln) and "fileID" not in ln:
            pass

# 3) declared fields in BattleUI.cs
print("\n=== declared in BattleUI.cs ===")
src = io.open(CS, encoding="utf-8", errors="ignore").read()
for m in re.finditer(r"(?m)^\s*(?:\[SerializeField\]\s*)?public\s+([\w<>\[\]]+)\s+([\w]+)\s*[;=]", src):
    print("  %s %s" % (m.group(1), m.group(2)))
