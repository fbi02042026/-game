# -*- coding: utf-8 -*-
"""记录每波用到的精灵到 _lastWaveSprites，并在关卡开始时清空（文件是 CRLF，按字节替换）。"""
import io

p = r"Assets/Scripts/Combat/WavePlanner.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 一波刷完后记账
anchor1 = (
    '        GamePerf.Log($"[BattleManager] wave {waveIndex + 1} spawned {wave.monsterCount} @x={engageBaseX:F1}");\r\n'
    "        _spawnWaveCo = null;\r\n"
)
if anchor1 not in s:
    raise SystemExit("锚点1未找到")
patch1 = (
    "        // 记账：这一波用到过哪些精灵，下一波优先换一批（只影响外观）\r\n"
    "        _lastWaveSprites.Clear();\r\n"
    "        _lastWaveSprites.UnionWith(usedSpritesThisWave);\r\n"
    "\r\n"
) + anchor1
s = s.replace(anchor1, patch1, 1)

# 2) 每关开波前清账
anchor2 = (
    "        _waves.Clear();\r\n"
    "        _totalWaves = 0;\r\n"
    "        _allWavesSpawned = false;\r\n"
    "        _activeWaveIndex = -1;\r\n"
    "        _firstWaveSpawned = true; // 关掉 Update/过场后的硬刷；真正刷怪只走 TutorialDirector\r\n"
)
if anchor2 in s:
    s = s.replace(anchor2, anchor2 + "        _lastWaveSprites.Clear();\r\n", 1)
    print("教程波：已清账")
else:
    print("教程波锚点未找到（跳过，不影响编译）")

# 3) BuildCombatWaves 开头清账
anchor3 = (
    "    void BuildCombatWaves(int stageIdx, bool elite)\r\n"
    "    {\r\n"
    "        float startX = GetStageStartX();\r\n"
)
if anchor3 in s:
    s = s.replace(anchor3,
                  "    void BuildCombatWaves(int stageIdx, bool elite)\r\n"
                  "    {\r\n"
                  "        _lastWaveSprites.Clear();\r\n"
                  "        float startX = GetStageStartX();\r\n", 1)
    print("普通/精英波：已清账")
else:
    print("BuildCombatWaves 锚点未找到（跳过）")

io.open(p, "w", encoding="utf-8", newline="").write(s)
print("ok")
