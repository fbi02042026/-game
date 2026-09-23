# -*- coding: utf-8 -*-
"""给 boss_stage_variant 表补 BOM、生成 .bytes 与两个 .meta（一次性脚本，可删）。"""
import os, uuid, io

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Assets", "Data", "Source", "Tables", "boss_stage_variant.csv")
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "Tables", "boss_stage_variant.bytes")

raw = io.open(SRC, "r", encoding="utf-8").read()
if raw.startswith("\ufeff"):
    raw = raw[1:]
text = "\ufeff" + raw.replace("\r\n", "\n")
io.open(SRC, "w", encoding="utf-8", newline="").write(text)
io.open(OUT, "w", encoding="utf-8", newline="").write(text)
print("csv/bytes written, chars =", len(text))

TEXT_META = "\ufefffileFormatVersion: 2\nguid: {g}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
CS_META = "fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def guid():
    return uuid.uuid4().hex


targets = [
    (SRC + ".meta", TEXT_META),
    (OUT + ".meta", TEXT_META),
    (os.path.join(ROOT, "Assets", "Scripts", "Config", "BossStageVariantTable.cs.meta"), CS_META),
]
for path, tpl in targets:
    if os.path.exists(path):
        print("skip (exists):", path)
        continue
    io.open(path, "w", encoding="utf-8", newline="").write(tpl.format(g=guid()))
    print("meta written:", path)
