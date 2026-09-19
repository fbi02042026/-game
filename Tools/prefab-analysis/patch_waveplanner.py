# -*- coding: utf-8 -*-
"""在 WavePlanner.PickSpriteByStyle 里插入「邻波换脸」逻辑（文件是 CRLF，必须按字节替换）。"""
import io

p = r"Assets/Scripts/Combat/WavePlanner.cs"
s = io.open(p, encoding="utf-8", newline="").read()

anchor = (
    "            if (unused.Count > 0)\r\n"
    "                filtered = unused;\r\n"
    "        }\r\n"
    "\r\n"
    "        if (ConfigManager.Instance == null)\r\n"
    "            return filtered[slotIndex % filtered.Count];\r\n"
)
if anchor not in s:
    raise SystemExit("锚点未找到（可能已改过）")

patch = (
    "            if (unused.Count > 0)\r\n"
    "                filtered = unused;\r\n"
    "        }\r\n"
    "\r\n"
    "        // 邻波换脸：挑得到「上一波没用过」的就优先挑它，挑不到保持原样（只影响外观种类）\r\n"
    "        if (_lastWaveSprites.Count > 0 && filtered.Count > 1)\r\n"
    "        {\r\n"
    "            var fresh = filtered.Where(idx => !_lastWaveSprites.Contains(idx)).ToList();\r\n"
    "            if (fresh.Count > 0) filtered = fresh;\r\n"
    "        }\r\n"
    "\r\n"
    "        if (ConfigManager.Instance == null)\r\n"
    "            return filtered[slotIndex % filtered.Count];\r\n"
)
s = s.replace(anchor, patch, 1)

io.open(p, "w", encoding="utf-8", newline="").write(s)
print("ok")
