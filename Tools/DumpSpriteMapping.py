#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""导出 prefab 中「节点路径 → 图片」的真实映射（人工修好后用它固化成果）。

流程
----
1. 在 Unity 里把缺图的节点**手动拖上正确的 sprite** 并保存；
2. 跑 `python Tools/DumpSpriteMapping.py --file PlayerJobSelect`；
3. 得到 `Tools/sprite-rebind.json`，之后 `ApplySpriteRebind.py --write` 可随时重放。

这样映射来自**你的真实选择**，而不是脚本猜测（猜测已被证明必错）。

用法
----
    python Tools/DumpSpriteMapping.py                          # 全部游戏本体
    python Tools/DumpSpriteMapping.py --file PlayerJobSelect   # 只看某个文件
    python Tools/DumpSpriteMapping.py --all-sprites            # 含未丢失的节点
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)
OUT = os.path.join(ROOT, "sprite-rebind.json")

SKIP_DIRS = ("/SPUM/", "Epic Toon FX", "Pixel Craft VFX", "Hyperbit",
             "AssetStoreTools", "/Demo/", "/Demos/")
GAME_ONLY = ("/Resources/Prefabs/", "/Scenes/")
GUID_RE = re.compile(r"m_Sprite: \{fileID: (-?\d+), guid: ([0-9a-zA-Z+/=]{16,}), type: 3\}")


def unescape(s):
    return re.sub(r"\\u([0-9a-fA-F]{4})",
                  lambda m: chr(int(m.group(1), 16)), s)


def parse_docs(text):
    parts = re.split(r"^---\s+!u!(\d+)\s+&(-?\d+)", text, flags=re.M)
    out = []
    for i in range(1, len(parts), 3):
        out.append((int(parts[i]), int(parts[i + 1]), parts[i + 2]))
    return out


def build_index():
    idx = {}
    for base in ["Assets", "Packages", os.path.join("Library", "PackageCache")]:
        abs_base = os.path.join(PROJ, base)
        if not os.path.isdir(abs_base):
            continue
        for dp, _dn, fn in os.walk(abs_base):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                with open(p, encoding="utf-8", errors="ignore") as fh:
                    m = re.search(r"^guid:\s*(\S+)", fh.read(6000), re.M)
                if m:
                    idx[m.group(1)] = os.path.relpath(p[:-5], PROJ).replace("\\", "/")
    return idx


def analyze(path, idx, all_sprites):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        text = fh.read()
    docs = parse_docs(text)

    go_name, tr_of_go, go_of_tr, father = {}, {}, {}, {}
    for cid, fid, body in docs:
        if cid == 1:
            m = re.search(r"^  m_Name:\s*(.*)$", body, re.M)
            go_name[fid] = (unescape(m.group(1).strip().strip('"')) if m else "?")
        elif cid in (4, 224):
            m = re.search(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body, re.M)
            if m:
                tr_of_go[int(m.group(1))] = fid
                go_of_tr[fid] = int(m.group(1))
            f = re.search(r"^  m_Father:\s*\{fileID:\s*(-?\d+)", body, re.M)
            father[fid] = int(f.group(1)) if f else 0

    def path_of(go_fid):
        tr = tr_of_go.get(go_fid)
        chain, cur, seen = [], tr, set()
        while cur and cur not in seen:
            seen.add(cur)
            g = go_of_tr.get(cur)
            if g is None:
                break
            chain.append(go_name.get(g, "?"))
            cur = father.get(cur, 0)
        return "/".join(reversed(chain)) if chain else go_name.get(go_fid, "?")

    out = {}
    for cid, fid, body in docs:
        m = GUID_RE.search(body)
        if not m:
            continue
        gm = re.search(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body, re.M)
        if not gm:
            continue
        npath = path_of(int(gm.group(1)))
        guid = m.group(2)
        img = idx.get(guid, "")
        if not all_sprites and img:
            # 只关心"曾经丢失、现在已被人工修好"的：记录全部更实用，这里保留
            pass
        if img:
            out[npath] = img
    return out


def main():
    only = None
    if "--file" in sys.argv:
        only = sys.argv[sys.argv.index("--file") + 1]
    all_sprites = "--all-sprites" in sys.argv

    idx = build_index()
    print("图片索引: %d" % len(idx))

    result = {}
    for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets")):
        for f in fn:
            if not f.endswith(".prefab"):
                continue
            p = os.path.join(dp, f).replace("\\", "/")
            rel = os.path.relpath(p, PROJ).replace("\\", "/")
            if any(s in rel for s in SKIP_DIRS) or not any(s in rel for s in GAME_ONLY):
                continue
            if only and only not in rel:
                continue
            m = analyze(p, idx, all_sprites)
            if m:
                result[rel] = m
                print("%-58s %d 处" % (rel, len(m)))

    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(result, fh, ensure_ascii=False, indent=2)
    print("\n映射表已写入: %s" % OUT)
    print("之后可用 ApplySpriteRebind.py --write 随时重放。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
