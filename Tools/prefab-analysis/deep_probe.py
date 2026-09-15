# -*- coding: utf-8 -*-
"""深探：本批悬空 guid 是否已知(_guid_cache)、对应预制体节点上下文。"""
import os, re, json
from collections import defaultdict

R = r"E:\xiangsumaoxian"
A = os.path.join(R, "Assets")
OWN = re.compile(rb"^guid:[ \t]*([^\r\n]+)", re.M)
HEX32 = re.compile(r"^[0-9a-f]{32}$")
REF = re.compile(rb"guid:[ \t]*([0-9a-f]{32})")

# 1) 当前所有 meta guid
meta = set()
for dp, dn, fn in os.walk(A):
    for f in fn:
        if f.endswith(".meta"):
            d = open(os.path.join(dp, f), "rb").read(400)
            m = OWN.search(d)
            if m:
                g = m.group(1).strip().decode("ascii", "replace")
                if HEX32.match(g):
                    meta.add(g)

# 2) _guid_cache.json 反转
cache = {}
cp = os.path.join(R, "Tools", "_guid_cache.json")
if os.path.isfile(cp):
    c = json.load(open(cp, encoding="utf-8"))
    p2g = c.get("path_to_guid", c)
    for path, g in p2g.items():
        if isinstance(g, str):
            cache.setdefault(g, path)
print("_guid_cache.json entries=%d  unique_guid=%d" % (len(p2g), len(cache)))

# 3) 只查游戏本体目标
targets = ["Resources\\Prefabs", "Scenes"]
dan = defaultdict(set)
for t in targets:
    for dp, dn, fn in os.walk(os.path.join(A, t)):
        for f in fn:
            if f.endswith(".meta"):
                continue
            p = os.path.join(dp, f)
            if not f.lower().endswith((".prefab", ".unity", ".asset")):
                continue
            data = open(p, "rb").read()
            for m in REF.finditer(data):
                g = m.group(1).decode()
                if g not in meta:
                    dan[p].add(g)

print("\n游戏本体悬空引用：")
known = 0
for p in sorted(dan):
    print(" -", os.path.relpath(p, R))
    for g in sorted(dan[p]):
        src = cache.get(g, "")
        tag = ("OK在cache: " + src) if src else "PHANTOM(未在任何meta/cache出现)"
        print("     %s  %s" % (g, tag))
        if src:
            known += 1
print("合计悬空 %d，其中 %d 能在 _guid_cache 找到对应资源" % (
    sum(len(v) for v in dan.values()), known))
