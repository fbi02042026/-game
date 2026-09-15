#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""为幽灵 sprite 引用推荐候选图片（同一 prefab 内正常引用的目录 + 节点名匹配）

只读，输出 Tools/phantom-rebind-candidates.txt
"""
import os
import re
import sys
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)
META_DIRS = ["Assets", "Packages", os.path.join("Library", "PackageCache")]
SCAN_EXT = (".prefab", ".unity")
GUID_RE = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")
HEX32 = re.compile(r"^[0-9a-f]{32}$")
B64 = re.compile(r"^[A-Za-z0-9+/]{16,}={0,2}$")
BUILTIN = re.compile(r"^0{8}[0-9a-f]{24}$")

# 游戏本体 UI（排除第三方 demo）
GAME_HINT = ("Battle", "Town", "Common", "Story", "Main", "battle", "冒险", "装备", "引导")


def build_index():
    idx = {}
    spr = {}
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
                        head = fh.read(8000)
                except OSError:
                    continue
                m = re.search(r"^guid:\s*(\S+)", head, re.M)
                if m:
                    idx[m.group(1)] = p[:-5]
                    if "textureType: 8" in head or "spriteMode" in head:
                        spr[m.group(1)] = p[:-5]
    return idx, spr


def valid(g):
    return bool(BUILTIN.match(g) or HEX32.match(g) or B64.match(g))


def parse_docs(text):
    parts = re.split(r"^---\s+!u!(\d+)\s+&(\d+)", text, flags=re.M)
    out = []
    for i in range(1, len(parts), 3):
        out.append((int(parts[i]), int(parts[i + 1]), parts[i + 2]))
    return out


def main():
    idx, spr = build_index()
    print("索引 %d，其中 sprite %d" % (len(idx), len(spr)))

    # 按文件名建 sprite 索引
    by_name = defaultdict(list)
    for g, p in spr.items():
        if not p.startswith(os.path.join(PROJ, "Assets") + os.sep):
            continue
        rel = os.path.relpath(p, PROJ).replace("\\", "/")
        name = os.path.splitext(os.path.basename(p))[0]
        by_name[name].append((g, rel))

    targets = []
    for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets")):
        for f in fn:
            if f.endswith(SCAN_EXT):
                targets.append(os.path.join(dp, f))

    out_lines = []
    for p in sorted(targets):
        rel = os.path.relpath(p, PROJ)
        if not any(h in rel for h in GAME_HINT):
            continue
        with open(p, encoding="utf-8", errors="ignore") as fh:
            text = fh.read()
        docs = parse_docs(text)
        name_of = {}
        for cid, fid, body in docs:
            if cid == 1:
                m = re.search(r"^  m_Name:\s*(.*)$", body, re.M)
                if m:
                    name_of[fid] = m.group(1).strip().strip('"')

        # 该文件内所有 guid
        all_g = set(GUID_RE.findall(text))
        ok_dirs = Counter()
        for g in all_g:
            if g in idx:
                rp = os.path.relpath(idx[g], PROJ).replace("\\", "/")
                if "/Art/" in rp or "/Sprite" in rp or "/Textures/" in rp:
                    ok_dirs[os.path.dirname(rp)] += 1
        bad = [g for g in all_g if g not in idx and valid(g) and not BUILTIN.match(g)]
        if not bad:
            continue

        # 悬空引用的宿主节点名
        node_of_bad = defaultdict(set)
        for cid, fid, body in docs:
            gm_obj = re.search(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body, re.M)
            node = name_of.get(int(gm_obj.group(1)), "") if gm_obj else ""
            for line in body.splitlines():
                m = re.search(r"guid:\s*([0-9a-zA-Z+/=]{16,})", line)
                if not m:
                    continue
                g = m.group(1)
                if g in bad and "m_Sprite" in line:
                    node_of_bad[g].add(node)

        out_lines.append("=" * 70)
        out_lines.append("文件: %s" % rel)
        out_lines.append("  正常引用的图片目录: %s" % (
            ", ".join("%s(x%d)" % (d, c) for d, c in ok_dirs.most_common(4)) or "(无)"))
        top_dirs = [d for d, _c in ok_dirs.most_common(4)]
        for g in sorted(bad):
            nodes = sorted(x for x in node_of_bad.get(g, []) if x)
            out_lines.append("  - 悬空 %s   节点: %s" % (g, ", ".join(nodes) if nodes else "?"))
            # 候选：目录内同名 / 全库同名
            cands = []
            for n in nodes or []:
                nn = n.strip()
                for cand_g, cand_rel in by_name.get(nn, []):
                    cands.append((cand_rel, "同名", cand_g))
                # 目录内包含匹配
                for cand_g, cand_rel in by_name.items():
                    pass
            # 目录优先候选
            for d in top_dirs:
                for name, lst in by_name.items():
                    for cand_g, cand_rel in lst:
                        if os.path.dirname(cand_rel) == d and any(
                                nn and (nn in name or name in nn) for nn in nodes):
                            cands.append((cand_rel, "同目录+名近", cand_g))
            seen = set()
            for cand_rel, why, cand_g in cands:
                if cand_rel in seen:
                    continue
                seen.add(cand_rel)
                out_lines.append("      候选[%s] %s  (guid %s)" % (why, cand_rel, cand_g))
            if not cands:
                out_lines.append("      (无候选)")

    out = os.path.join(ROOT, "phantom-rebind-candidates.txt")
    with open(out, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out_lines) + "\n")
    print("候选报告: %s" % out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
