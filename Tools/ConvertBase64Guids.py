# -*- coding: utf-8 -*-
"""
把全工程 56 位 base64 guid 的 .meta 一次性转成 hex32（根治团结 base64 写不进 prefab 的坑）
- 只改 .meta 里第一行 `guid: <old>` -> `guid: <new>`
- 改前自动备份原 .meta 到 .workbuddy/backup/b64_guid_<时间戳>/
- 默认 dry-run，传 --apply 才真写
"""
import os, re, sys, io, json, time, shutil
sys.stdout.reconfigure(encoding='utf-8')
ROOT = r'Y:\PixelAdventureTown'
PLAN = os.path.join(ROOT, r'.workbuddy\base64_convert_plan.json')
APPLY = '--apply' in sys.argv

data = json.loads(io.open(PLAN, encoding='utf-8').read())
cmap = data['convert_map']          # old_guid -> new_hex32
paths = data['meta_paths']          # old_guid -> rel_path

print('模式:', 'APPLY（真改）' if APPLY else 'DRY-RUN（只看不改）')
print('待转换 .meta 数:', len(cmap))

# 备份目录
ts = time.strftime('%Y%m%d_%H%M%S')
bk = os.path.join(ROOT, r'.workbuddy\backup', 'b64_guid_' + ts)

done = 0
skipped = 0
errors = []
for old, new in cmap.items():
    rel = paths[old]
    mp = os.path.join(ROOT, rel + '.meta')
    if not os.path.exists(mp):
        errors.append((rel, 'meta不存在')); continue
    try:
        raw = io.open(mp, encoding='utf-8').read()
    except Exception as e:
        errors.append((rel, '读失败:%s' % e)); continue
    # 只替换第一处 guid: <old>
    pat = re.compile(r'^(guid:\s*)' + re.escape(old) + r'(\s*)$', re.MULTILINE)
    if not pat.search(raw):
        # 可能 old 后面有别的空白，宽松匹配
        pat2 = re.compile(r'^(guid:\s*)' + re.escape(old) + r'(\s*$)', re.MULTILINE)
        if not pat2.search(raw):
            skipped += 1
            errors.append((rel, '未找到旧guid行（可能已转过）'))
            continue
    new_raw = pat.sub(lambda m: m.group(1) + new + m.group(2), raw, count=1)
    if APPLY:
        # 备份
        os.makedirs(bk, exist_ok=True)
        bkp = os.path.join(bk, rel.replace('\\', '__').replace('/', '__') + '.meta.bak')
        os.makedirs(os.path.dirname(bkp), exist_ok=True)
        shutil.copy2(mp, bkp)
        # 写回（原子写：先写临时再替换，但 Windows 跨盘符 os.replace 可能受限，直接写）
        io.open(mp, 'w', encoding='utf-8', newline='').write(new_raw)
    done += 1

print('\n转换完成:', done)
print('跳过:', skipped)
if errors:
    print('错误/跳过明细（前20）:')
    for rel, msg in errors[:20]:
        print('  ', rel, '->', msg)
if APPLY:
    print('\n备份目录:', bk)
    print('备份文件数:', len(os.listdir(bk)) if os.path.exists(bk) else 0)
print('\n下一步: 重启团结引擎（让 Library 按 hex32 重建），再重新拖图/保存预制体。')
