# -*- coding: utf-8 -*-
"""
战斗/关卡/经济数值体检（2026-10-24）
数据真源：
- Assets/Data/Source/Tables/monster_stats.csv（怪血/攻/金/经验）
- GameConfig.Combat.cs / Progression.cs / Waves.cs（常量，见下方 CONSTANTS，逐条注明出处行号语义）
- StageGoldDefs.cs / ActProgressionDefs.cs / EconomyDefs.cs / SlotMachineDefs 口径（分册 抽奖与经济.md）
- player_job_base_stats.csv（职业基底）
运行：python Tools/balance_audit_20261024.py
标注约定：[表]=读真实 CSV；[码]=代码常量；[假设]=本次审计假设，需主人确认
"""
import csv, math, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSV_MONSTER = os.path.join(ROOT, "Assets", "Data", "Source", "Tables", "monster_stats.csv")

# ---------- [码] 常量（出处见注释） ----------
CHAPTER_SCALE = {1:0.88, 2:1.10, 3:1.55, 4:1.72, 5:1.24, 6:1.50, 7:1.72, 8:2.12}  # chapter_stat_scale.csv
STAGE_RAMP = [1.00,1.03,1.06,1.11,1.18,1.23,1.28,1.33,1.41,1.50]                  # stage_stat_scale.csv（10 档）
SUBS_PER_GRAND = 5            # GameConfig.Progression.cs:60
GRANDS_PER_ACT = {1:10,2:10,3:14,4:16,5:18,6:18,7:20,8:20}  # ActProgressionDefs.ActStageCounts /5
HP_GLOBAL = 0.5               # MONSTER_HP_GLOBAL_MUL（10-22 砍半）
DMG_MUL = 1.8                 # MONSTER_DAMAGE_MULTIPLIER
BOSS_TTK_HP = 1.35            # BOSS_TTK_HP_MUL
ELITE_TTK_HP = 1.15           # ELITE_TTK_HP_MUL
BOSS_TTK_ATK = 1.35           # BOSS_TTK_ATK_MUL
MINI_BOSS = 0.6               # MINI_BOSS_STAT_MUL（B1 后 stageIdx0==4 覆盖所有大关 Boss）
ELITE_HP_AVG = 2.6            # ELITE_HP_FROM_CHAPTER_AVG
ELITE_ATK_AVG = 1.6           # ELITE_ATK_FROM_CHAPTER_AVG
ELITE_TANK = (1.1, 0.92)      # ELITE_TANK_HP/ATK_MUL
ELITE_GLASS = (0.75, 1.35)    # ELITE_GLASS_HP/ATK_MUL
RANGED_MUL = 0.675            # MONSTER_RANGED_ATK_MUL（Boss 不吃）
DEF_N, DEF_E, DEF_B = 2.0, 6.0, 12.0   # MONSTER_NORMAL/ELITE/BOSS_DEF
ATK_SPD = 0.39                # MONSTER_ATK_SPEED_MUL × 表 1.5 = 0.585 次/秒
MON_BASE, MON_STEP = 10, 4.5  # 每小关怪量 10,14.5,19,23.5,28
GOLD_N, GOLD_E, GOLD_B = 80, 100, 200    # StageGoldDefs
GOLD_CH_GROW = 0.1            # CHAPTER_GOLD_GROWTH
CLEAR_N = lambda ch: 3 + ch   # EconomyDefs.NormalClearGold
CLEAR_B = {1:20,2:30,3:40,4:50,5:60,6:70,7:80,8:200}  # EconomyDefs.BossClearGold
WAVE_MUL_AVG = 1.1            # 波次系数 1+wave*0.05 取中值 [假设]
DEF_K = 100.0                 # DamageFormula.DefenseK
GOLD_PER_STONE = 100          # GOLD_PER_TALENT_POINT
ADVENTURE_COST = 10           # StaminaSystem.ADVENTURE_COST（EconomyDefs 引用）

def dr(defense):  # 乘法减伤，0.75 封顶
    d = max(0.0, defense)
    return min(0.75, d / (d + DEF_K))

# ---------- [表] 怪物表 ----------
rows = []
with open(CSV_MONSTER, encoding="utf-8-sig") as f:
    for line in f:
        line = line.strip()
        if not line or line.startswith("#") or line.startswith("id,"):
            continue
        rows.append(line.split(","))
