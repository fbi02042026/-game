#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate CharacterUI.prefab shell. Grid cells filled at runtime by TownBackpackGrid.BuildGrid."""
from __future__ import annotations
import os

OUT = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Town\CharacterUI.prefab"
GUID_BTN = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_IMG = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_UI = "a7c1d9e8b6f04a2e91d3c5b7e8f90123"
GUID_BAG = "b8d2e0f9c7a15b3f02e4d6c8f9a01234"
GUID_SKILL = "c9e3f1a0d8b26c4a13f5e7d9a0b12345"

_nid = 6000


def nid():
    global _nid
    _nid += 1
    return _nid


class Node:
    def __init__(self, name, **kwargs):
        self.name = name
        self.go = nid()
        self.rt = nid()
        self.children = []
        self.components = []
        self.anchor_min = kwargs.get("amin", (0.5, 0.5))
        self.anchor_max = kwargs.get("amax", (0.5, 0.5))
        self.pivot = kwargs.get("pivot", (0.5, 0.5))
        self.pos = kwargs.get("pos", (0.0, 0.0))
        self.size = kwargs.get("size", (100.0, 100.0))
        self.offset_min = kwargs.get("omin", None)
        self.offset_max = kwargs.get("omax", None)
        self.active = kwargs.get("active", True)
        self.refs = {}

    def add(self, c):
        self.children.append(c)
        return c


def v2(t):
    return f"{{x: {t[0]}, y: {t[1]}}}"


def color(c):
    return f"{{r: {c[0]}, g: {c[1]}, b: {c[2]}, a: {c[3]}}}"


def emit_go(n):
    comps = [n.rt] + [cid for cid, _ in n.components]
    lines = [
        f"--- !u!1 &{n.go}",
        "GameObject:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  serializedVersion: 7",
        "  m_Component:",
    ]
    for c in comps:
        lines.append(f"  - component: {{fileID: {c}}}")
    lines += [
        "  m_Layer: 5",
        "  m_HasEditorInfo: 1",
        f"  m_Name: {n.name}",
        "  m_TagString: Untagged",
        "  m_Icon: {fileID: 0}",
        "  m_NavMeshLayer: 0",
        "  m_StaticEditorFlags: 0",
        f"  m_IsActive: {1 if n.active else 0}",
    ]
    return "\n".join(lines)


def emit_rt(n, father_id):
    if n.offset_min is not None and n.offset_max is not None:
        L, B = n.offset_min
        R, T = n.offset_max
        ri, ti = (-R, -T) if R <= 0 and T <= 0 else (R, T)
        pos = ((L - ri) * 0.5, (B - ti) * 0.5)
        size = (-(L + ri), -(B + ti))
    else:
        pos, size = n.pos, n.size
    lines = [
        f"--- !u!224 &{n.rt}",
        "RectTransform:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {n.go}}}",
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
        "  m_LocalPosition: {x: 0, y: 0, z: 0}",
        "  m_LocalScale: {x: 1, y: 1, z: 1}",
        "  m_ConstrainProportionsScale: 0",
        "  m_Children:" + (" []" if not n.children else ""),
    ]
    if n.children:
        for c in n.children:
            lines.append(f"  - {{fileID: {c.rt}}}")
    lines += [
        f"  m_Father: {{fileID: {father_id}}}",
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
        f"  m_AnchorMin: {v2(n.anchor_min)}",
        f"  m_AnchorMax: {v2(n.anchor_max)}",
        f"  m_AnchoredPosition: {v2(pos)}",
        f"  m_SizeDelta: {v2(size)}",
        f"  m_Pivot: {v2(n.pivot)}",
    ]
    return "\n".join(lines)


def add_cr(n):
    cid = nid()
    n.components.append((cid, f"""--- !u!222 &{cid}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n.go}}}
  m_CullTransparentMesh: 1"""))


