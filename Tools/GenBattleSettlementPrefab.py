#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate BattleSettlement.prefab"""
from __future__ import annotations
import hashlib
import os

GUID_IMG = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_BTN = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_FONT = "b7c8d9e0f1a2435566778899aabbcc01"
GUID_UI = "f1a2b3c4d5e64789a0b1c2d3e4f5a6b7"  # BattleSettlementUI.cs.meta

_nid = 700000


def nid():
    global _nid
    _nid += 1
    return _nid


def esc(s: str) -> str:
    return "".join(f"\\u{ord(c):04X}" for c in s)


class LNode:
    def __init__(self, name, amin=(0, 0), amax=(1, 1), apos=(0, 0), size=(0, 0), pivot=(0.5, 0.5)):
        self.name = name
        self.go = nid()
        self.rt = nid()
        self.children = []
        self.comps = []
        self.amin, self.amax, self.apos, self.size, self.pivot = amin, amax, apos, size, pivot

    def add(self, c):
        self.children.append(c)
        return c

    def image(self, r, g, b, a, raycast=1):
        cid = nid()
        self.comps.append(("img", cid, f"""--- !u!114 &{cid}
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
"""))
        return cid

    def text(self, content, size, align=4):
        cid = nid()
        self.comps.append(("txt", cid, f"""--- !u!114 &{cid}
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
"""))
        return cid

    def button(self):
        cid = nid()
        self.comps.append(("btn", cid, f"""--- !u!114 &{cid}
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
"""))
        return cid


def emit(n: LNode, father: int) -> str:
    comps = [f"  - component: {{fileID: {n.rt}}}"]
    cr = None
    if any(k in ("img", "txt") for k, _, _ in n.comps):
        cr = nid()
        comps.append(f"  - component: {{fileID: {cr}}}")
    for _, cid, _ in n.comps:
        comps.append(f"  - component: {{fileID: {cid}}}")
    kids = "\n".join(f"  - {{fileID: {c.rt}}}" for c in n.children)
    kids = ("\n" + kids) if kids else " []"
    out = f"""--- !u!1 &{n.go}
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
--- !u!224 &{n.rt}
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
  m_Children:{kids}
  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {n.amin[0]}, y: {n.amin[1]}}}
  m_AnchorMax: {{x: {n.amax[0]}, y: {n.amax[1]}}}
  m_AnchoredPosition: {{x: {n.apos[0]}, y: {n.apos[1]}}}
  m_SizeDelta: {{x: {n.size[0]}, y: {n.size[1]}}}
  m_Pivot: {{x: {n.pivot[0]}, y: {n.pivot[1]}}}
"""
    if cr is not None:
        out += f"""--- !u!222 &{cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {n.go}}}
  m_CullTransparentMesh: 1
"""
    for _, _, body in n.comps:
        out += body
    for c in n.children:
        out += emit(c, n.rt)
    return out


def main():
    root = LNode("Root")
    dim = LNode("Dim")
    dim.image(0, 0, 0, 0.72)
    root.add(dim)
    panel = LNode("Panel", (0.5, 0.5), (0.5, 0.5), (0, 0), (600, 900))
    panel_img = panel.image(0.12, 0.1, 0.14, 0.98)
    root.add(panel)
    title = LNode("Title", (0.5, 1), (0.5, 1), (0, -40), (540, 48), (0.5, 1))
    title_id = title.text("撤离成功", 36, 4)
    panel.add(title)
    sub = LNode("Subtitle", (0.5, 1), (0.5, 1), (0, -92), (540, 36), (0.5, 1))
    sub_id = sub.text("本局成果", 22, 4)
    panel.add(sub)
    stats = LNode("Stats", (0.5, 1), (0.5, 1), (0, -140), (520, 320), (0.5, 1))
    stats_id = stats.text("统计", 22, 0)
    panel.add(stats)
    rewards = LNode("Rewards", (0.5, 1), (0.5, 1), (0, -480), (520, 220), (0.5, 1))
    rewards_id = rewards.text("获得", 22, 0)
    panel.add(rewards)
    btn = LNode("ConfirmButton", (0.5, 0), (0.5, 0), (0, 36), (280, 56), (0.5, 0))
    btn.image(0.32, 0.48, 0.28, 1)
    btn_id = btn.button()
    panel.add(btn)
    lbl = LNode("Label")
    lbl.text("返回城镇", 26, 4)
    btn.add(lbl)

    host_go, host_rt = nid(), nid()
    canvas_id, scaler_id, ray_id, script_id = nid(), nid(), nid(), nid()
    fields = f"""  root: {{fileID: {root.go}}}
  titleText: {{fileID: {title_id}}}
  subtitleText: {{fileID: {sub_id}}}
  statsText: {{fileID: {stats_id}}}
  rewardsText: {{fileID: {rewards_id}}}
  confirmButton: {{fileID: {btn_id}}}
  confirmLabel: {{fileID: 0}}
  panel: {{fileID: {panel_img}}}
"""
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
  m_Name: BattleSettlement
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
  m_SortingOrder: 990
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
  m_Script: {{fileID: 11500000, guid: {GUID_UI}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
{fields}
"""
    path = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Battle\BattleSettlement.prefab"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(header + emit(root, host_rt))
    g = hashlib.md5(path.encode()).hexdigest()
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(f"""fileFormatVersion: 2
guid: {g}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""")
    print("Wrote", path)


if __name__ == "__main__":
    main()
