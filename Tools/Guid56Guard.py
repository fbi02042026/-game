# -*- coding: utf-8 -*-
"""
团结 GUID 守卫（56 字符防复发 · 快照 + diff）

为什么要有这个脚本：
  团结引擎"重新导入"时会把部分 .meta 的 guid 写成 **56 字符长串**（引擎自有标识，
  不是合法 hex32），prefab 里存的旧 hex32 引用立刻断链 → UI 空图。
  实测会反复长：2026-09-24 换发过 626 个，2026-09-29 又长出 309 个。
  主人 09-30 拍板：**以后尽量避免重新导入**，所以需要一个便宜的哨兵。

它做什么：
  快照存「全量 .meta 的 相对路径 → guid」（不只存 56 的），这样能抓出
  **hex32 → 56 的蜕变**——那才是真正会断链的信号。只存 56 的不够用。
  对蜕变的项，还会拿快照里的旧 hex32 去 prefab/scene/asset 里查有没有残留引用，
  直接判定"断链了没"，不用人肉去编辑器里看。

用法：
  python Tools/Guid56Guard.py             # 默认：扫 + 比对 + 报（不写任何文件）
  python Tools/Guid56Guard.py --save      # 把当前状态存为基线快照
  python Tools/Guid56Guard.py --fast      # 跳过"残留引用确认"，只做 diff（更快）

默认不写盘、不改任何资源。快照放 .workbuddy/（被 .gitignore 排除，不进仓库、不跨机）。
"""
import os, re, sys, io, time
sys.stdout.reconfigure(encoding='utf-8')

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
if not os.path.isdir(os.path.join(ROOT, 'Assets')):
    ROOT = r'Y:\PixelAdventureTown'

SNAP = os.path.join(ROOT, '.workbuddy', 'guid56_snapshot.txt')
SAVE = '--save' in sys.argv
FAST = '--fast' in sys.argv

HEX32 = re.compile(r'^[0-9a-f]{32}$')
HEX32_RUN = re.compile(r'[0-9a-f]{32}')
ASSET_EXT = ('.prefab', '.unity', '.asset')


def read_guid(meta_path):
    try:
        for line in io.open(meta_path, encoding='utf-8', errors='replace'):
            m = re.match(r'^\s*guid:\s*(\S+)', line)
            if m:
                return m.group(1)
    except Exception:
        return None
    return None


def scan():
    """返回 {相对路径: guid}，只扫 Assets 下的 .meta。"""
    out = {}
    adir = os.path.join(ROOT, 'Assets')
    for dp, dn, fn in os.walk(adir):
        for f in fn:
            if not f.endswith('.meta'):
                continue
            p = os.path.join(dp, f)
            g = read_guid(p)
            if g:
                out[os.path.relpath(p, ROOT)] = g
    return out


def kind(g):
    if HEX32.match(g):
        return 'hex32'
    if len(g) == 56:
        return '56'
    return 'other'


def find_refs(guids):
    """在 prefab/scene/asset 里查这些 guid 还有没有残留引用。全量只读一遍。"""
    if not guids:
        return {}
    gset = set(guids)
    hits = dict((g, []) for g in guids)
    adir = os.path.join(ROOT, 'Assets')
    for dp, dn, fn in os.walk(adir):
        for f in fn:
            if not f.endswith(ASSET_EXT):
                continue
            fp = os.path.join(dp, f)
            try:
                t = io.open(fp, encoding='utf-8', errors='replace').read()
            except Exception:
                continue
            found = set(HEX32_RUN.findall(t)) & gset
            for g in found:
                hits[g].append(os.path.relpath(fp, ROOT))
    return hits


# ---------------------------------------------------------------- 主流程
t0 = time.time()
cur = scan()
print('=' * 68)
print('团结 GUID 守卫（56 字符防复发）   模式: %s' % ('SAVE（写基线快照）' if SAVE else 'CHECK（只报告，不写盘）'))
print('=' * 68)
print('Assets 下 .meta 总数: %d    扫描耗时: %.2fs' % (len(cur), time.time() - t0))

cur56 = [(p, g) for p, g in cur.items() if kind(g) == '56']
cur_bad = [(p, g) for p, g in cur.items() if kind(g) == 'other']
print('当前 56 字符: %d    其它非法形态: %d    合法 hex32: %d'
      % (len(cur56), len(cur_bad), len(cur) - len(cur56) - len(cur_bad)))

if SAVE:
    os.makedirs(os.path.dirname(SNAP), exist_ok=True)
    with io.open(SNAP, 'w', encoding='utf-8', newline='') as fh:
        fh.write('# 团结 .meta guid 基线快照（.workbuddy 下，不进仓库、不跨机）\n')
        fh.write('# 生成时间: %s\n' % time.strftime('%Y-%m-%d %H:%M:%S'))
        fh.write('# 格式: 相对路径\\tguid\n')
        for p in sorted(cur):
            fh.write('%s\t%s\n' % (p, cur[p]))
    print('\n✅ 已写入基线快照: %s' % SNAP)
    print('   共 %d 条。下次跑 %s 不带参数即可比对。' % (len(cur), os.path.basename(__file__)))
    sys.exit(0)

