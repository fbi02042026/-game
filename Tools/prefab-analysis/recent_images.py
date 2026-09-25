# -*- coding: utf-8 -*-
"""列出 Assets 下最近 N 天内修改过的图片（找主人刚放进来的 / 刚替换的图）。
用法: python recent_images.py [天数，默认3]
"""
import io, os, sys, time

ROOT = r"E:\xiangsumaoxian\Assets"
DAYS = float(sys.argv[1]) if len(sys.argv) > 1 else 3.0
EXT = ('.png', '.jpg', '.jpeg', '.psd', '.tga')

now = time.time()
rows = []
for dirpath, dirnames, filenames in os.walk(ROOT):
    for fn in filenames:
        if not fn.lower().endswith(EXT):
            continue
        p = os.path.join(dirpath, fn)
        try:
            st = os.stat(p)
        except Exception:
            continue
        age = (now - st.st_mtime) / 86400.0
        if age <= DAYS:
            rows.append((st.st_mtime, os.path.relpath(p, ROOT), st.st_size, age))

rows.sort(key=lambda r: -r[0])
print('最近 %.1f 天内修改过的图片：%d 个' % (DAYS, len(rows)))
for mt, rel, size, age in rows:
    print('  %s  %9d B  %.2f 天前  %s' % (time.strftime('%m-%d %H:%M', time.localtime(mt)), size, age, rel))
