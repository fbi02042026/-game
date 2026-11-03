# -*- coding: utf-8 -*-
"""全量清点 Assets/Scripts 里的硬编码颜色（只读，不改任何文件）。
输出到 .workbuddy/backup/color_audit_20261103/report.txt
"""
import os, re, math, io

ROOT = r"Y:\PixelAdventureTown\Assets\Scripts"
OUT_DIR = r"Y:\PixelAdventureTown\.workbuddy\backup\color_audit_20261103"
os.makedirs(OUT_DIR, exist_ok=True)
OUT = os.path.join(OUT_DIR, "report.txt")

# 参考色
REF = {
    "FFB833": (255, 184, 51),   # 橙金 Legendary
    "59ADFA": (89, 173, 250),   # 蓝 Rare
    "CCD1DB": (204, 209, 219),  # 灰白 Common
}

def hex_rgb(h):
    h = h.upper()
    return (int(h[0:2],16), int(h[2:4],16), int(h[4:6],16))

def rgb_to_hex(r,g,b):
    return "%02X%02X%02X" % (int(round(r*255)), int(round(g*255)), int(round(b*255)))

def hue_of(rgb):
    r,g,b = [x/255.0 for x in rgb]
    mx,mn = max(r,g,b), min(r,g,b)
    d = mx-mn
    if d == 0: return 0.0, 0.0, mx  # h, s, v
    if mx == r: h = ((g-b)/d) % 6
    elif mx == g: h = (b-r)/d + 2
    else: h = (r-g)/d + 4
    h *= 60
    s = 0 if mx == 0 else d/mx
    return h, s, mx

def classify(rgb):
    h,s,v = hue_of(rgb)
    # 灰白系：低饱和高明度
    if s <= 0.18 and v >= 0.65: return "GRAY"
    # 橙金系：色相 25~55 度
    if 25 <= h <= 55 and s >= 0.25 and v >= 0.5: return "GOLD"
    # 蓝系：色相 195~225 度
    if 195 <= h <= 225 and s >= 0.3 and v >= 0.4: return "BLUE"
    return "OTHER"

def delta(rgb, ref):
    return max(abs(a-b) for a,b in zip(rgb, ref))

pat_new_color   = re.compile(r'new\s+Color\s*\(([^)]*)\)')
pat_new_color32 = re.compile(r'new\s+Color32\s*\(([^)]*)\)')
pat_richtext    = re.compile(r'<color=#([0-9A-Fa-f]{6,8})')
pat_hexstr      = re.compile(r'"#([0-9A-Fa-f]{6,8})"')

results = []  # (kind, family, hexval, file, line, snippet)

def add(kind, family, hexval, path, ln, snip):
    results.append((kind, family, hexval, os.path.relpath(path, ROOT), ln, snip.strip()[:160]))

for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in ("obj", "bin")]
    for fn in filenames:
        if not fn.endswith(".cs"): continue
        p = os.path.join(dirpath, fn)
        with open(p, "rb") as f:
            data = f.read()
        text = data.decode("utf-8", errors="replace")
        for i, line in enumerate(text.splitlines(), 1):
            s = line.strip()
            if s.startswith("//") or s.startswith("///"): continue
            for m in pat_new_color.finditer(line):
                args = m.group(1)
                parts = [a.strip() for a in args.split(",")]
                if len(parts) < 3: continue
                try:
                    vals = []
                    for a in parts[:4]:
                        a = a.replace("f","").replace("F","").strip()
                        if "/" in a:
                            n,d = a.split("/")
                            vals.append(float(n)/float(d))
                        else:
                            vals.append(float(a))
                    r,g,b = vals[0], vals[1], vals[2]
                    if any(v > 1.0 for v in (r,g,b)):  # 写成了 0-255 的
                        r,g,b = r/255.0, g/255.0, b/255.0
                    hx = rgb_to_hex(r,g,b)
                    add("Color()", classify(hex_rgb(hx)), hx, p, i, s)
                except Exception:
                    pass
            for m in pat_new_color32.finditer(line):
                parts = [a.strip() for a in m.group(1).split(",")]
                if len(parts) < 3: continue
                try:
                    vals = []
                    for a in parts[:3]:
                        a = a.replace("f","").replace("F","").strip()
                        if "/" in a:
                            n,d = a.split("/")
                            vals.append(float(n)/float(d)*255)
                        else:
                            vals.append(float(a))
                    hx = rgb_to_hex(vals[0]/255.0, vals[1]/255.0, vals[2]/255.0)
                    add("Color32()", classify(hex_rgb(hx)), hx, p, i, s)
                except Exception:
                    pass
            for m in pat_richtext.finditer(line):
                hx = m.group(1)[:6].upper()
                add("rich<color>", classify(hex_rgb(hx)), hx, p, i, s)
            for m in pat_hexstr.finditer(line):
                hx = m.group(1)[:6].upper()
                add("hex串", classify(hex_rgb(hx)), hx, p, i, s)

# 汇总：按 family + hex 聚合
agg = {}
for kind, fam, hx, rel, ln, snip in results:
    key = (fam, hx)
    if key not in agg: agg[key] = {"count":0, "files":{}, "kinds":set()}
    agg[key]["count"] += 1
    agg[key]["files"].setdefault(rel, []).append(ln)
    agg[key]["kinds"].add(kind)

with io.open(OUT, "w", encoding="utf-8") as f:
    f.write("全库硬编码颜色清点（只读扫描） %s\n" % __import__("datetime").datetime.now().strftime("%Y-%m-%d %H:%M"))
    f.write("参考基准：橙金#FFB833 / 蓝#59ADFA / 灰白#CCD1DB（RarityPalette）\n")
    f.write("总命中：%d 处（同一行多处各算一次）\n\n" % len(results))

    for fam in ("GOLD","BLUE","GRAY","OTHER"):
        f.write("="*90 + "\n【%s 系】\n" % fam)
        keys = sorted([k for k in agg if k[0]==fam], key=lambda k:-agg[k]["count"])
        for (famk, hx) in keys:
            info = agg[(famk,hx)]
            if famk != "OTHER":
                ref = REF[{"GOLD":"FFB833","BLUE":"59ADFA","GRAY":"CCD1DB"}[famk]]
                rgb = hex_rgb(hx)
                d = delta(rgb, ref)
                mark = "  <<< 与基准最大差 %d" % d if d > 8 else "  (≈基准)"
            else:
                mark = ""
            f.write("\n#%s  ×%d  %s%s\n" % (hx, info["count"], ",".join(sorted(info["kinds"])), mark))
            for rel in sorted(info["files"]):
                f.write("    %s : %s\n" % (rel, ",".join(str(x) for x in info["files"][rel][:20])))
        f.write("\n")

print("done ->", OUT)
