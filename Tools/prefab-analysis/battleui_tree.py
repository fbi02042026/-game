# -*- coding: utf-8 -*-
import re, os, io, json

ROOT = r"E:\xiangsumaoxian"
PREFAB = os.path.join(ROOT, "Assets", "Resources", "Prefabs", "Battle", "BattleUI.prefab")

# --- guid -> script class name ---
guid2name = {}
extra_roots = [
    os.path.join(ROOT, "Assets"),
    os.path.join(ROOT, "Library", "PackageCache", "com.unity.ugui@1.0.0"),
]
for scan_root in extra_roots:
    for base, dirs, files in os.walk(scan_root):
        if "PackageCache" in base and "com.unity.ugui" not in base:
            continue
        for f in files:
            if f.endswith(".cs.meta"):
                try:
                    t = io.open(os.path.join(base, f), encoding="utf-8", errors="ignore").read()
                except Exception:
                    continue
                m = re.search(r"guid:\s*([0-9a-fA-F]{32})", t)
                if m:
                    guid2name[m.group(1)] = f[:-8]  # strip ".cs.meta"

txt = io.open(PREFAB, encoding="utf-8", errors="ignore").read()
docs = re.split(r"(?m)^--- ", txt)

gameobjects, transforms, monos = {}, {}, {}

CHILD_BLOCK = re.compile(r"(?m)^\s*m_Children:\s*(\[\])?\s*$")

for d in docs:
    head = d.split("\n", 1)[0]
    m = re.match(r"!u!(\d+)\s*&(-?\d+)", head.strip())
    if not m:
        continue
    cls, fid = m.group(1), m.group(2)
    body = d[len(head):]

    if cls == "1":
        nm = re.search(r"(?m)^\s*m_Name:\s*(.*)$", body)
        raw = nm.group(1).strip() if nm else "?"
        # Unity 把非 ASCII 写成 "\u5934" 这类转义
        if raw.startswith('"') and "\\u" in raw:
            try:
                raw = json.loads(raw)
            except Exception:
                raw = raw.strip('"')
        else:
            raw = raw.strip('"')
        gameobjects[fid] = raw
    elif cls in ("4", "224"):
        go = re.search(r"m_GameObject:\s*\{fileID:\s*(-?\d+)\}", body)
        fa = re.search(r"m_Father:\s*\{fileID:\s*(-?\d+)\}", body)
        kids = []
        cm = CHILD_BLOCK.search(body)
        if cm and not cm.group(1):
            seg = body[cm.end():]
            for line in seg.split("\n"):
                km = re.match(r"\s*-\s*\{fileID:\s*(-?\d+)\}\s*$", line)
                if km:
                    kids.append(km.group(1))
                elif line.strip() and not line.strip().startswith("-"):
                    break
        transforms[fid] = dict(go=go.group(1) if go else None,
                               father=fa.group(1) if fa else "0",
                               children=kids)
    elif cls == "114":
        go = re.search(r"m_GameObject:\s*\{fileID:\s*(-?\d+)\}", body)
        sc = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]{32})", body)
        if go:
            name = guid2name.get(sc.group(1), "Mono") if sc else "Mono"
            monos.setdefault(go.group(1), []).append(name)

roots = [k for k, v in transforms.items()
         if v["father"] == "0" or v["father"] not in transforms]

out = []
seen = set()
def walk(tid, depth):
    if tid in seen:
        return
    seen.add(tid)
    t = transforms[tid]
    gid = t["go"]
    name = gameobjects.get(gid, "?")
    comps = sorted(set(c for c in monos.get(gid, []) if c != "CanvasRenderer"))
    out.append("  " * depth + f"{name}    [{', '.join(comps)}]")
    for c in t["children"]:
        if c in transforms:
            walk(c, depth + 1)

for r in roots:
    walk(r, 0)

print("\n".join(out))
print("\n=== NODES: %d | ORPHANS(unvisited): %d ===" % (len(gameobjects), len(gameobjects) - len({transforms[t]['go'] for t in seen})))
