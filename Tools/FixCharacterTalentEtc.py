# -*- coding: utf-8 -*-
"""Fix CharacterUI / TalentUI / Tavern / AdventureLog / BattleStageMap orphan sprites."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(r"Y:\PixelAdventureTown\Assets")


def guid_of(p: Path) -> str:
    m = re.search(
        r"^guid:\s*([0-9a-fA-F]{32})",
        Path(str(p) + ".meta").read_text(encoding="utf-8-sig"),
        re.M,
    )
    return m.group(1)


def find(folder: Path, suf: str) -> Path | None:
    if not folder.is_dir():
        return None
    exact = None
    ends = None
    for p in folder.rglob("*.png"):
        n = p.name.lstrip("\x7f")
        if n == suf:
            exact = p
            break
        if n.endswith(suf) and ends is None:
            ends = p
    return exact or ends


def main():
    attr = ROOT / "Art/UI/Icons/属性图标"
    talent = ROOT / "Art/UI/Talent"
    nav = ROOT / "Art/UI/NavCharacter"
    tavern = ROOT / "Art/UI/Tavern"
    stage = ROOT / "Art/UI/关卡"
    skill_icon = ROOT / "Art/UI/Icons/SkillIcon"
    log = ROOT / "Art/UI/冒险日志"
    common = ROOT / "Art/UI/Common"

    orphan: dict[str, str] = {}

    def add(guid: str, p: Path | None, label: str):
        if p is None or not p.is_file():
            print("WARN", label)
            return
        orphan[guid] = guid_of(p)
        print("MAP", label, p.name)

    # Shared attr icons
    add("25b966b591ba985418d6cfcb5f7f805a", find(attr, "_生命.png"), "AttrHp")
    add("cd276c565df36604b925ea20a7c96574", find(attr, "_攻击.png"), "AttrAtk")
    add("929b57a48948c254a96bc4a48a670bce", find(attr, "_防御.png"), "AttrDef")
    add("520da3208862e5542922532a84f337e3", find(attr, "_攻速.png"), "AttrSpd")
    add("5dc54be54206c124d99b8b725365d8b7", find(attr, "_暴击.png"), "AttrCrit")

    # CharacterUI
    add("351abac89f0467048bbdd2d4c63baa07", find(nav, "_标头.png"), "Header")
    add("6a33faf6ec9c5f04a80ca3f12bf6e78f", find(nav, "_玩家头像.png"), "Portrait")
    add("cd5541a74aa4aa549bedaed6abe7d701", find(nav, "_技能底框.png") or find(nav, "0002s_0006_技能底框.png"), "SkillSlot")
    add("f572543dae6f7b34fa25fd5ddca3019d", find(nav, "_天赋.png"), "TalentBtn")
    add("306c310d7ae68fb428e813fdbb4317d8", find(nav, "_技能.png"), "SkillBtn")
    add("6c52d1671113788439bf880fa9414e7c", find(nav, "_图层-6.png") or find(nav, "0001s_0009_图层-6.png"), "AttrPanel")
    add("a7a4ee3b4ca4070468e7aaa50fc486fa", find(nav, "_图层-9.png") or find(nav, "技能底框.png"), "SkillSelectPanel")
    add("1afc3458dd63f6d458beb0a29e15e680", find(talent, "_关闭.png") or find(common, "取消按钮.png"), "Close")
    add("3ad0fcad66f16124899daa77f7eff865", find(nav, "_切换.png"), "HeaderIcon")
    add("595f0b373f5ac6141aedc8f8212f6fb8", find(nav, "_图层-7.png"), "RightBg")
    add("261cddcc689c787459c14c8902b30591", find(nav, "_背景.png"), "CharBg")
    add("869790aacf5f15346ac728bf8f8b3e68", find(nav, "_头像框.png"), "LeftSkillImg")
    add("d75e33802677b63408758a207f4ae699", find(nav, "_标头.png"), "biaotou")

    # Skill slots 1-5 — use skill icons if present
    skills = sorted(skill_icon.glob("*.png")) if skill_icon.is_dir() else []
    skill_guids = [
        "570b0f79b911e274e88cf7969a2fa391",
        "65c02fdfb0715d745acd8af5c7b71743",
        "aeb680522a9188d4baf12b4f6ad38b32",
        "c7d942418041ca84dbd054c47e16e105",
        "204effacad8253f4489cb62da915d0e3",
    ]
    for i, g in enumerate(skill_guids):
        art = skills[i] if i < len(skills) else find(nav, "_技能底框.png")
        add(g, art, f"Skill_{i+1}")

    # TalentUI
    add("20a25a064f116284282855d1f22a59e2", find(talent, "_bg.png") or find(talent, "0020_bg.png"), "TalentPanel")
    add("7612939304a27ec41ae90b7d634d5a72", find(talent, "_关闭.png"), "TalentClose")
    add("090e1c7b819f3324893f067082340234", find(talent, "天赋石购买.png") or find(common, "星.png"), "StonePlus")
    add("4e8455fd1cf79b8438141e4719f1919f", find(talent, "_属性底.png") or find(talent, "0006_属性底.png"), "LeftColumn")
    add("1865efc931bf8c44587ac32b4084f23a", find(talent, "_底条.png"), "LeftHeader")
    add("2359ea129c2d3fe4caa0477eaa7cf5e8", find(talent, "_右侧天赋底.png"), "RightColumn")
    add("a2a65cb76551d0246b8cc1b667adc3af", find(talent, "_重置天赋亮.png") or find(talent, "重置天赋灰.png"), "Reset")
    add("f6a721e7ad54ab94288949f8607e3e29", find(talent, "_链接1.png") or find(talent, "0010_链接1.png"), "LeftLine")
    add("3c4439ab2be97124cb9619b0ec96d352", find(talent, "_技能底.png") or find(talent, "0000_技能底.png"), "Iconbg")
    add("498bbd477516f2e4691e5e73076ff462", find(talent, "_右侧天赋底.png"), "RightRow")
    add("5257f5425e676e9429d022e09ba65931", find(talent, "_可升级.png") or find(common, "星.png"), "Diamond")
    add("690ec33641d1d0846abb4e706b027ec5", find(common, "锁.png") or find(talent, "_不可升级.png"), "Lock")
    add("649557b0503715149960e23c83c817bd", find(talent, "_基础属性解锁.png"), "Opt0")
    add("eebf1e3e5ce7d8f4497dba3b97ef5186", find(talent, "_基础属性未解锁.png"), "Opt1")
    add("9446cfd794c56124399531ec968d679c", find(talent, "_技能可用.png"), "Opt2")
    add("f5184f7a1ba4dd74fa54e5e6194c826d", find(talent, "图层 1.png") or find(talent, "_技能可用.png"), "OptIcon")
    add("54b8c725c3884f24591ea9cdf3cd0549", find(talent, "_技能可用.png"), "可用标")
    add("9e13be5c03fb3814eae1db543c1c6b6a", find(talent, "_箭头.png"), "箭头")
    add("8693254c32b337a4e829740deeb4ca33", find(talent, "_链接2.png"), "RightLine")
    add("a16588c1964bcf549a707e9b5ca26570", find(talent, "天赋石.png"), "StoneIcon")
    add("283b8c4e7b0f27d47a34306754d76e3c", find(talent, "_金币升级.png"), "Choice0")
    add("9f0bec8bfa996f6428fb199788a37313", find(talent, "重置天赋灰.png"), "ResetGray")

    # Tavern — map by unique guids from audit (dump more if needed)
    # BattleStageMap
    add("441a8a5adbb337e41a9756eddf944196", find(stage, "底座.png"), "StageBase?")
    add("0f6bb114a9cd7514aab0ebc14a3ad7ba", find(stage, "背景.png"), "StageBg?")
    add("da09a09e1c3066941a22b322d8bb7b8e", find(stage, "层级调整组件_0007_背景.png") or find(stage, "背景.png"), "StageTitle?")

    # AdventureLog common
    add("c622cce519744844d95d2c3411013c02", find(log, "内容底.png") or find(log, "标签底.png"), "LogBg?")
    add("97e57638c9e9b344aa2310ed73738f18", find(log, "标头.png"), "LogHeader?")
    add("5ecfe44eb1725a247b0f0d1824c4e5ac", find(log, "选中.png"), "LogSel?")

    # Apply to all prefabs (global orphan replace)
    targets = list((ROOT / "Resources/Prefabs").rglob("*.prefab"))
    touched = reps = 0
    for pref in targets:
        text = pref.read_text(encoding="utf-8")
        orig = text
        for o, n in orphan.items():
            if o in text:
                reps += text.count(o)
                text = text.replace(o, n)
        if text != orig:
            pref.write_text(text, encoding="utf-8", newline="\n")
            touched += 1
            print("wrote", pref.name)
    print(f"DONE files={touched} reps={reps} map={len(orphan)}")


if __name__ == "__main__":
    main()
