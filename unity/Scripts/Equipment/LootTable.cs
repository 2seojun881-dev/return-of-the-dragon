using System.Collections.Generic;
using UnityEngine;

/// <summary>사냥터별 드롭 테이블. Create → Game System → Loot Table</summary>
[CreateAssetMenu(fileName = "NewLootTable", menuName = "Game System/Loot Table")]
public class LootTable : ScriptableObject
{
    public HuntingZone zone;
    public int minLevel = 1;
    public int maxLevel = 15;
    [Range(0f, 1f)] public float dropChance = 0.035f;   // 일반 몬스터 1마리당 장비 드롭 확률
    [Range(0f, 1f)] public float bossDropChance = 0.5f;
    public List<EquipmentItem> items = new List<EquipmentItem>();
}
