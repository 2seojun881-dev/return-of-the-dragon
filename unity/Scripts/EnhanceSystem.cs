using System.Collections.Generic;
using UnityEngine;

// 강화 결과
public enum EnhanceResult
{
    Success,    // 성공
    Keep,       // 유지 (단순 실패)
    Downgrade,  // 하락 (-1단계)
    Destroyed,  // 장비 파괴
    Invalid     // 최대 단계 / 재료 부족 등으로 시도 불가
}

public enum StoneGrade { Low, Mid, High, Top }

// 한 단계(현재 → 목표)의 확률·비용 정보
[System.Serializable]
public class EnhanceRow
{
    public int currentLevel;
    public float successRate;
    public float keepRate;
    public float downgradeRate;
    public float destroyRate;
    public int goldCost;
    public StoneGrade stone;
    public int stoneCount;

    public EnhanceRow(int level, float success, float keep, float downgrade, float destroy, int gold, StoneGrade stone, int count)
    {
        currentLevel = level; successRate = success; keepRate = keep; downgradeRate = downgrade; destroyRate = destroy;
        goldCost = gold; this.stone = stone; stoneCount = count;
    }
}

[System.Serializable]
public class Equipment
{
    public string itemName;
    public int enhanceLevel;
    public bool destroyed;
}

/// <summary>
/// 대장장이 보르곤의 장비 강화 시스템.
/// 기획 테이블(+0~+15), 보호의 주문서, 장비의 파편(복구), 보르곤의 망치(마일리지)를 포함합니다.
/// 웹 프로토타입(dragon-raid/index.html)과 같은 규칙입니다.
/// </summary>
public class EnhanceSystem : MonoBehaviour
{
    public const int MaxLevel = 15;
    public const int ShardsPerDestroy = 3;   // 파괴 시 획득하는 장비의 파편
    public const int ShardsToRestore = 3;    // 0강 원본 장비 복구에 필요한 파편

    [Header("보르곤의 망치 (마일리지, 0~100)")]
    public float mileage;

    private readonly Dictionary<int, EnhanceRow> table = new Dictionary<int, EnhanceRow>();

    void Awake()
    {
        InitializeEnhanceTable();
    }

    private void InitializeEnhanceTable()
    {
        table.Clear();
        Add(0, 100, 0, 0, 0, 1000, StoneGrade.Low, 1);
        Add(1, 100, 0, 0, 0, 2000, StoneGrade.Low, 2);
        Add(2, 100, 0, 0, 0, 3000, StoneGrade.Low, 3);
        Add(3, 80, 20, 0, 0, 5000, StoneGrade.Low, 5);
        Add(4, 60, 40, 0, 0, 8000, StoneGrade.Low, 7);
        Add(5, 50, 50, 0, 0, 12000, StoneGrade.Mid, 1);
        Add(6, 40, 60, 0, 0, 18000, StoneGrade.Mid, 2);
        Add(7, 35, 45, 20, 0, 25000, StoneGrade.Mid, 4);
        Add(8, 30, 40, 30, 0, 35000, StoneGrade.Mid, 6);
        Add(9, 25, 30, 40, 5, 50000, StoneGrade.High, 1);
        Add(10, 20, 25, 45, 10, 70000, StoneGrade.High, 2);
        Add(11, 15, 20, 50, 15, 100000, StoneGrade.High, 3);
        Add(12, 10, 15, 55, 20, 150000, StoneGrade.High, 5);
        Add(13, 5, 10, 60, 25, 220000, StoneGrade.High, 7);
        Add(14, 3, 7, 60, 30, 300000, StoneGrade.Top, 1);
    }

    private void Add(int lv, float s, float k, float d, float x, int gold, StoneGrade stone, int count)
    {
        table[lv] = new EnhanceRow(lv, s, k, d, x, gold, stone, count);
    }

    public EnhanceRow GetRow(int level) => table.TryGetValue(level, out var row) ? row : null;

    /// <summary>실패 1회당 쌓이는 마일리지(%)</summary>
    public static float MileageGain(int level) => level <= 6 ? 10f : level <= 9 ? 7f : 5f;

