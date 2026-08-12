#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate Assets/Resources/Prefabs/Town/AdventureUI.prefab with full UI hierarchy."""
from __future__ import annotations
import os

OUT = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Town\AdventureUI.prefab"

GUID_BTN = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_IMG = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_ADV = "a3f7e2b1c4d58690e1f234567890abcd"

_nid = 1000


def nid():
    global _nid
    _nid += 1
    return _nid


class Node:
    def __init__(self, name, **kwargs):
        self.name = name
        self.go = nid()
        self.rt = nid()
        self.children: list[Node] = []
        self.components = []  # list of (comp_id, yaml_body_fn)
        self.anchor_min = kwargs.get("amin", (0.5, 0.5))
        self.anchor_max = kwargs.get("amax", (0.5, 0.5))
        self.pivot = kwargs.get("pivot", (0.5, 0.5))
        self.pos = kwargs.get("pos", (0.0, 0.0))
        self.size = kwargs.get("size", (100.0, 100.0))
        self.offset_min = kwargs.get("omin", None)
        self.offset_max = kwargs.get("omax", None)
        self.layer = 5
        self.refs = {}  # logical keys -> component fileIDs

    def add(self, child: "Node") -> "Node":
        self.children.append(child)
        return child


def v2(t):
    return f"{{x: {t[0]}, y: {t[1]}}}"


def color(c):
    r, g, b, a = c
    return f"{{r: {r}, g: {g}, b: {b}, a: {a}}}"


def emit_go(n: Node) -> str:
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
        f"  m_Layer: {n.layer}",
        "  m_HasEditorInfo: 1",
        f"  m_Name: {n.name}",
        "  m_TagString: Untagged",
        "  m_Icon: {fileID: 0}",
        "  m_NavMeshLayer: 0",
        "  m_StaticEditorFlags: 0",
        "  m_IsActive: 1",
    ]
    return "\n".join(lines)


