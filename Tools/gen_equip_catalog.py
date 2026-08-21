import os
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
icon_dir = os.path.join(ROOT, 'Assets', 'Art', 'UI', 'Icons', 'EquipIcons')
icons = sorted([f[:-4] for f in os.listdir(icon_dir) if f.endswith('.png')])

def infer(name):
    n = name
    lower = n.lower()
    if n.startswith('New_Helmet_') or n.startswith('Helmet_') or n.startswith('Normal_Helmet') or n == 'F_SR_Helmet':
        return 'Head', 1, 1, 'None'
    if n.startswith('New_Armor_') or n.startswith('Armor_') or n.startswith('Normal_Armor'):
        return 'Chest', 2, 1, 'None'
    if n.startswith('New_Pant_') or n == 'F_SR_Pants':
        return 'Feet', 2, 1, 'None'
    if n.startswith('Foot_'):
        return 'Feet', 1, 1, 'None'
    if (n.startswith('Cloth_') and not n.startswith('New_Cloth')) or n == 'F_SR_Cloth':
        return 'Cape', 1, 1, 'None'
    if n.startswith('New_Cloth_') or n.startswith('Normal_Cloth'):
        return 'Hands', 1, 1, 'None'
    if 'shield' in lower or n.startswith('New_Shield_') or n.startswith('Shield_'):
        return 'OffHand', 2, 2, 'None'
    if n.startswith('Bow_'):
        return 'MainHand', 2, 2, 'TwoHand'
    if n.startswith('Spear_') or n == 'Soon_Spear' or 'axelong' in lower or n == 'F_SR_Hammer':
        return 'MainHand', 2, 3, 'TwoHand'
    if n in ('New_Weapon_06', 'New_Weapon_07', 'New_Weapon_18', 'New_Weapon_19', 'New_Weapon_20'):
        return 'MainHand', 2, 3, 'TwoHand'
    if n.startswith('New_Weapon_') or n.startswith('New_weapon_') or n.startswith('Sword_') or lower.startswith('axe'):
        return 'MainHand', 1, 2, 'OneHand'
    return 'Hands', 1, 1, 'None'

lines = [
    '# 装备占格对照表',
    '',
    '图标目录：`Assets/Art/UI/Icons/EquipIcons/`',
    '',
    '**用法**：在「确认宽」「确认高」列填入最终占格（留空=用建议值）。修改后 Unity 菜单 **Tools/装备/从对照表生成装备模板**。',
    '',
    '| 图标文件名 | 模板ID | 槽位 | 建议宽 | 建议高 | **确认宽** | **确认高** | 武器类型 | 备注 |',
    '|-----------|--------|------|--------|--------|-----------|-----------|----------|------|',
]
for name in icons:
    slot, w, h, wt = infer(name)
    tid = 'equip_' + name.lower().replace(' ', '_')
    note = '短武器默认1×2' if wt == 'OneHand' else ('双手/长柄默认2×3' if wt == 'TwoHand' and h == 3 else '')
    lines.append(f'| {name} | {tid} | {slot} | {w} | {h} |  |  | {wt} | {note} |')

out = os.path.join(ROOT, 'Docs', '装备占格对照表.md')
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))
print(len(icons), out)
