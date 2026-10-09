# -*- coding: utf-8 -*-
"""一次性校验：152 个装备模板的 baseAttr / globalBonus / minLevel / baseRarity 块结构是否完整。"""
import os
import re

D = r"Y:\PixelAdventureTown\Assets\Resources\Config\Equips"

BLOCK = re.compile(
    r"  baseAttr:\n"
    r"(?:  - attrType: -?\d+\n    value: -?[\d.]+\n    isPercent: [01]\n)+"
    r"  globalBonus:\n    attrType: -?\d+\n    value: -?[\d.]+\n    isPercent: [01]\n"
)

bad = []
n = 0
for fn in sorted(os.listdir(D)):
    if not fn.endswith(".asset"):
        continue
    n += 1
    t = open(os.path.join(D, fn), encoding="utf-8").read().replace("\r\n", "\n")
    if not t.startswith("%YAML 1.1"):
        bad.append((fn, "no YAML head"))
        continue
    if "  minLevel: " not in t or "  baseRarity: " not in t:
        bad.append((fn, "missing minLevel/baseRarity"))
        continue
    if not BLOCK.search(t):
        bad.append((fn, "baseAttr/globalBonus block broken"))

print("checked %d, bad %d" % (n, len(bad)))
for fn, why in bad[:30]:
    print("  BAD  %-28s %s" % (fn, why))
if not bad:
    print("ALL OK")
