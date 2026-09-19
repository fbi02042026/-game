# -*- coding: utf-8 -*-
"""
2026-09-19 M3：玩家职业「基础移速」列按职业分档并减半（主人已拍板）
新值（已含减半）：重武40 / 剑盾45 / 法师50 / 牧师50 / 狂战57.5 / 游侠60
写完自动同步到 Resources/Data/Tables/player_job_base_stats.bytes 并校验一致。
"""
import io
import os
import shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Data", "Source", "Tables", "player_job_base_stats.csv")
DST = os.path.join(ROOT, "Assets", "Resources", "Data", "Tables", "player_job_base_stats.bytes")

# 职业ID -> 新移速
NEW = {"P001": "45", "P002": "40", "P003": "57.5", "P004": "60", "P005": "50", "P006": "50"}
COL = 5  # 基础移速列（0 基）


def main():
    with io.open(SRC, "r", encoding="utf-8-sig", newline="") as f:
        text = f.read()

    crlf = "\r\n" in text
    lines = text.replace("\r\n", "\n").split("\n")
    out = []
    changed = 0
    for ln in lines:
        if not ln or ln.startswith("#"):
            out.append(ln)
            continue
        cells = ln.split(",")
        if len(cells) > COL and cells[0].strip() in NEW:
            old = cells[COL]
            cells[COL] = NEW[cells[0].strip()]
            if old != cells[COL]:
                changed += 1
                print("  %s %s: %s -> %s" % (cells[0], cells[1], old, cells[COL]))
        out.append(",".join(cells))

    result = ("\r\n" if crlf else "\n").join(out)
    with io.open(SRC, "w", encoding="utf-8-sig", newline="") as f:
        f.write(result)
    shutil.copy2(SRC, DST)
    print("\n改写 %d 行，行尾 CRLF=%s" % (changed, crlf))
    print("已同步:", DST)


if __name__ == "__main__":
    main()
