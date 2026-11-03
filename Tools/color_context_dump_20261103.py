# -*- coding: utf-8 -*-
"""dump 疑似稀有度语义的散色点上下文（只读）"""
import io, os

OUT = r"Y:\PixelAdventureTown\.workbuddy\backup\color_audit_20261103\context.txt"
ROOT = r"Y:\PixelAdventureTown\Assets\Scripts"

# (相对路径, [行号列表])
TARGETS = [
    ("UI/CharacterSlotUI.cs", [70, 85, 774]),
    ("Story/FirstRunGuide.cs", [87]),
    ("UI/DailyLoginUI.cs", [30, 55]),
    ("UI/DailyLoginUI.Show.cs", [253]),
    ("UI/DailyLoginUI.Refresh.cs", [59]),
    ("UI/BattleUI.WidgetFactory.cs", [182, 214]),
    ("UI/RunSkillBarUI.cs", [105, 110, 117, 123, 206]),
    ("UI/SlotMachineUI.cs", [25]),
    ("Systems/StageRoller.cs", [246]),
    ("UI/ArenaPopupUI.cs", [114, 115]),
    ("UI/BackpackItemActionUI.cs", [71]),
    ("UI/LevelUpDraftUI.cs", [287, 293, 299, 552, 608, 614, 624]),
    ("UI/MercDrawPoolUI.Cards.cs", [173, 184, 219, 277]),
    ("UI/GuildHallUI.cs", [309, 567]),
    ("UI/PlayerPowerHud.cs", [159]),
    ("UI/BattleSettlementUI.cs", [486, 529, 531, 533, 937, 1009]),
    ("UI/TavernUI.cs", [766, 774, 780, 807, 817]),
    ("UI/SkillSelectUI.cs", [146, 148, 519, 538, 548]),
    ("UI/EquipDropPopupUI.cs", [1073]),
    ("UI/MercFavorUI.cs", [785, 905]),
    ("Systems/StageClearRewardDirector.cs", [1223, 1288]),
    ("UI/BambooBasketGame.cs", [329, 402]),
    ("UI/BattleEntryDraftPanel.Build.cs", [277, 305, 306]),
    ("UI/BattleUI.Hud.cs", [214]),
    ("Story/AdventureLogMileage.cs", [168]),
    ("UI/NextStageRouletteUI.cs", [218, 619, 642, 657, 676]),
    ("UI/TalentUI.Bind.cs", [74]),
    ("UI/DraftOddsPopupUI.cs", [252]),
    ("UI/MercenaryRecruitPopupUI.Cards.cs", [271, 320]),
    ("Config/DraftPool.cs", [296, 331, 359, 365]),
    ("UI/AdventureLogPhase3Panel.cs", [71, 72]),
    ("Config/MercFavorDefs.cs", [202]),
    ("UI/BattleBossHpBar.cs", [354]),
    ("UI/QuestHudBar.cs", [71]),
    ("UI/QuestPanelUI.cs", [44]),
    ("Config/PlayerSkillDefs.cs", [159, 211]),
    ("Config/MonsterAffixDefs.cs", [51]),
    ("Unit/Monster.StackLabel.cs", [161]),
    ("UI/HealthNoticeUI.cs", [82]),
    ("UI/BackpackGridVisual.cs", [556, 480]),
    ("UI/CharacterUI.Bind.cs", [144, 178, 194]),
    ("UI/TalentUI.Build.cs", [211]),
    ("UI/AdventureUI.Build.cs", [260, 372, 395, 462]),
    ("UI/AdventureUI.Detail.cs", [390]),
    ("UI/AdventureUI.Refresh.cs", [469, 493, 540]),
    ("UI/SettingsPopupUI.cs", [758, 800, 852]),
    ("UI/WorldMapPopup.cs", [969, 584, 1097]),
    ("UI/RestStagePopupUI.cs", [451, 458, 465, 480, 496, 509, 525, 533, 543]),
    ("UI/GlobalToastUI.cs", [226, 314, 318]),
    ("UI/StageShopPopupUI.cs", [123, 195, 311]),
    ("UI/DailyLoginUI.Build.cs", [364]),
    ("UI/LoginLogoMotion.cs", [190, 238]),
    ("UI/BuffHudUI.cs", [94, 95, 96]),
    ("UI/PlayerJobSelectUI.cs", [449, 535, 727, 742, 964]),
    ("UI/DamageTextSystem.cs", [35, 37, 47, 52, 57, 69, 72, 75]),
    ("UI/RuntimeLockSprite.cs", [56]),
    ("Story/PlaceholderArt.cs", [27]),
    ("UI/BattleResultScreen.cs", [535]),
    ("UI/AdventureLogCodexPanel.cs", [689, 698]),
    ("UI/ItemNoticeText.cs", [26]),
]

lines_by_file = {}
for rel, lns in TARGETS:
    p = os.path.join(ROOT, rel.replace("/", os.sep))
    if not os.path.exists(p):
        continue
    with open(p, "rb") as f:
        data = f.read().decode("utf-8", errors="replace")
    lines_by_file[rel] = data.splitlines()

with io.open(OUT, "w", encoding="utf-8") as f:
    for rel, lns in TARGETS:
        if rel not in lines_by_file: continue
        arr = lines_by_file[rel]
        f.write("="*100 + "\n### %s\n" % rel)
        for ln in lns:
            lo, hi = max(1, ln-4), min(len(arr), ln+3)
            f.write("--- 行 %d 附近 ---\n" % ln)
            for i in range(lo, hi+1):
                f.write("%5d| %s\n" % (i, arr[i-1].rstrip()))
        f.write("\n")
print("done ->", OUT)
