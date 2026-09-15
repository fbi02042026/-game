#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""检查「表/配置里的资源路径」在 Assets/Resources 下是否真实存在。

很多"图片丢失"并非 guid 悬空，而是运行时 Resources.Load(path) 找不到文件
（预制体里没有静态引用，因此 guid 检查发现不了）。

用法:
    python Tools/CheckResourcePaths.py            # 只报缺失
    python Tools/CheckResourcePaths.py --all      # 连存在的也列出
"""
import csv
import os
import re
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)
RES = os.path.join(PROJ, "Assets", "Resources")
ART = os.path.join(PROJ, "Assets")

TABLE_DIRS = [
    os.path.join(PROJ, "Assets", "Data", "Source", "Tables"),
    os.path.join(PROJ, "Assets", "Resources", "Data", "Tables"),
]

# 看起来像资源路径的单元格
PATH_LIKE = re.compile(r"^[A-Za-z0-9_\u4e00-\u9fff][A-Za-z0-9_\u4e00-\u9fff/\- ]*$")
KEY_HINT = ("icon", "portrait", "sprite", "image", "art", "avatar", "head", "illust")


def resource_exists(rel):
    """Resources.Load 的路径：既可能是 Resources/xxx.png 也可能是图集子资源"""
    if not rel:
        return False
    rel = rel.strip().replace("\\", "/")
    cands = [
        os.path.join(RES, rel),
        os.path.join(RES, rel + ".png"),
        os.path.join(RES, rel + ".jpg"),
        os.path.join(RES, rel + ".prefab"),
        os.path.join(RES, rel + ".asset"),
    ]
    for c in cands:
        if os.path.isfile(c) or os.path.isfile(c + ".meta"):
            return True
    return False


def build_sprite_name_index():
    """图集子资源名 -> 图集文件（读 .meta 的 spriteSheet 段）"""
    idx = {}
    for dp, _dn, fn in os.walk(RES):
        for f in fn:
            if not f.endswith(".png.meta") and not f.endswith(".jpg.meta"):
                continue
            p = os.path.join(dp, f)
            with open(p, encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
            for m in re.finditer(r"^\s+name:\s*(.+)$", text, re.M):
                idx[m.group(1).strip()] = os.path.relpath(p, PROJ).replace("\\", "/")
    return idx


SPRITE_NAMES = None


def scan_csv(path):
    """返回 [(行号, 列名, 值)]"""
    out = []
    try:
        with open(path, encoding="utf-8-sig", errors="ignore", newline="") as fh:
            sample = fh.read(4096)
            fh.seek(0)
            delim = "\t" if sample.count("\t") > sample.count(",") else ","
            rd = csv.reader(fh, delimiter=delim)
            rows = list(rd)
    except OSError:
        return out
    if not rows:
        return out
    header = rows[0]
    for ri, row in enumerate(rows[1:], start=2):
        if len(row) != len(header):
            continue
        for ci, col in enumerate(header):
            val = row[ci].strip()
            if not val or len(val) > 120:
                continue
            low = col.lower()
            if not any(k in low for k in KEY_HINT):
                continue
            if not PATH_LIKE.match(val):
                continue
            out.append((ri, col, val))
    return out


def main():
    global SPRITE_NAMES
    SPRITE_NAMES = build_sprite_name_index()
    print("图集子资源名索引: %d" % len(SPRITE_NAMES))
    show_all = "--all" in sys.argv
    total = 0
    missing = 0
    by_table = {}
    for td in TABLE_DIRS:
        if not os.path.isdir(td):
            continue
        for f in sorted(os.listdir(td)):
            if not f.lower().endswith((".csv", ".bytes")):
                continue
            p = os.path.join(td, f)
            items = scan_csv(p)
            if not items:
                continue
            def is_missing(v):
                if "/" in v:
                    return not resource_exists(v)
                return v not in SPRITE_NAMES

            bad = [(ri, col, v) for ri, col, v in items if is_missing(v)]
            total += len(items)
            missing += len(bad)
            if bad:
                by_table[f] = bad
            if show_all:
                print("%-34s 检查 %d，缺失 %d" % (f, len(items), len(bad)))

    print("=" * 66)
    print("表内资源路径检查：共 %d 条，缺失 %d 条" % (total, missing))
    print("=" * 66)
    for f, bad in by_table.items():
        print("\n【%s】缺失 %d" % (f, len(bad)))
        seen = set()
        for ri, col, v in bad:
            key = (col, v)
            if key in seen:
                continue
            seen.add(key)
            print("   行%-5s %-16s %s" % (ri, col, v))
    if not by_table:
        print("（无缺失）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
