# -*- coding: utf-8 -*-
"""用 git archive 把 HEAD 上的原始装备模板导出到备份目录（本轮改坏时可整目录拷回）。"""
import io
import os
import subprocess
import tarfile

ROOT = r"Y:\PixelAdventureTown"
REL = "Assets/Resources/Config/Equips"
OUT = os.path.join(ROOT, ".workbuddy", "backup", "20261009-equip-templates-orig")

raw = subprocess.run(["git", "archive", "HEAD", REL], cwd=ROOT, stdout=subprocess.PIPE).stdout
os.makedirs(OUT, exist_ok=True)
with tarfile.open(fileobj=io.BytesIO(raw)) as tf:
    tf.extractall(OUT)
print("导出 %d 个文件 -> %s" % (len(os.listdir(os.path.join(OUT, REL))), OUT))
