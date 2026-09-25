# -*- coding: utf-8 -*-
"""同步 Assets/Art/UI/NavCharacter -> Assets/Resources/UI/NavCharacter
按文件名比较；缺的复制，内容不同的覆盖（保留 Resources 侧已有 .meta，guid 不变）。
用法: python sync_navchar.py [--apply]
"""
import hashlib, io, os, shutil, sys

ART = r"E:\xiangsumaoxian\Assets\Art\UI\NavCharacter"
RES = r"E:\xiangsumaoxian\Assets\Resources\UI\NavCharacter"
APPLY = '--apply' in sys.argv
# --only 只处理这些文件名（避免把大背景图也塞进 Resources 打成包体）
# --only 只处理这些文件名（避免把大背景图也塞进 Resources 打成包体）
ONLY = None
if '--only' in sys.argv:
    i = sys.argv.index('--only')
    ONLY = set(sys.argv[i + 1:])
# --only-recent N 只处理最近 N 天内改过的文件（命令行传中文名容易乱码时用这个）
RECENT = None
if '--only-recent' in sys.argv:
    RECENT = float(sys.argv[sys.argv.index('--only-recent') + 1])


def md5(p):
    h = hashlib.md5()
    with io.open(p, 'rb') as f:
        for b in iter(lambda: f.read(65536), b''):
            h.update(b)
    return h.hexdigest()


def listing(d):
    out = {}
    if not os.path.isdir(d):
        return None
    for fn in os.listdir(d):
        p = os.path.join(d, fn)
        if os.path.isfile(p) and not fn.endswith('.meta'):
            out[fn] = (os.path.getsize(p), md5(p), os.path.getmtime(p))
    return out


def main():
    a = listing(ART)
    r = listing(RES)
    if a is None:
        print('ART 目录不存在: ' + ART)
        return
    if r is None:
        print('RES 目录不存在（将整目录复制）: ' + RES)
        r = {}

    missing = sorted(n for n in a if n not in r)
    changed = sorted(n for n in a if n in r and a[n][1] != r[n][1])
    only_res = sorted(n for n in r if n not in a)
    if ONLY is not None:
        missing = [n for n in missing if n in ONLY]
        changed = [n for n in changed if n in ONLY]
    if RECENT is not None:
        import time as _t
        cut = _t.time() - RECENT * 86400.0
        missing = [n for n in missing if a[n][2] >= cut]
        changed = [n for n in changed if a[n][2] >= cut]

    print('ART  %d 个文件' % len(a))
    print('RES  %d 个文件' % len(r))
    print('')
    print('[缺失] 需复制到 Resources:')
    for n in missing:
        print('   + ' + n + '  (%d bytes)' % a[n][0])
    print('[内容不同] 需覆盖:')
    for n in changed:
        print('   ~ ' + n + '  ART %d bytes -> RES %d bytes' % (a[n][0], r[n][0]))
    if only_res:
        print('[只在 Resources 里] 未动:')
        for n in only_res:
            print('   ? ' + n)
    print('')

    if not APPLY:
        print('（dry-run，加 --apply 才真正复制）')
        return

    if not os.path.isdir(RES):
        os.makedirs(RES)
    done = 0
    for n in missing + changed:
        src = os.path.join(ART, n)
        dst = os.path.join(RES, n)
        shutil.copy2(src, dst)
        done += 1
        print('copied: ' + n)
    print('')
    print('完成，共复制/覆盖 %d 个文件' % done)


if __name__ == '__main__':
    main()
