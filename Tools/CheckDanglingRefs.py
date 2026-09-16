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
    1) 悬空引用：被引用的 guid 不在表里（含内置资源跳过）
    2) 形态非法：.meta 的 guid 不是「32 位十六进制」也不是「56 字符 base64（解码 41 字节）」
       —— 只有这两种形态团结引擎接受；44 字符 base64（解码 32 字节）会被拒绝并忽略该资源。
       详见 guid_kind() 的注释，别再把 581 个合法 base64 当成非法去改。
"""
import argparse
import base64
import pathlib
import re
import sys
from collections import defaultdict

ROOT = pathlib.Path(__file__).resolve().parent.parent

RG = re.compile(rb'guid:\s*([0-9A-Za-z+/\-_=]+)')
BUILTIN = re.compile(r'^0{16}[0-9a-f]0{15}$')
HEX32 = re.compile(r'^[0-9a-fA-F]{32}$')

GUID_DIRS = ['Assets', 'Packages', 'Library/PackageCache']
DEFAULT_SCAN = ['Assets/Resources/Prefabs', 'Assets/Scenes', 'Assets/Resources/UI']
EXTS = {'.prefab', '.unity', '.asset', '.mat', '.anim', '.controller',
        '.overridecontroller', '.playable', '.spriteatlas', '.physicsmaterial2d'}

# 第三方插件的 Demo/示例场景，断链属正常，不算问题
NOISE = ('/Demo/', '/DemoResources/', 'PreviewScene', 'Sample',
         'Sprite Shaders Ultimate', 'Epic Toon FX', 'SPUM',
         'Hyperbit', 'Pixel Craft VFX URP', 'UIShaderEffects',
         '2D Pixel RPG Monster Pack', 'RPG Props and Items')


def guid_kind(g):
    """判断 .meta 里 guid 的形态。

    2026-09-16 实测全工程 7746 个 .meta：
      32 字符十六进制              x 7164  合法（标准形态）
      56 字符 base64（解码 41 字节）x  581  合法（团结引擎自有格式，千万别改）
      44 字符 base64（解码 32 字节）x    1  非法，引擎直接忽略该资源

    早期版本曾把 581 个合法的 56 字符 base64 误判为非法并全量改写，
    导致 582 个资源重导入、UI 大面积缺图。判定务必按字节数，不要只看"是不是 base64"。
    """
    if HEX32.match(g):
        return 'hex32'
    try:
        raw = g.encode('ascii')
        dec = base64.b64decode(raw + b'=' * (-len(raw) % 4))
    except Exception:
        return 'unknown'
    if len(dec) == 41:
        return 'b64-ok'      # 团结引擎合法格式
    if len(dec) == 32:
        return 'b64-bad'     # 唯一被引擎拒绝的形态
    return 'unknown'


def bad_form_metas():
    """扫出形态非法的 .meta（引擎会忽略这些资源）。"""
    out = []
    for p in (ROOT / 'Assets').rglob('*.meta'):
        try:
            m = RG.search(p.read_bytes())
        except Exception:
            continue
        if not m:
            continue
        g = m.group(1).decode()
        k = guid_kind(g)
        if k in ('b64-bad', 'unknown'):
            out.append((p.relative_to(ROOT).as_posix(), g, k))
    return out


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
    print('===== 形态非法的 .meta guid（引擎会忽略该资源）=====')
    bad = bad_form_metas()
    if not bad:
        print('  无')
    for rel, g, k in bad:
        print('  [%s] %s' % (k, rel))
        print('        guid=%s' % g)

    print()
    print('===== 汇总 =====')
    print('  脚本类断链 guid 数 : %d' % len(real_script))
    print('  资源类断链 guid 数 : %d' % len(rows))
    print('  形态非法 .meta 数  : %d' % len(bad))
    if args.strict and (real_script or bad):
        sys.exit(1)


if __name__ == '__main__':
    main()
