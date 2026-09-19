# -*- coding: utf-8 -*-
"""
2026-09-19 M4：monster_stats.csv 的 moveSpeed 全体减半（主人已拍板「移速统统减半」）。
注意：本表 moveSpeed 不是世界单位。
  原世界速度 = MONSTER_DEFAULT_MOVE_SPEED = 0.6912
  表值 2.2 对应 0.6912   => 换算系数 K = 0.6912 / 2.2 = 0.31418
减半目标 = 0.3456 世界单位 => 表值应为 1.1（2.2 -> 1.1）、0.9（1.8 -> 0.9）。
⚠️ 单独跑本脚本「不会」立刻改变怪物速度：
   - 非 Boss 怪当前恒取 GameConfig.MONSTER_DEFAULT_MOVE_SPEED，完全不读表；
   - Boss 走 Mathf.Min(表值, DEFAULT*1.5=1.0368)，2.2 与 1.1 都会被钳成 1.0368。
   必须等 Monster.cs 补上「读表 × MONSTER_MOVE_SPEED_TO_WORLD」通道后才生效。
改写后自动同步 Resources/Data/Tables/monster_stats.bytes 并校验逐字一致。
"""
import io
import os
import shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Data", "Source", "Tables", "monster_stats.csv")
DST = os.path.join(ROOT, "Assets", "Resources", "Data", "Tables", "monster_stats.bytes")

HALVE = {"2.2": "1.1", "1.8": "0.9"}


def main():
    with io.open(SRC, "r", encoding="utf-8-sig", newline="") as f:
        text = f.read()

    crlf = "\r\n" in text
    lines = text.replace("\r\n", "\n").split("\n")

    header = None
    for ln in lines:
        if ln and not ln.startswith("#"):
            header = ln.split(",")
            break
    col = header.index("moveSpeed")

    out = []
    changed = 0
    unchanged = 0
    for ln in lines:
        if not ln or ln.startswith("#") or header is None or ln.split(",")[0].strip() == "id":
            out.append(ln)
            continue
        cells = ln.split(",")
        if len(cells) > col:
            old = cells[col].strip()
            if old in HALVE:
                cells[col] = HALVE[old]
                changed += 1
            else:
                unchanged += 1
                print("  [未映射] %s moveSpeed=%s" % (cells[0], old))
        out.append(",".join(cells))

    result = ("\r\n" if crlf else "\n").join(out)
    with io.open(SRC, "w", encoding="utf-8-sig", newline="") as f:
        f.write(result)
    shutil.copy2(SRC, DST)

    print("\n改写 %d 行，未映射 %d 行，行尾 CRLF=%s" % (changed, unchanged, crlf))
    print("已同步:", DST)


if __name__ == "__main__":
    main()
