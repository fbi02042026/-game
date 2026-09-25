# -*- coding: utf-8 -*-
r"""
通用 UI 预制体层级 dump：打印树形结构 + 每个节点 RectTransform 的锚点/尺寸数值。

用法（Windows，用托管 Python 绝对路径）：
  C:/Users/Admin/.workbuddy/binaries/python/versions/3.13.12/python.exe ^
      Tools/prefab-analysis/dump_ui_tree.py <prefab相对E:\xiangsumaoxian的路径> [最大深度]

用途：UI 竖屏适配排查时，先看清楚「谁挂在谁下面、锚点贴哪边」，再决定改法。
只读取，不修改任何文件。
"""
import re, os, io, json, sys

ROOT = r"E:\xiangsumaoxian"

def f2(x):
    return ("%.2f" % x).rstrip("0").rstrip(".")

def parse_prefab(path):
    txt = io.open(path, encoding="utf-8", errors="ignore").read()
    docs = re.split(r"(?m)^--- ", txt)
    gameobjects, transforms = {}, {}
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
            r = dict(go=go.group(1) if go else None,
                     father=fa.group(1) if fa else "0",
                     children=kids, is_rect=(cls == "224"))
            for key, pat in (
                ("anchorMin", r"m_AnchorMin:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}"),
                ("anchorMax", r"m_AnchorMax:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}"),
                ("anchoredPosition", r"m_AnchoredPosition:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}"),
                ("sizeDelta", r"m_SizeDelta:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}"),
                ("pivot", r"m_Pivot:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}"),
            ):
                mm = re.search(pat, body)
                r[key] = (float(mm.group(1)), float(mm.group(2))) if mm else None
            transforms[fid] = r
    return gameobjects, transforms

def main():
    if len(sys.argv) < 2:
        print("用法: dump_ui_tree.py <prefab路径> [最大深度]")
        sys.exit(1)
    rel = sys.argv[1]
    max_depth = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    path = rel if os.path.isabs(rel) else os.path.join(ROOT, rel)
    gos, trs = parse_prefab(path)

    roots = [k for k, v in trs.items() if v["father"] == "0" or v["father"] not in trs]
    out, seen = [], set()

    def desc(t):
        if not t.get("is_rect"):
            return "(Transform)"
        am, aM = t.get("anchorMin"), t.get("anchorMax")
        ap, sd, pv = t.get("anchoredPosition"), t.get("sizeDelta"), t.get("pivot")
        s = "anchor[%s,%s -> %s,%s]" % (
            f2(am[0]) if am else "?", f2(am[1]) if am else "?",
            f2(aM[0]) if aM else "?", f2(aM[1]) if aM else "?")
        if ap:
            s += " pos(%s,%s)" % (f2(ap[0]), f2(ap[1]))
        if sd:
            s += " size(%s,%s)" % (f2(sd[0]), f2(sd[1]))
        if pv:
            s += " pivot(%s,%s)" % (f2(pv[0]), f2(pv[1]))
        if am and aM and ap and sd:
            # Unity 语义：offsetMin = anchoredPosition - sizeDelta * pivot
            #            offsetMax = offsetMin + sizeDelta
            # （曾经错写成 ±sizeDelta/2，只有 pivot=0.5 时才碰巧对，pivot=0/1 的节点会全错）
            omin = (ap[0] - sd[0] * pv[0], ap[1] - sd[1] * pv[1])
            omax = (omin[0] + sd[0], omin[1] + sd[1])
            s += " | offMin(%s,%s) offMax(%s,%s)" % (
                f2(omin[0]), f2(omin[1]), f2(omax[0]), f2(omax[1]))
        return s

    def walk(tid, depth):
        if tid in seen or depth > max_depth:
            return
        seen.add(tid)
        t = trs[tid]
        name = gos.get(t["go"], "?")
        out.append("  " * depth + "- %s  %s" % (name, desc(t)))
        for c in t["children"]:
            if c in trs:
                walk(c, depth + 1)

    for r in roots:
        walk(r, 0)
    print("\n".join(out))

if __name__ == "__main__":
    main()
