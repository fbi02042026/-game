# -*- coding: utf-8 -*-
"""查 PlayerJobSelect / 职业立绘链路"""
import os
import re

PROJ = r"E:\xiangsumaoxian"
GUID = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")


def meta_guid(p):
    if not os.path.isfile(p + ".meta"):
        return None
    with open(p + ".meta", encoding="utf-8", errors="ignore") as fh:
        m = re.search(r"^guid:\s*(\S+)", fh.read(6000), re.M)
    return m.group(1) if m else None


def build_index():
    idx = {}
    for base in ["Assets", "Packages", os.path.join("Library", "PackageCache")]:
        for dp, _dn, fn in os.walk(os.path.join(PROJ, base)):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                g = meta_guid(p[:-5])
                if g:
                    idx[g] = os.path.relpath(p[:-5], PROJ).replace("\\", "/")
    return idx


idx = build_index()

for rel in ["Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab"]:
    full = os.path.join(PROJ, rel)
    if not os.path.isfile(full):
        print("缺", rel)
        continue
    with open(full, encoding="utf-8", errors="ignore") as fh:
        text = fh.read()
    dang = [g for g in set(GUID.findall(text)) if g not in idx
            and not re.match(r"^0{8}[0-9a-f]{24}$", g)]
    print("%s: 引用 %d，悬空 %d" % (rel, len(set(GUID.findall(text))), len(dang)))
    for g in dang:
        print("   【悬空】%s" % g)
    # 立绘相关节点
    print("  -- 含 portrait/立绘/art 字样的节点 --")
    for m in re.finditer(r"m_Name:\s*(.*(?:ortrait|立绘|[Aa]rt|Job|职业).*)", text):
        print("     ", m.group(1).strip()[:60])
    # 代码动态加载的引用（Sprite 名）
    loads = re.findall(r'Resources\.Load(?:<[^>]+>)?\(\s*"([^"]+)"', text)
    if loads:
        print("  -- Resources.Load --", set(loads))

# 代码里的职业立绘加载
print("\n--- 代码中的职业/立绘加载 ---")
for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets", "Scripts")):
    for f in fn:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(dp, f)
        with open(p, encoding="utf-8", errors="ignore") as fh:
            t = fh.read()
        for m in re.finditer(r'.*(?:Resources\.Load|LoadSprite|portrait|Portrait|立绘).*', t):
            s = m.group(0).strip()
            if "Resources.Load" in s or "Sprite" in s:
                print("  %-30s %s" % (f, s[:90]))
