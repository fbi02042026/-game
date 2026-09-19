# -*- coding: utf-8 -*-
"""把指定 .cs 统一成 CRLF（工程约定：源码 CRLF，团结写的 .meta 才是 LF）。"""
import io, sys

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

for p in files:
    b = io.open(p, "rb").read()
    crlf = b.count(b"\r\n")
    lf = b.count(b"\n")
    if crlf == lf and lf > 0:
        print("skip (already CRLF): %s" % p)
        continue
    fixed = b.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
    io.open(p, "wb").write(fixed)
    print("fixed -> CRLF: %s (crlf %d -> %d)" % (p, crlf, fixed.count(b"\r\n")))
