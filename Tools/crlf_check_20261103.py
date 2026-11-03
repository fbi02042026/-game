# -*- coding: utf-8 -*-
"""行尾校验：统计三个目标文件 CRLF / 裸LF 数量（改前基线 & 改后对比用）"""
import io

FILES = [
    r"Y:\PixelAdventureTown\Assets\Scripts\Config\MercFavorDefs.cs",
    r"Y:\PixelAdventureTown\Assets\Scripts\Config\DraftPool.cs",
    r"Y:\PixelAdventureTown\Assets\Scripts\UI\BattleSettlementUI.cs",
]

with io.open(r"Y:\PixelAdventureTown\.workbuddy\backup\color_audit_20261103\crlf_check.txt", "a", encoding="utf-8") as out:
    import datetime
    out.write("==== %s ====\n" % datetime.datetime.now().strftime("%H:%M:%S"))
    for p in FILES:
        with open(p, "rb") as f:
            b = f.read()
        crlf = b.count(b"\r\n")
        lf = b.count(b"\n") - crlf
        out.write("%s  CRLF=%d  裸LF=%d  bytes=%d\n" % (p.split("\\")[-1], crlf, lf, len(b)))
print("ok")