mons = {}
for c in rows:
    mons.setdefault(int(c[1]), []).append({
        "name": c[3], "hp": float(c[7]), "atk": float(c[8]), "gold": float(c[11]),
        "exp": float(c[12]), "boss": c[5] == "1",
    })

def ch_avg(ch):
    ns = [m for m in mons[ch] if not m["boss"]]
    return (sum(m["hp"] for m in ns)/len(ns), sum(m["atk"] for m in ns)/len(ns),
            sum(m["exp"] for m in ns)/len(ns))

def boss_pair(ch):
    bs = [m for m in mons[ch] if m["boss"]]
    return bs[0], bs[1]

print("="*100)
print("一、怪物数值曲线（普通怪=章均值；倍率=章×大关内爬坡；血含全局砍半0.5；攻含1.8×章减免）")
print("="*100)
hdr = f"{'章':<3}{'章倍率':>6} | {'小怪HP':>8}{'小怪近攻':>9}{'小怪远攻':>9}{'防':>6} | {'精英HP':>9}{'精英攻':>8} | {'BossA HP':>10}{'BossA攻':>9} | {'BossB HP':>10}{'BossB攻':>9}"
print(hdr)
for ch in range(1, 9):
    cs = CHAPTER_SCALE[ch]
    avgHp, avgAtk, avgExp = ch_avg(ch)
    ba, bb = boss_pair(ch)
    ramp0, ramp4 = STAGE_RAMP[0], STAGE_RAMP[4]
    # 大关1-1（ramp 1.00，wave 取 1.0）
    nHp0 = avgHp*cs*ramp0*HP_GLOBAL
    nAtk0 = avgAtk*cs*ramp0*DMG_MUL*({1:0.35,2:0.35,3:0.6}.get(ch,0.8))
    nAtkR0 = nAtk0*RANGED_MUL
    # 大关5-5 Boss 前普通怪（ramp 1.18，wave 1.2 末波）
    nHp4 = avgHp*cs*ramp4*HP_GLOBAL*1.2
    nAtk4 = nAtk0*ramp4/1.0*1.2
    # 精英（大关3小关 ramp≈1.06~1.11 取 1.11 tank 档）
    eHp = avgHp*ELITE_HP_AVG*ELITE_TANK[0]*cs*1.11*1.15*HP_GLOBAL
    eAtk = avgAtk*ELITE_ATK_AVG*ELITE_TANK[1]*cs*1.11*DMG_MUL*({1:0.35,2:0.35,3:0.6}.get(ch,0.8))
    # Boss（大关5 ramp 1.18，wave1.2；全部 ×MINI_BOSS 0.6 —— 见发现F2）
    bAtkMul = ({1:0.35,2:0.35,3:0.6}.get(ch,0.8))
    def boss_eff(b):
        hp = b["hp"]*cs*ramp4*BOSS_TTK_HP*MINI_BOSS*HP_GLOBAL*1.2
        atk = b["atk"]*cs*ramp4*DMG_MUL*BOSS_TTK_ATK*bAtkMul
        return hp, atk
    bhA, baA = boss_eff(ba); bhB, baB = boss_eff(bb)
    dN, dE, dB = DEF_N*cs, DEF_E*cs, DEF_B*cs
    print(f"{ch:<3}{cs:>6.2f} | {nHp0:>5.1f}/{nHp4:<5.1f}{nAtk0:>5.2f}/{nAtk4:<5.2f}{nAtkR0:>5.2f}{'':>2}{dN:>4.1f} | {eHp:>9.1f}{eAtk:>8.2f} | {bhA:>10.1f}{baA:>9.2f} | {bhB:>10.1f}{baB:>9.2f}")

print()
print("发现 F3 验证 —— 主线 Boss 血量倒挂（Boss 表值×章倍率×1.18×1.35×0.6×0.5×1.2）")
eff = {}
for ch in range(1, 9):
    ba, bb = boss_pair(ch)
    cs = CHAPTER_SCALE[ch]
    eff[ch] = (ba["hp"]*cs, bb["hp"]*cs)
