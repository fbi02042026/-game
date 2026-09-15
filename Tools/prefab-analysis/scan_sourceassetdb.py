# -*- coding: utf-8 -*-
"""在 Library/SourceAssetDB 里按二进制 guid 反查资源路径（抢救幽灵引用）"""
import os
import re
import sys

PROJ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DB = os.path.join(PROJ, "Library", "SourceAssetDB")
CACHE = os.path.join(PROJ, "Tools", "_guid_cache.json")


def load_phantoms():
    """复用诊断器逻辑：收集悬空 guid"""
    sys.path.insert(0, os.path.join(PROJ, "Tools"))
    idx = {}
    for base in ["Assets", "Packages", os.path.join("Library", "PackageCache")]:
        abs_base = os.path.join(PROJ, base)
        if not os.path.isdir(abs_base):
            continue
        for dp, _dn, fn in os.walk(abs_base):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                with open(os.path.join(dp, f), encoding="utf-8", errors="ignore") as fh:
                    m = re.search(r"^guid:\s*(\S+)", fh.read(4000), re.M)
                if m:
                    idx[m.group(1)] = True
    bad = set()
    GUID = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")
    for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets")):
        for f in fn:
            if not f.endswith((".prefab", ".unity", ".asset", ".mat", ".controller")):
                continue
            with open(os.path.join(dp, f), encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
            for g in set(GUID.findall(text)):
                if g in idx or not re.match(r"^[0-9a-f]{32}$", g):
                    continue
                if re.match(r"^0{8}[0-9a-f]{24}$", g):
                    continue
                bad.add(g)
    return bad


def main():
    bad = load_phantoms()
    print("幽灵 guid: %d" % len(bad))
    if not os.path.isfile(DB):
        print("无 SourceAssetDB")
        return 1
    with open(DB, "rb") as fh:
        blob = fh.read()
    print("DB 大小 %.1f MB" % (len(blob) / 1048576.0))

    hit = 0
    results = []
    for g in sorted(bad):
        raw = bytes.fromhex(g)
        pos = blob.find(raw)
        if pos < 0:
            continue
        # 取周围 600 字节里的 ASCII/UTF8 路径
        seg = blob[max(0, pos - 200): pos + 600]
        pat = re.compile(
            rb"Assets[\\/][ -~]{3,120}\.(?:png|jpg|jpeg|tga|psd|mat|prefab|asset|controller|anim|shader|ttf|otf)")
        paths = pat.findall(seg)
        # 中文路径：放宽字节范围再试
        pat2 = re.compile(
            rb"Assets[\\/](?:[\x20-\xff]{1,3}){2,60}\.(?:png|jpg|jpeg|tga|psd|mat|prefab|asset|controller|anim|shader|ttf|otf)")
        paths += pat2.findall(seg)
        dec = []
        for p in paths:
            try:
                s = p.decode("utf-8")
            except UnicodeDecodeError:
                try:
                    s = p.decode("gbk")
                except UnicodeDecodeError:
                    continue
            dec.append(s.replace("\\", "/"))
        if dec:
            hit += 1
            results.append((g, sorted(set(dec))[:3]))
    print("DB 命中: %d / %d" % (hit, len(bad)))
    for g, ps in results[:60]:
        print("  %s -> %s" % (g, " | ".join(ps)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
