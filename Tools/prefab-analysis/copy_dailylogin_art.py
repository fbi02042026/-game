# -*- coding: utf-8 -*-
"""把 Assets/Art/UI/每日登录 的 9 张图复制一份到 Assets/Resources/UI/每日登录（供 Resources.Load 用）。
- 只复制，不动 Art 源图，不改任何 prefab / 不改界面代码
- 每张图配 .meta（Sprite(2D and UI) 模板，guid / spriteID 重新生成，避免与其它资源重复）
- 目录本身也配 .meta（folderAsset）
"""
import os, io, shutil, uuid

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC_DIR = os.path.join(ROOT, "Assets", "Art", "UI", "每日登录")
DST_DIR = os.path.join(ROOT, "Assets", "Resources", "UI", "每日登录")
TPL_PNG = os.path.join(ROOT, "Assets", "Resources", "UI", "Common", "锁.png.meta")

FOLDER_META = "fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

if not os.path.isdir(SRC_DIR):
    raise SystemExit("源目录不存在: " + SRC_DIR)

os.makedirs(DST_DIR, exist_ok=True)
folder_meta = DST_DIR + ".meta"
if not os.path.exists(folder_meta):
    io.open(folder_meta, "w", encoding="utf-8", newline="").write(FOLDER_META.format(g=uuid.uuid4().hex))
    print("meta:", folder_meta)

tpl = io.open(TPL_PNG, "r", encoding="utf-8").read()
if "textureType: 8" not in tpl or "spriteMode: 1" not in tpl:
    raise SystemExit("模板不是 Sprite(2D and UI)，中止")

import re
files = sorted(f for f in os.listdir(SRC_DIR) if f.lower().endswith(".png"))
print("源图数量:", len(files))
for fn in files:
    src = os.path.join(SRC_DIR, fn)
    dst = os.path.join(DST_DIR, fn)
    if not os.path.exists(dst):
        shutil.copy2(src, dst)
        print("copy:", fn)
    meta_path = dst + ".meta"
    if os.path.exists(meta_path):
        print("skip meta (exists):", fn)
        continue
    m = tpl
    m = re.sub(r"guid: \S+", "guid: " + uuid.uuid4().hex, m, count=1)
    m = re.sub(r"spriteID: \S*", "spriteID: " + uuid.uuid4().hex, m, count=1)
    io.open(meta_path, "w", encoding="utf-8", newline="").write(m)
    print("meta:", fn + ".meta")

print("完成。Resources 加载路径示例: UI/每日登录/每日登录_0009_每日登录背景")
