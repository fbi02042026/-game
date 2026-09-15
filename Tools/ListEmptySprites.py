#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""列出「sprite 被清空」的具体节点层级路径。

为什么按"节点路径"而不是按 guid？
    Unity 会把悬空引用清成 `m_Sprite: {fileID: 0}`，此时原始 guid 永久丢失，
    按 guid 已经无从匹配。但**节点层级路径是稳定的**（Unity 不会改节点名和父子关系）。
    因此本工具输出节点路径，作为建立「永久映射表」的锚点：
        PlayerJobSelect/JobCard0/Portrait => Assets/Art/xxx.png
    以后无论被清空多少次，按路径回填即可一键恢复。

用法:
    python Tools/ListEmptySprites.py                       # 全部游戏本体
    python Tools/ListEmptySprites.py --file PlayerJobSelect # 只看某个文件
    python Tools/ListEmptySprites.py --json out.json        # 导出映射表骨架
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)

SKIP_DIRS = ("/SPUM/", "Epic Toon FX", "Pixel Craft VFX", "Hyperbit",
             "AssetStoreTools", "/Demo/", "/Demos/")
GAME_ONLY = ("/Resources/Prefabs/", "/Scenes/")

EMPTY = "m_Sprite: {fileID: 0}"


def unescape(s):
    """Unity YAML 把非 ASCII 写成 \\uXXXX，解码后再输出中文节点名"""
    return re.sub(r"\\u([0-9a-fA-F]{4})",
                  lambda m: chr(int(m.group(1), 16)), s)


def parse_docs(text):
    parts = re.split(r"^---\s+!u!(\d+)\s+&(-?\d+)", text, flags=re.M)
    out = []
    for i in range(1, len(parts), 3):
        out.append((int(parts[i]), int(parts[i + 1]), parts[i + 2]))
    return out


def analyze(path):
    """返回 [节点路径]（该节点上的 Image.sprite 为空）"""
    with open(path, encoding="utf-8", errors="ignore") as fh:
        text = fh.read()
    docs = parse_docs(text)

    go_name = {}          # GameObject fileID -> m_Name
    tr_of_go = {}         # GameObject fileID -> Transform fileID
    father = {}           # Transform fileID -> 父 Transform fileID
    go_of_tr = {}         # Transform fileID -> GameObject fileID

    for cid, fid, body in docs:
        if cid == 1:      # GameObject
            m = re.search(r"^  m_Name:\s*(.*)$", body, re.M)
            go_name[fid] = (unescape(m.group(1).strip().strip('"')) if m else "?")
        elif cid == 4 or cid == 224:   # Transform / RectTransform
            m = re.search(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body, re.M)
            if m:
                tr_of_go[int(m.group(1))] = fid
                go_of_tr[fid] = int(m.group(1))
            f = re.search(r"^  m_Father:\s*\{fileID:\s*(-?\d+)", body, re.M)
            father[fid] = int(f.group(1)) if f else 0

    def path_of(go_fid):
        tr = tr_of_go.get(go_fid)
        if tr is None:
            return [go_name.get(go_fid, "?")]
        chain = []
        cur = tr
        seen = set()
        while cur and cur not in seen:
            seen.add(cur)
            g = go_of_tr.get(cur)
            if g is None:
                break
            chain.append(go_name.get(g, "?"))
            cur = father.get(cur, 0)
        return list(reversed(chain))

    out = []
    for cid, fid, body in docs:
        if EMPTY not in body:
            continue
        m = re.search(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body, re.M)
        if not m:
            continue
        out.append("/".join(path_of(int(m.group(1)))))
    return out


def main():
    only = None
    jsout = None
    args = sys.argv[1:]
    if "--file" in args:
        only = args[args.index("--file") + 1]
    if "--json" in args:
        jsout = args[args.index("--json") + 1]

    targets = []
    for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets")):
        for f in fn:
            if not f.endswith((".prefab", ".unity")):
                continue
            p = os.path.join(dp, f).replace("\\", "/")
            rel = os.path.relpath(p, PROJ).replace("\\", "/")
            if any(s in rel for s in SKIP_DIRS):
                continue
            if not any(s in rel for s in GAME_ONLY):
                continue
            targets.append((p, rel))

    total = 0
    result = {}
    for p, rel in sorted(targets):
        if only and only not in rel:
            continue
        paths = analyze(p)
        if not paths:
            continue
        total += len(paths)
        result[rel] = paths
        print("=" * 68)
        print("%s   （%d 处空 sprite）" % (rel, len(paths)))
        for np_ in paths:
            print("   %s" % np_)

    print("\n合计 %d 处空 sprite" % total)

    if jsout:
        skel = {rel: {n: "" for n in sorted(set(v))} for rel, v in result.items()}
        with open(jsout, "w", encoding="utf-8") as fh:
            json.dump(skel, fh, ensure_ascii=False, indent=2)
        print("映射表骨架已导出: %s" % jsout)
    return 0


if __name__ == "__main__":
    sys.exit(main())
