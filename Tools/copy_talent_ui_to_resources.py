# -*- coding: utf-8 -*-
import os
import shutil

ROOT = os.path.join(os.path.dirname(__file__), '..')
SRC = os.path.join(ROOT, 'Assets', 'Art', 'UI', 'Talent')
DST = os.path.join(ROOT, 'Assets', 'Resources', 'UI', 'Talent')

NAMES = [
    '天赋_0020_bg',
    '天赋_0007_关闭',
    '天赋_0006_属性底',
    '天赋_0000_技能底',
    '天赋_0000s_0002_底条',
    '天赋_0000s_0001_重置天赋亮',
    '天赋_0001_金币升级',
    '天赋_0013_天赋石升级',
    '天赋_0004_基础属性未解锁',
    '天赋_0005_基础属性解锁',
    '天赋_0011_不可升级',
    '天赋_0012_可升级',
    '天赋_0009_链接2',
    '天赋_0008_lianjie3',
    '天赋_0002_图层-2',
    '天赋_0003_图层-3',
    '天赋_0016_技能可用-拷贝',
    '天赋_0018_技能可用',
    '天赋_0017_技能链接-拷贝',
    '天赋_0019_技能链接',
    '图层 4',
    '天赋_0015_箭头',
    '金币',
]

os.makedirs(DST, exist_ok=True)
for stem in NAMES:
    src = os.path.join(SRC, stem + '.png')
    dst = os.path.join(DST, stem + '.png')
    if os.path.isfile(src):
        shutil.copy2(src, dst)
        print('copied', stem)
    else:
        print('missing', src)
