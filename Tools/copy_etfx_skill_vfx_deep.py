# -*- coding: utf-8 -*-
"""Deep-copy ETFX skill VFX + dependencies into Resources/VFX/Skills."""
import os
import re
import uuid

PROJECT = r"Y:\PixelAdventureTown"
ASSETS = os.path.join(PROJECT, "Assets")
ETFX_ROOT = os.path.join(ASSETS, "Art", "Effects", "Epic Toon FX")
ETFX_PREFABS = os.path.join(ETFX_ROOT, "Prefabs")
SKILLS_ROOT = os.path.join(ASSETS, "Resources", "VFX", "Skills")
DEPS_ROOT = os.path.join(SKILLS_ROOT, "_Deps")

MAP = {
    "Ally/ally_heal": "Combat/Magic/Nova/MagicNovaGreen.prefab",
    "Ally/ally_atk_up": "Combat/Magic/Buff/MagicBuffYellow.prefab",
    "Ally/ally_atk_speed": "Combat/Magic/Charge/MagicChargeYellow.prefab",
    "Ally/ally_crit_up": "Combat/Magic/Enchant/MagicEnchantYellow.prefab",
    "Ally/ally_thunder": "Combat/Explosions/LightningExplosion/LightningExplosionYellow.prefab",
    "Ally/ally_shield": "Combat/Magic/Shield/ShieldYellow.prefab",
    "Monster/mon_slam_multi": "Combat/Brawling/Soft/SoftBodySlam.prefab",
    "Monster/mon_magic_burst": "Combat/Explosions/MagicExplosion/MagicExplosionPink.prefab",
    "Merc/SK001": "Combat/Brawling/Soft/SoftPunchMedium.prefab",
    "Merc/SK003": "Combat/Brawling/Soft/SoftFightAction2.prefab",
    "Merc/SK005": "Combat/Explosions/FireballExplosion/ExplosionFireballFire.prefab",
    "Merc/SK007": "Combat/Magic/Aura/MagicAuraBlue.prefab",
    "Merc/SK008": "Combat/Magic/Shield/ShieldYellow.prefab",
    "Merc/SK010": "Combat/Magic/Field/MagicFieldWhite.prefab",
    "Merc/SK011": "Combat/Magic/Nova/MagicNovaGreen.prefab",
    "Merc/SK013": "Combat/Explosions/MagicSoftExplosion/MagicSoftExplosionYellow.prefab",
    "Merc/SK015": "Combat/Magic/Field/MagicFieldGreen.prefab",
    "Merc/SK016": "Combat/Explosions/MagicExplosion/MagicExplosionBlue.prefab",
    "Merc/SK018": "Combat/Brawling/StunnedCirclingStars.prefab",
    "Merc/SK020": "Combat/Explosions/SoulExplosion/SoulExplosionPurple.prefab",
    "Merc/SK002": "Combat/Blood/Red/BloodSplatDirectional.prefab",
    "Merc/SK004": "Combat/Magic/Buff/MagicBuffYellow.prefab",
    "Merc/SK006": "Combat/Explosions/- Misc/MetalHit.prefab",
    "Merc/SK009": "Combat/Magic/Shield/ShieldBlue.prefab",
    "Merc/SK012": "Combat/Magic/Aura/MagicAuraGreen.prefab",
    "Merc/SK014": "Combat/Magic/Enchant/MagicEnchantGreen.prefab",
    "Merc/SK017": "Combat/Magic/Charge/MagicChargeBlue.prefab",
    "Merc/SK019": "Combat/Magic/Sphere/MagicSphereYellow.prefab",
}

GUID_LINE_RE = re.compile(r"^guid: ([a-f0-9]{32})\s*$", re.M | re.I)
GUID_REF_RE = re.compile(r"guid: ([a-f0-9]{32})", re.I)
BUILTIN_GUIDS = {
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
}

