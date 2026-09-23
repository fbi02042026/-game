# -*- coding: utf-8 -*-
"""只读扫描 E 盘回收站里被删的 .prefab，还原其原始路径（用于定位丢失的美术预制体）。"""
import os, io, struct, time, collections

BIN = r"E:\$RECYCLE.BIN"
hits = []
counts = collections.Counter()

def parse_orig_path(data):
    # $I 文件：Win10 v2 头部后紧跟 UTF-16LE 原始路径
    for off in (0x1C, 0x14, 0x18):
        if off + 4 > len(data):
            continue
        try:
            raw = data[off:]
            end = raw.find(b"\x00\x00")
            if end <= 0:
                continue
            s = raw[:end + 1].decode("utf-16-le", errors="ignore").strip("\x00")
            if ":" in s and "\\" in s:
                return s
        except Exception:
            pass
    return ""

for root, dirs, files in os.walk(BIN):
    for fn in files:
        if not fn.lower().endswith(".prefab"):
            continue
        if not fn.startswith("$I"):
            continue
        p = os.path.join(root, fn)
        try:
            data = io.open(p, "rb").read()
            mt = os.path.getmtime(p)
        except Exception:
            continue
        orig = parse_orig_path(data)
        if not orig:
            continue
        counts[orig.split("\\")[-1]] += 1
        hits.append((mt, orig, p, fn))

hits.sort(reverse=True)
print("回收站 prefab 总数（可解析路径）:", len(hits))
print("=== 最近 20 条 ===")
for mt, orig, p, fn in hits[:20]:
    print(time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(mt)), "|", orig)

print()
print("=== 文件名含 Daily / 登录 的 ===")
for mt, orig, p, fn in hits:
    low = orig.lower()
    if "daily" in low or "登录" in orig:
        print(time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(mt)), "|", orig, "|", fn)

print()
print("=== 今天(2026-09-23)删除的 prefab 计数 ===")
today = [h for h in hits if time.strftime("%Y-%m-%d", time.localtime(h[0])) == "2026-09-23"]
print("今天删除:", len(today))
dirs = collections.Counter(os.path.dirname(h[1]) for h in today)
for d, c in dirs.most_common(15):
    print(f"  {c:4d}  {d}")
