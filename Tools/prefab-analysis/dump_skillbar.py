# -*- coding: utf-8 -*-
"""Dump BattleUI prefab: 找出 SkillBar / skill 容器及其子节点（判断战斗中 4 技槽是否存在）。
注意：m_Children 在 Transform/RectTransform（类号 4 / 224）上，不在 GameObject（类号 1）上。
"""
import sys, re, io

path = sys.argv[1] if len(sys.argv) > 1 else r"Assets/Resources/Prefabs/Battle/BattleUI.prefab"
raw = io.open(path, encoding="utf-8").read()

docs = re.split(r"(?m)^--- !u!", raw)
objs = {}
order = []
for seg in docs:
    if not seg.strip():
        continue
    m = re.match(r"\s*(\d+)\s*&(\d+)", seg)
    if not m:
        continue
    objs[m.group(2)] = {"class": m.group(1), "body": seg}
    order.append(m.group(2))

def field(body, key):
    m = re.search(r"(?m)^\s*" + re.escape(key) + r":\s*(.*)$", body)
    return m.group(1).strip() if m else None

def gobj_name(fid):
    return field(objs.get(fid, {}).get("body", ""), "m_Name") or "(unnamed)"

def components(fid):
    b = objs.get(fid, {}).get("body", "")
    out = []
    for mm in re.finditer(r"fileID:\s*(-?\d+)", b.split("m_Component:")[1] if "m_Component:" in b else ""):
        out.append(mm.group(1))
    return out

def rect_of(fid):
    """返回该 GameObject 的 Transform/RectTransform fileID"""
    for c in components(fid):
        if objs.get(c, {}).get("class") in ("4", "224"):
            return c
    return None

def children(goid):
    r = rect_of(goid)
    if r is None:
        return []
    b = objs[r]["body"]
    m = re.search(r"(?m)^\s*m_Children:\s*$", b)
    if not m:
        return []
    out = []
    for line in b[m.end():].splitlines():
        mm = re.match(r"^\s*-\s*\{fileID:\s*(-?\d+)\}\s*$", line)
        if mm:
            out.append(mm.group(1))
        elif line.strip():
            break
    # Transform.m_Children 存的是 Transform fileID，需反查 GameObject
    res = []
    for t in out:
        res.append(transform_owner(t))
    return res

# Transform -> GameObject 反查：m_GameObject: {fileID: X}
owner_cache = {}
def transform_owner(tf):
    if tf in owner_cache:
        return owner_cache[tf]
    b = objs.get(tf, {}).get("body", "")
    m = re.search(r"m_GameObject:\s*\{fileID:\s*(-?\d+)\}", b)
    v = m.group(1) if m else tf
    owner_cache[tf] = v
    return v

targets = []
for fid in order:
    if objs[fid]["class"] != "1":
        continue
    nm = gobj_name(fid)
    if re.search(r"(^skillbar$|^skill$|^zhuangbei$|^backpackpanel$)", nm, re.I):
        targets.append((fid, nm))

for fid, nm in targets:
    ch = children(fid)
    print("=== %s  children=%d" % (nm, len(ch)))
    for i, c in enumerate(ch):
        print("   [%d] %-22s children=%d" % (i, gobj_name(c), len(children(c))))
        for j, g in enumerate(children(c)):
            print("         - %-20s children=%d" % (gobj_name(g), len(children(g))))
            for k, h in enumerate(children(g)):
                print("              + %-18s children=%d" % (gobj_name(h), len(children(h))))
            if j > 14:
                print("         ...(truncated)")
                break
    print()
