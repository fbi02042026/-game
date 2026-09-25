# -*- coding: utf-8 -*-
"""检查已删除的 Editor 菜单脚本是否仍被工程里其它 .cs 引用（类名级别）。"""
import os, re

ARCHIVE = r"E:\xiangsumaoxian\Tools\_menu_archive_2026-09-25"

# 收集归档里的类名
names = set()
for root, dirs, files in os.walk(ARCHIVE):
    for f in files:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        try:
            s = open(p, "r", encoding="utf-8", errors="ignore").read()
        except Exception:
            continue
        for m in re.finditer(r"\b(?:public\s+|internal\s+|static\s+)*(?:class|struct|enum|interface)\s+(\w+)", s):
            names.add(m.group(1))

print("归档类名数量:", len(names))

# 扫描工程内所有 .cs（排除归档目录自身）
hits = {}
for base in [r"E:\xiangsumaoxian\Assets"]:
    for root, dirs, files in os.walk(base):
        for f in files:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(root, f)
            try:
                s = open(p, "r", encoding="utf-8", errors="ignore").read()
            except Exception:
                continue
            for n in names:
                # 词边界匹配，避免误伤
                if re.search(r"\b" + n + r"\b", s):
                    hits.setdefault(n, []).append(os.path.relpath(p, r"E:\xiangsumaoxian"))

if not hits:
    print("OK: 工程内没有任何 .cs 引用已删除的类。")
else:
    print("!! 仍被引用的类:")
    for n in sorted(hits):
        print("  -", n, "->", sorted(set(hits[n])))
