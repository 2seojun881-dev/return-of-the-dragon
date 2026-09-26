using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 사망 시 해당 사냥터의 레벨 구간에 맞는 무기·방어구 중
/// 플레이어 직업에 맞는 아이템을 확률적으로 드롭합니다.
/// </summary>
public class LootSystem : MonoBehaviour
{
    public List<LootTable> tables = new List<LootTable>();
    [Tooltip("월드에 떨어뜨릴 픽업 프리팹 (없으면 인벤토리로 바로 지급)")]
    public GameObject pickupPrefab;

    static readonly PlayerClass[] Promoted = { PlayerClass.Warrior, PlayerClass.Rogue, PlayerClass.Merchant, PlayerClass.Shaman };

    public LootTable GetTable(HuntingZone zone) { return tables.Find(t => t != null && t.zone == zone); }

    /// <param name="luckBonus">상인 '금화 수색' 패시브면 0.10 (드롭률 +10%)</param>
    public EquipmentItem DropLoot(HuntingZone zone, int monsterLevel, bool isBoss, PlayerClass playerClass, float luckBonus, Vector3 position)
    {
        LootTable table = GetTable(zone);
        if (table == null || table.items.Count == 0) return null;

        float chance = (isBoss ? table.bossDropChance : table.dropChance) * (1f + luckBonus);
        if (Random.Range(0f, 1f) >= chance) return null;

        // 초보자는 아직 직업이 없으므로 네 직업 중 하나의 장비가 나옵니다 (진급 후 사용).
        PlayerClass targetClass = playerClass == PlayerClass.Novice ? Promoted[Random.Range(0, Promoted.Length)] : playerClass;

        // 몬스터 레벨에 맞는 구간(예: 묘지 Lv.15 / Lv.30 장비) 중 직업이 맞는 것만
        var candidates = new List<EquipmentItem>();
        int bestTier = 0;
        foreach (var it in table.items)
        {
            if (it == null || !it.IsUsableBy(targetClass)) continue;
            if (it.RequiredLevel > monsterLevel + 2) continue;
            candidates.Add(it);
            bestTier = Mathf.Max(bestTier, it.RequiredLevel);
        }
        if (candidates.Count == 0) return null;

        // 높은 구간 장비 55%, 낮은 구간 45%
        bool pickHigh = Random.Range(0f, 1f) < 0.55f;
        var pool = candidates.FindAll(it => pickHigh ? it.RequiredLevel == bestTier : it.RequiredLevel < bestTier);
        if (pool.Count == 0) pool = candidates;
        EquipmentItem drop = pool[Random.Range(0, pool.Count)];

        if (pickupPrefab != null) Instantiate(pickupPrefab, position + Vector3.up * 0.5f, Quaternion.identity);
        Debug.Log($"[드롭] {drop.itemName} ({drop.ItemType}, Lv.{drop.RequiredLevel}, {(drop.anyClass ? "공용" : drop.RequiredClass.KoreanName() + " 전용")})");
        return drop;
    }
}
