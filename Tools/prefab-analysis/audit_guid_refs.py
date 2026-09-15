# -*- coding: utf-8 -*-
"""扫描工程：找出所有 .meta 的 guid 合法性，以及所有对不存在 guid 的悬空引用。
用法：python audit_guid_refs.py
输出：META 统计、非法 .meta、悬空引用分组（= 丢失的图片/预制体/脚本）。
"""
import os, re, sys
from collections import defaultdict

ROOT = r"E:\xiangsumaoxian"
META_DIRS = [os.path.join(ROOT, "Assets"),
             os.path.join(ROOT, "Packages"),
             os.path.join(ROOT, "Library", "PackageCache")]
REF_DIRS = [os.path.join(ROOT, "Assets")]

OWN_GUID = re.compile(rb"^guid:[ \t]*([^\r\n]+)", re.M)
HEX32 = re.compile(r"^[0-9a-f]{32}$")
REF_ANY = re.compile(rb"guid:[ \t]*([^\r\n,}\s]+)")
BUILTIN = re.compile(r"^0{16}[0-9a-f]{16}$")

meta_guids = {}
bad_metas = []
for base in META_DIRS:
    if not os.path.isdir(base):
        continue
    for dp, dn, fn in os.walk(base):
        for f in fn:
            if not f.endswith(".meta"):
                continue
            p = os.path.join(dp, f)
            try:
                data = open(p, "rb").read()
            except Exception:
                continue
            m = OWN_GUID.search(data)
            if not m:
                bad_metas.append((p, "NO_GUID"))
                continue
            g = m.group(1).strip().decode("ascii", "replace")
            if HEX32.match(g):
                meta_guids[g] = p
            else:
                bad_metas.append((p, g))

print("META valid=%d  BAD=%d" % (len(meta_guids), len(bad_metas)))
for p, g in bad_metas[:80]:
    print("  BAD  %-50s %s" % (g[:48], os.path.relpath(p, ROOT)))
if len(bad_metas) > 80:
    print("  ... 还有 %d 个" % (len(bad_metas) - 80))

dangling = defaultdict(set)
ref_total = 0
for base in REF_DIRS:
    for dp, dn, fn in os.walk(base):
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
            if b"guid:" not in data:
                continue
            for m in REF_ANY.finditer(data):
                g = m.group(1).decode("ascii", "replace")
                if len(g) != 32:
                    continue
                ref_total += 1
                if BUILTIN.match(g):
                    continue
                if g not in meta_guids:
                    dangling[p].add(g)

print("REF hex-refs=%d  files_with_dangling=%d" % (ref_total, len(dangling)))
tot = 0
for p in sorted(dangling, key=lambda k: -len(dangling[k])):
    gs = sorted(dangling[p])
    tot += len(gs)
    print("  %-60s x%d" % (os.path.relpath(p, ROOT), len(gs)))
    for g in gs[:30]:
        print("      %s" % g)
print("TOTAL dangling uniq=%d" % tot)