    /// <summary>
    /// 확률만 굴려서 결과를 돌려줍니다 (재화 차감·장비 반영은 Enhance()가 담당).
    /// </summary>
    public EnhanceResult Roll(int currentLevel, bool useProtectionScroll)
    {
        if (currentLevel >= MaxLevel) return EnhanceResult.Invalid;
        EnhanceRow row = GetRow(currentLevel);
        if (row == null)
        {
            Debug.LogError($"강화 확률 테이블에 {currentLevel} 단계 정보가 없습니다!");
            return EnhanceResult.Invalid;
        }

        float keep = row.keepRate;
        float destroy = row.destroyRate;
        if (useProtectionScroll && destroy > 0f)
        {
            // 파괴 확률만큼을 '유지'로 넘긴다. 하락(-1)은 막지 못한다.
            keep += destroy;
            destroy = 0f;
        }

        // Random.value 는 [0, 1] 이므로 [0, 100) 로 맞추고 '<' 로 비교해
        // 100% 성공 구간에서 경계값 오차가 생기지 않도록 한다.
        float r = Mathf.Min(Random.value * 100f, 99.9999f);
        float acc = row.successRate;
        if (r < acc) return EnhanceResult.Success;
        acc += keep;
        if (r < acc) return EnhanceResult.Keep;
        acc += row.downgradeRate;
        if (r < acc) return EnhanceResult.Downgrade;
        acc += destroy;
        if (r < acc) return EnhanceResult.Destroyed;
        return EnhanceResult.Keep;
    }

    /// <summary>
    /// 재화 차감 → (마일리지 확정 성공 또는 확률 판정) → 장비에 결과 반영.
    /// </summary>
    public EnhanceResult Enhance(Equipment eq, PlayerWallet wallet, bool useProtectionScroll)
    {
        if (eq == null || eq.destroyed || eq.enhanceLevel >= MaxLevel) return EnhanceResult.Invalid;
        EnhanceRow row = GetRow(eq.enhanceLevel);
        if (!wallet.CanPay(row.goldCost, row.stone, row.stoneCount)) return EnhanceResult.Invalid;

        bool guaranteed = mileage >= 100f;
        bool scroll = !guaranteed && useProtectionScroll && row.destroyRate > 0f && wallet.protectionScrolls > 0;

        wallet.Pay(row.goldCost, row.stone, row.stoneCount);
        if (scroll) wallet.protectionScrolls--;

        EnhanceResult result = guaranteed ? EnhanceResult.Success : Roll(eq.enhanceLevel, scroll);
        int from = eq.enhanceLevel;

        switch (result)
        {
            case EnhanceResult.Success:
                eq.enhanceLevel++;
                if (guaranteed) mileage = 0f;
                break;
            case EnhanceResult.Keep:
                mileage = Mathf.Min(100f, mileage + MileageGain(from));
                break;
            case EnhanceResult.Downgrade:
                eq.enhanceLevel = Mathf.Max(0, eq.enhanceLevel - 1);
                mileage = Mathf.Min(100f, mileage + MileageGain(from));
                break;
            case EnhanceResult.Destroyed:
                eq.destroyed = true;
                wallet.equipmentShards += ShardsPerDestroy;
                mileage = Mathf.Min(100f, mileage + MileageGain(from));
                break;
        }
        Debug.Log($"[강화] +{from} → 결과: {result} (현재 +{eq.enhanceLevel}, 마일리지 {mileage}%)");
        return result;
    }

    /// <summary>장비의 파편으로 파괴된 장비를 0강 원본으로 복구합니다.</summary>
    public bool Restore(Equipment eq, PlayerWallet wallet)
    {
        if (eq == null || !eq.destroyed || wallet.equipmentShards < ShardsToRestore) return false;
        wallet.equipmentShards -= ShardsToRestore;
        eq.destroyed = false;
        eq.enhanceLevel = 0;
        return true;
    }
}

[System.Serializable]
public class PlayerWallet
{
    public int gold;
    public int[] stones = new int[4]; // Low, Mid, High, Top
    public int protectionScrolls;
    public int equipmentShards;

    public bool CanPay(int goldCost, StoneGrade stone, int count) => gold >= goldCost && stones[(int)stone] >= count;

    public void Pay(int goldCost, StoneGrade stone, int count)
    {
        gold -= goldCost;
        stones[(int)stone] -= count;
    }
}
