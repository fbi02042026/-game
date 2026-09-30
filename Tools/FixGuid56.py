# -*- coding: utf-8 -*-
"""
把「重新导入写坏成 56 字符」的 .meta guid 写回原来的 hex32。

原理：prefab 里存的是旧 hex32，重新导入时引擎给 .meta 换了个 56 字符标识 → 引用断链。
把 prefab 里那份旧 hex32 原样写回 .meta，两边就对上了（不改 prefab、不动图）。

旧 hex32 从基线快照 .workbuddy/guid56_snapshot.txt 取。
默认只预览；加 --apply 才写盘，写盘前把 4 个 .meta 备份到 .workbuddy/backup/20260930-meta-guid56-fix3/。

🔴 改完必须重启引擎才生效（Library 缓存还按旧值索引）。
"""
import re, os, sys, shutil, time

sys.stdout.reconfigure(encoding='utf-8')

ROOT = r"Y:\PixelAdventureTown"
SNAP = os.path.join(ROOT, r".workbuddy\guid56_snapshot.txt")
BACKUP_DIR = os.path.join(ROOT, r".workbuddy\backup\20260930-meta-guid56-fix3")
# 本轮要修的 4 个（2026-09-30 16:10 复查：hex32 → 56 字符，且 prefab 里确认有残留引用）
FIX_LIST = [
    r"Assets\Art\UI\NavCharacter\技能.png.meta",
    r"Assets\Art\UI\NavCharacter\背包.png.meta",
    r"Assets\Art\UI\NavCharacter\天赋.png.meta",
    r"Assets\Art\UI\NavCharacter\立绘.png.meta",
]
HEX32 = re.compile(r'^[0-9a-f]{32}$')
# 用来确认"旧 hex32 确实还被引用着"的两个 prefab
REF_FILES = [
    r"Assets\Resources\Prefabs\Town\CharacterUI.prefab",
    r"Assets\Resources\Prefabs\Battle\BattleUI.prefab",
]


def load_snapshot():
    d = {}
    for ln in open(SNAP, encoding='utf-8'):
        ln = ln.rstrip('\n')
        if not ln or ln.startswith('#'):
            continue
        parts = ln.split('\t')
        if len(parts) == 2:
            d[parts[0]] = parts[1]
    return d


def read_guid(meta_path):
    """读 .meta 里的 guid 那一行，返回 (值, 行索引, 行原文)。"""
    lines = open(meta_path, encoding='utf-8', newline='').read().split('\n')
    for i, s in enumerate(lines):
        if s.strip().startswith('guid:'):
            v = s.strip()[len('guid:'):].strip()
            return v, i, lines
    return None, -1, lines


def main():
    apply = '--apply' in sys.argv
    snap = load_snapshot()
    print('快照条目: %d' % len(snap))
    print('')

    # 先把两个 prefab 读进内存，用于确认旧 hex32 还有引用
    ref_text = ''
    for rel in REF_FILES:
        p = os.path.join(ROOT, rel)
        if os.path.exists(p):
            ref_text += open(p, encoding='utf-8', errors='ignore').read()

    plan = []
    for rel in FIX_LIST:
        meta = os.path.join(ROOT, rel)
        if not os.path.exists(meta):
            print('  [缺失] %s' % rel)
            continue
        cur, idx, lines = read_guid(meta)
        old = snap.get(rel)
        ok_old = bool(old and HEX32.match(old))
        refs = ref_text.count(old) if ok_old else 0
        print('  %s' % rel)
        print('      当前: %s (长度 %d)' % (cur, len(cur or '')))
        print('      旧值: %s  %s' % (old, '✅合法hex32' if ok_old else '❌快照里没有/不合法'))
        print('      在 CharacterUI+BattleUI 里的引用数: %d' % refs)
        if ok_old and refs > 0 and cur != old:
            plan.append((rel, meta, old, cur, idx, lines))
        else:
            print('      -> 跳过（旧值不可用或已一致）')

    print('')
    if not plan:
        print('[中止] 没有可修的')
        return
    if not apply:
        print('[只读] 待修 %d 个。确认后加 --apply' % len(plan))
        return

    os.makedirs(BACKUP_DIR, exist_ok=True)
    stamp = time.strftime('%H%M%S')
    for rel, meta, old, cur, idx, lines in plan:
        name = os.path.basename(rel)
        shutil.copy2(meta, os.path.join(BACKUP_DIR, name))
        lines[idx] = 'guid: %s' % old
        open(meta, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
        print('  [已修] %s' % rel)
        print('          %s...  ->  %s' % (cur[:20], old))
    print('[完成] %d 个，备份在 %s' % (len(plan), BACKUP_DIR))
    print('🔴 接下来必须重启引擎才生效')


if __name__ == '__main__':
    main()
