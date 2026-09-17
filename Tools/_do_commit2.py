# -*- coding: utf-8 -*-
"""提交：Resources 同步产物 + 待办清单文档。"""
import subprocess, os
from pathlib import Path

ROOT = r'Y:\PixelAdventureTown'
os.chdir(ROOT)


def git(*args):
    r = subprocess.run(['git', '-c', 'core.quotepath=false'] + list(args),
                       capture_output=True, text=True, encoding='utf-8', errors='replace')
    return r.stdout.strip(), r.stderr.strip(), r.returncode


# 先删自己的写入脚本，免得被一起提交
prev = Path('Tools/_write_todo.py')
if prev.exists():
    prev.unlink()
    print('已删除 Tools/_write_todo.py')

out, err, rc = git('add', '-A')
print('add rc=', rc)

out, err, rc = git('status', '--porcelain')
lines = out.splitlines()
staged = [l for l in lines]
untracked = [l for l in lines if l.startswith('??')]
print('提交条目数 =', len(staged), ' 其中未跟踪 =', len(untracked))
for l in untracked[:25]:
    print('   ', l)

msg = """chore(art): 同步徽记/碎片底版到 Resources + 补待办清单文档

【美术资源同步 Art -> Resources】
- Resources/Icons/JobBadge 18 张（6 职业 x 普通/稀有/传奇）
- Resources/Icons/MercFragmentBase 3 张（普通/稀有/传奇碎片底版）
- 导入设置与菜单工具一致：
  Sprite / Single / nPOT=None / mipmap off / alphaIsTransparency / FullRect
  （FullRect 是给 Mask 裁剪用的，Tight 网格会让裁剪边缘不贴合）
- Resources/Icons/MercHead / MercStand 一并与 Art 对齐
  （代码按「路径」Resources.Load，guid 变化不影响引用）
- 新增两个目录的 .meta，保证资源在打包时被收录

【文档】
- Docs/待办清单_2026-09-17.md
  集中记录：已做完的 / 需要你在 Unity 里验证的 / 需要拍板的 4 条 /
  徽记与碎片卡待指定落点（附依赖前提）/ 已确认不用管的历史遗留

【提交前自检】
- Tools/CheckDanglingRefs.py --strict：无新增断链
  脚本类 1 个 = WorldMapPopup 已知误报
  资源类 10 个 = 既有潜在风险清单
  形态非法 .meta = 0
- guid 总数 15453 -> 15476（+23 = 21 张图 + 2 个目录 .meta，对得上）
"""

Path('Tools/_commit_msg.txt').write_text(msg, encoding='utf-8')
out, err, rc = git('commit', '-F', 'Tools/_commit_msg.txt')
print()
print('=== commit rc =', rc, '===')
print(out[:2500])
print(err[:800])
m = Path('Tools/_commit_msg.txt')
if m.exists():
    m.unlink()
