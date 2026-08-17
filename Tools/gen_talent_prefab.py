#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate TalentUI.prefab shell (templates + layout). Runtime fills L/R lists."""
from __future__ import annotations
import os

OUT = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Talent\TalentUI.prefab"
GUID_BTN = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_IMG = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_SCROLL = "1aa08ab6e0800fa44ae55d278d1423e3"
GUID_MASK = "31a19414c41e5ae4aae2af33fee712f6"
GUID_UI = "b8c4e2f1a0d9473e9c5b6a7d8e9f0123"

_nid = 5000


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
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 1
    m_LineSpacing: 1
  m_Text: "{safe}" """))
    n.refs["text"] = cid
    return cid


def add_button(n, target_id, transition=1):
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


def add_mask(n, show=0):
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
  m_Script: {{fileID: 11500000, guid: {GUID_MASK}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_ShowMaskGraphic: {show}"""))
    return cid


def add_scroll(n, viewport_rt, content_rt):
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
  m_Script: {{fileID: 11500000, guid: {GUID_SCROLL}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Content: {{fileID: {content_rt}}}
  m_Horizontal: 0
  m_Vertical: 1
  m_MovementType: 2
  m_Elasticity: 0.1
  m_Inertia: 1
  m_DecelerationRate: 0.135
  m_ScrollSensitivity: 24
  m_Viewport: {{fileID: {viewport_rt}}}
  m_HorizontalScrollbar: {{fileID: 0}}
  m_VerticalScrollbar: {{fileID: 0}}
  m_HorizontalScrollbarVisibility: 0
  m_VerticalScrollbarVisibility: 0
  m_HorizontalScrollbarSpacing: 0
  m_VerticalScrollbarSpacing: 0
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []"""))
    n.refs["scroll"] = cid
    return cid


def walk(n, father, out):
    out.append(emit_go(n))
    out.append(emit_rt(n, father))
    for cid, body in n.components:
        out.append(body)
    for c in n.children:
        walk(c, n.rt, out)


def make_scroll_column(parent_name, bg, header_label, tip, cost_name):
    col = Node(parent_name, amin=(0, 0), amax=(0.5, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(-4, 0))
    if parent_name.startswith("Right"):
        col.anchor_min = (0.5, 0)
        col.anchor_max = (1, 1)
        col.offset_min = (4, 0)
        col.offset_max = (0, 0)
    add_image(col, bg, 0)

    head = Node("Header", amin=(0.5, 1), amax=(0.5, 1), pivot=(0.5, 1), pos=(0, -8), size=(260, 40))
    add_image(head, (0.45, 0.35, 0.55, 1) if "Right" in parent_name else (0.55, 0.5, 0.45, 1), 0)
    hl = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(hl, header_label, 22, (1, 1, 1, 1), 4)
    head.add(hl)
    col.add(head)

    scroll = Node("LeftScroll" if "Left" in parent_name else "RightScroll",
                  amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(8, 48), omax=(-8, -56))
    viewport = Node("Viewport", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_image(viewport, (1, 1, 1, 0.02), 1)
    add_mask(viewport, 0)
    content = Node("Content", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, 800))
    viewport.add(content)
    scroll.add(viewport)
    add_scroll(scroll, viewport.rt, content.rt)
    col.add(scroll)

    tip_n = Node("LeftTip" if "Left" in parent_name else "RightTip",
                 amin=(0, 0), amax=(1, 0), pivot=(0, 0), pos=(12, 10), size=(-80, 32))
    add_text(tip_n, tip, 16, (0.45, 0.2, 0.35, 1), 3)
    col.add(tip_n)
    cost = Node(cost_name, amin=(1, 0), amax=(1, 0), pivot=(1, 0), pos=(-16, 10), size=(70, 32))
    add_text(cost, "0", 20, (0.7, 0.5, 0.2, 1), 5)
    col.add(cost)
    col.refs["scroll"] = scroll.refs["scroll"]
    col.refs["content_rt"] = content.rt
    col.refs["cost"] = cost.refs["text"]
    col.refs["tip"] = tip_n.refs["text"]
    return col


def build():
    root = Node("TalentUI", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
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
  m_SortingOrder: 90
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
    add_image(dim, (0, 0, 0, 0.55), 1)
    root.add(dim)

    panel = Node("Panel", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, 0), size=(680, 1100))
    add_image(panel, (0.45, 0.28, 0.16, 1), 1)
    refs["panel"] = panel.refs["image"]

    title_bar = Node("TitleBar", amin=(0.5, 1), amax=(0.5, 1), pivot=(0.5, 1), pos=(0, -8), size=(420, 56))
    add_image(title_bar, (0.35, 0.2, 0.12, 1), 0)
    title = Node("TitleText", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(title, "天赋", 34, (1, 1, 1, 1), 4)
    title_bar.add(title)
    close = Node("CloseButton", amin=(1, 1), amax=(1, 1), pivot=(1, 1), pos=(36, 8), size=(48, 48))
    ci = add_image(close, (0.75, 0.2, 0.18, 1), 1)
    add_button(close, ci)
    cx = Node("X", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(cx, "X", 28, (1, 1, 1, 1), 4)
    close.add(cx)
    title_bar.add(close)
    panel.add(title_bar)
    refs["close"] = close.refs["button"]
    refs["title"] = title.refs["text"]

    res = Node("ResourceRow", amin=(0.5, 1), amax=(0.5, 1), pivot=(0.5, 1), pos=(0, -78), size=(560, 44))
    gi = Node("GoldIcon", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(8, 0), size=(36, 36))
    add_image(gi, (0.95, 0.8, 0.25, 1), 0)
    res.add(gi)
    gt = Node("GoldText", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(52, 0), size=(140, 36))
    add_text(gt, "999999+", 24, (1, 0.95, 0.7, 1), 3)
    res.add(gt)
    si = Node("StoneIcon", amin=(0.55, 0.5), amax=(0.55, 0.5), pivot=(0, 0.5), pos=(0, 0), size=(36, 36))
    add_image(si, (0.55, 0.35, 0.85, 1), 0)
    res.add(si)
    st = Node("StoneText", amin=(0.55, 0.5), amax=(0.55, 0.5), pivot=(0, 0.5), pos=(44, 0), size=(100, 36))
    add_text(st, "999+", 24, (0.9, 0.8, 1, 1), 3)
    res.add(st)
    sp = Node("StonePlus", amin=(0.55, 0.5), amax=(0.55, 0.5), pivot=(0, 0.5), pos=(150, 0), size=(36, 36))
    spi = add_image(sp, (0.4, 0.7, 0.35, 1), 1)
    add_button(sp, spi)
    spl = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(spl, "+", 28, (1, 1, 1, 1), 4)
    sp.add(spl)
    res.add(sp)
    panel.add(res)
    refs["gold"] = gt.refs["text"]
    refs["stone"] = st.refs["text"]
    refs["stone_plus"] = sp.refs["button"]

    columns = Node("Columns", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(18, 150), omax=(-18, -120))
    left = make_scroll_column("LeftColumn", (0.91, 0.86, 0.75, 1), "属性天赋", "消耗金币解锁属性天赋", "LeftCostText")
    right = make_scroll_column("RightColumn", (0.78, 0.74, 0.86, 1), "辅助/专精天赋", "消耗天赋石解锁辅助/专精天赋", "RightCostText")
    columns.add(left)
    columns.add(right)
    panel.add(columns)
    refs["left_scroll"] = left.refs["scroll"]
    refs["right_scroll"] = right.refs["scroll"]
    refs["left_cost"] = left.refs["cost"]
    refs["right_cost"] = right.refs["cost"]
    refs["left_tip"] = left.refs["tip"]
    refs["right_tip"] = right.refs["tip"]

    footer = Node("Footer", amin=(0, 0), amax=(1, 0), pivot=(0.5, 0), pos=(0, 12), size=(-24, 120))
    add_image(footer, (0.32, 0.2, 0.12, 1), 0)
    sl = Node("SumLabel", amin=(0, 1), amax=(0.55, 1), pivot=(0, 1), pos=(16, -8), size=(0, 28))
    add_text(sl, "已获得属性加成", 20, (0.75, 0.95, 0.7, 1), 3)
    footer.add(sl)
    sums = []
    for i, (nm, val) in enumerate([("SumAttack", "+0"), ("SumHp", "+0"), ("SumDef", "+0"), ("SumCrit", "+0%"), ("SumAtkSpd", "+0%")]):
        x0 = i * 0.18
        sn = Node(nm, amin=(x0, 0), amax=(x0 + 0.16, 0.55), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
        add_text(sn, val, 20, (0.7, 0.95, 0.65, 1), 4)
        footer.add(sn)
        sums.append(sn.refs["text"])
    reset = Node("ResetButton", amin=(1, 0.5), amax=(1, 0.5), pivot=(1, 0.5), pos=(-16, -8), size=(160, 64))
    ri = add_image(reset, (0.65, 0.18, 0.16, 1), 1)
    add_button(reset, ri)
    rl = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(rl, "重置天赋", 26, (1, 0.92, 0.75, 1), 4)
    reset.add(rl)
    footer.add(reset)
    panel.add(footer)
    refs["sums"] = sums
    refs["reset"] = reset.refs["button"]
    root.add(panel)

    # Left template
    left_t = Node("LeftNodeTemplate", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, 80), active=False)
    lti = add_image(left_t, (0.95, 0.9, 0.8, 0.01), 1)
    add_button(left_t, lti)
    icon = Node("Icon", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(10, 0), size=(56, 56))
    add_image(icon, (0.4, 0.35, 0.3, 1), 0)
    left_t.add(icon)
    nt = Node("NameText", amin=(0, 0.55), amax=(1, 1), pivot=(0, 0.5), pos=(78, 0), size=(-120, 0))
    add_text(nt, "力量 I", 22, (0.25, 0.15, 0.1, 1), 3)
    left_t.add(nt)
    et = Node("EffectText", amin=(0, 0), amax=(1, 0.5), pivot=(0, 0.5), pos=(78, 0), size=(-120, 0))
    add_text(et, "攻击 +3", 18, (0.4, 0.3, 0.22, 1), 3)
    left_t.add(et)
    ck = Node("Check", amin=(1, 0.5), amax=(1, 0.5), pivot=(1, 0.5), pos=(-12, 0), size=(28, 28))
    add_image(ck, (0.55, 0.55, 0.55, 1), 0)
    left_t.add(ck)
    ln = Node("Line", amin=(0, 0), amax=(0, 0), pivot=(0.5, 1), pos=(38, 0), size=(4, 12))
    add_image(ln, (0.35, 0.28, 0.2, 0.7), 0)
    left_t.add(ln)
    root.add(left_t)

    # Right template
    right_t = Node("RightRowTemplate", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, 100), active=False)
    add_image(right_t, (0.85, 0.8, 0.92, 0.25), 0)
    rt = Node("TitleText", amin=(0, 1), amax=(0.7, 1), pivot=(0, 1), pos=(10, -4), size=(0, 28))
    add_text(rt, "武器专精", 20, (0.25, 0.15, 0.35, 1), 3)
    right_t.add(rt)
    rc = Node("CostText", amin=(0.7, 1), amax=(1, 1), pivot=(1, 1), pos=(-36, -4), size=(0, 28))
    add_text(rc, "12", 18, (0.45, 0.25, 0.7, 1), 5)
    right_t.add(rc)
    lk = Node("Lock", amin=(1, 0), amax=(1, 0), pivot=(1, 0), pos=(-8, 8), size=(24, 24))
    add_image(lk, (0.3, 0.3, 0.35, 0.9), 0)
    right_t.add(lk)
    for o in range(3):
        opt = Node(f"Opt_{o}", amin=(0, 0), amax=(0, 0), pivot=(0, 0), pos=(12 + o * 72, 10), size=(64, 64))
        oi = add_image(opt, (0.55, 0.45, 0.65, 1), 1)
        add_button(opt, oi)
        oicon = Node("Icon", amin=(0.5, 0.55), amax=(0.5, 0.55), pivot=(0.5, 0.5), pos=(0, 0), size=(40, 40))
        add_image(oicon, (0.75, 0.7, 0.85, 1), 0)
        opt.add(oicon)
        ol = Node("Label", amin=(0, 0), amax=(1, 0.35), pivot=(0.5, 0), pos=(0, 0), size=(0, 0))
        add_text(ol, "选项", 14, (1, 1, 1, 1), 4)
        opt.add(ol)
        right_t.add(opt)
    root.add(right_t)

    # Choice popup
    pop = Node("ChoicePopup", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0), active=False)
    add_image(pop, (0, 0, 0, 0.65), 1)
    box = Node("Box", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, 0), size=(480, 420))
    add_image(box, (0.93, 0.88, 0.78, 1), 0)
    pop.add(box)
    pt = Node("Title", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, 160), size=(400, 40))
    add_text(pt, "选择天赋", 28, (0.25, 0.15, 0.1, 1), 4)
    pop.add(pt)
    choice_btns = []
    choice_labs = []
    for i in range(3):
        c = Node(f"Choice_{i}", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, 70 - i * 90), size=(400, 72))
        cimg = add_image(c, (0.55, 0.35, 0.55, 1), 1)
        add_button(c, cimg)
        cl = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
        add_text(cl, "选项", 22, (1, 1, 1, 1), 4)
        c.add(cl)
        pop.add(c)
        choice_btns.append(c.refs["button"])
        choice_labs.append(cl.refs["text"])
    cancel = Node("Cancel", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, -170), size=(200, 48))
    cai = add_image(cancel, (0.45, 0.3, 0.2, 1), 1)
    add_button(cancel, cai)
    cal = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(cal, "取消", 24, (1, 1, 1, 1), 4)
    cancel.add(cal)
    pop.add(cancel)
    root.add(pop)
    refs["choice_title"] = pt.refs["text"]
    refs["choice_btns"] = choice_btns
    refs["choice_labs"] = choice_labs
    refs["cancel"] = cancel.refs["button"]

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
  panelImage: {{fileID: {refs['panel']}}}
  closeButton: {{fileID: {refs['close']}}}
  titleText: {{fileID: {refs['title']}}}
  goldText: {{fileID: {refs['gold']}}}
  stoneText: {{fileID: {refs['stone']}}}
  stonePlusButton: {{fileID: {refs['stone_plus']}}}
  leftScroll: {{fileID: {refs['left_scroll']}}}
  leftContent: {{fileID: {left.refs['content_rt']}}}
  leftTipText: {{fileID: {refs['left_tip']}}}
  leftCostText: {{fileID: {refs['left_cost']}}}
  leftNodeTemplate: {{fileID: {left_t.go}}}
  rightScroll: {{fileID: {refs['right_scroll']}}}
  rightContent: {{fileID: {right.refs['content_rt']}}}
  rightTipText: {{fileID: {refs['right_tip']}}}
  rightCostValueText: {{fileID: {refs['right_cost']}}}
  rightRowTemplate: {{fileID: {right_t.go}}}
  sumAttackText: {{fileID: {sums[0]}}}
  sumHpText: {{fileID: {sums[1]}}}
  sumDefText: {{fileID: {sums[2]}}}
  sumCritText: {{fileID: {sums[3]}}}
  sumAtkSpdText: {{fileID: {sums[4]}}}
  resetButton: {{fileID: {refs['reset']}}}
  choicePopup: {{fileID: {pop.go}}}
  choiceTitleText: {{fileID: {refs['choice_title']}}}
  choiceButtons:
{arr(choice_btns)}
  choiceLabels:
{arr(choice_labs)}
  choiceCancelButton: {{fileID: {refs['cancel']}}}
  onClosed:
    m_PersistentCalls:
      m_Calls: []
  onLeftUnlockRequested:
    m_PersistentCalls:
      m_Calls: []
  onRightChoiceRequested:
    m_PersistentCalls:
      m_Calls: []
  onResetRequested:
    m_PersistentCalls:
      m_Calls: []"""))

    out = ["%YAML 1.1", "%TAG !u! tag:yousandi.cn,2023:"]
    walk(root, 0, out)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    print("Wrote", OUT)


if __name__ == "__main__":
    build()
