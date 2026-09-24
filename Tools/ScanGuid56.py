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
"""
import os, re, sys, io, time, shutil, random
sys.stdout.reconfigure(encoding='utf-8')
ROOT = r'Y:\PixelAdventureTown'
APPLY = '--apply' in sys.argv

HEX32 = re.compile(r'^[0-9a-f]{32}$')

def read_guid(meta_path):
    try:
        for line in io.open(meta_path, encoding='utf-8', errors='replace'):
            m = re.match(r'^\s*guid:\s*(\S+)', line)
            if m:
                return m.group(1)
    except Exception:
        return None
    return None

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

if not b56:
    print('\n✅ 没有 56 字符的 .meta guid，无需处理。')
    sys.exit(0)

# 2) 逐条 Warning + 是否被引擎引用（决定"换标识"是否安全）
print('\n=== ⚠️ 56 字符 Base64（非法 GUID Base64，规范不允许解码转换）===')
by_dir = {}
safe, risky = [], []
for g, rel in b56.items():
    d = os.path.dirname(rel.replace(ROOT + '\\', '')) or '(根)'
    by_dir[d] = by_dir.get(d, 0) + 1
    # 在 prefab/scene/asset 里是否被引用
    hits = 0
    for dp, dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
        for f in fn:
            if not f.endswith(('.prefab', '.unity', '.asset')):
                continue
            try:
                t = io.open(os.path.join(dp, f), encoding='utf-8', errors='replace').read()
            except Exception:
                continue
            if g in t:
                hits += 1
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
