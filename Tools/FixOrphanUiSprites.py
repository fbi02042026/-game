# -*- coding: utf-8 -*-
"""
把预制体里「幽灵 Sprite GUID」重绑到当前 Art 资源（按节点路径 / 孤儿 GUID 用途推断）。
根因：GUID 加密修复只改了 .meta，预制体仍指向加密前本地 hex，且不少从未入库。
"""
from __future__ import annotations

import json
import os
import re
from pathlib import Path

ROOT = Path(r"Y:\PixelAdventureTown\Assets")
CACHE = Path(r"Y:\PixelAdventureTown\Tools\_guid_cache.json")


def find_by_suffix(folder: Path, suffix: str) -> str | None:
    """Match file whose name ends with suffix (e.g. '_冒险.png' or '锁.png')."""
    if not folder.is_dir():
        return None
    for p in folder.rglob("*.png"):
        name = p.name.lstrip("\x7f")
        if name == suffix or name.endswith(suffix):
            rel = p.relative_to(ROOT).as_posix()
            return rel
    return None


def build_path_to_guid() -> dict[str, str]:
    if CACHE.is_file():
        data = json.loads(CACHE.read_text(encoding="utf-8"))
        return data["path_to_guid"]
    raise SystemExit("run RebindOrphanSprites once to build cache, or rebuild here")


def resolve(path_to_guid: dict[str, str], rel: str | None) -> str | None:
    if not rel:
        return None
    rel = rel.replace("\\", "/")
    if rel in path_to_guid:
        return path_to_guid[rel]
    # try basename
    base = Path(rel).name.lower()
    return path_to_guid.get(base)


