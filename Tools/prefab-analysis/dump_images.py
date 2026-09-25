# -*- coding: utf-8 -*-
"""列出预制体里每个带 Image 的节点，并把 sprite guid 解析成真实文件名。

用法:
    dump_images.py <prefab> [文件名关键字]

为什么要有它:
    「底图不对 / 是换了图还是几何错位 / 这个节点到底用的哪张图」这类问题，
    只看 guid 判断不了；dump_sprites.py 只给 guid 和节点名。
    本工具顺手把 guid → 文件名反查出来（遍历 Assets/**/*.meta 建表），
    并打印「节点名 <- 父节点名  图片文件名」，一眼定位哪一格用了哪张底图。

注意: 本机 bash 的 PATH 缺 /usr/bin；正则别走内联 node -e（转义会坏），一律写成 .py 跑。
"""
import io
import os
import re
import sys

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)")
GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
FID_RE = re.compile(r"fileID: (\d+)")
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"   # UnityEngine.UI.Image


def build_guid_map(assets_root):
    m = {}
    for root, _dirs, files in os.walk(assets_root):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            try:
                with io.open(os.path.join(root, fn), "r", encoding="utf-8", errors="replace") as f:
                    head = f.read(400)
            except Exception:
                continue
            g = GUID_RE.search(head)
            if g:
                m[g.group(1)] = fn[:-5]
    return m


def parse(path):
    lines = io.open(path, "r", encoding="utf-8", errors="replace").read().splitlines()
    docs, cur = [], None
    for ln in lines:
        mm = DOC_RE.match(ln)
        if mm:
            if cur:
                docs.append(cur)
            cur = {"type": int(mm.group(1)), "id": mm.group(2), "ln": []}
            continue
        if cur is not None:
            cur["ln"].append(ln)
    if cur:
        docs.append(cur)
    return docs


def mk_field():
    cache = {}

    def field(doc, name):
        key = name
        pat = cache.get(key)
        if pat is None:
            pat = re.compile(r"^\s*" + re.escape(name) + r": (.*)$")
            cache[key] = pat
        for ln in doc["ln"]:
            mm = pat.match(ln)
            if mm:
                return mm.group(1).strip()
        return None

    return field


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return
    prefab = sys.argv[1]
    keyword = sys.argv[2] if len(sys.argv) > 2 else ""

    here = os.path.dirname(os.path.abspath(prefab))
    assets_root = None
    probe = here
    for _ in range(8):
        if os.path.basename(probe) == "Assets":
            assets_root = probe
            break
        probe = os.path.dirname(probe)
    if assets_root is None:
        assets_root = "E:/xiangsumaoxian/Assets"
    guid_map = build_guid_map(assets_root)

    field = mk_field()
    docs = parse(prefab)
    by_id = {d["id"]: d for d in docs}
    name_of = {}
    for d in docs:
        if d["type"] == 1:
            name_of[d["id"]] = field(d, "m_Name") or ""

    # 子 → 父 节点名
    parent_of = {}
    for d in docs:
        if d["type"] not in (224, 4):
            continue
        gof = field(d, "m_GameObject")
        if not gof:
            continue
        go = FID_RE.search(gof)
        if not go:
            continue
        fid = field(d, "m_Father")
        if not fid:
            continue
        pf = FID_RE.search(fid)
        if not pf or pf.group(1) == "0":
            continue
        pdoc = by_id.get(pf.group(1))
        if not pdoc:
            continue
        pgo = field(pdoc, "m_GameObject")
        pgn = FID_RE.search(pgo or "")
        if pgn:
            parent_of[go.group(1)] = name_of.get(pgn.group(1), "?")

    rows = []
    for d in docs:
        if d["type"] != 114:
            continue
        scr = field(d, "m_Script") or ""
        if IMAGE_GUID not in scr:
            continue
        gof = field(d, "m_GameObject") or ""
        go = FID_RE.search(gof)
        gid = go.group(1) if go else ""
        nodename = name_of.get(gid, "?")

        sprline = field(d, "m_Sprite") or ""
        gm = re.search(r"guid: ([0-9a-f]{32})", sprline)
        spr = guid_map.get(gm.group(1), "?(guid " + gm.group(1) + ")") if gm else "(无图 fileID:0)"
        rows.append((nodename, parent_of.get(gid, "-"), spr))

    if keyword:
        rows = [r for r in rows if keyword in r[2]]

    print("命中 %d 个 Image 节点（关键字=%r，guid 表 %d 条）" % (len(rows), keyword, len(guid_map)))
    for n, p, s in rows:
        print("  %-24s <- %-22s %s" % (n, p, s))


if __name__ == "__main__":
    main()
