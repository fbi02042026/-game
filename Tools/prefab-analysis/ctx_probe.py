# -*- coding: utf-8 -*-
"""对游戏本体 prefab 的 phantom sprite 引用，打印其所在 GameObject/字段上下文。"""
import os, re

R = r"E:\xiangsumaoxian"
A = os.path.join(R, "Assets")
PKG = os.path.join(R, "Library", "PackageCache")

OWN = re.compile(rb"^guid:[ \t]*([^\r\n]+)", re.M)
HEX32 = re.compile(r"^[0-9a-f]{32}$")
BUILTIN = re.compile(r"^0{16}[0-9a-f]{16}$")

meta = set()
for base in (A, PKG):
    for dp, dn, fn in os.walk(base):
        for f in fn:
            if f.endswith(".meta"):
                d = open(os.path.join(dp, f), "rb").read(300)
                m = OWN.search(d)
                if m:
                    g = m.group(1).strip().decode("ascii", "replace")
                    if HEX32.match(g):
                        meta.add(g)

def context(lines, idx):
    # 找离该行最近的 m_Name（向上），再找最近的 m_Script
    name = "?"
    for j in range(idx, max(-1, idx - 400), -1):
        if lines[j].strip().startswith("m_Name:"):
            name = lines[j].split(":", 1)[1].strip()
            break
    script = "?"
    for j in range(idx, max(-1, idx - 80), -1):
        if "m_Script:" in lines[j]:
            script = lines[j].strip(); break
    # 字段名：该行本身
    field = lines[idx].strip()
    return name, field

for rel in [r"Resources\Prefabs\Battle\BattleUI.prefab",
            r"Resources\Prefabs\UI\SettingsPopup.prefab",
            r"Resources\Prefabs\Battle\BattleSettlement.prefab"]:
    p = os.path.join(A, rel)
    lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
    print("=" * 70)
    print(rel)
    for i, line in enumerate(lines):
        m = re.search(r"guid:\s*([0-9a-f]{32})", line)
        if not m:
            continue
        g = m.group(1)
        if g in meta or BUILTIN.match(g):
            continue
        name, field = context(lines, i)
        print("  [%s]  %s" % (name, field))
        print("        guid=%s" % g)

print("\n==== box.prefab.meta 当前 guid ====")
for dp, dn, fn in os.walk(A):
    for f in fn:
        if f == "box.prefab.meta":
            t = open(os.path.join(dp, f), "rb").read(200).decode("utf-8", "replace")
            print(os.path.relpath(os.path.join(dp, f), R))
            print("   ", next(l for l in t.splitlines() if l.startswith("guid:")))

print("\n==== guid-remap-last.json ====")
gj = os.path.join(R, "Tools", "guid-remap-last.json")
if os.path.isfile(gj):
    print(open(gj, encoding="utf-8").read())
