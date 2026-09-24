import subprocess, sys
sys.stdout.reconfigure(encoding='utf-8')

ROOT = 'Y:/PixelAdventureTown'


def git(args, allow_fail=False):
    p = subprocess.run(['git'] + args, cwd=ROOT, capture_output=True,
                       text=True, encoding='utf-8', errors='replace')
    print('$ git', ' '.join(args))
    if p.stdout:
        print(p.stdout[-3000:])
    if p.stderr:
        print('STDERR:', p.stderr[-1500:])
    if p.returncode != 0 and not allow_fail:
        print('!!! 失败，退出码', p.returncode)
        sys.exit(p.returncode)
    return p


git(['add', '-A'])
git(['status', '--porcelain'])

msg = """feat(battle): 攻击目标指示器 + BOSS 血条入场；修复摇杆/章节卡/宝箱与 GUID 换发

战斗系统
- 新增 TargetIndicator：头顶箭头 + 脚下光圈指示当前攻击目标
  普通怪用 PT、精英怪用 JY、BOSS 不显示（已有屏幕血条）
- BattleBossHpBar：BOSS 出场时血条 1.2s 从 0 涨满，与走进场同步
  期间隐藏顶部 ProgressBar/QuestMap；播放中不被真实血量覆盖
- 摇杆常显：Singleton 新增 InstanceQuiet（战斗外取单例不打 LogError）
  BattleJoystick 每帧自愈可见性，修掉 BattleUI.Awake 早于装配导致的摇杆消失
- 技能冷却改为黑色 Radial360 遮罩转一圈（SkillAvatarUI.SetCooldownRatio）
- 章节开场卡移到进战斗之前（GameSceneManager.LoadBattleAsync）
- 教程：怪四面围出（flank→around），台词末句改「糟了，是陷阱！」

宝箱
- 重建 box.prefab（close 下落 / open1 开箱过程 / open2 常开 / effect 1-2-3 按稀有度）
  换发合法 hex32 guid，修复 Battle.unity 的 Missing Prefab
- 运行时兜底 EnsureBoxVisualFallback；位置锁死不再被贴地逻辑强挪

资源
- PT/JY/箭头01 复制到 Resources 供运行时加载（原图 guid 为 56 字符 base64，未处理）
- 教程 tutorial_battle.csv 与 .bytes 同步"""

git(['commit', '-m', msg])
git(['log', '--oneline', '-1'])
print('\n===== 开始推送 =====')
git(['push'])
git(['status', '-sb'])