print(f"  ch2 BossA 有效血 {eff[2][0]:.0f}  vs  ch5 BossA {eff[5][0]:.0f}  → ch5 是 ch2 的 {eff[5][0]/eff[2][0]*100:.0f}%")
print(f"  ch4 BossB 有效血 {eff[4][1]:.0f}  vs  ch5 BossB {eff[5][1]:.0f}  → ch5 是 ch4 的 {eff[5][1]/eff[4][1]*100:.0f}%")

print()
print("="*100)
print("二、玩家成长 vs 关卡需求（剑盾 P001 基准；[假设A] 每章装备/抽奖攻击成长 +35%/章线性；佣兵/天赋另列）")
print("="*100)
# 玩家每局升级：exp = Σ小怪 exp + Boss 100
for tag, ch in [("ch1",1),("ch4",4),("ch8",8)]:
    avgHp, avgAtk, avgExp = ch_avg(ch)
    exp_run = sum((MON_BASE + MON_STEP*s) for s in range(5))*avgExp + 100
    # 50L + 10L(L+1) + 0? Σ_{i=0}^{L-1}(50+20i) = 50L + 10L(L-1)
    L = int((math.sqrt(50*50 + 40*exp_run) - 50) / 40) if exp_run > 0 else 0
    print(f"  {tag} 单次大关（5小关）经验 ≈ {exp_run:.0f} → 升 {L} 级（+{3*L}攻 +{25*L}血 +{L}防）")

def hits_player(dmg_player_atk, target_hp, target_def):
    return target_hp / max(1.0, dmg_player_atk*(1-dr(target_def)))

def surv_time(mon_atk, player_hp, player_def, mon_rate=0.585):
    per_hit = mon_atk*(1-dr(player_def))
    return player_hp / max(0.1, per_hit*mon_rate)

GEAR = lambda ch: 1 + 0.35*(ch-1)   # [假设A]
print(f"{'章':<4}{'局等级':>5}{'玩家攻':>8}{'玩家血':>8}{'玩家防':>7} | {'砍小怪刀':>8}{'砍精英刀':>9}{'砍Boss刀':>9} | {'被小怪杀(s)':>10}{'被Boss杀(s)':>11}")
for ch in range(1, 9):
    avgHp, avgAtk, avgExp = ch_avg(ch)
    exp_run = sum((MON_BASE + MON_STEP*s) for s in range(5))*avgExp + 100
    L = int((math.sqrt(50*50 + 40*exp_run) - 50) / 40)
    atk = (21 + 3*L)*GEAR(ch)          # [假设A] 初始武器21 + 升级 + 装备成长
    hp = 360 + 25*L
    df = 10.5 + L
    cs = CHAPTER_SCALE[ch]; ramp4 = STAGE_RAMP[4]
    bAtkMul = ({1:0.35,2:0.35,3:0.6}.get(ch,0.8))
    nHp = avgHp*cs*STAGE_RAMP[0]*HP_GLOBAL*1.1
    nDef = DEF_N*cs
    eHp = avgHp*ELITE_HP_AVG*ELITE_TANK[0]*cs*1.11*1.15*HP_GLOBAL
    eDef = DEF_E*cs
    ba, _ = boss_pair(ch)
    bHp = ba["hp"]*cs*ramp4*BOSS_TTK_HP*MINI_BOSS*HP_GLOBAL*1.2
    bDef = DEF_B*cs
    bAtk = ba["atk"]*cs*ramp4*DMG_MUL*BOSS_TTK_ATK*bAtkMul
    nAtk = avgAtk*cs*STAGE_RAMP[0]*DMG_MUL*bAtkMul
    hN, hE, hB = hits_player(atk,nHp,nDef), hits_player(atk,eHp,eDef), hits_player(atk,bHp,bDef)
    print(f"{ch:<4}{L:>5}{atk:>8.1f}{hp:>8.0f}{df:>7.1f} | {hN:>8.1f}{hE:>9.1f}{hB:>9.1f} | {surv_time(nAtk,hp,df):>10.1f}{surv_time(bAtk,hp,df):>11.1f}")
print("  注：未计入佣兵（传说佣兵攻≈玩家2.6×）、技能、天赋、词条；玩家攻速1.0刀/s，怪0.585次/s（表1.5×0.39）。")

