# -*- coding: utf-8 -*-
"""粗查改过的 .cs：大括号/圆括号是否配平（去掉字符串与注释后统计）。"""
import io, re, sys

files = [
    r"Assets/Scripts/UI/LoadingFlavorText.cs",
    r"Assets/Scripts/Core/StartupPreloader.cs",
    r"Assets/Scripts/UI/TownHubController.cs",
    r"Assets/Scripts/UI/LoadingTips.cs",
    r"Assets/Scripts/UI/BattleWaveAnnounceUI.cs",
    r"Assets/Scripts/Combat/WavePlanner.cs",
    r"Assets/Scripts/Managers/BattleManager.cs",
    r"Assets/Scripts/UI/BattleUI.Skills.cs",
]

def strip(src):
    out = []
    i = 0
    n = len(src)
    while i < n:
        c = src[i]
        if c == '"':
            # 逐字字符串 / 普通字符串
            if src.startswith('@"', i):
                i += 2
                while i < n:
                    if src[i] == '"' and i + 1 < n and src[i + 1] == '"':
                        i += 2
                        continue
                    if src[i] == '"':
                        i += 1
                        break
                    i += 1
                continue
            if src.startswith('$"', i):
                i += 2
            else:
                i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == '/' and i + 1 < n and src[i + 1] == '/':
            j = src.find('\n', i)
            i = n if j < 0 else j
            continue
        if c == '/' and i + 1 < n and src[i + 1] == '*':
            j = src.find('*/', i + 2)
            i = n if j < 0 else j + 2
            continue
        if c == "'":
            i += 1
            while i < n and src[i] != "'":
                i += 2 if src[i] == '\\' else 1
            i += 1
            continue
        out.append(c)
        i += 1
    return "".join(out)

bad = 0
for p in files:
    src = io.open(p, encoding="utf-8").read()
    s = strip(src)
    for open_c, close_c in (("{", "}"), ("(", ")"), ("[", "]")):
        d = s.count(open_c) - s.count(close_c)
        if d != 0:
            print("!! %s  %s%s 差 %d" % (p, open_c, close_c, d))
            bad += 1
    print("ok %s" % p)
print("DONE bad=%d" % bad)
