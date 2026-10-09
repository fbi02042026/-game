# -*- coding: utf-8 -*-
"""
把 Data/Source/Tables 里的 csv 同步成 Resources/Data/Tables 的 .bytes。
实测规则：.bytes == .csv 去掉 UTF-8 BOM（bytes == csv[3:]），CRLF 保持一致。
用法：python Tools/sync_table_bytes.py 表名1 表名2 ...
"""
import os
import shutil
import sys

ROOT = r"Y:\PixelAdventureTown"
SRC_DIR = os.path.join(ROOT, "Assets", "Data", "Source", "Tables")
DST_DIR = os.path.join(ROOT, "Assets", "Resources", "Data", "Tables")
BACKUP = os.path.join(ROOT, ".workbuddy", "backup", "20261009-equip-tables")


def main():
    names = sys.argv[1:]
    if not names:
        print("用法：python Tools/sync_table_bytes.py 表名1 表名2 ...")
        return
    os.makedirs(BACKUP, exist_ok=True)
    for n in names:
        s = os.path.join(SRC_DIR, n + ".csv")
        d = os.path.join(DST_DIR, n + ".bytes")
        if not os.path.isfile(s) or not os.path.isfile(d):
            print("  跳过（文件不存在）：%s" % n)
            continue
        shutil.copy2(s, os.path.join(BACKUP, n + ".csv"))
        shutil.copy2(d, os.path.join(BACKUP, n + ".bytes"))
        c = open(s, "rb").read()
        open(d, "wb").write(c[3:] if c[:3] == b"\xef\xbb\xbf" else c)
        print("  %-24s csv %5d  bytes %5d  bom=%s  crlf=%d/%d"
              % (n, len(c), os.path.getsize(d), c[:3] == b"\xef\xbb\xbf", c.count(b"\r\n"), c.count(b"\n")))
    print("备份目录：" + BACKUP)


if __name__ == "__main__":
    main()