print()
print("="*100)
print("三、经济：局外金币（StageGoldDefs：普通80/精英100/Boss200 ×1.1^章 + 通关金）")
print("="*100)
cum = 0
print(f"{'章':<4}{'大关数':>5}{'每大关金':>9}{'整章(全通)':>11}{'累计':>9} | {'尝试模型收入':>12}（EconomyDefs Gates，failStages 已过期→钳5=全额）")
income_ch = {}
for ch in range(1, 9):
    mul = (1+GOLD_CH_GROW)**(ch-1)
    per_grand = (3*GOLD_N + GOLD_E + GOLD_B)*mul + 3*CLEAR_N(ch) + CLEAR_B[ch]
    grand_n = GRANDS_PER_ACT[ch]
    full = per_grand*grand_n
    tries = {1:10,2:20,3:22,4:25,5:25,6:28,7:30,8:35}[ch]
    # 失败局按清到第 3 小关计 [假设B]（原 failStages 4.5~8.5 已被钳成 5=全额，见 F4）
    fail_pay = (3*GOLD_N*mul + 3*CLEAR_N(ch))*0.85
    model = (tries-1)*fail_pay + per_grand
    income_ch[ch] = model
    cum += full
    print(f"{ch:<4}{grand_n:>5}{per_grand:>9.0f}{full:>11.0f}{cum:>9.0f} | {model:>12.0f}")
print(f"  全游戏全通总收入 ≈ {cum:.0f} 金；含失败重试 ≈ {sum(income_ch.values()):.0f} 金")
print(f"  全游戏小关总数 = {sum(GRANDS_PER_ACT.values())*SUBS_PER_GRAND}（大关 {sum(GRANDS_PER_ACT.values())} 座）")

print()
print("水槽（主人已定值的口径）：")
print("  天赋左列（金币）       ≈ 13,700            （EconomyDefs 注释）")
free_stones = 440
need_stones_max = 1915 - free_stones
print(f"  天赋右列（天赋石）     全满 1915 石，一周目免费 440 → 金币换 {need_stones_max} 石 = {need_stones_max*GOLD_PER_STONE:,} 金（100金/石）")
merc_costs = [1500,1800,5000,500,2000,600,1600,1900,6000,500]  # 花名册已见 10 行
print(f"  佣兵解锁（已见10人）   {sum(merc_costs):,}（全 22 人估 4~5 万 [假设C]）")
print(f"  商店/铁匠/杂项         估 2~3 万 [假设C]")
sinks_lo = 13700 + need_stones_max*GOLD_PER_STONE + sum(merc_costs)
print(f"  → 核心水槽合计（不含商店）≈ {sinks_lo:,} 金；含商店上限 ≈ {sinks_lo+30000:,} 金")
print(f"  → 全通收入（含重试）≈ {sum(income_ch.values()):.0f} 金：{'够花，有富余' if sum(income_ch.values())>sinks_lo else '不够'}")
print()
print("每日节奏（体力 100 上限 / 10每次 / 自然回复 24每天 [EconomyDefs]）：")
for ch in (1, 8):
    mul = (1+GOLD_CH_GROW)**(ch-1)
    per_grand = (3*GOLD_N + GOLD_E + GOLD_B)*mul + 3*CLEAR_N(ch) + CLEAR_B[ch]
    print(f"  ch{ch}：满体力日 10 次 ≈ {10*per_grand:,.0f} 金；纯自然回复日 2.4 次 ≈ {2.4*per_grand:,.0f} 金")
print()
print("广告位价值核查（现值：体力 +20/次×5，金币 +60/次×10）：")
for ch in (1, 8):
    mul = (1+GOLD_CH_GROW)**(ch-1)
    per_grand = (3*GOLD_N + GOLD_E + GOLD_B)*mul + 3*CLEAR_N(ch) + CLEAR_B[ch]
    print(f"  ch{ch}：金币广告 10 次/天 = 600 金 = {600/per_grand:.2f} 次大关收入（占比 {600/(10*per_grand)*100:.1f}% 日收入）")
print("  体力广告恒 worth：20 体力 = 2 次冒险 = 稳定；金币广告后期贬值严重（F6）。")
