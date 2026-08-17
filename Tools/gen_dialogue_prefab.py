#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate DialogueUI.prefab with choices + skip (placeholder colors)."""
from __future__ import annotations
import os

OUT = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Dialogue\DialogueUI.prefab"
GUID_BTN = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_IMG = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_UI = "e7f3a1b9c4d85061f2a3948b5ce6d7a8"

_nid = 4000


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
        self.scale = kwargs.get("scale", (1.0, 1.0, 1.0))
        self.active = kwargs.get("active", True)
        self.refs = {}

    def add(self, c):
        self.children.append(c)
        return c


def v2(t):
    return f"{{x: {t[0]}, y: {t[1]}}}"


def v3(t):
    return f"{{x: {t[0]}, y: {t[1]}, z: {t[2]}}}"


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
    sx, sy, sz = n.scale
    if n.offset_min is not None and n.offset_max is not None:
        L, B = n.offset_min
        R, T = n.offset_max
        if R <= 0 and T <= 0:
            ri, ti = -R, -T
        else:
            ri, ti = R, T
        ap_x = (L - ri) * 0.5
        ap_y = (B - ti) * 0.5
        sd_x = -(L + ri)
        sd_y = -(B + ti)
        pos = (ap_x, ap_y)
        size = (sd_x, sd_y)
    else:
        pos = n.pos
        size = n.size

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
        f"  m_LocalScale: {{x: {sx}, y: {sy}, z: {sz}}}",
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
    safe = text.replace("\\", "\\\\").replace('"', '\\"')
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
    m_HorizontalOverflow: 1
    m_VerticalOverflow: 1
    m_LineSpacing: 1
  m_Text: "{safe}" """))
    n.refs["text"] = cid
    return cid


def add_button(n, target_id, transition=0):
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
  m_Transition: {transition}
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


def name_plate_on_box(name, icon_left, plate_col, label):
    """名牌相对对话框上沿固定（左上 / 右上）。"""
    if icon_left:
        n = Node(name, amin=(0, 1), amax=(0, 1), pivot=(0, 0), pos=(24, 0), size=(220, 48))
    else:
        n = Node(name, amin=(1, 1), amax=(1, 1), pivot=(1, 0), pos=(-24, 0), size=(220, 48))
    add_image(n, plate_col, 0)
    if icon_left:
        icon = Node("Icon", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(10, 0), size=(32, 32))
    else:
        icon = Node("Icon", amin=(1, 0.5), amax=(1, 0.5), pivot=(1, 0.5), pos=(-10, 0), size=(32, 32))
    add_image(icon, (0.85, 0.75, 0.4, 1), 0)
    n.add(icon)
    align = 3 if icon_left else 5
    if icon_left:
        txt = Node("NameText", amin=(0, 0.5), amax=(1, 0.5), pivot=(0, 0.5), pos=(50, 0), size=(-60, 40))
    else:
        txt = Node("NameText", amin=(0, 0.5), amax=(1, 0.5), pivot=(1, 0.5), pos=(-50, 0), size=(-60, 40))
    add_text(txt, label, 24, (1, 0.95, 0.85, 1), align)
    n.add(txt)
    n.refs["icon"] = icon.refs["image"]
    n.refs["name"] = txt.refs["text"]
    return n


def build():
    root = Node("DialogueUI", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    canvas_id, scaler_id, ray_id, ui_id = nid(), nid(), nid(), nid()
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
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 0
  m_AdditionalShaderChannelsFlag: 0
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 80
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

    refs = {}

    dim = Node("Dim", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_image(dim, (0.18, 0.1, 0.08, 1), 0)
    root.add(dim)

    click = Node("AdvanceClick", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    img = add_image(click, (0, 0, 0, 0.01), 1)
    add_button(click, img, 0)
    root.add(click)
    refs["adv"] = click.refs["button"]

    # 立绘：占位尺寸；运行时 ShowLine 会 SetNativeSize，右侧水平翻转
    lp = Node("LeftPortrait", amin=(0.18, 0), amax=(0.18, 0), pivot=(0.5, 0), pos=(0, 280), size=(100, 100))
    add_image(lp, (0.35, 0.45, 0.4, 0.85), 0)
    root.add(lp)
    refs["lp"] = lp.refs["image"]

    rp = Node(
        "RightPortrait",
        amin=(0.82, 0),
        amax=(0.82, 0),
        pivot=(0.5, 0),
        pos=(0, 280),
        size=(100, 100),
        scale=(-1, 1, 1),
    )
    add_image(rp, (0.45, 0.32, 0.28, 0.85), 0)
    root.add(rp)
    refs["rp"] = rp.refs["image"]

    # 对话框：左右随界面宽度拉伸（size.x 为负边距）
    box = Node("DialogueBox", amin=(0, 0), amax=(1, 0), pivot=(0.5, 0), pos=(0, 40), size=(-40, 260))
    add_image(box, (0.93, 0.88, 0.75, 1), 0)
    body = Node(
        "DialogueText",
        amin=(0, 0),
        amax=(1, 1),
        pivot=(0.5, 0.5),
        omin=(36, 36),
        omax=(-36, -28),
    )
    add_text(body, "在这里写对话正文……\\n（点击继续；有选项时点选项）", 28, (0.22, 0.14, 0.1, 1), 0)
    box.add(body)
    arrow = Node("NextArrow", amin=(1, 0), amax=(1, 0), pivot=(1, 0), pos=(-28, 20), size=(28, 22))
    add_image(arrow, (0.35, 0.55, 0.85, 1), 0)
    box.add(arrow)

    # 名牌挂在对话框上沿，相对对话框固定
    left_plate = name_plate_on_box("LeftNamePlate", True, (0.55, 0.18, 0.15, 1), "发起方")
    right_plate = name_plate_on_box("RightNamePlate", False, (0.2, 0.28, 0.55, 1), "对方")
    box.add(left_plate)
    box.add(right_plate)
    root.add(box)
    refs["box"] = box.refs["image"]
    refs["dialogue"] = body.refs["text"]
    refs["arrow"] = arrow.refs["image"]
    refs["lnp"] = left_plate.refs["image"]
    refs["rnp"] = right_plate.refs["image"]
    refs["lni"] = left_plate.refs["icon"]
    refs["rni"] = right_plate.refs["icon"]
    refs["lnt"] = left_plate.refs["name"]
    refs["rnt"] = right_plate.refs["name"]

    choice_panel = Node("ChoicePanel", amin=(0.5, 0.72), amax=(0.5, 0.72), pivot=(0.5, 0.5), pos=(0, 0), size=(520, 280), active=False)
    choice_imgs = []
    choice_btns = []
    choice_txts = []
    for i in range(3):
        y = 90 - i * 88
        c = Node(f"Choice_{i}", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, y), size=(480, 72), active=False)
        ci = add_image(c, (0.93, 0.88, 0.75, 1), 1)
        add_button(c, ci, 1)
        lab = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
        add_text(lab, f"选项 {i+1}", 28, (0.28, 0.16, 0.1, 1), 4)
        c.add(lab)
        choice_panel.add(c)
        choice_imgs.append(c.refs["image"])
        choice_btns.append(c.refs["button"])
        choice_txts.append(lab.refs["text"])
    root.add(choice_panel)

    skip = Node("SkipButton", amin=(1, 1), amax=(1, 1), pivot=(1, 1), pos=(-28, -36), size=(120, 48))
    si = add_image(skip, (0.35, 0.22, 0.15, 0.9), 1)
    add_button(skip, si, 1)
    st = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(st, "跳过", 26, (1, 0.95, 0.85, 1), 4)
    skip.add(st)
    root.add(skip)
    refs["skip"] = skip.refs["button"]

    def arr(ids):
        return "\n".join(f"  - {{fileID: {i}}}" for i in ids)

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
  dialogueBoxImage: {{fileID: {refs['box']}}}
  leftPortraitImage: {{fileID: {refs['lp']}}}
  rightPortraitImage: {{fileID: {refs['rp']}}}
  leftNamePlateImage: {{fileID: {refs['lnp']}}}
  rightNamePlateImage: {{fileID: {refs['rnp']}}}
  leftNameIcon: {{fileID: {refs['lni']}}}
  rightNameIcon: {{fileID: {refs['rni']}}}
  nextArrowImage: {{fileID: {refs['arrow']}}}
  choiceButtonImages:
{arr(choice_imgs)}
  dialogueText: {{fileID: {refs['dialogue']}}}
  leftNameText: {{fileID: {refs['lnt']}}}
  rightNameText: {{fileID: {refs['rnt']}}}
  choiceTexts:
{arr(choice_txts)}
  advanceButton: {{fileID: {refs['adv']}}}
  skipButton: {{fileID: {refs['skip']}}}
  choiceButtons:
{arr(choice_btns)}
  onAdvance:
    m_PersistentCalls:
      m_Calls: []
  onSkip:
    m_PersistentCalls:
      m_Calls: []
  onChoiceSelected:
    m_PersistentCalls:
      m_Calls: []
  portraitArtFacesRight: 1"""))

    out = ["%YAML 1.1", "%TAG !u! tag:yousandi.cn,2023:"]
    walk(root, 0, out)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    print("Wrote", OUT)


if __name__ == "__main__":
    build()