PREFAB_META = """fileFormatVersion: 2
guid: {guid}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def build_guid_index():
    guid_to_path = {}
    path_to_guid = {}
    for root, _, files in os.walk(ASSETS):
        for name in files:
            if not name.endswith(".meta"):
                continue
            asset_path = os.path.join(root, name[:-5])
            try:
                text = open(os.path.join(root, name), "r", encoding="utf-8").read()
            except Exception:
                continue
            m = GUID_LINE_RE.search(text)
            if not m:
                continue
            g = m.group(1).lower()
            guid_to_path[g] = asset_path
            path_to_guid[asset_path.replace("\\", "/")] = g
    return guid_to_path, path_to_guid


def read_text(path):
    return open(path, "r", encoding="utf-8").read()


def write_text(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def write_bytes(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def collect_guids_from_file(path, guid_to_path):
    if not os.path.isfile(path):
        return set()
    try:
        text = read_text(path)
    except Exception:
        data = open(path, "rb").read()
        try:
            text = data.decode("utf-8")
        except Exception:
            return set()
    found = set(GUID_REF_RE.findall(text))
    found -= BUILTIN_GUIDS
    queue = list(found)
    seen = set(found)
    while queue:
        g = queue.pop()
        p = guid_to_path.get(g.lower())
        if not p or not os.path.isfile(p):
            continue
        if not p.replace("\\", "/").startswith(ETFX_ROOT.replace("\\", "/")):
            continue
        try:
            t = read_text(p)
        except Exception:
            continue
        for ng in GUID_REF_RE.findall(t):
            if ng in BUILTIN_GUIDS:
                continue
            gl = ng.lower()
            if gl not in seen:
                seen.add(gl)
                queue.append(gl)
    return seen


def rel_under_etfx(asset_path):
    norm = asset_path.replace("\\", "/")
    root = ETFX_ROOT.replace("\\", "/")
    if not norm.startswith(root + "/"):
        return None
    return norm[len(root) + 1:]


def remap_text(text, guid_map):
    def repl(m):
        old = m.group(1).lower()
        return "guid: " + guid_map.get(old, m.group(1))
    return GUID_REF_RE.sub(repl, text)


def ensure_folder_meta(folder):
    meta = folder + ".meta"
    if os.path.isfile(meta):
        return
    write_text(meta, FOLDER_META.format(guid=uuid.uuid4().hex))


def copy_dep(asset_path, guid_to_path, path_to_guid, guid_map, copied_paths):
    rel = rel_under_etfx(asset_path)
    if not rel:
        return
    if rel.lower().startswith("scripts/") or asset_path.lower().endswith(".cs"):
        return
    if rel in copied_paths:
        return
    copied_paths.add(rel)
    dst = os.path.join(DEPS_ROOT, rel.replace("/", os.sep))
    src_meta = asset_path + ".meta"
    if not os.path.isfile(src_meta):
        return

    old_guid = path_to_guid_for(asset_path, path_to_guid)
    if not old_guid:
        return
    if old_guid not in guid_map:
        guid_map[old_guid] = uuid.uuid4().hex

    if os.path.isfile(asset_path):
        write_bytes(dst, open(asset_path, "rb").read())
    meta_text = read_text(src_meta)
    meta_text = remap_text(meta_text, guid_map)
    meta_text = GUID_LINE_RE.sub("guid: " + guid_map[old_guid], meta_text, count=1)
    write_text(dst + ".meta", meta_text)

    # recurse nested refs inside this asset
    for g in collect_guids_from_file(asset_path, guid_to_path):
        p = guid_to_path.get(g)
        if p:
            copy_dep(p, guid_to_path, path_to_guid, guid_map, copied_paths)


def path_to_guid_for(asset_path, path_to_guid):
    norm = asset_path.replace("\\", "/")
    if norm in path_to_guid:
        return path_to_guid[norm]
    for p, g in path_to_guid.items():
        if os.path.normcase(p) == os.path.normcase(norm):
            return g
    return None


def main():
    guid_to_path, path_to_guid = build_guid_index()
    guid_map = {}
    copied_paths = set()

    # gather all deps for all skills
    all_guids = set()
    for src_rel in MAP.values():
        src = os.path.join(ETFX_PREFABS, src_rel.replace("/", os.sep))
        all_guids |= collect_guids_from_file(src, guid_to_path)

    for g in sorted(all_guids):
        p = guid_to_path.get(g)
        if p:
            copy_dep(p, guid_to_path, path_to_guid, guid_map, copied_paths)

    ensure_folder_meta(DEPS_ROOT)

    lines = ["skillId,etfxSource,resourcesPath,depsRoot"]
    ok = 0
    for key, src_rel in MAP.items():
        folder, sid = key.split("/", 1)
        src = os.path.join(ETFX_PREFABS, src_rel.replace("/", os.sep))
        if not os.path.isfile(src):
            print("MISSING", src)
            continue

        out_dir = os.path.join(SKILLS_ROOT, folder)
        ensure_folder_meta(out_dir)
        dst = os.path.join(out_dir, sid + ".prefab")
        dst_meta = dst + ".meta"

        old_prefab_guid = path_to_guid_for(src, path_to_guid)
        if old_prefab_guid and old_prefab_guid not in guid_map:
            guid_map[old_prefab_guid] = uuid.uuid4().hex

        text = read_text(src)
        text = remap_text(text, guid_map)
        text, _ = re.subn(r"(m_Name: ).*", r"\1" + sid, text, count=1)
        write_text(dst, text)

        prefab_guid = guid_map.get(old_prefab_guid, uuid.uuid4().hex)
        if os.path.isfile(dst_meta):
            m = GUID_LINE_RE.search(read_text(dst_meta))
            if m:
                prefab_guid = m.group(1)
        write_text(dst_meta, PREFAB_META.format(guid=prefab_guid))

        lines.append("%s,%s,VFX/Skills/%s/%s,VFX/Skills/_Deps" % (sid, src_rel, folder, sid))
        ok += 1
        print("OK", key)

    map_path = os.path.join(SKILLS_ROOT, "etfx_skill_vfx_map.csv")
    write_text(map_path, "\n".join(lines) + "\n")
    print("deps", len(copied_paths), "guid remap", len(guid_map), "skills", ok)


if __name__ == "__main__":
    main()
