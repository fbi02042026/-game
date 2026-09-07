# -*- coding: utf-8 -*-
import os
import random

random.seed(20260907)

def nid():
    return random.randint(10**17, 9 * 10**17)

OUT = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\UI\PlayerJobSelect.prefab"
META = OUT + ".meta"
os.makedirs(os.path.dirname(OUT), exist_ok=True)

GUID_IMAGE = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TEXT = "5f7201a12d95ffc409449d95f23cf332"
GUID_BUTTON = "4e29b1a8efbd4b44bb3f3716e73f07ff"
GUID_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_RAY = "dc42784cf147c0c48a680349fa168899"
GUID_JOB = "D35K4HyuVCgxWN6mGGx7KaPICI8GpB7xxsmlQUkXz70jW0QLesgtqtQ="

objs = {}


def add_go(key, name):
    go = nid()
    rt = nid()
    objs[key] = {"go": go, "rt": rt, "kids": [], "comps": [rt], "name": name}
    return objs[key]


def add_image(o, color):
    cr = nid()
    img = nid()
    o["comps"] += [cr, img]
    o["cr"] = cr
    o["img"] = img
    o["color"] = color


def add_text(o, text, size=22, color=(1, 1, 1, 1), align=4):
    cr = nid()
    tx = nid()
    o["comps"] += [cr, tx]
    o["cr"] = cr
    o["tx"] = tx
    o["text"] = text
    o["size"] = size
    o["tcolor"] = color
    o["align"] = align


def add_button(o):
    btn = nid()
    o["comps"] += [btn]
    o["btn"] = btn


root = add_go("root", "PlayerJobSelect")
root_job = nid()
root_canvas = nid()
root_scaler = nid()
root_ray = nid()
root["comps"] += [root_job, root_canvas, root_scaler, root_ray]
root["job"] = root_job
root["canvas"] = root_canvas
root["scaler"] = root_scaler
root["ray"] = root_ray

dim = add_go("dim", "Dim")
add_image(dim, (0, 0, 0, 0.72))
panel = add_go("panel", "Panel")
add_image(panel, (0.78, 0.68, 0.48, 0.98))
title = add_go("title", "Title")
add_text(title, "选择职业", 36, (0.35, 0.22, 0.08, 1), 4)
hint = add_go("hint", "Hint")
add_text(hint, "请选择你想成为的职业，踏入裂隙，迎接挑战！", 18, (0.35, 0.28, 0.18, 1), 4)
row = add_go("row", "CardRow")

cards = []
for i in range(3):
    c = add_go("card%d" % i, "JobCard%d" % i)
    add_image(c, (0.92, 0.86, 0.72, 1))
    add_button(c)
    fr = add_go("frame%d" % i, "Frame")
    add_image(fr, (0.45, 0.32, 0.18, 0.35))
    ic = add_go("icon%d" % i, "Icon")
    add_image(ic, (0.55, 0.5, 0.42, 1))
    nm = add_go("name%d" % i, "Name")
    add_text(nm, "职业", 24, (0.2, 0.12, 0.06, 1), 4)
    st = add_go("stats%d" % i, "Stats")
    add_text(st, "血量 中\n攻击 中\n难度 中", 16, (0.25, 0.18, 0.1, 1), 0)
    ds = add_go("desc%d" % i, "Desc")
    add_text(ds, "描述", 15, (0.28, 0.22, 0.14, 1), 1)
    rtg = add_go("rating%d" % i, "Rating")
    add_text(rtg, "◆◇◇", 20, (0.75, 0.45, 0.12, 1), 4)
    c["kids"] = [fr, ic, nm, st, ds, rtg]
    cards.append(c)
    row["kids"].append(c)

refresh = add_go("refresh", "RefreshBtn")
add_image(refresh, (0.4, 0.32, 0.5, 1))
add_button(refresh)
rl = add_go("rl", "Label")
add_text(rl, "刷新 (1)", 22, (1, 1, 1, 1), 4)
refresh["kids"] = [rl]

enter = add_go("enter", "EnterRiftBtn")
add_image(enter, (0.45, 0.22, 0.55, 1))
add_button(enter)
el = add_go("el", "Label")
add_text(el, "进入裂隙", 28, (1, 1, 1, 1), 4)
enter["kids"] = [el]

panel["kids"] = [title, hint, row, refresh, enter]
root["kids"] = [dim, panel]

