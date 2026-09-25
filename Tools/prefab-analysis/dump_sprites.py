# -*- coding: utf-8 -*-
"""打印预制体里指定节点的 Image sprite / color / type 引用。
用法: dump_sprites.py <prefab> [节点名1 节点名2 ...]
用途: 排查「底框不对 / 白框」——确认预制体里到底有没有挂图、挂的是哪张。
"""
import sys
import re
import io

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)")


def parse(path):
    with io.open(path, "r", encoding="utf-8", errors="replace") as f:
        lines = f.read().splitlines()
    docs = []
    cur = None
    for ln in lines:
        m = DOC_RE.match(ln)
        if m:
            if cur:
                docs.append(cur)
            cur = {"type": int(m.group(1)), "id": m.group(2), "ln": []}
            continue
        if cur is not None:
            cur["ln"].append(ln)
    if cur:
        docs.append(cur)
    return docs


def field(doc, name):
    pat = re.compile(r"^\s*" + re.escape(name) + r": (.*)$")
    for ln in doc["ln"]:
        m = pat.match(ln)
        if m:
            return m.group(1).strip()
    return None


def main():
    if len(sys.argv) < 2:
        print("用法: dump_sprites.py <prefab> [节点名...]")
        return
    path = sys.argv[1]
    want = sys.argv[2:]
    docs = parse(path)
    by_id = {d["id"]: d for d in docs}
    name_of = {}
    comps_of = {}
    for d in docs:
        if d["type"] != 1:
            continue
        gid = d["id"]
        name_of[gid] = field(d, "m_Name") or ""
        comps = []
        in_list = False
        for ln in d["ln"]:
            if re.match(r"^\s*m_Component:", ln):
                in_list = True
                continue
            if in_list:
                m = re.match(r"^\s*- component: \{fileID: (-?\d+)\}", ln)
                if m:
                    comps.append(m.group(1))
                    continue
                if re.match(r"^\s*m_", ln):
                    in_list = False
        comps_of[gid] = comps

    if not want:
        want = [n for n in name_of.values() if n]

    print("总 GameObject = %d" % len(name_of))
    for t in want:
        gid = None
        for k, v in name_of.items():
            if v == t:
                gid = k
                break
        if gid is None:
            print("%-16s (未找到)" % t)
            continue
        info = []
        for cid in comps_of[gid]:
            d = by_id.get(cid)
            if not d:
                continue
            s = field(d, "m_Sprite")
            if s is None:
                continue
            info.append("sprite=" + s)
            for k in ("m_Color", "m_Type", "m_PreserveAspect", "m_FillMethod", "m_RaycastTarget"):
                v = field(d, k)
                if v:
                    info.append("%s=%s" % (k.replace("m_", ""), v))
        print("%-16s comps=%d  %s" % (t, len(comps_of[gid]), " | ".join(info) if info else "(无 Image)"))


if __name__ == "__main__":
    main()
