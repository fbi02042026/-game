# -*- coding: utf-8 -*-
"""统计 guid 形态分布：美术资源侧 vs 预制体引用侧"""
import os
import re
from collections import Counter

PROJ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HEX = re.compile(r"^[0-9a-f]{32}$")
GUID = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")


def shape(g):
    if HEX.match(g):
        return "hex32"
    if re.match(r"^0{8}[0-9a-f]{24}$", g):
        return "builtin"
    return "base64"


def meta_shapes(base):
    c = Counter()
    for dp, _dn, fn in os.walk(os.path.join(PROJ, base)):
        for f in fn:
            if not f.endswith(".meta"):
                continue
            with open(os.path.join(dp, f), encoding="utf-8", errors="ignore") as fh:
                head = fh.read(3000)
            m = re.search(r"^guid:\s*(\S+)", head, re.M)
            if m:
                c[shape(m.group(1))] += 1
    return c


print("Assets/Art       .meta guid 形态:", dict(meta_shapes("Assets/Art")))
print("Assets/Resources .meta guid 形态:", dict(meta_shapes(os.path.join("Assets", "Resources"))))
print("Assets/Scripts   .meta guid 形态:", dict(meta_shapes(os.path.join("Assets", "Scripts"))))

for rel in ["Assets/Resources/Prefabs/Battle/BattleUI.prefab",
            "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab",
            "Assets/Resources/Prefabs/Town/SettingsPopup.prefab",
            "Assets/Scenes/Battle.unity"]:
    p = os.path.join(PROJ, rel)
    if not os.path.isfile(p):
        print("缺:", rel)
        continue
    with open(p, encoding="utf-8", errors="ignore") as fh:
        text = fh.read()
    c = Counter(shape(g) for g in set(GUID.findall(text)))
    print("%-58s %s" % (os.path.basename(rel), dict(c)))
