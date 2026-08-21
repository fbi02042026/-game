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
GUID_CN = "b7c8d9e0f1a2435566778899aabbcc01"
GUID_NUM = "f8e4a2b1c9d0476e8a3b5c7d9e1f2034"

SPR = {
    "bg": "DXpOty/8U30bCxwrZuQTXqgFp9A3HcjjyM6dhaQXgNzp7tzBkk0NQbE=",
    "close": "CHwetyOuWngWMzoS0nyqF6dO0ckTAWcsZ3WpUq3Q0Uo047Z62i1c25M=",
    "left_card": "Cy8XsS+oBS/oNvqH1ZCsYfdPkQgCd2pZfEOYLkoDp+ygCSFweYSTOXg=",
    "right_card": "D38c4yj/AC0Mps//d3Z1WUPlOnqp85K5SDhn240AXOTOPIlCBJnwbX4=",
    "right_col": "C3MX53j5V3yJHmQexs4BL6xPSLlN34Sj4nvO7jVxNLtnRwiuh/syKQM=",
    "footer": "D3xNvSn8BX5uJl378wc0G0F7A9WMhIqUMM5nGacTl/JshZG4az8nU+8=",
    "reset": "XnhOsy/+AXxpcRyrht6W1ojZhcPm/cEldaw2I7xdNJ61GTbDm2nk4/8=",
    "gold_bar": "CHwZ5CytVnkXCbZ/D0PWUJiWqqNYb9V1ZdIlf6QZOcFLZYPro8Y5BjE=",
    "stone_bar": "DXobtC+tBXtJp1uGGQvNLWNYvyQg+qcmEanzxt/u0q8ZGtgLa2CtMvI=",
    "hex_off": "XSwWtn7+UX8zP+w6nhO+522q9PnYP0Fmf0tVsatzsTVWMjy3BWujl48=",
    "hex_on": "DCkbsSmkAilrYbCXj+dUq5MHtsH5WybUa6W1xZTzTPuSTNDtt+0trzU=",
    "check_off": "DylO5C+sUy4CcnkEFIme5iNcvD0gTxvOzzCluYJ7Zq8SH+RC4QATkbA=",
    "check_on": "BytNvX6tAXrinfXg3mr/ALNcCaAIfOu85h9s/8d7ZNZW3WWnW0oSt9Q=",
    "link_off": "WnMXsij/VHJ14GSgjvoYqf2tn+D82XUZmSDLseCdoYaAfshIckinIMs=",
    "link_on": "WXxOsiisBnyo8Qo+eQLav0RsVyB+fEjzPmVFNgn4Mzlg4vJ2n2ltaQ8=",
    "rhex": "CSlNs3j+AiikHu/wm5DRtE8/9E9zllAuim+13kx9SlW6NNheNfMgWeE=",
    "rhex_alt": "DXIc5yL+Vy6dB1vz4pXE12SrXtygQo5pYAnEPUnMPSpw3qGn3VGWPwo=",
    "dia_off": "B3MZtSmlUHtZx7whI+VCVCxLTIT8FxaojtRGMndjpwj4f/gPEzDOPFs=",
    "dia_on": "CngasnyoV3m/vDoJ7k7tcLObcIxCEdZsquIyNJqPyo0+f5us4UCydk4=",
    "rlink_off": "CSsXsiKqWy883wuIYwhviYuOBk9LdOdayFvemH7/gaVyw76s8MMyChI=",
    "rlink_on": "B3wWtiioVyjEnh8PTPc0zQYJ8PEJcbAVe8Mf8ZfUtGK+09Fok9O2j3c=",
    "lock": "CXMf4HmuUH2Pz6B8TrJ01+PIvZNj/9ZdeRcKZZnkEmv7V50aiJtg1as=",
    "arrow": "DHgc5i6tBimbaOSssYTUa4OGkTpZTwvzG8FNRD7k0FZKCbQo/WznKkI=",
}


def spr(key):
    return f"{{fileID: 21300000, guid: {SPR[key]}, type: 3}}"

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


