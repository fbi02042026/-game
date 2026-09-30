# -*- coding: utf-8 -*-
"""
团结 56 字符 GUID 扫描器（按《团结引擎资源GUID编码规范 v2》§6.1 / §2 铁律 3 执行）

规范要点（已写入脚本行为，不得绕过）：
  - 56 字符 Base64 解码 = 40~42 字节，≠16 字节 → **不是合法 GUID Base64**，规范不允许"解码还原"式转换。
  - §6.1 三条检测条件（长度 24/22、字符集、解码恰好 16 字节）必须全部命中才告警转换；56 字符第一条就失败。
  - 扫描脚本对这类串：**不转换、不静默跳过**，打 Warning 并记录路径 + 原串，交人工确认。
  - 禁止为凑长度修改 GuidHelper.BusinessBase64ToEngineHex32。

默认行为：只出检测报告（不写任何文件）。
确实需要给资产换发新 hex32 标识时（人工确认后），显式加 --apply 执行。

2026-09-28 性能修复：引用扫描原来对每个 56 字符 guid 都重新 os.walk + 全量读盘
（304 × 全部 prefab/scene/asset），跑到超时。现改为**全量只读一遍**再按字符段比对，
判定结果与 --apply 行为完全不变（只是快了）。
"""
import os, re, sys, io, time, shutil, random
sys.stdout.reconfigure(encoding='utf-8')

# 仓库根：优先按脚本自身位置推导（换机 / 换盘符也能跑），推导不出来再退回本机硬编码路径。
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
if not os.path.isdir(os.path.join(ROOT, 'Assets')):
    ROOT = r'Y:\PixelAdventureTown'
APPLY = '--apply' in sys.argv

HEX32 = re.compile(r'^[0-9a-f]{32}$')
# 连续 56 个以上“GUID/Base64 字符”，用来把引用扫描从「每个 guid 全量重扫一遍」压成「全量只读一遍」。
GUID_RUN = re.compile(r'[0-9A-Za-z+/=]{56,}')
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

def _list_asset_files():
    """全部可能持有资源引用的文件（prefab / scene / asset），只枚举一次。"""
    out = []
    for dp, dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
        for f in fn:
            if f.endswith(ASSET_EXT):
                out.append(os.path.join(dp, f))
    return out

def count_refs(guids):
    """一次性读完 prefab/scene/asset，返回 {guid: 被多少个文件引用}。

    与原实现（对每个 guid 重新 os.walk + 全量读盘）**判定结果等价**：
    任何一次「子串命中」必然落在某个长度 ≥56 的同类字符连续段里，所以先切段再比对，
    不会漏报；也不会因为共用一次读盘而多报。
    """
    hits = dict((g, 0) for g in guids)
    gset = set(guids)
    for fp in _list_asset_files():
        try:
            t = io.open(fp, encoding='utf-8', errors='replace').read()
        except Exception:
            continue
        runs = set(GUID_RUN.findall(t))
        if not runs:
            continue
        matched = set(runs & gset)          # 整段就是一个 guid 的情况（绝大多数）
        for r in runs:
            if len(r) == 56:
                continue
            for g in guids:                 # 更长段里再找子串，保证不漏
                if g in r:
                    matched.add(g)
        for g in matched:
            hits[g] += 1
    return hits

# 1) 扫全工程 .meta
t0 = time.time()
b56, hexn, other = {}, 0, []
for dp, dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fn:
        if not f.endswith('.meta'):
            continue
        p = os.path.join(dp, f)
        g = read_guid(p)
        if g is None:
            continue
        if HEX32.match(g):
            hexn += 1
        elif len(g) == 56:
            b56[g] = p[:-5]
        else:
            other.append((len(g), p[:-5]))

print('=' * 68)
print('团结 GUID 扫描报告   模式: %s' % ('APPLY（人工确认后执行）' if APPLY else 'REPORT（只报告，不写盘）'))
print('=' * 68)
print('hex32 合法: %d    56字符Base64: %d    其它形态: %d' % (hexn, len(b56), len(other)))
print('meta 扫描耗时: %.2fs' % (time.time() - t0))

if not b56:
    print('\n✅ 没有 56 字符的 .meta guid，无需处理。')
    sys.exit(0)

# 2) 逐条 Warning + 是否被引擎引用（决定"换标识"是否安全）
print('\n=== ⚠️ 56 字符 Base64（非法 GUID Base64，规范不允许解码转换）===')
print('[扫描] 正在统计引用（prefab/scene/asset 只读一遍）...')
_t_ref = time.time()
_refs = count_refs(list(b56.keys()))
print('[扫描] 引用统计完成，耗时 %.2fs' % (time.time() - _t_ref))
by_dir = {}
safe, risky = [], []
for g, rel in b56.items():
    d = os.path.dirname(rel.replace(ROOT + '\\', '')) or '(根)'
    by_dir[d] = by_dir.get(d, 0) + 1
    hits = _refs.get(g, 0)
    (safe if hits == 0 else risky).append((rel, g, hits))

print('按目录分布:')
for d in sorted(by_dir):
    print('   %-52s %d' % (d, by_dir[d]))
print('\n被引擎引用（换标识会断链，禁止自动处理）: %d' % len(risky))
for rel, g, h in risky[:10]:
    print('   ✋ %s  (x%d)' % (rel.replace(ROOT + '\\', ''), h))
print('零引用（人工确认后可换发新 hex32）: %d' % len(safe))
for rel, g, h in safe[:10]:
    print('   ⚠️ %s  guid=%s...' % (rel.replace(ROOT + '\\', ''), g[:24]))

print('\n规范判定：56 字符解码约 41 字节 ≠ 16 字节 → 不是 §6.1 的合法 GUID Base64。')
print('          TryParseSuspectedGuidBase64 = false；BusinessBase64ToEngineHex32 会抛异常（防线，勿绕过）。')
print('          本脚本 --apply 做的不是"解码还原"，而是给资产**换发新的合法 hex32 标识**，必须人工确认。')

if not APPLY:
    print('\n[REPORT] 未做任何改动。确认要给零引用资产换发标识后加 --apply 执行。')
    sys.exit(0)

# 3) APPLY：仅处理零引用的，换发新 hex32
existing = set()
for dp, dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fn:
        if f.endswith('.meta'):
            g = read_guid(os.path.join(dp, f))
            if g:
                existing.add(g)
random.seed(int(time.time()))
ts = time.strftime('%Y%m%d_%H%M%S')
bk = os.path.join(ROOT, r'.workbuddy\backup', 'guid56_' + ts)
done = 0
for rel, g, h in safe:
    mp = rel + '.meta'
    raw = io.open(mp, encoding='utf-8').read()
    while True:
        cand = ''.join(random.choice('0123456789abcdef') for _ in range(32))
        if cand not in existing:
            existing.add(cand); break
    new_raw = re.sub(r'^(guid:\s*)' + re.escape(g) + r'(\s*)$',
                     lambda m: m.group(1) + cand + m.group(2), raw, count=1, flags=re.MULTILINE)
    os.makedirs(bk, exist_ok=True)
    bkp = os.path.join(bk, rel.replace(ROOT + '\\', '').replace('\\', '__') + '.meta.bak')
    shutil.copy2(mp, bkp)
    io.open(mp, 'w', encoding='utf-8', newline='').write(new_raw)
    done += 1
print('\n[APPLY] 已换发新 hex32 标识: %d 个（仅零引用项）' % done)
print('       备份目录:', bk)
print('       跳过（被引擎引用，需人工处理）:', len(risky))
print('\n下一步: 重启引擎让 Library 按新标识重建。')
