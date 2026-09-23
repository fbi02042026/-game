# -*- coding: utf-8 -*-
"""只读统计 E 盘回收站里「今天 19:50 之后」被删的工程文件（规模 / 类型 / 目录分布）。"""
import os, io, time, collections

BIN = r"E:\$RECYCLE.BIN"
CUTOFF = time.mktime(time.strptime("2026-09-23 19:45:00", "%Y-%m-%d %H:%M:%S"))

def parse_orig_path(data):
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

rows = []
for root, dirs, files in os.walk(BIN):
    for fn in files:
        if not fn.startswith("$I"):
            continue
        p = os.path.join(root, fn)
        try:
            mt = os.path.getmtime(p)
            if mt < CUTOFF:
                continue
            data = io.open(p, "rb").read(4096)
        except Exception:
            continue
        orig = parse_orig_path(data)
        if not orig:
            continue
        if "xiangsumaoxian" not in orig:
            continue
        rows.append((mt, orig))

rows.sort()
print("19:45 之后删除的工程文件总数:", len(rows))
if rows:
    print("时间范围:", time.strftime("%H:%M:%S", time.localtime(rows[0][0])), "→",
          time.strftime("%H:%M:%S", time.localtime(rows[-1][0])))
print()
print("=== 按扩展名 ===")
for ext, c in collections.Counter(os.path.splitext(r[1])[1].lower() for r in rows).most_common(12):
    print(f"  {c:6d}  {ext or '(无扩展)'}")
print()
print("=== 按目录（前 12） ===")
for d, c in collections.Counter(os.path.dirname(r[1]) for r in rows).most_common(12):
    print(f"  {c:6d}  {d}")
print()
print("=== 按分钟 ===")
for m, c in collections.Counter(time.strftime("%H:%M", time.localtime(r[0])) for r in rows).most_common(10):
    print(f"  {c:6d}  {m}")

print()
print("=== 工程内顶层目录 ===")
rel = [r[1].split("xiangsumaoxian\\", 1)[-1] for r in rows]
for k, c in collections.Counter(x.split("\\")[0] for x in rel).most_common(10):
    print(f"  {c:6d}  {k}")

print()
print("=== 非 Assets 的文件样例（前 10） ===")
other = [x for x in rel if not x.startswith("Assets\\")]
for x in other[:10]:
    print("   ", x)
print("非 Assets 总数:", len(other))
