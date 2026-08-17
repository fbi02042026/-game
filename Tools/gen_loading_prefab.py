#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate LoadingUI.prefab matching runtime BattleLoadingOverlay layout."""
from pathlib import Path

OUT = Path(r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Loading\LoadingUI.prefab")
GUID_IMG = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_OUTLINE = "e19747de3f5aca642ab2be37e372fb86"
GUID_UI = "f1a2b3c4d5e6478091a2b3c4d5e67890"
GUID_BG = "c6ab257eeb0243f9abbf84559d03f17a"
GUID_CN = "b7c8d9e0f1a2435566778899aabbcc01"
GUID_NUM = "f8e4a2b1c9d0476e8a3b5c7d9e1f2034"

_nid = 8000


def nid():
    global _nid
    _nid += 1
    return _nid


def v2(t):
    return f"{{x: {t[0]}, y: {t[1]}}}"


def color(c):
    return f"{{r: {c[0]}, g: {c[1]}, b: {c[2]}, a: {c[3]}}}"


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
        self.refs = {}

    def add(self, c):
        self.children.append(c)
        return c


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


def add_image(n, col, sprite_guid=None, ray=1):
    add_cr(n)
    cid = nid()
    spr = f"{{fileID: 21300000, guid: {sprite_guid}, type: 3}}" if sprite_guid else "{fileID: 0}"
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
  m_Sprite: {spr}
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


def add_text(n, text, size, col, align, font_guid, wrap=False):
    add_cr(n)
    cid = nid()
    hov = 0 if wrap else 1
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
    m_Font: {{fileID: 12800000, guid: {font_guid}, type: 3}}
    m_FontSize: {size}
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: {align}
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: {hov}
    m_VerticalOverflow: 1
    m_LineSpacing: 1
  m_Text: "{text}" """))
    n.refs["text"] = cid
    return cid


def add_outline(n):
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
  m_Script: {{fileID: 11500000, guid: {GUID_OUTLINE}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_EffectColor: {{r: 0, g: 0, b: 0, a: 0.85}}
  m_EffectDistance: {{x: 1.5, y: -1.5}}
  m_UseGraphicAlpha: 1"""))


def walk(n, father, chunks):
    chunks.append(emit_go(n))
    chunks.append(emit_rt(n, father))
    for _, yaml in n.components:
        chunks.append(yaml)
    for c in n.children:
        walk(c, n.rt, chunks)


def main():
    root = Node("LoadingUI", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), pos=(0, 0), size=(0, 0))

    canvas_id = nid()
    scaler_id = nid()
    ray_id = nid()
    ui_id = nid()
    root.components.append((canvas_id, f"""--- !u!223 &{canvas_id}
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root.go}}}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
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
  m_SortingOrder: 9999
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

    bg = root.add(Node("Bg", amin=(0, 0), amax=(1, 1), pivot=(0.5, 0.5), pos=(0, 0), size=(0, 0)))
    add_image(bg, (1, 1, 1, 1), GUID_BG, ray=1)

    tip = root.add(Node("StoryTip", amin=(0.5, 0), amax=(0.5, 0), pivot=(0.5, 0.5), pos=(0, 280), size=(640, 100)))
    add_text(tip, "\\u52A0\\u8F7D\\u4E2D\\u2026", 26, (1, 0.96, 0.88, 0.95), 4, GUID_CN, wrap=True)
    add_outline(tip)

    corner = root.add(Node("ProgressCorner", amin=(1, 0), amax=(1, 0), pivot=(1, 0), pos=(-36, 48), size=(280, 40)))
    lab = corner.add(Node("Label", amin=(0, 0), amax=(0.55, 1), pivot=(0.5, 0.5), pos=(0, 0), size=(0, 0)))
    add_text(lab, "\\u52A0\\u8F7D\\u4E2D", 24, (1, 1, 1, 1), 3, GUID_CN)
    pct = corner.add(Node("Percent", amin=(0.55, 0), amax=(1, 1), pivot=(0.5, 0.5), pos=(0, 0), size=(0, 0)))
    add_text(pct, "0%", 26, (1, 1, 1, 1), 5, GUID_NUM)

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
  backgroundImage: {{fileID: {bg.refs["image"]}}}
  tipText: {{fileID: {tip.refs["text"]}}}
  labelText: {{fileID: {lab.refs["text"]}}}
  percentText: {{fileID: {pct.refs["text"]}}}"""))

    chunks = ["%YAML 1.1", "%TAG !u! tag:yousandi.cn,2023:"]
    walk(root, 0, chunks)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(chunks) + "\n", encoding="utf-8")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