def add_image(n, col, ray=1):
    add_cr(n)
    cid = nid()
    n.components.append((cid, f"""--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_IMG}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {color(col)}
  m_RaycastTarget: {ray}
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1"""))
    n.refs["image"] = cid
    return cid


def add_text(n, text, size, col, align=4):
    add_cr(n)
    cid = nid()
    safe = text.replace('"', '\\"')
    n.components.append((cid, f"""--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_TXT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {color(col)}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 0}}
    m_FontSize: {size}
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: {align}
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 1
    m_LineSpacing: 1
  m_Text: "{safe}" """))
    n.refs["text"] = cid
    return cid


def add_button(n, target_id):
    cid = nid()
    n.components.append((cid, f"""--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_BTN}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.96, g: 0.96, b: 0.96, a: 1}}
    m_PressedColor: {{r: 0.78, g: 0.78, b: 0.78, a: 1}}
    m_SelectedColor: {{r: 0.96, g: 0.96, b: 0.96, a: 1}}
    m_DisabledColor: {{r: 0.78, g: 0.78, b: 0.78, a: 0.5}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {target_id}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []"""))
    n.refs["button"] = cid
    return cid


def walk(n, father, out):
    out.append(emit_go(n))
    out.append(emit_rt(n, father))
    for cid, body in n.components:
        out.append(body)
    for c in n.children:
        walk(c, n.rt, out)


def btn(parent, name, label, amin, amax, pivot, pos, size, col):
    n = Node(name, amin=amin, amax=amax, pivot=pivot, pos=pos, size=size)
    img = add_image(n, col, 1)
    add_button(n, img)
    lab = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(lab, label, 24, (1, 1, 1, 1), 4)
    n.add(lab)
    parent.add(n)
    return n