if not os.path.exists(SNAP):
    print('\n⚠ 还没有基线快照，无法比对。先跑一次：')
    print('   python Tools/Guid56Guard.py --save')
    sys.exit(0)

# 读快照
old = {}
with io.open(SNAP, encoding='utf-8', errors='replace') as fh:
    for line in fh:
        line = line.rstrip('\n')
        if not line or line.startswith('#'):
            continue
        parts = line.split('\t')
        if len(parts) == 2:
            old[parts[0]] = parts[1]

added = [p for p in cur if p not in old]
gone = [p for p in old if p not in cur]
mutated = [(p, old[p], cur[p]) for p in cur if p in old and old[p] != cur[p]]

to56 = [(p, o, n) for p, o, n in mutated if kind(o) == 'hex32' and kind(n) == '56']
fixed = [(p, o, n) for p, o, n in mutated if kind(o) == '56' and kind(n) == 'hex32']
reshuffled = [(p, o, n) for p, o, n in mutated
              if not (p, o, n) in to56 and not (p, o, n) in fixed]

print('\n=== 与基线快照的差异（快照生成于 %s）==='
      % time.strftime('%Y-%m-%d %H:%M', time.localtime(os.path.getmtime(SNAP))))
print('  新增资源: %d    删除资源: %d    guid 变化: %d'
      % (len(added), len(gone), len(mutated)))

if not to56 and not fixed and not reshuffled:
    print('\n✅ 没有任何 .meta 的 guid 发生形态蜕变，56 字符没有新增。')

if to56:
    print('\n🔴🔴 高危：hex32 → 56 字符（重新导入写坏的，会断链）: %d 个' % len(to56))
    if FAST:
        print('   （--fast 模式，跳过残留引用确认）')
    else:
        print('   正在查这些旧 hex32 在 prefab/scene/asset 里还有没有残留引用...')
        t1 = time.time()
        refs = find_refs([o for _, o, _ in to56])
        print('   引用确认完成，耗时 %.2fs' % (time.time() - t1))
        broken = 0
        for p, o, n in to56:
            r = refs.get(o, [])
            if r:
                broken += 1
                print('   ✋ %s' % p)
                for x in r[:3]:
                    print('        被引用: %s' % x)
                if len(r) > 3:
                    print('        ...共 %d 处' % len(r))
        print('   其中确认断链: %d / %d'
              % (broken, len(to56)))
        if broken:
            print('\n   修法（须人工确认）：把 prefab 里已存的旧 hex32 写回该 .meta 的 guid: 一行，')
            print('   或换发全新 hex32 并同步改引用。改完**必须重启引擎**才生效。')

if fixed:
    print('\n✅ 已修好（56 → hex32）: %d 个' % len(fixed))
    for p, o, n in fixed[:12]:
        print('   %s' % p)
    if len(fixed) > 12:
        print('   ...共 %d 个' % len(fixed))

if reshuffled:
    print('\n⚠️ 换了另一个 hex32（可能是换发，也可能撞号）: %d 个' % len(reshuffled))
    for p, o, n in reshuffled[:10]:
        print('   %s\n      %s -> %s' % (p, o, n))
    if len(reshuffled) > 10:
        print('   ...共 %d 个' % len(reshuffled))

if added:
    print('\n🆕 新增资源: %d 个（新拖的图 / 新建的资产）' % len(added))
    for p in added[:8]:
        print('   %s   [%s]' % (p, kind(cur[p])))
    if len(added) > 8:
        print('   ...共 %d 个' % len(added))
    new56 = [p for p in added if kind(cur[p]) == '56']
    if new56:
        print('   其中一进来就是 56 字符: %d 个（新资源，暂无旧引用，暂不会断链）' % len(new56))

if gone:
    print('\n🗑 删除资源: %d 个' % len(gone))
    for p in gone[:8]:
        print('   %s' % p)
    if len(gone) > 8:
        print('   ...共 %d 个' % len(gone))

print('\n' + '=' * 68)
print('当前 56 字符总量: %d' % len(cur56))
if cur56:
    by = {}
    for p, g in cur56:
        d = os.path.dirname(p) or '(根)'
        by[d] = by.get(d, 0) + 1
    print('按目录分布（前 10）:')
    for d in sorted(by, key=lambda k: -by[k])[:10]:
        print('   %-52s %d' % (d, by[d]))
print('\n顺带可跑断链检查: python Tools/CheckDanglingRefs.py --strict')
print('本脚本未做任何改动（.meta 一字未动）。')
