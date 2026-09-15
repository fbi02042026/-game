#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""幽灵 sprite 引用诊断器：悬空 guid -> 宿主节点名 -> 候选图片

用于定位「预制体/场景里 m_Sprite 指向本工程不存在的资源」这类白块问题。
不做任何写操作，只输出报告。

输出：Tools/phantom-sprite-report.txt
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)
META_DIRS = ["Assets", "Packages", os.path.join("Library", "PackageCache")]
SCAN_EXT = (".prefab", ".unity", ".asset", ".mat", ".anim",
            ".controller", ".overrideController")
GUID_RE = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")
HEX32 = re.compile(r"^[0-9a-f]{32}$")
B64 = re.compile(r"^[A-Za-z0-9+/]{16,}={0,2}$")
BUILTIN = re.compile(r"^0{8}[0-9a-f]{24}$")

# UGUI Image 脚本 guid（团结/Unity 通用）
IMAGE_SCRIPT = "fe87c0e1cc204ed48ad3b37840f39efc"


def build_index():
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
                    idx[m.group(1)] = p[:-5]
    return idx


def valid(g):
    return bool(BUILTIN.match(g) or HEX32.match(g) or B64.match(g))


def parse_yaml_docs(text):
    """返回 [(classid, fileID, body)]"""
    docs = []
    parts = re.split(r"^---\s+!u!(\d+)\s+&(\d+)", text, flags=re.M)
    # parts[0] = 文件头, 之后 3 个一组
    for i in range(1, len(parts), 3):
        cid, fid, body = parts[i], parts[i + 1], parts[i + 2]
        docs.append((int(cid), int(fid), body))
    return docs


def main():
    idx = build_index()
    print("当前工程 .meta 索引: %d" % len(idx))

    rows = []  # (file, guid, node_name, prop)
    files = 0
    for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets")):
        for f in fn:
            if not f.endswith(SCAN_EXT):
                continue
            p = os.path.join(dp, f)
            rel = os.path.relpath(p, PROJ)
            with open(p, encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
            docs = parse_yaml_docs(text)
            # fileID -> GameObject 名
            name_of = {}
            for cid, fid, body in docs:
                if cid == 1:
                    m = re.search(r"^  m_Name:\s*(.*)$", body, re.M)
                    if m:
                        name_of[fid] = m.group(1).strip()
            # 扫描悬空引用
            for cid, fid, body in docs:
                for line in body.splitlines():
                    gm = re.search(r"guid:\s*([0-9a-zA-Z+/=]{16,})", line)
                    if not gm:
                        continue
                    g = gm.group(1)
                    if g in idx or not valid(g) or BUILTIN.match(g):
                        continue
                    key = line.strip().split(":")[0]
                    gm_obj = re.search(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body, re.M)
                    node = ""
                    if gm_obj:
                        node = name_of.get(int(gm_obj.group(1)), "?")
                    rows.append((rel, g, node, key))
                    files += 1

    # 汇总
    by_guid = {}
    for rel, g, node, key in rows:
        by_guid.setdefault(g, {"nodes": set(), "files": set(), "keys": set()})
        by_guid[g]["nodes"].add(node)
        by_guid[g]["files"].add(os.path.basename(rel))
        by_guid[g]["keys"].add(key)

    out = os.path.join(ROOT, "phantom-sprite-report.txt")
    with open(out, "w", encoding="utf-8") as fh:
        fh.write("幽灵 guid 引用报告（悬空总数 %d 处，去重 guid %d 个）\n" % (len(rows), len(by_guid)))
        fh.write("=" * 70 + "\n\n")
        for g, info in sorted(by_guid.items(), key=lambda kv: sorted(kv[1]["files"])[0]):
            nodes = sorted(x for x in info["nodes"] if x)
            fh.write("guid %s\n" % g)
            fh.write("  属性: %s\n" % ", ".join(sorted(info["keys"])))
            fh.write("  宿主节点: %s\n" % (", ".join(nodes) if nodes else "(未知)"))
            fh.write("  所在文件: %s\n\n" % ", ".join(sorted(info["files"])))
    print("悬空引用 %d 处 / %d 个去重 guid" % (len(rows), len(by_guid)))
    print("报告已写入: %s" % out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
