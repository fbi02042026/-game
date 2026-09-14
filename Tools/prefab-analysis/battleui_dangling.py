# -*- coding: utf-8 -*-
import re, os, io

ROOT = r"E:\xiangsumaoxian"
PREFAB = os.path.join(ROOT, "Assets", "Resources", "Prefabs", "Battle", "BattleUI.prefab")
CS = os.path.join(ROOT, "Assets", "Scripts", "UI", "BattleUI.cs")

meta = io.open(CS + ".meta", encoding="utf-8", errors="ignore").read()
guid = re.search(r"guid:\s*([0-9a-fA-F]{32})", meta).group(1)
txt = io.open(PREFAB, encoding="utf-8", errors="ignore").read()
docs = re.split(r"(?m)^--- ", txt)

anchors = set()
for d in docs:
    m = re.match(r"!u!\d+\s*&(-?\d+)", d.strip().split("\n", 1)[0])
    if m:
        anchors.add(m.group(1))

body = None
for d in docs:
    if "m_Script: {fileID: 11500000, guid: %s" % guid in d:
        body = d
        break

print("prefab anchors: %d" % len(anchors))
bad = []
for ln in body.split("\n"):
    if "fileID:" not in ln or ln.strip().startswith("m_Script"):
        continue
    key = ln.split(":")[0].strip().lstrip("- ").strip()
    fm = re.search(r"fileID:\s*(-?\d+)", ln)
    if not fm:
        continue
    v = fm.group(1)
    if v == "0":
        continue
    if v not in anchors:
        bad.append((key, v))

if bad:
    print("!! DANGLING (指向已删除的旧节点):")
    for k, v in bad:
        print("   %s -> &%s" % (k, v))
else:
    print("OK: 已绑定的引用全部指向现存节点")
