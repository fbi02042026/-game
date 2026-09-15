#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
安装 git pre-commit 钩子：提交前跑 CheckGuidRefs.py，发现 guid 悬空/非法就拦截提交。

用法：
    python Tools/install-git-hooks.py          # 安装/更新 pre-commit
    python Tools/install-git-hooks.py --remove # 卸载
可反复运行；会先备份已有的 pre-commit 为 pre-commit.bak。
跳过本次拦截：git commit --no-verify
"""
from __future__ import annotations

import os
import shutil
import stat
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HOOK = os.path.join(ROOT, ".git", "hooks", "pre-commit")

HOOK_BODY = """#!/bin/sh
# 由 Tools/install-git-hooks.py 生成 —— 提交前校验 guid 完整性。
# hex / base64 均为合法 guid；只拦「引用悬空」。详见 Tools/CheckGuidRefs.py 顶部说明。
PY="{py}"
if [ ! -x "$PY" ]; then
    for c in python python3 py; do
        if command -v "$c" >/dev/null 2>&1; then PY="$c"; break; fi
    done
fi
if ! command -v "$PY" >/dev/null 2>&1; then
    echo "[pre-commit] 未找到 python，跳过 guid 校验"
    exit 0
fi
"$PY" "{script}" --quiet
if [ $? -ne 0 ]; then
    echo ""
    echo "✖ 预提交被拦截：检测到 guid 悬空引用或非法 .meta。"
    echo "  运行  python Tools/CheckGuidRefs.py  查看明细（也会写 Tools/guid-audit-report.txt）。"
    echo "  注意：不要用「base64↔hex 互转」去'修'，那会造成引用悬空（详见脚本头部说明）。"
    echo "  确需跳过本次检查： git commit --no-verify"
    exit 1
fi
exit 0
"""


def main() -> int:
    if "--remove" in sys.argv:
        if os.path.isfile(HOOK):
            os.remove(HOOK)
            print("已卸载 pre-commit:", HOOK)
        else:
            print("没有可卸载的 pre-commit")
        return 0

    hooks_dir = os.path.dirname(HOOK)
    if not os.path.isdir(hooks_dir):
        print("找不到 .git/hooks 目录，确认这是 git 仓库根目录：", ROOT)
        return 1

    if os.path.isfile(HOOK):
        shutil.copy2(HOOK, HOOK + ".bak")
        print("已备份原钩子 ->", HOOK + ".bak")

    py = sys.executable or "python"
    script = os.path.join(ROOT, "Tools", "CheckGuidRefs.py").replace("\\", "/")
    with open(HOOK, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(HOOK_BODY.format(py=py.replace("\\", "/"), script=script))
    os.chmod(HOOK, os.stat(HOOK).st_mode | stat.S_IEXEC | stat.S_IXGRP | stat.S_IXOTH)
    print("已安装 pre-commit ->", HOOK)
    print("  python:", py)
    return 0


if __name__ == "__main__":
    sys.exit(main())
