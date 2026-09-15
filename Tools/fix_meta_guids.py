#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
修复被写成非法 base64 的 Unity .meta guid。

现象：Unity 报 "does not have a valid GUID and its corresponding Asset file will be ignored"。
根因：部分 .meta 的 `guid:` 字段是 base64 垃圾值，Unity 要求必须是 32 位小写 hex。

做法（安全、可重复）：
- 只扫描 Assets 下的 .meta
- 对 guid 非 32-hex 的文件，在字节级把该 guid 行的值替换为新的 uuid4().hex（32 位小写 hex）
- 只替换值本身，保留文件其余字节（包括 CRLF 换行），避免引入换行符噪音
- 已是合法 guid 的文件不动 -> 可反复运行

用法：
    python fix_meta_guids.py            # 实际修复
    python fix_meta_guids.py --dry-run  # 只列出待修复文件，不写盘
"""
import os
import re
import sys
import uuid

ASSETS = r"E:\xiangsumaoxian\Assets"
GUID_RE = re.compile(rb"guid:[ \t]*([^\r\n]+)")
HEX_RE = re.compile(r"^[0-9a-f]{32}$")


def main():
    dry = "--dry-run" in sys.argv
    fixed = 0
    skipped = 0
    for root, _, files in os.walk(ASSETS):
        for f in files:
            if not f.endswith(".meta"):
                continue
            p = os.path.join(root, f)
            try:
                with open(p, "rb") as fh:
                    data = fh.read()
            except Exception as e:  # noqa
                print("READ_ERR", p, e)
                continue
            m = GUID_RE.search(data)
            if not m:
                continue
            val = m.group(1).strip()
            try:
                val_str = val.decode("ascii")
            except Exception:
                val_str = repr(val)
            if HEX_RE.match(val_str):
                skipped += 1
                continue
            if dry:
                print("WOULD_FIX", p, "->", val_str)
                fixed += 1
                continue
            new = uuid.uuid4().hex.encode("ascii")
            new_data = data[: m.start(1)] + new + data[m.end(1) :]
            with open(p, "wb") as fh:
                fh.write(new_data)
            fixed += 1
    print("FIXED", fixed, "SKIPPED_VALID", skipped)


if __name__ == "__main__":
    main()
