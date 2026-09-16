# -*- coding: utf-8 -*-
"""
团结/Unity 工程断链（悬空引用）扫描器

背景（2026-09-16 事故）：
    本工程曾出现大量 prefab / scene 引用了工程里根本不存在的 guid，
    表现为 Inspector 里 "Missing Script"、UI 上图片空掉。
    guid 的唯一真值是 Assets 下的 .meta 文件；Library/ 只是导入缓存，
    删 Library 重新导入**修不了**断链，只会让问题暴露得更彻底。

用法：
    python Tools/CheckDanglingRefs.py                 # 扫游戏本体（默认范围）
    python Tools/CheckDanglingRefs.py --all           # 扫整个 Assets
    python Tools/CheckDanglingRefs.py --strict        # 有脚本类断链就以非 0 退出（CI/钩子用）

判定：
    guid 表 = Assets + Packages + Library/PackageCache 下所有 .meta 里的 guid
    内置资源 guid（形如 0000000000000000?000000000000000）跳过
    被引用的 guid 不在表里 => 悬空
"""
import argparse
import pathlib
import re
import sys
from collections import defaultdict

ROOT = pathlib.Path(__file__).resolve().parent.parent

RG = re.compile(rb'guid:\s*([0-9A-Za-z+/\-_=]+)')
BUILTIN = re.compile(r'^0{16}[0-9a-f]0{15}$')

GUID_DIRS = ['Assets', 'Packages', 'Library/PackageCache']
DEFAULT_SCAN = ['Assets/Resources/Prefabs', 'Assets/Scenes', 'Assets/Resources/UI']
EXTS = {'.prefab', '.unity', '.asset', '.mat', '.anim', '.controller',
        '.overridecontroller', '.playable', '.spriteatlas', '.physicsmaterial2d'}

# 第三方插件的 Demo/示例场景，断链属正常，不算问题
NOISE = ('/Demo/', '/DemoResources/', 'PreviewScene', 'Sample',
         'Sprite Shaders Ultimate', 'Epic Toon FX', 'SPUM',
         'Hyperbit', 'Pixel Craft VFX URP', 'UIShaderEffects',
         '2D Pixel RPG Monster Pack', 'RPG Props and Items')


def build_guid_table():
    table = {}
    for base in GUID_DIRS:
        d = ROOT / base
        if not d.exists():
            continue
        for p in d.rglob('*.meta'):
            try:
                m = RG.search(p.read_bytes())
            except Exception:
                continue
            if m:
                table.setdefault(m.group(1).decode(), p.relative_to(ROOT).as_posix())
    return table


def scan(dirs, table):
    script_hits = defaultdict(set)   # guid -> files
    asset_hits = defaultdict(lambda: defaultdict(set))  # guid -> field -> files
    for d in dirs:
        dd = ROOT / d
        if not dd.exists():
            continue
        for p in dd.rglob('*'):
            if not p.is_file() or p.suffix.lower() not in EXTS:
                continue
            rel = p.relative_to(ROOT).as_posix()
            try:
                txt = p.read_bytes().decode('utf-8', 'replace')
            except Exception:
                continue
            for ln in txt.splitlines():
                m = re.search(r'guid:\s*([0-9A-Za-z+/\-_=]+)', ln)
                if not m:
                    continue
                g = m.group(1)
                if BUILTIN.match(g) or g in table:
                    continue
                if 'm_Script' in ln:
                    script_hits[g].add(rel)
                else:
                    field = ln.strip().split(':')[0].strip()
                    asset_hits[g][field].add(rel)
    return script_hits, asset_hits


def is_noise(files):
    return all(any(n in f for n in NOISE) for f in files)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--all', action='store_true', help='扫整个 Assets（含第三方插件 Demo）')
    ap.add_argument('--strict', action='store_true', help='发现脚本类断链时退出码为 1')
    args = ap.parse_args()

    print('[1/2] 建立 guid 表 ...')
    table = build_guid_table()
    print('      共 %d 个 guid' % len(table))

    dirs = ['Assets'] if args.all else DEFAULT_SCAN
    print('[2/2] 扫描 %s ...' % ('整个 Assets' if args.all else '游戏本体'))
    script_hits, asset_hits = scan(dirs, table)

    print()
    print('===== 脚本类断链（Missing Script，必须修）=====')
    real_script = {g: f for g, f in script_hits.items() if not is_noise(f)}
    if not real_script:
        print('  无')
    for g, files in sorted(real_script.items(), key=lambda kv: -len(kv[1])):
        print('  %s  (%d 个文件)' % (g, len(files)))
        for f in sorted(files)[:8]:
            print('       ', f)

    print()
    print('===== 资源类断链（缺图/缺材质，按数量排序）=====')
    rows = []
    for g, fields in asset_hits.items():
        files = set()
        for k in fields:
            files |= fields[k]
        if is_noise(files):
            continue
        rows.append((len(files), g, sorted(fields.keys()), sorted(files)))
    rows.sort(reverse=True)
    if not rows:
        print('  无')
    total = 0
    for n, g, fields, files in rows:
        total += 1
        if total > 25:
            print('  ... 其余 %d 个 guid 省略' % (len(rows) - 25))
            break
        print('  %s | %s | %d 个文件' % (g, ','.join(fields[:4]), n))
        for f in files[:3]:
            print('       ', f)

    print()
    print('===== 汇总 =====')
    print('  脚本类断链 guid 数 : %d' % len(real_script))
    print('  资源类断链 guid 数 : %d' % len(rows))
    if args.strict and real_script:
        sys.exit(1)


if __name__ == '__main__':
    main()