def add_image(n, col, ray=1, sprite=None, preserve=0, itype=0):
    add_cr(n)
    cid = nid()
    if sprite:
        col = (1, 1, 1, 1)
        sprite_ref = spr(sprite)
    else:
        sprite_ref = "{fileID: 0}"
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
  m_Sprite: {sprite_ref}
  m_Type: {itype}
  m_PreserveAspect: {preserve}
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1"""))
    n.refs["image"] = cid
    return cid


def add_text(n, text, size, col, align=4, font=None):
    add_cr(n)
    cid = nid()
    safe = text.replace("\\", "\\\\").replace('"', '\\"')
    fg = font or GUID_CN
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
    m_Font: {{fileID: 12800000, guid: {fg}, type: 3}}
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


def make_scroll_column(parent_name, header_label, tip, cost_name, card_key, bar_key):
    col = Node(parent_name, amin=(0, 0), amax=(0.5, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(-4, 0))
    if parent_name.startswith("Right"):
        col.anchor_min = (0.5, 0)
        col.anchor_max = (1, 1)
        col.offset_min = (4, 0)
        col.offset_max = (0, 0)
    add_image(col, (1, 1, 1, 0.04), 0)

    head = Node("Header", amin=(0.5, 1), amax=(0.5, 1), pivot=(0.5, 1), pos=(0, -6), size=(240, 36))
    add_image(head, (1, 1, 1, 0), 0)
    hl = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(hl, header_label, 20, (0.28, 0.16, 0.1, 1) if "Left" in parent_name else (0.28, 0.14, 0.4, 1), 4)
    head.add(hl)
    col.add(head)

    scroll = Node("LeftScroll" if "Left" in parent_name else "RightScroll",
                  amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(4, 50), omax=(-4, -46))
    viewport = Node("Viewport", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_image(viewport, (1, 1, 1, 0.02), 1)
    add_mask(viewport, 0)
    content = Node("Content", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, 800))
    viewport.add(content)
    scroll.add(viewport)
    add_scroll(scroll, viewport.rt, content.rt)
    col.add(scroll)

    bar = Node("GoldBar" if "Left" in parent_name else "StoneBar",
               amin=(1, 0), amax=(1, 0), pivot=(1, 0), pos=(-8, 8), size=(90, 36))
    add_image(bar, (1, 1, 1, 1), 0, sprite=bar_key, preserve=1)
    cost = Node(cost_name, amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(cost, "0", 18, (1, 0.93, 0.55, 1), 4, font=GUID_NUM)
    bar.add(cost)
    col.add(bar)

    tip_n = Node("LeftTip" if "Left" in parent_name else "RightTip",
                 amin=(0, 0), amax=(1, 0), pivot=(0, 0), pos=(10, 8), size=(-100, 32))
    add_text(tip_n, tip, 15, (0.4, 0.2, 0.35, 1), 3)
    col.add(tip_n)
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
  m_SortingOrder: 200
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

    panel = Node("Panel", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, 0), size=(700, 1240))
    add_image(panel, (1, 1, 1, 1), 1, sprite="bg")
    refs["panel"] = panel.refs["image"]

    title_bar = Node("TitleBar", amin=(0.5, 1), amax=(0.5, 1), pivot=(0.5, 1), pos=(0, -58), size=(280, 52))
    title = Node("TitleText", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(title, "天赋", 36, (1, 0.93, 0.72, 1), 4)
    title_bar.add(title)
    panel.add(title_bar)
    close = Node("CloseButton", amin=(1, 1), amax=(1, 1), pivot=(1, 1), pos=(-18, -18), size=(64, 64))
    ci = add_image(close, (1, 1, 1, 1), 1, sprite="close", preserve=1)
    add_button(close, ci)
    panel.add(close)
    refs["close"] = close.refs["button"]
    refs["title"] = title.refs["text"]

    res = Node("ResourceRow", amin=(0.5, 1), amax=(0.5, 1), pivot=(0.5, 1), pos=(0, -118), size=(520, 40))
    gi = Node("GoldIcon", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(8, 0), size=(32, 32))
    add_image(gi, (1, 1, 1, 1), 0, sprite="check_on", preserve=1)
    res.add(gi)
    gt = Node("GoldText", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(52, 0), size=(140, 36))
    add_text(gt, "999999+", 24, (1, 0.95, 0.7, 1), 3, font=GUID_NUM)
    res.add(gt)
    si = Node("StoneIcon", amin=(0.55, 0.5), amax=(0.55, 0.5), pivot=(0, 0.5), pos=(0, 0), size=(36, 36))
    add_image(si, (1, 1, 1, 1), 0, sprite="dia_on", preserve=1)
    res.add(si)
    st = Node("StoneText", amin=(0.55, 0.5), amax=(0.55, 0.5), pivot=(0, 0.5), pos=(44, 0), size=(100, 36))
    add_text(st, "999+", 24, (0.9, 0.8, 1, 1), 3, font=GUID_NUM)
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

    columns = Node("Columns", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(36, 168), omax=(-36, -168))
    left = make_scroll_column("LeftColumn", "属性天赋", "消耗金币解锁属性天赋", "LeftCostText", "left_card", "gold_bar")
    right = make_scroll_column("RightColumn", "辅助/专精天赋", "消耗天赋石解锁辅助/专精天赋", "RightCostText", "right_col", "stone_bar")
    columns.add(left)
    columns.add(right)
    panel.add(columns)
    refs["left_scroll"] = left.refs["scroll"]
    refs["right_scroll"] = right.refs["scroll"]
    refs["left_cost"] = left.refs["cost"]
    refs["right_cost"] = right.refs["cost"]
    refs["left_tip"] = left.refs["tip"]
    refs["right_tip"] = right.refs["tip"]

    footer = Node("Footer", amin=(0.5, 0), amax=(0.5, 0), pivot=(0.5, 0), pos=(0, 28), size=(620, 118))
    add_image(footer, (1, 1, 1, 1), 0, sprite="footer", itype=1)
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
    reset = Node("ResetButton", amin=(1, 0.5), amax=(1, 0.5), pivot=(1, 0.5), pos=(-12, -6), size=(196, 70))
    ri = add_image(reset, (1, 1, 1, 1), 1, sprite="reset")
    add_button(reset, ri)
    rl = Node("Label", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), omin=(0, 0), omax=(0, 0))
    add_text(rl, "重置天赋", 26, (1, 0.92, 0.75, 1), 4)
    reset.add(rl)
    footer.add(reset)
    panel.add(footer)
    refs["sums"] = sums
    refs["reset"] = reset.refs["button"]
    root.add(panel)

    # Left template: card + hex + connecting line
    left_t = Node("LeftNodeTemplate", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, 100), active=False)
    lti = add_image(left_t, (1, 1, 1, 1), 1, sprite="left_card")
    add_button(left_t, lti)
    ln = Node("Line", amin=(0, 0.5), amax=(0, 0.5), pivot=(0.5, 1), pos=(46, -28), size=(12, 56))
    add_image(ln, (1, 1, 1, 1), 0, sprite="link_on")
    left_t.add(ln)
    icon = Node("Icon", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(10, 0), size=(72, 72))
    add_image(icon, (1, 1, 1, 1), 0, sprite="hex_off", preserve=1)
    left_t.add(icon)
    nt = Node("NameText", amin=(0, 0.52), amax=(1, 1), pivot=(0, 0.5), pos=(88, 0), size=(-128, 0))
    add_text(nt, "力量 I", 22, (0.25, 0.15, 0.1, 1), 3)
    left_t.add(nt)
    et = Node("EffectText", amin=(0, 0), amax=(1, 0.52), pivot=(0, 0.5), pos=(88, 0), size=(-128, 0))
    add_text(et, "攻击 +3", 18, (0.4, 0.28, 0.18, 1), 3)
    left_t.add(et)
    ck = Node("Check", amin=(1, 0.5), amax=(1, 0.5), pivot=(1, 0.5), pos=(-10, 0), size=(32, 28))
    add_image(ck, (1, 1, 1, 1), 0, sprite="check_off", preserve=1)
    left_t.add(ck)
    root.add(left_t)

    # Right template: card + diamond spine + connecting line + option hexes
    right_t = Node("RightRowTemplate", amin=(0, 1), amax=(1, 1), pivot=(0.5, 1), pos=(0, 0), size=(0, 118), active=False)
    add_image(right_t, (1, 1, 1, 1), 0, sprite="right_card")
    rln = Node("Line", amin=(0, 0.5), amax=(0, 0.5), pivot=(0.5, 1), pos=(18, -16), size=(10, 72))
    add_image(rln, (1, 1, 1, 1), 0, sprite="rlink_on")
    right_t.add(rln)
    dia = Node("Diamond", amin=(0, 0.5), amax=(0, 0.5), pivot=(0.5, 0.5), pos=(18, 8), size=(30, 26))
    add_image(dia, (1, 1, 1, 1), 0, sprite="dia_off", preserve=1)
    right_t.add(dia)
    rt = Node("TitleText", amin=(0, 1), amax=(0.72, 1), pivot=(0, 1), pos=(38, -4), size=(0, 26))
    add_text(rt, "武器专精", 18, (0.22, 0.12, 0.32, 1), 3)
    right_t.add(rt)
    rc = Node("CostText", amin=(0.72, 1), amax=(1, 1), pivot=(1, 1), pos=(-36, -4), size=(0, 26))
    add_text(rc, "12", 16, (0.42, 0.22, 0.62, 1), 5, font=GUID_NUM)
    right_t.add(rc)
    lk = Node("Lock", amin=(1, 0), amax=(1, 0), pivot=(1, 0), pos=(-6, 8), size=(28, 40))
    add_image(lk, (1, 1, 1, 1), 0, sprite="lock", preserve=1)
    right_t.add(lk)
    for o in range(3):
        opt = Node(f"Opt_{o}", amin=(0, 0), amax=(0, 0), pivot=(0, 0), pos=(40 + o * 68, 8), size=(62, 62))
        oi = add_image(opt, (1, 1, 1, 1), 1, sprite="rhex_alt" if o == 1 else "rhex", preserve=1)
        add_button(opt, oi)
        oicon = Node("Icon", amin=(0.5, 0.55), amax=(0.5, 0.55), pivot=(0.5, 0.5), pos=(0, 0), size=(36, 36))
        add_image(oicon, (1, 1, 1, 0.15), 0)
        opt.add(oicon)
        ol = Node("Label", amin=(0, 0), amax=(1, 0.32), pivot=(0.5, 0), pos=(0, 0), size=(0, 0))
        add_text(ol, "选项", 12, (1, 1, 1, 1), 4)
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
  sprPanelBg: {spr('bg')}
  sprClose: {spr('close')}
  sprLeftCard: {spr('left_card')}
  sprRightCard: {spr('right_card')}
  sprFooter: {spr('footer')}
  sprReset: {spr('reset')}
  sprGoldBar: {spr('gold_bar')}
  sprStoneBar: {spr('stone_bar')}
  sprLeftHexOff: {spr('hex_off')}
  sprLeftHexOn: {spr('hex_on')}
  sprCheckOff: {spr('check_off')}
  sprCheckOn: {spr('check_on')}
  sprLeftLinkOff: {spr('link_off')}
  sprLeftLinkOn: {spr('link_on')}
  sprRightHex: {spr('rhex')}
  sprRightHexAlt: {spr('rhex_alt')}
  sprDiamondOff: {spr('dia_off')}
  sprDiamondOn: {spr('dia_on')}
  sprRightLinkOff: {spr('rlink_off')}
  sprRightLinkOn: {spr('rlink_on')}
  sprLock: {spr('lock')}
  sprArrow: {spr('arrow')}
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
