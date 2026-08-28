/// <summary>
/// 武器挂点部位（与 EquipSlotType 护甲槽区分）：
/// - MainHand：主手武器（攻击手，替换剑/弓/杖主手位）
/// - OffHand：副手（盾，或标注为副手的剑→狂战双持）
/// 双手武器（WeaponType.TwoHand）不看本字段，替换时清空主+副整套。
/// </summary>
public enum WeaponHandSlot
{
    None = 0,
    MainHand = 1,
    OffHand = 2,
}
