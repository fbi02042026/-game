# -*- coding: utf-8 -*-
"""
2026-09-19：替换主人自制本命碎片预制体的两个图片引用。

结构（主人做的）：
  yongbingsuipian (root 100x100)
  |-- suipianicon (70x70)      Image.sprite = 碎片稀有度底图   [原 guid 04f63b2c...]
  `-- mask (scale 0.79)        Image.sprite = 碎片图，用作 Mask 遮罩形状 [原 guid 04f63b2c...]
      `-- HeadIcon (200x200)   Image.sprite = 佣兵头像          [原 guid d6d7cc06...]

原两个 guid 在整个工程（含 Library）都搜不到 = 外部资源，Unity 里显示为 Missing。

替换目标：
  碎片 -> Assets/Resources/Icons/MercFragmentBase/普通碎片.png   (hex guid，安全)
  头像 -> Assets/Resources/Icons/MercHead/H001.png               (团结 base64 guid)

⚠️ 只替换 guid 字符串，绝不改动 m_SizeDelta / m_AnchoredPosition / m_LocalScale
   （主人明确要求"大小不要做改动"）。
"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PREFAB = os.path.join(
    ROOT, "Assets", "Resources", "Prefabs", "other", "yongbingsuipian.prefab"
)

# 旧 guid -> 新 guid
REPL = {
    # suipianicon 与 mask 的 Image 都用它（碎片图）
    "04f63b2c54a59184f98b353ae0e3215c": "99bf3a42d2f04b15ae9a1b70d4b92ba0",
    # HeadIcon（佣兵头像 H001）
    "d6d7cc069da110f498da190e6005213f": "XHgX5y78W3u+N/fGMkly5kD7CEJa36InxdAHyonSWr2hCXTskYYzOuE=",
}


def main():
    with io.open(PREFAB, "r", encoding="utf-8", newline="") as f:
        text = f.read()

    crlf = "\r\n" in text
    total = 0
    for old, new in REPL.items():
        n = text.count(old)
        total += n
        print("%s -> %s  (替换 %d 处)" % (old[:12] + "...", new[:12] + "...", n))
        text = text.replace(old, new)

    with io.open(PREFAB, "w", encoding="utf-8", newline="") as f:
        f.write(text)

    print("\n共替换 %d 处，行尾 CRLF=%s" % (total, crlf))

    # 尺寸自检：确认没动到几何
    for key in ("m_SizeDelta", "m_AnchoredPosition", "m_LocalScale"):
        vals = [
            ln.strip()
            for ln in text.replace("\r\n", "\n").split("\n")
            if ln.strip().startswith(key)
        ]
        print("%s: %s" % (key, " / ".join(v.split(": ", 1)[1] for v in vals)))


if __name__ == "__main__":
    main()
