# -*- coding: utf-8 -*-
"""按 guid 反查资源文件：扫 Assets 下所有 .meta 找 guid，返回去掉 .meta 的资源路径。
用法: python resolve_guids.py <guid1> <guid2> ...
"""
import io, os, sys, glob

ROOT = r"E:\xiangsumaoxian\Assets"

def main():
    want = set(g.lower() for g in sys.argv[1:])
    found = {}
    if not want:
        print('no guid given')
        return
    for dirpath, dirnames, filenames in os.walk(ROOT):
        if 'Library' in dirpath:
            continue
        for fn in filenames:
            if not fn.endswith('.meta'):
                continue
            p = os.path.join(dirpath, fn)
            try:
                with io.open(p, 'r', encoding='utf-8', errors='replace') as f:
                    head = f.read(4096)
            except Exception:
                continue
            for line in head.split('\n'):
                s = line.strip()
                if s.startswith('guid:'):
                    g = s[5:].strip().lower()
                    if g in want:
                        found[g] = p[:-5]
                    break
    for g in want:
        if g in found:
            print(g + '  ->  ' + os.path.relpath(found[g], ROOT))
        else:
            print(g + '  ->  NOT FOUND')

if __name__ == '__main__':
    main()
