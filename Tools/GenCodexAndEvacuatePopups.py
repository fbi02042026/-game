#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate CodexInfoPopup + EvacuateConfirmPopup prefabs (placeholder art)."""
from __future__ import annotations

import os

# Built-in Unity GUIDs used across this project
GUID_IMG = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_BTN = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_FONT = "b7c8d9e0f1a2435566778899aabbcc01"
GUID_CODEX = "b1c2d3e4f5a64789b0c1d2e3f4a5b6c7"
GUID_EVAC = "c2d3e4f5a6b74890c1d2e3f4a5b6c7d8"

_nid = 900000


def nid() -> int:
    global _nid
    _nid += 1
    return _nid


def esc(s: str) -> str:
    return "".join(f"\\u{ord(c):04X}" for c in s)


class Node:
    def __init__(self, name: str):
        self.name = name
        self.go = nid()
        self.rt = nid()
        self.children: list[Node] = []
        self.comps: list[tuple[str, int, str]] = []  # kind, id, body

    def add(self, child: "Node") -> "Node":
        self.children.append(child)
        return child

    def image(self, r, g, b, a, raycast=1) -> int:
        cid = nid()
        self.comps.append(
            (
                "img",
                cid,
                f"""--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {self.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_IMG}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: {r}, g: {g}, b: {b}, a: {a}}}
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
  m_PixelsPerUnitMultiplier: 1
""",
            )
        )
        return cid

    def text(self, content: str, size: int, align: int = 4) -> int:
        cid = nid()
        self.comps.append(
            (
                "txt",
                cid,
                f"""--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {self.go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_TXT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {GUID_FONT}, type: 3}}
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
  m_Text: "{esc(content)}"
""",
            )
        )
        return cid

    def button(self) -> int:
        cid = nid()
        self.comps.append(
            (
                "btn",
                cid,
                f"""--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {self.go}}}
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
  m_TargetGraphic: {{fileID: 0}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
""",
            )
        )
        return cid


def emit_node(n: Node, father: int, amin, amax, apos, size, pivot=(0.5, 0.5)) -> list[str]:
    lines: list[str] = []
    comps = [f"  - component: {{fileID: {n.rt}}}"]
    # canvas renderer if has graphic
    cr = None
    if any(k in ("img", "txt") for k, _, _ in n.comps):
        cr = nid()
        comps.append(f"  - component: {{fileID: {cr}}}")
    for _, cid, _ in n.comps:
        comps.append(f"  - component: {{fileID: {cid}}}")

    lines.append(
        f"""--- !u!1 &{n.go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 7
  m_Component:
{chr(10).join(comps)}
  m_Layer: 0
  m_HasEditorInfo: 1
  m_Name: {n.name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
    )
    kids = "\n".join(f"  - {{fileID: {c.rt}}}" for c in n.children)
    if kids:
        kids = "\n" + kids
    lines.append(
        f"""--- !u!224 &{n.rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n.go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{kids if kids else " []"}
  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {amin[0]}, y: {amin[1]}}}
  m_AnchorMax: {{x: {amax[0]}, y: {amax[1]}}}
  m_AnchoredPosition: {{x: {apos[0]}, y: {apos[1]}}}
  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}
  m_Pivot: {{x: {pivot[0]}, y: {pivot[1]}}}
"""
    )
    if cr is not None:
        lines.append(
            f"""--- !u!222 &{cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n.go}}}
  m_CullTransparentMesh: 1
