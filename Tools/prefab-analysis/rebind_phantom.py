#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""幽灵 guid 溯源 + 自动重绑（只读预览 / --write 落盘）

背景：预制体/场景里引用了本工程不存在的 guid（幽灵 guid），导致 Image.sprite 悬空变白块。
本脚本利用 Tools/_guid_cache.json（旧工程 Y:\\PixelAdventureTown 的 guid->路径 快照）
把幽灵 guid 反查成相对路径，再在**当前工程**里找同路径资源，取其现行 guid 做重绑。

用法：
    python Tools/prefab-analysis/rebind_phantom.py              # 预览，不写盘
    python Tools/prefab-analysis/rebind_phantom.py --write      # 落盘（自动备份 .bak）
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CACHE = os.path.join(ROOT, "Tools", "_guid_cache.json")
OLD_ROOT_MARK = "PixelAdventureTown"

META_DIRS = ["Assets", "Packages", os.path.join("Library", "PackageCache")]
SCAN_EXT = (".prefab", ".unity", ".asset", ".mat", ".anim",
            ".controller", ".overrideController", ".playable")
GUID_RE = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")
HEX32 = re.compile(r"^[0-9a-f]{32}$")
B64 = re.compile(r"^[A-Za-z0-9+/]{16,}={0,2}$")
BUILTIN = re.compile(r"^0{8}[0-9a-f]{24}$")


def build_index():
    idx = {}
    for base in META_DIRS:
        abs_base = os.path.join(ROOT, base)
        if not os.path.isdir(abs_base):
            continue
        for dp, _dn, fn in os.walk(abs_base):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                try:
                    with open(p, encoding="utf-8", errors="ignore") as fh:
                        head = fh.read(4000)
                except OSError:
                    continue
                m = re.search(r"^guid:\s*(\S+)", head, re.M)
                if m:
                    idx[m.group(1)] = p[:-5]
    return idx


def is_valid_guid(g):
    if BUILTIN.match(g):
        return True
    if HEX32.match(g):
        return True
    return bool(B64.match(g))


def collect_dangling(idx):
    dangling = {}
    assets = os.path.join(ROOT, "Assets")
    for dp, _dn, fn in os.walk(assets):
        for f in fn:
            if not f.endswith(SCAN_EXT):
                continue
            p = os.path.join(dp, f)
            try:
                with open(p, encoding="utf-8", errors="ignore") as fh:
                    text = fh.read()
            except OSError:
                continue
            found = set(GUID_RE.findall(text))
            for g in found:
                if g in idx:
                    continue
                if not is_valid_guid(g):
                    continue
                dangling.setdefault(g, set()).add(os.path.relpath(p, ROOT))
    return dangling


def load_cache():
    if not os.path.isfile(CACHE):
        return {}, {}
    with open(CACHE, encoding="utf-8") as fh:
        d = json.load(fh)
    return d.get("guid_to_path", {}), d.get("path_to_guid", {})


def to_rel(old_abs):
    """Y:\\PixelAdventureTown\\Assets\\x.png -> Assets/x.png"""
    if not old_abs:
        return None
    s = old_abs.replace("\\", "/")
    if OLD_ROOT_MARK not in s:
        return None
    tail = s.split(OLD_ROOT_MARK, 1)[1]
    tail = tail.lstrip("/")
    # 去掉可能的盘符残留
    return tail


def main():
    do_write = "--write" in sys.argv
    idx = build_index()
    print("当前工程 .meta 索引: %d" % len(idx))

    dangling = collect_dangling(idx)
    print("悬空 guid: %d" % len(dangling))

    g2p, _p2g = load_cache()
    print("历史缓存: %d 条" % len(g2p))

    plan = {}   # old_guid -> (new_guid, rel_path)
    unknown = {}
    for g in dangling:
        old_abs = g2p.get(g)
        rel = to_rel(old_abs)
        if not rel:
            unknown[g] = dangling[g]
            continue
        target = os.path.join(ROOT, rel)
        meta = target + ".meta"
        if os.path.isfile(meta):
            with open(meta, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid:\s*(\S+)", fh.read(4000), re.M)
            if m and m.group(1) in idx:
                plan[g] = (m.group(1), rel)
                continue
        unknown[g] = dangling[g]

    print("可自动重绑: %d   无法溯源: %d" % (len(plan), len(unknown)))
    print("-" * 70)
    for g, (ng, rel) in sorted(plan.items(), key=lambda kv: kv[1][1]):
        refs = sorted(dangling[g])
        print("[OK ] %s\n     -> %s  (新 guid %s)\n     引用者: %s"
              % (g, rel, ng, ", ".join(os.path.basename(r) for r in refs[:4])))
    print("-" * 70)
    for g in sorted(unknown):
        refs = sorted(unknown[g])
        print("[?? ] %s  引用者: %s" % (g, ", ".join(os.path.basename(r) for r in refs[:4])))

    if not do_write:
        print("\n（预览模式，未写盘。加 --write 执行重绑）")
        return 0

    # 落盘
    changed = 0
    for root_d, _dn, fn in os.walk(os.path.join(ROOT, "Assets")):
        for f in fn:
            if not f.endswith(SCAN_EXT):
                continue
            p = os.path.join(root_d, f)
            with open(p, encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
            orig = text
            for g, (ng, _rel) in plan.items():
                if g in text:
                    text = text.replace("guid: " + g, "guid: " + ng)
            if text == orig:
                continue
            with open(p + ".bak", "w", encoding="utf-8", newline="") as fh:
                fh.write(orig)
            with open(p, "w", encoding="utf-8", newline="") as fh:
                fh.write(text)
            changed += 1
            print("已重绑 %s" % os.path.relpath(p, ROOT))
    print("\n完成，改写 %d 个文件（原文件已备份为 .bak）" % changed)
    print("请在 Unity 里回到工程触发一次刷新，然后逐页目视复核。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