def attr_cell(parent, name, label, value, x0):
    root = Node(name + "Root", amin=(x0, 0), amax=(x0 + 1 / 6, 0.7), pivot=(0.5, 0.5), omin=(4, 4), omax=(-4, -4))
    lab = Node("Label", amin=(0, 0.55), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(lab, label, 16, (0.85, 0.8, 0.7, 1), 4)
    root.add(lab)
    val = Node(name, amin=(0, 0), amax=(1, 0.55), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(val, value, 20, (0.7, 0.95, 0.7, 1), 4)
    root.add(val)
    parent.add(root)
    return val.refs["text"]


def build_skill_popup(parent):
    pop = Node("SkillSelectUI", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0), active=False)
    dim = Node("Dim", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_image(dim, (0, 0, 0, 0.55), 1)
    pop.add(dim)
    panel = Node("Panel", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, 40), size=(620, 360))
    add_image(panel, (0.32, 0.2, 0.12, 1), 1)
    title = Node("Title", amin=(0.5, 1), amax=(0.5, 1), pivot=(0.5, 1), pos=(0, -16), size=(220, 40))
    add_text(title, "技能", 28, (1, 1, 1, 1), 4)
    panel.add(title)
    close = btn(panel, "CloseButton", "X", (1, 1), (1, 1), (1, 1), (-12, -12), (44, 44), (0.75, 0.2, 0.18, 1))
    skills = Node("Skills", amin=(0.5, 0.55), amax=(0.5, 0.55), pivot=(0.5, 0.5), pos=(0, 20), size=(560, 96))
    names = ["治愈之泉", "圣盾壁垒", "战意爆发", "疾风架势", "致命专注", "天雷裁决"]
    cols = [(0.35, 0.75, 0.4), (0.35, 0.55, 0.9), (0.9, 0.75, 0.25), (0.4, 0.7, 0.95), (0.95, 0.55, 0.25), (0.65, 0.4, 0.9)]
    sbtns, slabs = [], []
    for i in range(6):
        sk = Node(f"Skill_{i}", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(-230 + i * 92, 0), size=(80, 80))
        si = add_image(sk, (0.55, 0.4, 0.25, 1), 1)
        add_button(sk, si)
        ic = Node("Icon", amin=(0.5, 0.55), amax=(0.5, 0.55), pivot=(0.5, 0.5), pos=(0, 4), size=(48, 48))
        add_image(ic, (*cols[i], 1), 0)
        sk.add(ic)
        lb = Node("Label", amin=(0, 0), amax=(1, 0.28), pivot=(0.5, 0), pos=(0, 2), size=(0, 0))
        add_text(lb, names[i], 14, (1, 1, 1, 1), 4)
        sk.add(lb)
        skills.add(sk)
        sbtns.append(sk.refs["button"])
        slabs.append(lb.refs["text"])
    panel.add(skills)
    desc = Node("DescText", amin=(0.5, 0), amax=(0.5, 0), pivot=(0.5, 0), pos=(0, 28), size=(540, 100))
    add_text(desc, "这里是技能名称和介绍", 24, (1, 0.95, 0.85, 1), 0)
    panel.add(desc)
    pop.add(panel)

    skill_comp = nid()
    pop.components.append((skill_comp, f"""--- !u!114 &{skill_comp}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {pop.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_SKILL}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  closeButton: {{fileID: {close.refs['button']}}}
  titleText: {{fileID: {title.refs['text']}}}
  descText: {{fileID: {desc.refs['text']}}}
  skillButtons:
{chr(10).join('  - {fileID: %s}' % b for b in sbtns)}
  skillIcons:
{chr(10).join('  - {fileID: 0}' for _ in range(6))}
  skillNames:
{chr(10).join('  - {fileID: %s}' % b for b in slabs)}
"""))
    parent.add(pop)
    return pop


def build():
    root = Node("CharacterUI", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    canvas_id, scaler_id, ray_id, ui_id, bag_id = nid(), nid(), nid(), nid(), nid()
    root.components.append((canvas_id, f"""--- !u!223 &{canvas_id}
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root.go}}}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 1
  m_Camera: {{fileID: 0}}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 1
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 0
  m_AdditionalShaderChannelsFlag: 0
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 20
  m_TargetDisplay: 0"""))
    root.components.append((scaler_id, f"""--- !u!114 &{scaler_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_SCALER}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {{x: 720, y: 1280}}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: 1
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0"""))
    root.components.append((ray_id, f"""--- !u!114 &{ray_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_RAY}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295"""))

    content = Node("Content", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 150), omax=(0, -120))
    header = Node("Header", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, 56))
    add_image(header, (0.28, 0.18, 0.1, 0.9), 0)
    title = Node("TitleText", amin=(0, 0), amax=(0.4, 1), pivot=(0, 0.5), pos=(24, 0), size=(0, 0))
    add_text(title, "角色", 32, (1, 0.9, 0.55, 1), 3)
    header.add(title)
    content.add(header)

    stage = Node("Stage", amin=(0, 0.42), amax=(1, 1), pivot=(0.5, 0.5), omin=(12, 0), omax=(-12, -64))
    add_image(stage, (0.35, 0.28, 0.22, 1), 0)
    portrait = Node("Portrait", amin=(0.5, 0.15), amax=(0.5, 0.15), pivot=(0.5, 0), pos=(0, 0), size=(160, 220))
    add_image(portrait, (0.45, 0.5, 0.55, 0.85), 0)
    stage.add(portrait)
    left = btn(stage, "LeftSkillButton", "技能", (0, 0.55), (0, 0.55), (0, 0.5), (16, 0), (100, 72), (0.4, 0.32, 0.55, 1))
    right = Node("RightButtons", amin=(1, 0.2), amax=(1, 0.95), pivot=(1, 1), pos=(-12, 0), size=(120, 0))
    talent = btn(right, "TalentButton", "天赋", (1, 1), (1, 1), (1, 1), (0, 0), (110, 72), (0.35, 0.55, 0.35, 1))
    skill = btn(right, "SkillButton", "技能", (1, 1), (1, 1), (1, 1), (0, -90), (110, 72), (0.4, 0.35, 0.65, 1))
    stage.add(right)
    content.add(stage)

    attrs = Node("AttrPanel", amin=(0, 0.30), amax=(1, 0.42), pivot=(0.5, 0.5), omin=(12, 4), omax=(-12, -4))
    add_image(attrs, (0.22, 0.16, 0.12, 0.95), 0)
    at = Node("AttrTitle", amin=(0.5, 1), amax=(0.5, 1), pivot=(0.5, 1), pos=(0, -4), size=(200, 28))
    add_text(at, "基础属性", 22, (1, 0.92, 0.75, 1), 4)
    attrs.add(at)
    attr_ids = []
    for i, (nm, lb, val) in enumerate([
        ("AttrHp", "生命", "5240"), ("AttrAtk", "攻击", "1280"), ("AttrDef", "防御", "860"),
        ("AttrSpd", "速度", "105"), ("AttrCrit", "暴击", "18%"), ("AttrResist", "抗性", "15%"),
    ]):
        attr_ids.append(attr_cell(attrs, nm, lb, val, i / 6))
    content.add(attrs)

    bag = Node("BagPanel", amin=(0, 0), amax=(1, 0.30), pivot=(0.5, 0.5), omin=(12, 8), omax=(-12, -4))
    add_image(bag, (0.4, 0.28, 0.16, 1), 0)
    bt = Node("BagTitle", amin=(0, 1), amax=(0, 1), pivot=(0, 1), pos=(16, -8), size=(100, 32))
    add_text(bt, "背包", 24, (1, 0.92, 0.75, 1), 3)
    bag.add(bt)
    cap = Node("CapacityText", amin=(1, 1), amax=(1, 1), pivot=(1, 1), pos=(-56, -8), size=(100, 32))
    add_text(cap, "0/21", 22, (1, 0.95, 0.8, 1), 5)
    bag.add(cap)
    plus = btn(bag, "CapacityPlus", "+", (1, 1), (1, 1), (1, 1), (-12, -8), (32, 32), (0.55, 0.35, 0.2, 1))
    # placeholder grid — runtime BuildGrid fills Cell_x_y
    grid = Node("GridContainer", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(12, 12), omax=(-12, -48))
    bag.add(grid)
    content.add(bag)
    root.add(content)

    reserve = Node("BottomNavReserve", amin=(0, 0), amax=(1, 0), pivot=(0.5, 0), pos=(0, 0), size=(0, 150))
    root.add(reserve)

    skill_pop = build_skill_popup(root)

    root.components.append((bag_id, f"""--- !u!114 &{bag_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_BAG}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  gridLayout: {{fileID: 0}}
  gridContainer: {{fileID: {grid.rt}}}
  rowLockOverlay: {{fileID: 0}}
"""))

    root.components.append((ui_id, f"""--- !u!114 &{ui_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_UI}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  talentButton: {{fileID: {talent.refs['button']}}}
  skillButton: {{fileID: {skill.refs['button']}}}
  leftSkillButton: {{fileID: {left.refs['button']}}}
  portraitImage: {{fileID: {portrait.refs['image']}}}
  flipPortraitButton: {{fileID: 0}}
  titleText: {{fileID: {title.refs['text']}}}
  attrHpText: {{fileID: {attr_ids[0]}}}
  attrAtkText: {{fileID: {attr_ids[1]}}}
  attrDefText: {{fileID: {attr_ids[2]}}}
  attrSpdText: {{fileID: {attr_ids[3]}}}
  attrCritText: {{fileID: {attr_ids[4]}}}
  attrResistText: {{fileID: {attr_ids[5]}}}
  bagCapacityText: {{fileID: {cap.refs['text']}}}
  backpackGrid: {{fileID: {bag_id}}}
  bagExpandButton: {{fileID: {plus.refs['button']}}}
  skillSelect: {{fileID: 0}}
"""))

    out = ["%YAML 1.1", "%TAG !u! tag:yousandi.cn,2023:"]
    walk(root, 0, out)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    print("Wrote", OUT, "skill_pop", skill_pop.go)


if __name__ == "__main__":
    build()
