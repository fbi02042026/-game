#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""从 git HEAD 版本收集「悬空 guid 清单」，供去源工程（Y 盘）差量拷贝。

为什么必须用 git HEAD 而不是工作区？
    Unity 会把悬空引用清成 `m_Sprite: {fileID: 0}`，工作区里原始 guid 已经永久丢失。
    而 git HEAD 版本还没被 Unity 碰过，原始 guid 仍在。
    要回源工程按 guid 取资源，就必须以 HEAD 版本为准。

输出：
    Tools/缺失资源guid清单.json   {guid: [引用方相对路径]}
    Tools/缺失资源guid清单.txt    可读报告
"""
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)
OUT_JSON = os.path.join(ROOT, "缺失资源guid清单.json")
OUT_TXT = os.path.join(ROOT, "缺失资源guid清单.txt")

META_DIRS = ["Assets", "Packages", os.path.join("Library", "PackageCache")]
REF_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller",
           ".overrideController", ".anim")
GUID_RE = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")
HEX32 = re.compile(r"^[0-9a-f]{32}$")
B64 = re.compile(r"^[A-Za-z0-9+/]{16,}={0,2}$")
BUILTIN = re.compile(r"^0{8}[0-9a-f]{24}$")
META_GUID = re.compile(rb"^guid:\s*(\S+)", re.M)

# 只关心游戏本体，第三方素材包的悬空不管
SKIP = ("/SPUM/", "Epic Toon FX", "Pixel Craft VFX", "Hyperbit",
        "AssetStoreTools", "/Demo/", "/Demos/", "All Shaders.unity")


def git(args, binary=False):
    p = subprocess.run(["git"] + args, cwd=PROJ,
                       stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    return p.stdout if binary else p.stdout.decode("utf-8", "ignore")


def build_current_index():
    idx = {}
    for base in META_DIRS:
        abs_base = os.path.join(PROJ, base)
        if not os.path.isdir(abs_base):
            continue
        for dp, _dn, fn in os.walk(abs_base):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                with open(p, encoding="utf-8", errors="ignore") as fh:
                    m = re.search(r"^guid:\s*(\S+)", fh.read(4000), re.M)
                if m:
                    idx[m.group(1)] = os.path.relpath(p[:-5], PROJ).replace("\\", "/")
    return idx


def cat_batch(shas):
    out = {}
    CHUNK = 1500
    for i in range(0, len(shas), CHUNK):
        chunk = shas[i:i + CHUNK]
        inp = b"".join(s.encode() + b"\n" for s in chunk)
        p = subprocess.run(["git", "cat-file", "--batch"], cwd=PROJ,
                           input=inp, stdout=subprocess.PIPE)
        data = p.stdout
        pos = 0
        for sha in chunk:
            nl = data.find(b"\n", pos)
            if nl < 0:
                break
            h = data[pos:nl].decode("utf-8", "ignore").split()
            if len(h) < 3:
                pos = nl + 1
                continue
            size = int(h[2])
            out[sha] = data[nl + 1: nl + 1 + size]
            pos = nl + 1 + size + 1
    return out


def main():
    commit = sys.argv[1] if len(sys.argv) > 1 else "HEAD"
    idx = build_current_index()
    print("当前 .meta 索引: %d" % len(idx))

    tree = git(["ls-tree", "-r", "-z", commit])
    refs = []
    for entry in tree.split("\0"):
        if not entry:
            continue
        try:
            meta, path = entry.split("\t", 1)
        except ValueError:
            continue
        if path.endswith(REF_EXT) and not any(s in path for s in SKIP):
            refs.append((meta.split()[2], path))
    print("扫描引用文件: %d（commit=%s）" % (len(refs), commit))

    blobs = cat_batch([s for s, _ in refs])

    bad = {}
    for sha, path in refs:
        body = blobs.get(sha)
        if not body:
            continue
        for g in set(GUID_RE.findall(body.decode("utf-8", "ignore"))):
            if g in idx or BUILTIN.match(g):
                continue
            if not (HEX32.match(g) or B64.match(g)):
                continue
            bad.setdefault(g, set()).add(path)

    print("悬空 guid: %d" % len(bad))

    data = {g: sorted(v) for g, v in sorted(bad.items())}
    with open(OUT_JSON, "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, indent=2)

    with open(OUT_TXT, "w", encoding="utf-8") as fh:
        fh.write("缺失资源 guid 清单（基于 %s，游戏本体）\n" % commit)
        fh.write("=" * 66 + "\n")
        fh.write("共 %d 个。用法：把本文件与 %s 一起带到源工程\n"
                 % (len(bad), os.path.basename(OUT_JSON)))
        fh.write("（原 Y:\\PixelAdventureTown），跑 FetchFromSourceProject.py 取资源。\n\n")
        for g, paths in data.items():
            fh.write("%s\n" % g)
            for p in paths[:4]:
                fh.write("      %s\n" % p)
            if len(paths) > 4:
                fh.write("      ...另 %d 处\n" % (len(paths) - 4))
    print("已输出:")
    print("  %s" % OUT_JSON)
    print("  %s" % OUT_TXT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
