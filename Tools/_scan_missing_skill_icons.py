# -*- coding: utf-8 -*-
"""只读扫描：技能图标是否齐全，列出缺失项给主人补图。
只扫「图标文件」这类真正写文件名的列；表头跳过 # 注释行。
用法: python Tools/_scan_missing_skill_icons.py
"""
import os, csv, io, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TABLES = os.path.join(ROOT, "Assets", "Data", "Source", "Tables")
RES = os.path.join(ROOT, "Assets", "Resources")
SKILL_ASSETS = os.path.join(RES, "Config", "Skills")

# Resources 下所有 png 的 stem -> 相对路径
icon_index = {}
for dirpath, dirnames, filenames in os.walk(RES):
    for fn in filenames:
        if fn.lower().endswith(".png"):
            stem = os.path.splitext(fn)[0]
            rel = os.path.relpath(os.path.join(dirpath, fn), ROOT)
            icon_index.setdefault(stem, []).append(rel.replace("\\", "/"))

def load_table(path):
    with io.open(path, "r", encoding="utf-8-sig", newline="") as f:
        rows = list(csv.reader(f))
    # 跳过 # 注释行，第一行非注释即表头
    hi = 0
    while hi < len(rows) and (not rows[hi] or not rows[hi][0].strip() or rows[hi][0].strip().startswith("#")):
        hi += 1
    if hi >= len(rows):
        return None, []
    return rows[hi], rows[hi + 1:]

missing = []

def check_table(fname):
    p = os.path.join(TABLES, fname)
    if not os.path.exists(p):
        return
    header, body = load_table(p)
    if header is None:
        return
    icon_cols = [i for i, h in enumerate(header) if h and ("图标文件" in h or h.strip().lower() == "icon")]
    id_col = next((i for i, h in enumerate(header) if h and "技能ID" in h), 0)
    name_col = next((i for i, h in enumerate(header) if h and "技能名称" in h), None)
    if not icon_cols:
        print("[info] %s: 无「图标文件」列（表头=%s）" % (fname, header[:8]))
        return
    print("=" * 72)
    print("表 %s  图标列=%s" % (fname, [header[i] for i in icon_cols]))
    n = 0
    for r in body:
        if not r or r[0].strip().startswith("#") or all(c.strip() == "" for c in r):
            continue
        for i in icon_cols:
            if i >= len(r):
                continue
            val = r[i].strip()
            if not val or val in ("-", "—", "0"):
                continue
            stem = os.path.splitext(os.path.basename(val))[0]
            if stem not in icon_index:
                n += 1
                missing.append((fname, r[id_col] if id_col < len(r) else "?",
                                r[name_col] if (name_col is not None and name_col < len(r)) else "",
                                val))
    print("  缺失 %d 条" % n)

# 技能 SO（玩家技能走 Config/Skills/Ally/*.asset）
def check_assets():
    if not os.path.isdir(SKILL_ASSETS):
        return
    print("=" * 72)
    print("扫描技能 SO:", os.path.relpath(SKILL_ASSETS, ROOT))
    n = 0
    for dirpath, _, filenames in os.walk(SKILL_ASSETS):
        for fn in filenames:
            if not fn.endswith(".asset"):
                continue
            fp = os.path.join(dirpath, fn)
            with io.open(fp, "r", encoding="utf-8", errors="ignore") as f:
                txt = f.read()
            m = re.search(r"^\s*icon(?:Name)?:\s*(\S+)", txt, re.M)
            if not m:
                continue
            val = m.group(1).strip()
            if not val or val in ("0", ""):
                continue
            stem = os.path.splitext(os.path.basename(val))[0]
            if stem not in icon_index:
                n += 1
                missing.append(("Skills SO", fn, "", val))
    print("  缺失 %d 条" % n)

for t in ["merc_skills.csv", "player_skills.csv", "merc_skill_map.csv", "skill_defs.csv",
          "player_skill_defs.csv", "skills.csv"]:
    check_table(t)
check_assets()

print("\n\n############ 缺失图标清单（给主人补图用） ############")
if not missing:
    print("（没有缺失，图标齐全）")
else:
    print("%-14s %-10s %-18s %s" % ("来源", "技能ID", "技能名", "表里写的图标"))
    for src, sid, nm, val in missing:
        print("%-14s %-10s %-18s %s" % (src, sid, nm, val))
    print("\n共 %d 条缺失" % len(missing))

# 顺带报告：技能图标实际都放在哪（给主人参考路径）
sample = [v for k, v in icon_index.items() if k.upper().startswith("SK0")]
if sample:
    print("\n参考：已存在的 SK0xx 图标目录示例 ->", sample[0][0])
