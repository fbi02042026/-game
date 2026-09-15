# -*- coding: utf-8 -*-
"""对照实验：用已知存在的 guid 探测 SourceAssetDB 的存储格式"""
import os
import re

PROJ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def find_metas(base, limit=8):
    out = []
    for dp, _dn, fn in os.walk(os.path.join(PROJ, base)):
        for f in fn:
            if not f.endswith(".png.meta"):
                continue
            p = os.path.join(dp, f)
            with open(p, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid:\s*(\S+)", fh.read(4000), re.M)
            if m:
                out.append((m.group(1), os.path.relpath(p[:-5], PROJ)))
            if len(out) >= limit:
                return out
    return out


def main():
    samples = find_metas(os.path.join("Assets", "Art"))
    for dbname in ["SourceAssetDB", "ArtifactDB"]:
        path = os.path.join(PROJ, "Library", dbname)
        if not os.path.isfile(path):
            continue
        with open(path, "rb") as fh:
            blob = fh.read()
        print("== %s (%.1f MB)" % (dbname, len(blob) / 1048576.0))
        for g, rel in samples:
            raw = bytes.fromhex(g)
            variants = {
                "原序": raw,
                "反转": raw[::-1],
                "前4交换": raw[0:4][::-1] + raw[4:8][::-1] + raw[8:12][::-1] + raw[12:16],
            }
            found = []
            for vn, v in variants.items():
                pos = blob.find(v)
                if pos >= 0:
                    found.append("%s@%d" % (vn, pos))
            print("  %s  %-46s %s" % (g, rel[:46], ",".join(found) or "未命中"))


if __name__ == "__main__":
    main()