layouts = {
    "root": dict(amin=(0, 0), amax=(1, 1), pos=(0, 0), size=(0, 0), pivot=(0.5, 0.5)),
    "dim": dict(amin=(0, 0), amax=(1, 1), pos=(0, 0), size=(0, 0), pivot=(0.5, 0.5)),
    "panel": dict(amin=(0.5, 0.5), amax=(0.5, 0.5), pos=(0, 0), size=(680, 980), pivot=(0.5, 0.5)),
    "title": dict(amin=(0.5, 1), amax=(0.5, 1), pos=(0, -28), size=(600, 44), pivot=(0.5, 1)),
    "hint": dict(amin=(0.5, 1), amax=(0.5, 1), pos=(0, -78), size=(600, 48), pivot=(0.5, 1)),
    "row": dict(amin=(0.5, 0.5), amax=(0.5, 0.5), pos=(0, 40), size=(640, 520), pivot=(0.5, 0.5)),
    "refresh": dict(amin=(0.5, 0), amax=(0.5, 0), pos=(-150, 40), size=(180, 52), pivot=(0.5, 0)),
    "enter": dict(amin=(0.5, 0), amax=(0.5, 0), pos=(110, 36), size=(280, 72), pivot=(0.5, 0)),
    "rl": dict(amin=(0, 0), amax=(1, 1), pos=(0, 0), size=(0, 0), pivot=(0.5, 0.5)),
    "el": dict(amin=(0, 0), amax=(1, 1), pos=(0, 0), size=(0, 0), pivot=(0.5, 0.5)),
}
card_w, gap, start_x = 196, 14, -210
for i, c in enumerate(cards):
    layouts["card%d" % i] = dict(
        amin=(0.5, 0.5), amax=(0.5, 0.5),
        pos=(start_x + i * (card_w + gap), 0), size=(card_w, 500), pivot=(0.5, 0.5))
    layouts["frame%d" % i] = dict(amin=(0, 0), amax=(1, 1), pos=(0, 0), size=(0, 0), pivot=(0.5, 0.5))
    layouts["icon%d" % i] = dict(amin=(0.5, 1), amax=(0.5, 1), pos=(0, -24), size=(140, 140), pivot=(0.5, 1))
    layouts["name%d" % i] = dict(amin=(0.5, 1), amax=(0.5, 1), pos=(0, -176), size=(180, 36), pivot=(0.5, 1))
    layouts["stats%d" % i] = dict(amin=(0.5, 1), amax=(0.5, 1), pos=(0, -220), size=(170, 72), pivot=(0.5, 1))
    layouts["desc%d" % i] = dict(amin=(0.05, 0.12), amax=(0.95, 0.48), pos=(0, 0), size=(0, 0), pivot=(0.5, 0.5))
    layouts["rating%d" % i] = dict(amin=(0.5, 0), amax=(0.5, 0), pos=(0, 16), size=(160, 32), pivot=(0.5, 0))

fathers = {}


def walk(parent_rt, children):
    for ch in children:
        k = None
        for kk, vv in objs.items():
            if vv is ch:
                k = kk
                break
        fathers[k] = parent_rt
        walk(ch["rt"], ch.get("kids", []))


walk(0, root["kids"])
fathers["root"] = 0

lines = ["%YAML 1.1", "%TAG !u! tag:yousandi.cn,2023:"]


def layout_of(key):
    return layouts.get(key, dict(amin=(0.5, 0.5), amax=(0.5, 0.5), pos=(0, 0), size=(100, 100), pivot=(0.5, 0.5)))


for key, o in objs.items():
    L = layout_of(key)
    kids = o.get("kids", [])
    kid_lines = "".join("  - {fileID: %d}\n" % k["rt"] for k in kids)
    father = fathers.get(key, 0)
    comps = "".join("  - component: {fileID: %d}\n" % c for c in o["comps"])
    lines.append(f"""--- !u!1 &{o['go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 7
  m_Component:
{comps}  m_Layer: 5
  m_HasEditorInfo: 1
  m_Name: {o['name']}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{o['rt']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
{kid_lines}  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {L['amin'][0]}, y: {L['amin'][1]}}}
  m_AnchorMax: {{x: {L['amax'][0]}, y: {L['amax'][1]}}}
  m_AnchoredPosition: {{x: {L['pos'][0]}, y: {L['pos'][1]}}}
  m_SizeDelta: {{x: {L['size'][0]}, y: {L['size'][1]}}}
  m_Pivot: {{x: {L['pivot'][0]}, y: {L['pivot'][1]}}}
""")

    if "img" in o:
        r, g, b, a = o["color"]
        lines.append(f"""--- !u!222 &{o['cr']}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
  m_CullTransparentMesh: 1
--- !u!114 &{o['img']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_IMAGE}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: {r}, g: {g}, b: {b}, a: {a}}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
""")

    if "tx" in o:
        tr, tg, tb, ta = o["tcolor"]
        t = o["text"].replace("\\", "\\\\").replace('"', '\\"')
        lines.append(f"""--- !u!222 &{o['cr']}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
  m_CullTransparentMesh: 1
--- !u!114 &{o['tx']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_TEXT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: {tr}, g: {tg}, b: {tb}, a: {ta}}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}}
    m_FontSize: {o['size']}
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: {o['align']}
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: "{t}"
""")

    if "btn" in o:
        lines.append(f"""--- !u!114 &{o['btn']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_BUTTON}, type: 3}}
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
    m_HighlightedColor: {{r: 0.9, g: 0.9, b: 0.9, a: 1}}
    m_PressedColor: {{r: 0.8, g: 0.8, b: 0.8, a: 1}}
    m_SelectedColor: {{r: 0.9, g: 0.9, b: 0.9, a: 1}}
    m_DisabledColor: {{r: 0.5, g: 0.5, b: 0.5, a: 0.5}}
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
  m_TargetGraphic: {{fileID: {o.get('img', 0)}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
""")

o = root
lines.append(f"""--- !u!114 &{o['job']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_JOB}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
--- !u!223 &{o['canvas']}
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
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
  m_SortingOrder: 900
  m_TargetDisplay: 0
--- !u!114 &{o['scaler']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
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
--- !u!114 &{o['ray']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {o['go']}}}
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
""")

with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")

with open(META, "w", encoding="utf-8", newline="\n") as f:
    f.write("""fileFormatVersion: 2
guid: a1b2c3d4e5f64789a0b1c2d3e4f5a6b7
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""")

print("wrote", OUT, "bytes", os.path.getsize(OUT), "objs", len(objs))
