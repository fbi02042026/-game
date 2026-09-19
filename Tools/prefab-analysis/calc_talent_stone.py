# -*- coding: utf-8 -*-
"""
天赋石产出模型 V2（2026-09-19）：冒险日志发放版
- 每关首次通关：按章 2/3/4/5/6/7/8/10 石（每章 10 关）
- 整章通关额外：3/5/7/9/11/14/17/20 石
- 每日保底：登录 3 + 每日任务 5 = 8 石/天
- 战斗内不再掉天赋石（精英/Boss 只掉金币装备）
- 重复通关：0 石

需求侧：开启全部 455 石 / 全部满级约 1150 石（见 Docs/天赋右列重制方案_2026-09-19.md）
旧版金币换算模型已作废（金币⇄天赋石兑换已取消）。
"""

PER_STAGE = [2, 3, 4, 5, 6, 7, 8, 10]          # 每通 1 关
CHAPTER_BONUS = [3, 5, 7, 9, 11, 14, 17, 20]   # 整章通关额外
STAGES_PER_CHAPTER = 10
DAILY_BASE = 8                                  # 每日保底
NEED_OPEN_ALL = 455
NEED_MAX_ALL = 1150

chapter_totals = [PER_STAGE[i] * STAGES_PER_CHAPTER + CHAPTER_BONUS[i] for i in range(8)]
total = sum(chapter_totals)

print("章 | 每关石 | 章节奖励 | 章小计 | 累计")
cum = 0
for i, t in enumerate(chapter_totals):
    cum += t
    print(" %d | %4dx10=%3d | %4d | %4d | %4d" % (
        i + 1, PER_STAGE[i], PER_STAGE[i] * STAGES_PER_CHAPTER, CHAPTER_BONUS[i], t, cum))

print()
print("一周目固定产出（日志） =", total, "石")
print("开启全部需求 =", NEED_OPEN_ALL, "-> 一周目覆盖率 %.0f%%" % (total / NEED_OPEN_ALL * 100))
print("全部满级需求 ~", NEED_MAX_ALL, "-> 需要 %.2f 个周目" % (NEED_MAX_ALL / total))
print()
print("加每日保底后：")
for days in (15, 20, 25, 30):
    t = total + DAILY_BASE * days
    print("  %2d 天打完 -> 总产出 %4d 石  开启覆盖率 %.0f%%  满级进度 %.0f%%" % (
        days, t, t / NEED_OPEN_ALL * 100, t / NEED_MAX_ALL * 100))
