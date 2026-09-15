#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""从 git 历史还原悬空 guid 引用（团结/Unity 工程专用）

原理
----
预制体里出现「本工程不存在的 guid」时，通常是因为 .meta 的 guid 被批量改写过
（本工程即 7e526d69「批量修复 581 个 .meta 的非法 base64 guid」）。
本脚本把 git 全历史里所有 .meta 版本的 (guid -> 路径) 建索引，
再用「幽灵 guid 历史上属于哪个 .meta」反推它今天对应的资源，
最后把引用方（prefab/unity/asset）里的旧 guid 改成该资源**现在的** guid。

安全性：只改引用方，不动 .meta，因此不会破坏任何现有有效引用。

用法
----
    python Tools/RestoreGuidsFromGit.py             # 预览
    python Tools/RestoreGuidsFromGit.py --write     # 落盘（先备份 .bak）
"""
import os
import re
import subprocess
import sys
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)
META_DIRS = ["Assets", "Packages", os.path.join("Library", "PackageCache")]
SCAN_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller",
            ".overrideController", ".anim")
GUID_RE = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")
HEX32 = re.compile(r"^[0-9a-f]{32}$")
B64 = re.compile(r"^[A-Za-z0-9+/]{16,}={0,2}$")
BUILTIN = re.compile(r"^0{8}[0-9a-f]{24}$")


def git(args, binary=False):
    p = subprocess.run(["git"] + args, cwd=PROJ, stdout=subprocess.PIPE,
                       stderr=subprocess.PIPE)
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
                try:
                    with open(p, encoding="utf-8", errors="ignore") as fh:
                        head = fh.read(4000)
                except OSError:
                    continue
                m = re.search(r"^guid:\s*(\S+)", head, re.M)
                if m:
                    idx[m.group(1)] = os.path.relpath(p[:-5], PROJ).replace("\\", "/")
    return idx


def valid(g):
    return bool(BUILTIN.match(g) or HEX32.match(g) or B64.match(g))


def collect_dangling(idx):
    bad = {}
    for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets")):
        for f in fn:
            if not f.endswith(SCAN_EXT):
                continue
            p = os.path.join(dp, f)
            rel = os.path.relpath(p, PROJ).replace("\\", "/")
            with open(p, encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
            for g in set(GUID_RE.findall(text)):
                if g in idx or not valid(g) or BUILTIN.match(g):
                    continue
                bad.setdefault(g, set()).add(rel)
    return bad


def build_history_guid_map(wanted):
    """git rev-list --objects --all -> 所有历史 .meta blob -> guid->路径"""
    print("扫描 git 历史对象（可能耗时数十秒）...")
    out = git(["rev-list", "--objects", "--all"], binary=True)
    shas = []
    for line in out.split(b"\n"):
        if not line:
            continue
        parts = line.split(b" ", 1)
        if len(parts) != 2:
            continue
        sha, path = parts
        if path.endswith(b".meta"):
            shas.append((sha.decode(), path.decode("utf-8", "ignore")))
    print("历史 .meta 对象: %d" % len(shas))
    if not shas:
        return {}

    hist = defaultdict(Counter)
    CHUNK = 2000
    for i in range(0, len(shas), CHUNK):
        chunk = shas[i:i + CHUNK]
        inp = b"".join(s.encode() + b"\n" for s, _p in chunk)
        p = subprocess.run(["git", "cat-file", "--batch"], cwd=PROJ,
                           input=inp, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        data = p.stdout
        pos = 0
        for sha, path in chunk:
            nl = data.find(b"\n", pos)
            if nl < 0:
                break
            header = data[pos:nl].decode("utf-8", "ignore").split()
            if len(header) < 3:
                pos = nl + 1
                continue
            size = int(header[2])
            body = data[nl + 1: nl + 1 + size]
            pos = nl + 1 + size + 1
            m = re.search(rb"^guid:\s*(\S+)", body, re.M)
            if m:
                g = m.group(1).decode("utf-8", "ignore")
                if g in wanted:
                    hist[g][path] += 1
        sys.stdout.write("\r  %d/%d" % (min(i + CHUNK, len(shas)), len(shas)))
        sys.stdout.flush()
    print()
    return hist


def main():
    do_write = "--write" in sys.argv
    idx = build_current_index()
    print("当前 .meta 索引: %d" % len(idx))
    bad = collect_dangling(idx)
    print("悬空 guid: %d" % len(bad))

    hist = build_history_guid_map(set(bad))

    plan = {}       # old_guid -> (new_guid, path)
    unresolved = []
    for g in bad:
        paths = hist.get(g)
        if not paths:
            unresolved.append(g)
            continue
        # 优先选当前仍存在的路径
        chosen = None
        for p, _c in paths.most_common():
            if os.path.isfile(os.path.join(PROJ, p)):
                chosen = p
                break
        if chosen is None:
            chosen = paths.most_common(1)[0][0]
        meta = os.path.join(PROJ, chosen + ".meta")
        if not os.path.isfile(meta):
            unresolved.append(g)
            continue
        with open(meta, encoding="utf-8", errors="ignore") as fh:
            m = re.search(r"^guid:\s*(\S+)", fh.read(4000), re.M)
        if not m or m.group(1) not in idx:
            unresolved.append(g)
            continue
        plan[g] = (m.group(1), chosen)

    print("可还原: %d   无法溯源: %d" % (len(plan), len(unresolved)))
    print("-" * 72)
    for g, (ng, p) in sorted(plan.items(), key=lambda kv: kv[1][1]):
        refs = sorted(bad[g])
        print("[OK] %s\n     -> %s\n        新 guid %s\n        引用者 %s"
              % (g, p, ng, ", ".join(os.path.basename(r) for r in refs[:3])))
    if unresolved:
        print("-" * 72)
        for g in sorted(unresolved):
            print("[??] %s  引用者 %s" % (g, ", ".join(
                os.path.basename(r) for r in sorted(bad[g])[:3])))

    if not do_write:
        print("\n（预览模式。确认无误后加 --write 执行）")
        return 0

    changed = 0
    for rel in sorted(set(r for refs in bad.values() for r in refs)):
        p = os.path.join(PROJ, rel)
        if not os.path.isfile(p):
            continue
        with open(p, encoding="utf-8", errors="ignore") as fh:
            text = fh.read()
        orig = text
        for g, (ng, _path) in plan.items():
            if g in text:
                text = text.replace("guid: %s" % g, "guid: %s" % ng)
        if text == orig:
            continue
        with open(p + ".bak", "w", encoding="utf-8", newline="") as fh:
            fh.write(orig)
        with open(p, "w", encoding="utf-8", newline="") as fh:
            fh.write(text)
        changed += 1
        print("已改写 %s" % rel)
    print("\n完成：%d 个文件（原文件备份为 .bak）" % changed)
    print("回到 Unity 触发刷新后逐页目视复核。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