"""
        )
    for _, _, body in n.comps:
        lines.append(body)
    return lines


def write_prefab(path: str, host_name: str, script_guid: str, sorting: int, build_fn, field_map: dict):
    global _nid
    _nid = 900000 if "Codex" in host_name else 800000

    host_go = nid()
    host_rt = nid()
    canvas_id = nid()
    scaler_id = nid()
    ray_id = nid()
    script_id = nid()

    root = Node("Root")
    refs = build_fn(root)

    # emit children recursively with layout
    chunks: list[str] = []

    def walk(n: Node, father: int, layout):
        chunks.extend(emit_node(n, father, *layout))
        for child, clayout in zip(n.children, layout_children.get(n, [])):
            walk(child, n.rt, clayout)

    # We need a different approach - attach layout when creating nodes
    # Rebuild with explicit layouts stored on nodes
    pass


# Simpler: build flat tree with layout attrs on Node
class LNode(Node):
    def __init__(self, name, amin=(0, 0), amax=(1, 1), apos=(0, 0), size=(0, 0), pivot=(0.5, 0.5)):
        super().__init__(name)
        self.amin = amin
        self.amax = amax
        self.apos = apos
        self.size = size
        self.pivot = pivot


def emit_tree(n: LNode, father: int) -> list[str]:
    out = emit_node(n, father, n.amin, n.amax, n.apos, n.size, n.pivot)
    for c in n.children:
        out.extend(emit_tree(c, n.rt))  # type: ignore
    return out


def build_codex():
    root = LNode("Root")
    dim = LNode("Dim")
    dim.image(0, 0, 0, 0.55)
    dim.button()
    root.add(dim)

    panel = LNode("Panel", (0.5, 0.5), (0.5, 0.5), (0, 0), (560, 720))
    panel.image(0.16, 0.12, 0.1, 0.98)
    root.add(panel)

    frame = LNode("PortraitFrame", (0.5, 1), (0.5, 1), (0, -36), (220, 220), (0.5, 1))
    frame.image(0.3, 0.22, 0.16, 1)
    panel.add(frame)

    portrait = LNode("Portrait")
    portrait.image(1, 1, 1, 0.2, raycast=0)
    frame.add(portrait)

    title = LNode("Title", (0.5, 1), (0.5, 1), (0, -280), (500, 44), (0.5, 1))
    title_id = title.text("名称", 32, 4)
    panel.add(title)

    meta = LNode("Meta", (0.5, 1), (0.5, 1), (0, -330), (500, 36), (0.5, 1))
    meta_id = meta.text("类型 · 地点", 20, 4)
    panel.add(meta)

    desc = LNode("Desc", (0.5, 1), (0.5, 1), (0, -360), (500, 140), (0.5, 1))
    desc_id = desc.text("描述", 22, 0)
    panel.add(desc)

    lore = LNode("Lore", (0.5, 1), (0.5, 1), (0, -520), (500, 100), (0.5, 1))
    lore_id = lore.text("趣闻", 20, 0)
    panel.add(lore)

    close = LNode("CloseButton", (0.5, 0), (0.5, 0), (0, 28), (200, 52), (0.5, 0))
    close.image(0.42, 0.3, 0.18, 1)
    close_btn = close.button()
    panel.add(close)

    close_lbl = LNode("Label")
    close_lbl.text("关闭", 24, 4)
    close.add(close_lbl)

    panel_img = next(c for k, c, _ in panel.comps if k == "img")
    portrait_img = next(c for k, c, _ in portrait.comps if k == "img")
    dim_btn = next(c for k, c, _ in dim.comps if k == "btn")

    return root, {
        "root": root.go,
        "panel": panel_img,
        "portrait": portrait_img,
        "titleText": title_id,
        "metaText": meta_id,
        "descText": desc_id,
        "loreText": lore_id,
        "closeButton": close_btn,
        "dimButton": dim_btn,
    }


def build_evac():
    root = LNode("Root")
    dim = LNode("Dim")
    dim.image(0, 0, 0, 0.6)
    dim.button()
    root.add(dim)

    panel = LNode("Panel", (0.5, 0.5), (0.5, 0.5), (0, 0), (520, 360))
    panel.image(0.14, 0.11, 0.16, 0.98)
    root.add(panel)

    title = LNode("Title", (0.5, 1), (0.5, 1), (0, -36), (460, 44), (0.5, 1))
    title_id = title.text("确认撤离？", 30, 4)
    panel.add(title)

    body = LNode("Body", (0.5, 1), (0.5, 1), (0, -100), (460, 120), (0.5, 1))
    body_id = body.text("撤离将结束本次裂缝探索。", 22, 1)
    panel.add(body)

    evac = LNode("EvacuateButton", (0.28, 0), (0.28, 0), (0, 28), (180, 52), (0.5, 0))
    evac.image(0.55, 0.28, 0.22, 1)
    evac_btn = evac.button()
    panel.add(evac)
    el = LNode("Label")
    el.text("撤离", 24, 4)
    evac.add(el)

    cont = LNode("ContinueButton", (0.72, 0), (0.72, 0), (0, 28), (180, 52), (0.5, 0))
    cont.image(0.28, 0.42, 0.3, 1)
    cont_btn = cont.button()
    panel.add(cont)
    cl = LNode("Label")
    cl.text("继续战斗", 24, 4)
    cont.add(cl)

    dim_btn = next(c for k, c, _ in dim.comps if k == "btn")
    return root, {
        "root": root.go,
        "titleText": title_id,
        "bodyText": body_id,
        "evacuateButton": evac_btn,
        "continueButton": cont_btn,
        "dimButton": dim_btn,
    }


def save(path: str, host_name: str, script_guid: str, sorting: int, root: LNode, fields: dict):
    host_go = nid()
    host_rt = nid()
    canvas_id = nid()
    scaler_id = nid()
    ray_id = nid()
    script_id = nid()

    field_lines = "\n".join(f"  {k}: {{fileID: {v}}}" for k, v in fields.items())

    header = f"""%YAML 1.1
%TAG !u! tag:yousandi.cn,2023:
--- !u!1 &{host_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 7
  m_Component:
  - component: {{fileID: {host_rt}}}
  - component: {{fileID: {canvas_id}}}
  - component: {{fileID: {scaler_id}}}
  - component: {{fileID: {ray_id}}}
  - component: {{fileID: {script_id}}}
  m_Layer: 0
  m_HasEditorInfo: 1
  m_Name: {host_name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{host_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {host_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0, y: 0, z: 0}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {root.rt}}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0, y: 0}}
--- !u!223 &{canvas_id}
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {host_go}}}
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
  m_SortingOrder: {sorting}
  m_TargetDisplay: 0
--- !u!114 &{scaler_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {host_go}}}
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
  m_PresetInfoIsWorld: 0
--- !u!114 &{ray_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {host_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_RAY}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295
--- !u!114 &{script_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {host_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
{field_lines}
"""
    body = "".join(emit_tree(root, host_rt))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(header + body)
    meta = path + ".meta"
    # stable hex guid for prefab asset
    import hashlib

    g = hashlib.md5(path.encode()).hexdigest()
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(
            f"""fileFormatVersion: 2
guid: {g}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
        )
    print("Wrote", path)


def main():
    root, fields = build_codex()
    save(
        r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Town\CodexInfoPopup.prefab",
        "CodexInfoPopup",
        GUID_CODEX,
        960,
        root,
        fields,
    )
    root2, fields2 = build_evac()
    save(
        r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Battle\EvacuateConfirmPopup.prefab",
        "EvacuateConfirmPopup",
        GUID_EVAC,
        980,
        root2,
        fields2,
    )


if __name__ == "__main__":
    main()
