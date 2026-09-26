using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>인벤토리와 무기/방어구 슬롯. 착용 조건(RequiredLevel, RequiredClass)을 검사합니다.</summary>
public class EquipmentManager : MonoBehaviour
{
    public ClassPromotion player;
    public EquipmentItem weapon;
    public EquipmentItem armor;
    public List<EquipmentItem> inventory = new List<EquipmentItem>();
    public int inventorySize = 40;

    public event Action<string> OnWarning;          // UI 토스트에 연결
    public event Action<EquipmentItem> OnEquipped;

    public bool AddToInventory(EquipmentItem item)
    {
        if (item == null) return false;
        if (inventory.Count >= inventorySize) { Warn("가방이 가득 찼습니다."); return false; }
        inventory.Add(item);
        return true;
    }

    /// <summary>착용 시도. 조건이 안 맞으면 false와 경고 메시지.</summary>
    public bool TryEquip(EquipmentItem item, out string warning)
    {
        warning = item == null ? "장비가 없습니다." : item.GetEquipWarning(player.stats.level, player.currentClass);
        if (warning != null) { Warn(warning); return false; }

        inventory.Remove(item);
        EquipmentItem old = item.ItemType == ItemType.Weapon ? weapon : armor;
        if (old != null) inventory.Add(old);
        if (item.ItemType == ItemType.Weapon) weapon = item; else armor = item;
        OnEquipped?.Invoke(item);
        return true;
    }

    public int TotalAttack { get { return weapon != null ? weapon.attack : 0; } }
    public int TotalDefense { get { return armor != null ? armor.defense : 0; } }

    void Warn(string msg) { Debug.LogWarning("[장착 불가] " + msg); OnWarning?.Invoke(msg); }
}
