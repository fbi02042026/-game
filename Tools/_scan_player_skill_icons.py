# -*- coding: utf-8 -*-
"""只读：玩家技能图标 = Resources.Load("Icons/SkillIcon/" + skillId)
按 player_skills.csv 的 id 逐个核对 Assets/Resources/Icons/SkillIcon/<id>.png 是否存在。
"""
import os, csv, io

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSV = os.path.join(ROOT, "Assets", "Data", "Source", "Tables", "player_skills.csv")
DIR = os.path.join(ROOT, "Assets", "Resources", "Icons", "SkillIcon")

have = set()
if os.path.isdir(DIR):
    for fn in os.listdir(DIR):
        if fn.lower().endswith(".png"):
            have.add(os.path.splitext(fn)[0])
else:
    print("[warn] 目录不存在:", DIR)

with io.open(CSV, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.reader(f))
hi = 0
while hi < len(rows) and (not rows[hi] or rows[hi][0].strip().startswith("#")):
    hi += 1
header = rows[hi]
body = rows[hi + 1:]
id_i = header.index("id")
name_i = header.index("displayName") if "displayName" in header else None

print("目录 Assets/Resources/Icons/SkillIcon 现有图标数:", len(have))
print("表头:", header)
print("-" * 60)
miss = []
for r in body:
    if not r or r[0].strip().startswith("#") or all(c.strip() == "" for c in r):
        continue
    sid = r[id_i].strip()
    nm = r[name_i].strip() if name_i is not None and name_i < len(r) else ""
    ok = sid in have
    if not ok:
        miss.append((sid, nm))
    print("%-6s %-14s %s" % (sid, nm, "OK" if ok else "*** 缺图 ***"))

print("\n######## 需要补的玩家技能图标 ########")
if not miss:
    print("（齐全）")
else:
    for sid, nm in miss:
        print("  缺: %s  (%s)  -> 应放 Assets/Resources/Icons/SkillIcon/%s.png" % (sid, nm, sid))
    print("共 %d 个" % len(miss))
