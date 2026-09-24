# -*- coding: utf-8 -*-
# 把 DailyLoginFull.prefab 的 13 个断链 guid 还原成 每日登录/ 现有 12 张图的 guid
# 依据：断链节点 sizeDelta 与图原始尺寸完全吻合（非猜测）
import os, re, sys, io, shutil, time
sys.stdout.reconfigure(encoding='utf-8')
ROOT = r'Y:\PixelAdventureTown'
PF = os.path.join(ROOT, r'Assets\Resources\Prefabs\UI\DailyLoginFull.prefab')
APPLY = '--apply' in sys.argv

# 旧断链guid -> 新图guid（每日登录/ 现有图，已是hex32）
MAP = {
    '61dcb522359f5034c874b2c08edbb065': ('59bb9f5e6e39fbf998e0ba6f32be5961', '每日登录_0009_每日登录背景'),  # Bg_Full 750x1125
    '91d4d93e2d88e3f4096539a7767cd7f5': ('8a5c4d925c82c0d3b34a05f649d8fc91', '每日登录_0002_普通天'),      # Cell普通 148x175
    'caa967a0c911130448b7e2e635539bcc': ('1124f6b7ced8deb6dc897ea620efeba9', '每日登录_0004_限定英雄天'),   # Cell限定 148x175
    '9cdbe11831264434bb440419f6ed22ce': ('01aa15a89c3e67aa793fc0166f7cb463', '每日登录_0003_限定字底'),     # Rare 56x32
    'b7fdac602809ac3419f233a0c69479e9': ('9fe1387705da7d53f95ff0ebb3c7597a', '每日登录_0001_x2'),          # X2 50x50
    '00e7b8e8b812f274a9f5c3aba43da197': ('70b7f0bd897ceb316b7f1a2462e4b7f2', '每日登录_0000_累计登录底'),   # InfoBar/bg 171x95
    'eae2ae3c253f25a489ea100b5145f9f3': ('5a1265d7e763d7b00094496b06f0d7cf', '每日登录_0008_累计大底'),     # InfoBar 703x153
    '50b2d5d5d5e61a14e9a2c42cb68b460e': ('a832a199419f4b058384fc61363504bc', '关闭'),                       # CloseButton 94x93
    '48c696164e6fc334dbacf35d6a87767f': ('e55d51d03f13c44fc24ed130114cb426', '每日登录_0005_连续登录底'),   # Image 254x116
    '3b8268082c46cf249b474838b3ab2a7f': ('a1d968942c2a48f83aaa49d9038f41fa', '佣兵形象长条'),               # MercIcon 400x200
    'ec81b82ceeafc784a93d6e0e15079ccc': ('484481ea056ae589a5e798e2ef15a50f', '每日登录_0006_天数底'),       # CellsPanel 724x479
    '7f43ff541f84ec846be78837d1e243c9': ('a06737ee6c7e40d476b2b8b8542856d2', '每日登录_0007_每日登录字'),   # Title 425x236
}

txt = io.open(PF, encoding='utf-8', errors='replace').read()
print('模式:', 'APPLY' if APPLY else 'DRY-RUN')
print('prefab:', PF)

# 验证每个新 guid 在 Assets 里有对应 .meta
def guid_exists(g):
    for dp, dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
        for f in fn:
            if f.endswith('.meta'):
                try:
                    for line in io.open(os.path.join(dp, f), encoding='utf-8', errors='replace'):
                        if g in line:
                            return True
                except Exception:
                    pass
    return False

new_ok = {}
for old, (new, name) in MAP.items():
    new_ok[old] = guid_exists(new)
print('\n新 guid 是否都有对应资源:')
for old, (new, name) in MAP.items():
    print('  %-40s %s' % (name, '✓' if new_ok[old] else '!! 新guid也找不到资源'))

print('\n替换计数（旧guid在prefab出现次数 -> 新guid）:')
total = 0
new_txt = txt
for old, (new, name) in MAP.items():
    c = new_txt.count(old)
    total += c
    print('  %-30s old x%d  (%s)' % (name, c, old[:12]))
    new_txt = new_txt.replace(old, new)
print('  总替换处数:', total)

if APPLY:
    ts = time.strftime('%Y%m%d_%H%M%S')
    bk = os.path.join(ROOT, r'.workbuddy\backup', 'DailyLoginFull_rewire_' + ts + '.prefab.bak')
    os.makedirs(os.path.dirname(bk), exist_ok=True)
    shutil.copy2(PF, bk)
    io.open(PF, 'w', encoding='utf-8', newline='').write(new_txt)
    # 验证
    after = io.open(PF, encoding='utf-8', errors='replace').read()
    leftover = sum(after.count(old) for old in MAP)
    print('\n已写回。备份:', bk)
    print('写回后旧 guid 残留:', leftover)
    # 新 guid 都在吗
    ok = all(new in after for new, _ in MAP.values())
    print('12 个新 guid 全部在 prefab 里:', ok)
else:
    print('\n(dry-run，未写盘。确认后加 --apply 执行)')