def main():
    path_to_guid = build_path_to_guid()
    # refresh path_to_guid for battle folder with weird \x7f names
    for p in (ROOT / "Art" / "UI").rglob("*.png"):
        meta = Path(str(p) + ".meta")
        if not meta.is_file():
            continue
        m = re.search(r"^guid:\s*([0-9a-fA-F]{32})", meta.read_text(encoding="utf-8-sig"), re.M)
        if not m:
            continue
        rel = p.relative_to(ROOT).as_posix()
        path_to_guid[rel] = m.group(1)
        # also register cleaned name key
        clean = p.name.lstrip("\x7f")
        path_to_guid[clean.lower()] = m.group(1)

    main_dir = ROOT / "Art" / "UI" / "Main"
    common = ROOT / "Art" / "UI" / "Common"
    story = ROOT / "Art" / "UI" / "Story"
    battle = ROOT / "Art" / "UI" / "battle"
    log_dir = ROOT / "Art" / "UI" / "冒险日志"
    frames = ROOT / "Art" / "UI" / "装备格子"

    def M(suf: str) -> str | None:
        return find_by_suffix(main_dir, suf)

    def C(suf: str) -> str | None:
        return find_by_suffix(common, suf)

    def S(name: str) -> str | None:
        p = story / name
        return p.relative_to(ROOT).as_posix() if p.is_file() else None

    def B(suf: str) -> str | None:
        return find_by_suffix(battle, suf)

    def L(suf: str) -> str | None:
        return find_by_suffix(log_dir, suf)

    def F(name: str) -> str | None:
        p = frames / name
        return p.relative_to(ROOT).as_posix() if p.is_file() else None

    # orphan guid -> art rel path
    orphan_map: dict[str, str] = {}

    def add(guid: str, art: str | None, label: str):
        if not art:
            print(f"  WARN no art for {label} ({guid[:8]})")
            return
        g = resolve(path_to_guid, art)
        if not g:
            # art may be full path already as key
            print(f"  WARN no guid for art {art} ({label})")
            return
        orphan_map[guid] = g
        print(f"  MAP {guid[:8]}… -> {art}")

    print("Building orphan GUID map…")
    # Dialogue
    add("c729c512ad2ae7f4596abe392c37801a", S("ui_dialogue_panel.png"), "DialogueBox")
    add("88aaf6c808b271a4baf71f4b829668a8", S("ui_choice.png"), "Choice")
    add("2dae1301b84ae8847b71a9197eca4698", S("ui_nameplate_npc.png"), "LeftNamePlate")
    add("44eacb98a19870645a6b1281661441f1", S("ui_nameplate_player.png"), "RightNamePlate")
    add("7feed38751f0da44798055c2d696935b", S("ui_tap.png"), "NextArrow")
    add("82fe7ad93bd69cb4eb6409ac104c9841", S("portrait_player.png"), "LeftPortrait")
    add("513cb066a1c423c4d8db30080c6bd67e", S("portrait_guildmaster.png"), "RightPortrait")

    # GuildHall chrome
    add("ced71667441293741ba077b5b0151a59", M("_图标底.png"), "NavBg")
    # prefer first 图标底 if two
    if "ced71667441293741ba077b5b0151a59" not in orphan_map:
        add("ced71667441293741ba077b5b0151a59", M("0002s_0001_图标底.png"), "NavBg2")
    add("53f6bf8dedab59d46811137e62280e57", L("选中.png") or C("选择.png"), "选中")
    add("a85b02f5eacd03c42972f92422ccb551", M("_图标底.png"), "HotspotBg")
    add("8e18353cfa356184bbed1fca0e5c77b3", M("_眼睛.png"), "eyes")
    add("925660d292a51fa4cb596f930802312d", M("_资源条-拷贝.png"), "GoldBg")
    add("519027b59930dad45a8ebdf779e7f2eb", M("_金币.png"), "CoinIcon")
    add("e687850dfa4bc7e40a6c02970d4fd442", M("_金币.png"), "StaminaCoinIcon")
    add("badc326c890a9124fa733023845385e5", M("_对话框.png"), "BubbleBg")
    add("ebc8fcf6cfe51b44eb1529043edf7baf", M("_长条.png"), "BottomNavBG")
    add("03dd6543535b4554aa87f999f9e3fc70", M("_角色.png"), "NavCharacter")
    add("b96c6ad947f27b2439e6dd620da5fca3", M("_公会.png"), "NavGuild")
    add("7a5d4735cc22d8745b6292dec85a7e3d", M("_冒险.png"), "NavAdventure")
    add("a64f795571204e444b2f271b264e3ee1", M("_酒馆.png"), "NavTavern")
    add("b39a737bafddb904ebecf653c24210d3", M("_冒险日志.png"), "NavLog")
    add("ba5ea2eec1c081b439d114bc22421515", M("_商城.png"), "Shop")
    add("2d904471893fb694e93e439c28ac2817", M("_排行.png"), "Rank")
    add("d6f4cf63af4ceca48857c7d5c8e326b7", M("_活动.png"), "Activity")
    add("37516588d163ec04990a9486d4a2a6ea", M("_公告.png"), "Notice")
    add("fe9da4533ff41694293cfe7641f28365", M("_邮件.png"), "Mail")
    add("ed375a437d882b04b89088b5b6c62da7", M("_设置.png"), "Settings")
    add("1a7ab9c4de6dd884ea017da1ba0b1f1f", M("_冒险者旗帜.png"), "BadgeBg")
    add("52cff23a41e8d26489652584771d269d", M("_冒险者名称.png"), "BadgeBg1")
    add("7233c6cdbe2ef4942aed880c22b8a0b5", M("_图层-8.png") or M("图层-9-拷贝-3.png"), "Background")

    # EquipDropPopup
    add("0cd0131d09632c44897139a40aef2ee2", B("大边框.png") or B("边框.png"), "EquipPanel")
    add("f0254556a12917a49b8a8a4c8ac00cf4", F("frame_common.png") or B("装备格.png"), "EquipCard")
    add("8a62894c776594c4a8a6123319cf2538", F("frame_white.png") or B("装备格.png"), "Iconbg")
    add("c333d293d7b95104f82ce8c7440f529c", C("确定按钮.png"), "PrimaryButton")
    add("5ee97af5309766848ac9499681e466bc", C("取消按钮.png"), "SecondaryButton")

    # BattleUI core repeats
    add("f625d1754aae7d147a2c3fb9a1a77ae5", B("格子锁.png") or C("锁.png"), "suo")
    add("6f017ecd729332d45b4ee2c274cb6bcc", B("装备格.png"), "Cell")
    add("d7f7c3f947eb46644b6aa8afcfa1a95f", B("装备格.png"), "CellBg")
    add("29277b8cd2bac3c4f90c044dd84935fb", B("蓝条.png"), "lanBarFill")
    add("bb6b98a0bdc08b24d95ab1bf089714a3", B("血条底.png") or B("资源底框.png"), "lanBarBg")
    add("0366cc4d703d53442ae5783bd3615d03", B("底图.png"), "BattleBackground")
    add("a1dc8c683aaa7b9418759dbd0ec2d62f", B("进度条.png"), "ProgressLine")
    add("7d8f24e838d890846b2064ac09b2ed39", B("进度.png"), "PlayerMarker")
    add("14f0808845b243c42bd68c264865dc60", B("头像框.png"), "MercSlot")
    add("fa65fd09d7de81d4a9b7c42dab07188b", B("任务底框.png"), "jianglitubiao")
    add("628d8a7dc1c877a45ad4407f2662b8ef", B("设置.png") or C("设置.png"), "BackpackBtn")

    print(f"orphan map size={len(orphan_map)}")

    # Apply to all prefabs + scenes under Assets
    targets = []
    for folder in [ROOT / "Resources" / "Prefabs", ROOT / "Scenes", ROOT / "Resources" / "Config"]:
        if not folder.is_dir():
            continue
        for p in folder.rglob("*"):
            if p.suffix.lower() in (".prefab", ".unity", ".asset"):
                targets.append(p)

    files_touched = 0
    replacements = 0
    for p in targets:
        text = p.read_text(encoding="utf-8")
        orig = text
        for old, new in orphan_map.items():
            if old in text:
                cnt = text.count(old)
                text = text.replace(old, new)
                replacements += cnt
        if text != orig:
            p.write_text(text, encoding="utf-8", newline="\n")
            files_touched += 1
            print(f"  wrote {p.relative_to(ROOT)}")

    # save map
    out_map = Path(r"Y:\PixelAdventureTown\Tools\orphan-sprite-rebind.json")
    # reverse for readability: old -> path
    readable = {}
    guid_to_path = {v: k for k, v in path_to_guid.items() if "/" in k}
    for old, new in orphan_map.items():
        readable[old] = {"newGuid": new, "path": guid_to_path.get(new, "?")}
    out_map.write_text(json.dumps(readable, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"DONE files={files_touched} replacements={replacements} map={out_map}")


if __name__ == "__main__":
    main()