def emit_rt(n: Node, father_id: int) -> str:
    child_lines = ""
    if n.children:
        child_lines = "\n".join(f"  - {{fileID: {c.rt}}}" for c in n.children)
    else:
        child_lines = ""
    # If stretch with offsets
    if n.offset_min is not None and n.offset_max is not None:
        amin, amax = n.anchor_min, n.anchor_max
        size = (0.0, 0.0)
        pos = (0.0, 0.0)
        # Unity stores offsetMin/Max via anchoredPosition+sizeDelta for non-stretch;
        # for stretch anchors, sizeDelta = offsetMax-offsetMin related...
        # Simpler: use anchor stretch + sizeDelta as (offsetMax.x - offsetMin.x style):
        # Actually for stretch: sizeDelta.x = -(left+right) if offsets used as padding...
        # We'll encode as: anchorMin/Max, anchoredPosition=0, sizeDelta from omin/omax convention:
        # offsetMin = (left, bottom), offsetMax = (-right, -top) when stretched
        left, bottom = n.offset_min
        right, top = n.offset_max  # expect negative top/right like Unity offsetMax
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
            "  m_Children:",
        ]
        if n.children:
            for c in n.children:
                lines.append(f"  - {{fileID: {c.rt}}}")
        else:
            lines.append("  []")
            # fix empty children - Unity uses empty list as just m_Children: []
            lines[-1] = "  m_Children: []"
            # remove duplicate - rewrite cleanly below
        # rewrite properly
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
            "  m_AnchoredPosition: {x: 0, y: 0}",
            "  m_SizeDelta: {x: 0, y: 0}",
            f"  m_Pivot: {v2(n.pivot)}",
        ]
        # For stretch with padding, Unity uses offsetMin/offsetMax fields in newer YAML?
        # In Unity YAML RectTransform uses anchoredPosition + sizeDelta even for stretch:
        # sizeDelta.x = right - left? Actually: offsetMin=(L,B), offsetMax=(-R,-T)
        # When anchors are stretch: anchoredPosition = ((L-R)/2, (B-T)/2), sizeDelta = (-L-R, -B-T)
        L, B = left, bottom
        # offset_max passed as (right_pad_negative_or_value, top_pad)
        # We'll accept omin=(L,B), omax=(R,T) where R,T are outer inset from right/top (positive means inset)
        R, T = right, top
        # If user passes Unity-style omax as negative, detect:
        if R <= 0 and T <= 0:
            # already unity offsetMax
            ap_x = (L + R) * 0.5
            ap_y = (B + T) * 0.5
            sd_x = L - R  # wait: sizeDelta = (offsetMax.x - offsetMin.x) when stretch? 
            # Correct formula for stretch:
            # anchoredPosition.x = offsetMin.x + (offsetMax.x - offsetMin.x) * pivot? 
            # Standard: sizeDelta = (offsetMax.x - offsetMin.x) NO
            # For stretch anchors: 
            # offsetMin = (left, bottom), offsetMax = (-right, -top)
            # sizeDelta = (offsetMax.x - offsetMin.x + parentWidth*(amin.x-amax.x)...) 
            # Simple known: sizeDelta.x = -(left+right), sizeDelta.y = -(bottom+top)
            # anchoredPosition.x = left - right? Actually AP = ((left-right)/2, (bottom-top)/2) with pivot 0.5
            right_inset = -R
            top_inset = -T
            ap_x = (L - right_inset) * 0.5
            ap_y = (B - top_inset) * 0.5
            sd_x = -(L + right_inset)
            sd_y = -(B + top_inset)
        else:
            # omin=(L,B), omax=(R,T) positive insets
            ap_x = (L - R) * 0.5
            ap_y = (B - T) * 0.5
            sd_x = -(L + R)
            sd_y = -(B + T)
        # 末尾顺序：AnchorMin, AnchorMax, AnchoredPosition, SizeDelta, Pivot
        lines[-3] = f"  m_AnchoredPosition: {{x: {ap_x}, y: {ap_y}}}"
        lines[-2] = f"  m_SizeDelta: {{x: {sd_x}, y: {sd_y}}}"
        return "\n".join(lines)

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
        f"  m_AnchoredPosition: {v2(n.pos)}",
        f"  m_SizeDelta: {v2(n.size)}",
        f"  m_Pivot: {v2(n.pivot)}",
    ]
    return "\n".join(lines)


def add_canvas_renderer(n: Node):
    cid = nid()
    body = f"""--- !u!222 &{cid}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n.go}}}
  m_CullTransparentMesh: 1"""
    n.components.append((cid, body))
    return cid


def add_image(n: Node, col=(1, 1, 1, 1), raycast=1):
    add_canvas_renderer(n)
    cid = nid()
    body = f"""--- !u!114 &{cid}
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
  m_RaycastTarget: {raycast}
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
  m_PixelsPerUnitMultiplier: 1"""
    n.components.append((cid, body))
    n.refs["image"] = cid
    return cid


def add_text(n: Node, text, size=20, col=(1, 1, 1, 1), align=4):
    """align: 0 UpperLeft ... 4 MiddleCenter"""
    add_canvas_renderer(n)
    cid = nid()
    body = f"""--- !u!114 &{cid}
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
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: {text}"""
    n.components.append((cid, body))
    n.refs["text"] = cid
    return cid


def add_button(n: Node, target_graphic_id):
    cid = nid()
    body = f"""--- !u!114 &{cid}
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
    m_HighlightedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_PressedColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}}
    m_SelectedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_DisabledColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}}
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
  m_TargetGraphic: {{fileID: {target_graphic_id}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []"""
    n.components.append((cid, body))
    n.refs["button"] = cid
    return cid


def make_btn(name, amin, amax, pos, size, pivot, bg_col, label, label_size=18):
    n = Node(name, amin=amin, amax=amax, pos=pos, size=size, pivot=pivot)
    img = add_image(n, bg_col)
    add_button(n, img)
    lbl = Node("Label", amin=(0, 0), amax=(1, 1), pos=(0, 0), size=(0, 0), pivot=(0.5, 0.5))
    # stretch label
    lbl.offset_min = (0, 0)
    lbl.offset_max = (0, 0)
    lbl.anchor_min = (0, 0)
    lbl.anchor_max = (1, 1)
    add_text(lbl, label, label_size, (0.95, 0.9, 0.75, 1), 4)
    n.add(lbl)
    return n


