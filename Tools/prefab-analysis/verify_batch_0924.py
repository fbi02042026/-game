# -*- coding: utf-8 -*-
import io, os, sys

FILES = [
    r"E:\xiangsumaoxian\Assets\Scripts\UI\DailyLoginUI.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\TownHubController.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\CharacterUI.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\BoardSelectPopupUI.cs",
    r"E:\xiangsumaoxian\Assets\Editor\UiReparentSetup.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\BackpackGridVisual.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\TownBackpackGrid.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\CharacterBagGrid.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\CharacterBagSystem.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\SkillSelectUI.cs",
    r"E:\xiangsumaoxian\Assets\Scripts\UI\BackpackPopupUI.cs",
]

RES = [
    r"E:\xiangsumaoxian\Assets\Resources\UI\Battle\装备格.png",
    r"E:\xiangsumaoxian\Assets\Resources\UI\Battle\恢复底框.png",
    r"E:\xiangsumaoxian\Assets\Resources\UI\Battle\图层 8.png",
]

def strip_code(s):
    out = []
    i = 0
    n = len(s)
    while i < n:
        c = s[i]
        if c == '/' and i + 1 < n and s[i+1] == '/':
            while i < n and s[i] != '\n':
                i += 1
            continue
        if c == '/' and i + 1 < n and s[i+1] == '*':
            i += 2
            while i + 1 < n and not (s[i] == '*' and s[i+1] == '/'):
                i += 1
            i += 2
            continue
        if c == '"':
            i += 1
            while i < n:
                if s[i] == '\\':
                    i += 2
                    continue
                if s[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == "'":
            i += 1
            while i < n:
                if s[i] == '\\':
                    i += 2
                    continue
                if s[i] == "'":
                    i += 1
                    break
                i += 1
            continue
        out.append(c)
        i += 1
    return ''.join(out)

ok = True
for f in FILES:
    if not os.path.exists(f):
        print("MISSING  " + f)
        ok = False
        continue
    with io.open(f, 'rb') as fh:
        raw = fh.read()
    crlf = raw.count(b'\r\n')
    bare_lf = raw.count(b'\n') - crlf
    bare_cr = raw.count(b'\r') - crlf
    txt = raw.decode('utf-8', errors='replace')
    code = strip_code(txt)
    d1 = d2 = d3 = 0
    bal = True
    for ch in code:
        if ch == '{': d1 += 1
        elif ch == '}':
            d1 -= 1
            if d1 < 0: bal = False
        elif ch == '(': d2 += 1
        elif ch == ')':
            d2 -= 1
            if d2 < 0: bal = False
        elif ch == '[': d3 += 1
        elif ch == ']':
            d3 -= 1
            if d3 < 0: bal = False
    good = (bare_lf == 0 and bare_cr == 0 and bal and d1 == 0 and d2 == 0 and d3 == 0)
    if not good: ok = False
    print(("PASS " if good else "FAIL ") + os.path.basename(f) +
          "  CRLF=%d bareLF=%d bareCR=%d  {}%d ()%d []%d" % (crlf, bare_lf, bare_cr, d1, d2, d3))

print("")
for f in RES:
    if os.path.exists(f):
        print("RES OK   %s  %d bytes" % (os.path.basename(f), os.path.getsize(f)))
    else:
        print("RES MISS " + f)
        ok = False

print("")
print("ALL OK" if ok else "HAS PROBLEM")
