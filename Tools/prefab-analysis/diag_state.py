# -*- coding: utf-8 -*-
"""状态诊断：base64 引用、Tools 资产、box.prefab 存在性、历史 .meta guid 全集。"""
import os, re, subprocess

R = r"E:\xiangsumaoxian"
A = os.path.join(R, "Assets")
B64REF = re.compile(rb"guid:[ \t]*([A-Za-z0-9+/]{40,}=)")
HEXREF = re.compile(rb"guid:[ \t]*([0-9a-f]{32})")

b64_files = {}
for dp, dn, fn in os.walk(A):
    for f in fn:
        if f.endswith(".meta"):
            continue
        p = os.path.join(dp, f)
        try:
            if os.path.getsize(p) > 8_000_000:
                continue
            data = open(p, "rb").read()
        except Exception:
            continue
        for m in B64REF.finditer(data):
            g = m.group(1).decode("ascii", "replace")
            b64_files.setdefault(g, set()).add(os.path.relpath(p, R))
print("== 当前工作区 base64 形式引用: %d 个不同 guid，分布在 %d 个文件 ==" % (
    len(b64_files), len(set(x for s in b64_files.values() for x in s))))
for g, fs in list(b64_files.items())[:20]:
    print("   %s  <- %d files e.g. %s" % (g[:24] + "...", len(fs), list(fs)[0]))

print("\n== Tools/ 目录 ==")
tid = os.path.join(R, "Tools")
for f in sorted(os.listdir(tid)):
    fp = os.path.join(tid, f)
    tag = "DIR " if os.path.isdir(fp) else "%6d" % os.path.getsize(fp)
    print("   %s  %s" % (tag, f))

print("\n== box.prefab 全局搜索 ==")
found = 0
for dp, dn, fn in os.walk(A):
    for f in fn:
        if f.lower() == "box.prefab" or (f.lower().startswith("box") and f.endswith(".prefab")):
            print("   ", os.path.relpath(os.path.join(dp, f), R)); found += 1
print("   found=%d" % found)

print("\n== 历史 .meta guid 全集（git log -p 解析）==")
p = subprocess.run(["git", "-C", R, "log", "--all", "-p", "--format=@@%H", "--", "*.meta"],
                   capture_output=True, encoding="utf-8", errors="replace")
cur = None
hist = {}
for line in p.stdout.splitlines():
    if line.startswith("@@"):
        cur = None
    elif line.startswith("+++ b/"):
        cur = line[6:].strip()
    elif line.startswith("+guid:") and cur:
        g = line[6:].strip()
        if re.match(r"^[0-9a-f]{32}$", g):
            hist.setdefault(g, set()).add(cur)
print("   历史合法 hex guid 数: %d" % len(hist))
import json
json.dump({k: sorted(v) for k, v in hist.items()},
          open(os.path.join(tid, "prefab-analysis", "_hist_guids.json"), "w", encoding="utf-8"))
print("   已落盘 Tools/prefab-analysis/_hist_guids.json")