def walk_emit(n: Node, father_rt: int, out: list):
    out.append(emit_go(n))
    out.append(emit_rt(n, father_rt))
    for cid, body in n.components:
        out.append(body)
    for c in n.children:
        walk_emit(c, n.rt, out)


def build():
    TOP_H, BOT_H, LEFT_W, DETAIL_H = 120.0, 150.0, 170.0, 340.0
    # mapH approx
    mapH = 1280 - TOP_H - BOT_H - DETAIL_H - 16

    root = Node("AdventureUI", amin=(0, 0), amax=(1, 1), pos=(0, 0), size=(0, 0), pivot=(0.5, 0.5))
    root.offset_min = (0, 0)
    root.offset_max = (0, 0)

    # Canvas etc on root
    canvas_id = nid()
    scaler_id = nid()
    ray_id = nid()
    adv_id = nid()
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

    # Overlay
    overlay = Node("Overlay", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5))
    overlay.offset_min = (0, 0)
    overlay.offset_max = (0, 0)
    add_image(overlay, (0, 0, 0, 0.45))
    root.add(overlay)

    # LeftSidebar: 左侧竖条，上下留出 TopBar / BottomNav
    left = Node(
        "LeftSidebar",
        amin=(0, 0),
        amax=(0, 1),
        pivot=(0, 0.5),
        pos=(LEFT_W * 0.5, (BOT_H - TOP_H) * 0.5),
        size=(LEFT_W, -(TOP_H + BOT_H)),
    )
    add_image(left, (0.10, 0.07, 0.03, 0.95))
    root.add(left)

    mode_btns = []
    mode_icons = []
    mode_labels = []
    mode_names = ["主线冒险", "精英挑战", "迷宫探索", "每日副本", "活动副本"]
    for i, mn in enumerate(mode_names):
        btn = Node(f"ModeBtn_{i}", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, -20 - i * 108), size=(0, 100))
        # stretch x: amin.x=0 amax.x=1 size.x=0
        img = add_image(btn, (0.18, 0.13, 0.06, 1))
        add_button(btn, img)
        icon = Node("Icon", amin=(0.5, 0.62), amax=(0.5, 0.62), pos=(0, 0), size=(52, 52))
        icon_img = add_image(icon, (1, 1, 1, 0.15))
        btn.add(icon)
        icon_txt = Node("IconText", amin=(0.5, 0.62), amax=(0.5, 0.62), pos=(0, 0), size=(52, 52))
        add_text(icon_txt, "◆", 28, (1, 1, 1, 1), 4)
        btn.add(icon_txt)
        label = Node("Label", amin=(0, 0), amax=(1, 0.45), pos=(0, 0), size=(0, 0))
        label.offset_min = (0, 0)
        label.offset_max = (0, 0)
        lt = add_text(label, mn, 18, (0.9, 0.82, 0.65, 1), 4)
        btn.add(label)
        left.add(btn)
        mode_btns.append(btn.refs["button"])
        mode_icons.append(icon_img)
        mode_labels.append(lt)

    # RightContent
    right = Node("RightContent", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5))
    # left=LEFT_W, bottom=BOT_H, right=0, top=TOP_H
    right.offset_min = (LEFT_W, BOT_H)
    right.offset_max = (0, -TOP_H)
    root.add(right)

    # MapRoot top
    map_root = Node("MapRoot", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, mapH))
    right.add(map_root)
    map_bg = Node("MapBg", amin=(0, 0), amax=(1, 1))
    map_bg.offset_min = (0, 0)
    map_bg.offset_max = (0, 0)
    map_bg_img = add_image(map_bg, (0.18, 0.26, 0.14, 1))
    map_root.add(map_bg)

    title_bar = Node("TitleBar", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, 54))
    add_image(title_bar, (0.10, 0.06, 0.02, 0.88))
    map_root.add(title_bar)
    prev = make_btn("PrevBtn", (0, 0.5), (0, 0.5), (30, 0), (44, 44), (0, 0.5), (0.25, 0.18, 0.08, 0.9), "◀", 22)
    title_bar.add(prev)
    nextb = make_btn("NextBtn", (1, 0.5), (1, 0.5), (-30, 0), (44, 44), (1, 0.5), (0.25, 0.18, 0.08, 0.9), "▶", 22)
    title_bar.add(nextb)
    ch_title = Node("ChapterTitle", amin=(0.1, 0), amax=(0.9, 1), pos=(0, 0), size=(0, 0))
    ch_title.offset_min = (0, 0)
    ch_title.offset_max = (0, 0)
    ch_title_txt = add_text(ch_title, "第一章  边境小镇", 26, (1, 0.92, 0.7, 1), 4)
    title_bar.add(ch_title)

    stage_nodes = Node("StageNodes", amin=(0, 0), amax=(1, 1))
    stage_nodes.offset_min = (0, 50)
    stage_nodes.offset_max = (0, -54)
    map_root.add(stage_nodes)

    positions = [
        (-180, 140, "1-1", False),
        (-40, 175, "1-2", False),
        (90, 185, "1-3", False),
        (0, 50, "BOSS", True),
        (160, 80, "1-4", False),
        (145, -40, "1-5", False),
        (50, -130, "1-6", False),
        (-130, -130, "1-7", False),
        (-175, -45, "1-8", False),
    ]
    for i, (x, y, lab, boss) in enumerate(positions):
        sz = 80 if boss else 64
        col = (0.55, 0.10, 0.08, 1) if boss else ((0.80, 0.65, 0.15, 1) if i == 2 else (0.22, 0.36, 0.22, 1))
        node = Node(f"Node_{lab}", amin=(0.5, 0.5), amax=(0.5, 0.5), pos=(x, y), size=(sz, sz))
        img = add_image(node, col)
        add_button(node, img)
        num = Node("Num", amin=(0, 0.35), amax=(1, 1), pos=(0, 0), size=(0, 0))
        num.offset_min = (0, 0)
        num.offset_max = (0, 0)
        add_text(num, lab, 16 if boss else 18, (1, 1, 1, 1), 4)
        node.add(num)
        if not boss:
            stars = Node("Stars", amin=(0, 0), amax=(1, 0.38), pos=(0, 0), size=(0, 0))
            stars.offset_min = (0, 0)
            stars.offset_max = (0, 0)
            add_text(stars, "★★★", 12, (1, 0.85, 0.2, 1), 4)
            node.add(stars)
        stage_nodes.add(node)

    map_bottom = Node("MapBottomBar", amin=(0, 0), amax=(1, 0), pivot=(0.5, 0), pos=(0, 0), size=(0, 52))
    add_image(map_bottom, (0.08, 0.06, 0.02, 0.85))
    map_root.add(map_bottom)
    prog = Node("Progress", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(10, 0), size=(80, 36))
    prog_txt = add_text(prog, "24/24", 20, (1, 0.85, 0.2, 1), 3)  # MiddleLeft=3
    map_bottom.add(prog)
    bar_bg = Node("ProgressBarBg", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(96, 0), size=(160, 14))
    add_image(bar_bg, (0.15, 0.12, 0.05, 1))
    fill = Node("Fill", amin=(0, 0), amax=(1, 1))
    fill.offset_min = (0, 0)
    fill.offset_max = (0, 0)
    fill_img = add_image(fill, (0.9, 0.75, 0.2, 1), raycast=0)
    bar_bg.add(fill)
    map_bottom.add(bar_bg)
    ch_reward = make_btn("ChapterReward", (1, 0.5), (1, 0.5), (-140, 0), (100, 40), (1, 0.5), (0.28, 0.22, 0.10, 1), "章节奖励", 17)
    map_bottom.add(ch_reward)
    adv_log = make_btn("AdventureLog", (1, 0.5), (1, 0.5), (-30, 0), (100, 40), (1, 0.5), (0.28, 0.22, 0.10, 1), "冒险日志", 17)
    map_bottom.add(adv_log)

    # DetailPanel
    detail = Node("DetailPanel", amin=(0, 0), amax=(1, 0), pivot=(0.5, 0), pos=(0, 0), size=(0, DETAIL_H))
    add_image(detail, (0.18, 0.13, 0.06, 0.98))
    right.add(detail)

    stage_name = Node("StageName", amin=(0, 1), amax=(0, 1), pivot=(0, 1), pos=(16, -14), size=(400, 32))
    stage_name_txt = add_text(stage_name, "1-3  哥布林营地", 24, (1, 0.92, 0.7, 1), 3)
    detail.add(stage_name)
    stage_desc = Node("StageDesc", amin=(0, 1), amax=(0, 1), pivot=(0, 1), pos=(16, -52), size=(280, 56))
    stage_desc_txt = add_text(stage_desc, "哥布林们在山洞前搭建了营地，小心他们的埋伏。", 18, (0.85, 0.78, 0.65, 1), 0)
    detail.add(stage_desc)

    enemy = Node("EnemyIcons", amin=(0, 1), amax=(0, 1), pivot=(0, 1), pos=(16, -112), size=(220, 48))
    detail.add(enemy)
    for i in range(4):
        slot = Node(f"Icon{i}", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(i * 58, 0), size=(52, 52))
        add_image(slot, (0.28, 0.22, 0.10, 1))
        enemy.add(slot)

    drop_title = Node("DropTitle", amin=(0, 1), amax=(0, 1), pivot=(0, 1), pos=(310, -14), size=(100, 28))
    add_text(drop_title, "可能掉落", 18, (0.85, 0.78, 0.65, 1), 3)
    detail.add(drop_title)
    drops = Node("DropIcons", amin=(1, 1), amax=(1, 1), pivot=(1, 1), pos=(-16, -44), size=(220, 48))
    detail.add(drops)
    for i in range(4):
        slot = Node(f"Icon{i}", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(i * 58, 0), size=(52, 52))
        add_image(slot, (0.28, 0.22, 0.10, 1))
        drops.add(slot)

    stamina = Node("StaminaCost", amin=(0, 1), amax=(0, 1), pivot=(0, 1), pos=(16, -186), size=(150, 30))
    stamina_txt = add_text(stamina, "消耗体力  10", 20, (0.9, 0.85, 0.3, 1), 3)
    detail.add(stamina)
    chances = Node("Chances", amin=(0, 1), amax=(0, 1), pivot=(0, 1), pos=(230, -186), size=(80, 30))
    chances_txt = add_text(chances, "3/3", 20, (1, 1, 1, 1), 3)
    detail.add(chances)
    add_ch = make_btn("AddChances", (0, 1), (0, 1), (310, -186), (36, 30), (0, 1), (0.85, 0.65, 0.15, 1), "+", 22)
    detail.add(add_ch)

    diff_names = ["普通", "困难", "噩梦", "地狱"]
    diff_cols = [(0.30, 0.55, 0.22, 1), (0.28, 0.42, 0.65, 1), (0.45, 0.22, 0.62, 1), (0.65, 0.18, 0.18, 1)]
    diff_btns = []
    diff_lbls = []
    for i, (dn, dc) in enumerate(zip(diff_names, diff_cols)):
        # bottom row of detail, 4 equal columns
        d = Node(f"Diff_{dn}", amin=(i / 4, 0), amax=((i + 1) / 4, 0), pivot=(0.5, 0), pos=(0, 78), size=(-8, 42))
        img = add_image(d, dc)
        add_button(d, img)
        lbl = Node("Lbl", amin=(0, 0), amax=(1, 1))
        lbl.offset_min = (0, 0)
        lbl.offset_max = (0, 0)
        lt = add_text(lbl, dn, 20, (1, 1, 1, 1), 4)
        d.add(lbl)
        detail.add(d)
        diff_btns.append(d.refs["button"])
        diff_lbls.append(lt)

    start = Node("StartBtn", amin=(0, 0), amax=(0.6, 0), pivot=(0, 0), pos=(8, 12), size=(-16, 56))
    # for partial stretch, sizeDelta x negative
    simg = add_image(start, (0.30, 0.55, 0.22, 1))
    add_button(start, simg)
    sl = Node("Lbl", amin=(0, 0), amax=(1, 1))
    sl.offset_min = (0, 0)
    sl.offset_max = (0, 0)
    add_text(sl, "开始冒险", 24, (1, 1, 1, 1), 4)
    start.add(sl)
    detail.add(start)

    sweep = Node("SweepBtn", amin=(0.6, 0), amax=(1, 0), pivot=(0, 0), pos=(4, 12), size=(-12, 56))
    swimg = add_image(sweep, (0.28, 0.42, 0.65, 1))
    add_button(sweep, swimg)
    swl = Node("Lbl", amin=(0, 0), amax=(1, 1))
    swl.offset_min = (0, 0)
    swl.offset_max = (0, 0)
    add_text(swl, "扫荡", 24, (1, 1, 1, 1), 4)
    sweep.add(swl)
    detail.add(sweep)

    # AdventureUI component with refs
    def arr(ids):
        if not ids:
            return "[]"
        return "\n".join(f"  - {{fileID: {i}}}" for i in ids)

    adv_body = f"""--- !u!114 &{adv_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_ADV}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  mapBackgroundSprite: {{fileID: 0}}
  modeButtonSpriteIcons:
  - {{fileID: 0}}
  - {{fileID: 0}}
  - {{fileID: 0}}
  - {{fileID: 0}}
  - {{fileID: 0}}
  enemySprites:
  - {{fileID: 0}}
  - {{fileID: 0}}
  - {{fileID: 0}}
  - {{fileID: 0}}
  dropSprites:
  - {{fileID: 0}}
  - {{fileID: 0}}
  - {{fileID: 0}}
  - {{fileID: 0}}
  modeButtons:
{arr(mode_btns)}
  modeButtonIcons:
{arr(mode_icons)}
  modeButtonLabels:
{arr(mode_labels)}
  chapterTitle: {{fileID: {ch_title_txt}}}
  prevChapterBtn: {{fileID: {prev.refs['button']}}}
  nextChapterBtn: {{fileID: {nextb.refs['button']}}}
  mapBg: {{fileID: {map_bg_img}}}
  stageNodeContainer: {{fileID: {stage_nodes.rt}}}
  progressLabel: {{fileID: {prog_txt}}}
  progressFill: {{fileID: {fill_img}}}
  chapterRewardBtn: {{fileID: {ch_reward.refs['button']}}}
  adventureLogBtn: {{fileID: {adv_log.refs['button']}}}
  stageNameLabel: {{fileID: {stage_name_txt}}}
  stageDescLabel: {{fileID: {stage_desc_txt}}}
  enemyIconContainer: {{fileID: {enemy.rt}}}
  dropIconContainer: {{fileID: {drops.rt}}}
  staminaCostLabel: {{fileID: {stamina_txt}}}
  remainChancesLabel: {{fileID: {chances_txt}}}
  addChancesBtn: {{fileID: {add_ch.refs['button']}}}
  difficultyButtons:
{arr(diff_btns)}
  difficultyLabels:
{arr(diff_lbls)}
  startBtn: {{fileID: {start.refs['button']}}}
  sweepBtn: {{fileID: {sweep.refs['button']}}}"""
    root.components.append((adv_id, adv_body))

    out = ["%YAML 1.1", "%TAG !u! tag:yousandi.cn,2023:"]
    walk_emit(root, 0, out)
    text = "\n".join(out) + "\n"
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"Wrote {OUT} ({len(text)} bytes, nodes ok)")


if __name__ == "__main__":
    build()
