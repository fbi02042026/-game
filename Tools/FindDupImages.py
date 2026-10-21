# -*- coding: utf-8 -*-
"""扫描全项目重复图片（按 MD5 内容比对），只读不写。
输出: .workbuddy/backup/20261021-dup-images/dup_report.json / dup_report.txt
"""
import os, hashlib, json, re, sys

ROOT = r"Y:\PixelAdventureTown"
OUT_DIR = os.path.join(ROOT, ".workbuddy", "backup", "20261021-dup-images")
os.makedirs(OUT_DIR, exist_ok=True)

SKIP_DIRS = {"Library", "Temp", "Logs", "obj", ".git", ".workbuddy", "Builds",
             "2022.3.62t12", "2022.3.62t16", "node_modules", "APK", "mem-log"}
IMG_EXT = {".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".gif", ".tif", ".tiff", ".exr"}

def md5_of(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()

def read_meta(path):
    mp = path + ".meta"
    info = {"guid": None, "guid_kind": "NO_META", "spriteMode": None}
    if not os.path.exists(mp):
        return info
    try:
        with open(mp, "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                if info["guid"] is None:
                    m = re.match(r"\s*guid:\s*(\S+)", line)
                    if m:
                        g = m.group(1)
                        info["guid"] = g
                        if re.fullmatch(r"[0-9a-f]{32}", g):
                            info["guid_kind"] = "hex32"
                        else:
                            info["guid_kind"] = "len%d" % len(g)
                if info["spriteMode"] is None:
                    m = re.match(r"\s*spriteMode:\s*(\d+)", line)
                    if m:
                        info["spriteMode"] = int(m.group(1))
                if info["guid"] and info["spriteMode"] is not None:
                    break
    except OSError:
        pass
    return info

# 1. collect images
files = []
for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
    for fn in filenames:
        if os.path.splitext(fn)[1].lower() in IMG_EXT:
            p = os.path.join(dirpath, fn)
            try:
                files.append((p, os.path.getsize(p)))
            except OSError:
                pass

# 2. group by size then md5
by_size = {}
for p, sz in files:
    by_size.setdefault(sz, []).append(p)

hash_map = {}
for sz, paths in by_size.items():
    if len(paths) < 2:
        continue
    for p in paths:
        try:
            hash_map.setdefault(md5_of(p), []).append(p)
        except OSError:
            pass

dup_groups = {h: ps for h, ps in hash_map.items() if len(ps) > 1}

# 3. read meta info for dup members
groups = []
all_guids = {}
for h, ps in sorted(dup_groups.items(), key=lambda kv: -os.path.getsize(kv[1][0])):
    members = []
    for p in ps:
        meta = read_meta(p)
        members.append({"path": p.replace(ROOT + os.sep, "").replace(os.sep, "/"),
                        "size": os.path.getsize(p),
                        "guid": meta["guid"], "guid_kind": meta["guid_kind"],
                        "spriteMode": meta["spriteMode"]})
        if meta["guid"] and re.fullmatch(r"[0-9a-f]{32}", meta["guid"]):
            all_guids[meta["guid"]] = p
    groups.append({"md5": h, "members": members})

# 4. reference scan (hex32 guids only) over text assets
TEXT_EXT = {".prefab", ".unity", ".asset", ".mat", ".controller", ".anim", ".cs", ".shader"}
ref_map = {g: set() for g in all_guids}
for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, "Assets")):
    dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
    for fn in filenames:
        if os.path.splitext(fn)[1].lower() in TEXT_EXT:
            p = os.path.join(dirpath, fn)
            try:
                with open(p, "r", encoding="utf-8", errors="replace") as f:
                    content = f.read()
            except OSError:
                continue
            for g in all_guids:
                if g in content:
                    ref_map[g].add(p.replace(ROOT + os.sep, "").replace(os.sep, "/"))

for grp in groups:
    for m in grp["members"]:
        m["refs"] = sorted(ref_map.get(m["guid"], [])) if m["guid_kind"] == "hex32" else []

# 5. write reports
with open(os.path.join(OUT_DIR, "dup_report.json"), "w", encoding="utf-8") as f:
    json.dump(groups, f, ensure_ascii=False, indent=1)

lines = []
total_waste = 0
lines.append("总图片数: %d  重复组数: %d" % (len(files), len(groups)))
lines.append("")
for i, grp in enumerate(groups, 1):
    sz = grp["members"][0]["size"]
    waste = sz * (len(grp["members"]) - 1)
    total_waste += waste
    lines.append("=== 组 %d | %d 张 x %d bytes (浪费 %d) | md5=%s ==="
                 % (i, len(grp["members"]), sz, waste, grp["md5"][:8]))
    for m in grp["members"]:
        lines.append("  [%s|%s] %s" % (m["guid_kind"],
                     "sp%d" % m["spriteMode"] if m["spriteMode"] is not None else "sp?",
                     m["path"]))
        lines.append("      guid=%s refs=%d" % (m["guid"], len(m["refs"])))
        for r in m["refs"][:10]:
            lines.append("        <- " + r)
        if len(m["refs"]) > 10:
            lines.append("        ... 共 %d 处" % len(m["refs"]))
    lines.append("")

lines.append("合计可省: %d bytes (%.1f KB)" % (total_waste, total_waste / 1024.0))
with open(os.path.join(OUT_DIR, "dup_report.txt"), "w", encoding="utf-8") as f:
    f.write("\n".join(lines))

print("groups=%d waste=%d" % (len(groups), total_waste))
