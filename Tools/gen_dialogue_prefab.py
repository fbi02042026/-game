#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate Assets/Resources/Prefabs/Dialogue/DialogueUI.prefab (placeholder colors)."""
from __future__ import annotations
import os

OUT = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Dialogue\DialogueUI.prefab"
GUID_BTN = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_IMG = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_UI = "e7f3a1b9c4d85061f2a3948b5ce6d7a8"

_nid = 3000


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
        "  m_IsActive: 1",
    ]
    return "\n".join(lines)


def emit_rt(n, father_id):
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
            f"  m_AnchoredPosition: {{x: {ap_x}, y: {ap_y}}}",
            f"  m_SizeDelta: {{x: {sd_x}, y: {sd_y}}}",
            f"  m_Pivot: {v2(n.pivot)}",
        ]
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
    # escape yaml-ish
    safe = text.replace("\n", "\\n")
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
  m_Transition: 0
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_PressedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_SelectedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_DisabledColor: {{r: 1, g: 1, b: 1, a: 0.5}}
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


def name_plate(name, ax, pos, label):
    n = Node(name, amin=(ax, 0), amax=(ax, 0), pivot=(0.5, 0.5), pos=pos, size=(200, 44))
    add_image(n, (0.28, 0.18, 0.12, 1))
    icon = Node("Icon", amin=(0, 0.5), amax=(0, 0.5), pivot=(0, 0.5), pos=(8, 0), size=(28, 28))
    add_image(icon, (0.55, 0.55, 0.55, 1), 0)
    n.add(icon)
    txt = Node("NameText", amin=(0, 0.5), amax=(1, 0.5), pivot=(0, 0.5), pos=(44, 0), size=(-52, 36))
    add_text(txt, label, 22, (1, 0.95, 0.85, 1), 3)
    n.add(txt)
    n.refs["icon"] = icon.refs["image"]
    n.refs["name"] = txt.refs["text"]
    return n


def build():
    root = Node("DialogueUI", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5))
    root.offset_min = (0, 0)
    root.offset_max = (0, 0)

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

    frame = Node("Frame", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5))
    frame.offset_min = (24, 180)
    frame.offset_max = (-24, -80)
    add_image(frame, (0.22, 0.14, 0.1, 1), 0)
    root.add(frame)
    refs["frame"] = frame.refs["image"]

    pattern = Node("PatternBg", amin=(0.5, 0.55), amax=(0.5, 0.55), pivot=(0.5, 0.5), pos=(0, 40), size=(620, 400))
    add_image(pattern, (0.18, 0.12, 0.1, 1), 0)
    root.add(pattern)
    refs["pattern"] = pattern.refs["image"]

    bl = Node("BannerLeft", amin=(0, 1), amax=(0, 1), pivot=(0, 1), pos=(48, -96), size=(48, 72))
    add_image(bl, (0.75, 0.15, 0.12, 1), 0)
    root.add(bl)
    refs["bl"] = bl.refs["image"]

    br = Node("BannerRight", amin=(1, 1), amax=(1, 1), pivot=(1, 1), pos=(-48, -96), size=(48, 72))
    add_image(br, (0.75, 0.15, 0.12, 1), 0)
    root.add(br)
    refs["br"] = br.refs["image"]

    lp = Node("LeftPortrait", amin=(0.28, 0.52), amax=(0.28, 0.52), pivot=(0.5, 0.5), pos=(0, 0), size=(220, 280))
    add_image(lp, (0.35, 0.45, 0.4, 0.85), 0)
    root.add(lp)
    refs["lp"] = lp.refs["image"]

    rp = Node("RightPortrait", amin=(0.72, 0.52), amax=(0.72, 0.52), pivot=(0.5, 0.5), pos=(0, 0), size=(220, 280))
    add_image(rp, (0.45, 0.35, 0.4, 0.85), 0)
    root.add(rp)
    refs["rp"] = rp.refs["image"]

    box = Node("DialogueBox", amin=(0.5, 0), amax=(0.5, 0), pivot=(0.5, 0), pos=(0, 220), size=(600, 200))
    add_image(box, (0.93, 0.88, 0.75, 1), 0)
    body = Node("DialogueText", amin=(0.5, 0.5), amax=(0.5, 0.5), pivot=(0.5, 0.5), pos=(0, 8), size=(540, 140))
    add_text(body, "在这里写对话正文……\\n（点击任意处继续）", 26, (0.22, 0.14, 0.1, 1), 0)
    box.add(body)
    arrow = Node("NextArrow", amin=(1, 0), amax=(1, 0), pivot=(1, 0), pos=(-28, 16), size=(28, 22))
    add_image(arrow, (0.35, 0.22, 0.12, 1), 0)
    box.add(arrow)
    root.add(box)
    refs["box"] = box.refs["image"]
    refs["dialogue"] = body.refs["text"]
    refs["arrow"] = arrow.refs["image"]

    left_plate = name_plate("LeftNamePlate", 0.22, (0, 430), "小美")
    right_plate = name_plate("RightNamePlate", 0.78, (0, 430), "玩家")
    root.add(left_plate)
    root.add(right_plate)
    refs["lnp"] = left_plate.refs["image"]
    refs["rnp"] = right_plate.refs["image"]
    refs["lni"] = left_plate.refs["icon"]
    refs["rni"] = right_plate.refs["icon"]
    refs["lnt"] = left_plate.refs["name"]
    refs["rnt"] = right_plate.refs["name"]

    click = Node("AdvanceClick", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5))
    click.offset_min = (0, 0)
    click.offset_max = (0, 0)
    img = add_image(click, (0, 0, 0, 0.01), 1)
    add_button(click, img)
    root.add(click)
    refs["adv"] = click.refs["button"]

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
  frameImage: {{fileID: {refs['frame']}}}
  patternBgImage: {{fileID: {refs['pattern']}}}
  bannerLeftImage: {{fileID: {refs['bl']}}}
  bannerRightImage: {{fileID: {refs['br']}}}
  leftPortraitImage: {{fileID: {refs['lp']}}}
  rightPortraitImage: {{fileID: {refs['rp']}}}
  dialogueBoxImage: {{fileID: {refs['box']}}}
  leftNamePlateImage: {{fileID: {refs['lnp']}}}
  rightNamePlateImage: {{fileID: {refs['rnp']}}}
  leftNameIcon: {{fileID: {refs['lni']}}}
  rightNameIcon: {{fileID: {refs['rni']}}}
  nextArrowImage: {{fileID: {refs['arrow']}}}
  dialogueText: {{fileID: {refs['dialogue']}}}
  leftNameText: {{fileID: {refs['lnt']}}}
  rightNameText: {{fileID: {refs['rnt']}}}
  advanceButton: {{fileID: {refs['adv']}}}
  onAdvance:
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
