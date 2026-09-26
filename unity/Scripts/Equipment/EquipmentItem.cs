using UnityEngine;

public enum ItemType { Weapon, Armor }

// 사냥터 (몬스터가 속한 구역)
public enum HuntingZone
{
    BeastHabitat,       // 서문 · 야수 서식지 (Lv.1~15)
    VengefulGraveyard,  // 동문 · 원혼의 묘지 (Lv.15~40)
    ClockworkMaze,      // 남문 · 태엽 미로 (Lv.40~70)
    AsuraCanyon,        // 북문 · 아수라 흑야 협곡 (Lv.70~99)
    DragonbloodAltar    // 용혈의 제단 (Lv.99, 고대 드래곤 이그니스)
}

/// <summary>무기/방어구 데이터. Project 창 → Create → Game System → Equipment Item</summary>
[CreateAssetMenu(fileName = "NewEquipment", menuName = "Game System/Equipment Item")]
public class EquipmentItem : ScriptableObject
{
    [Header("=== 기본 정보 ===")]
    public string itemName;
    public ItemType ItemType;
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("=== 착용 조건 ===")]
    public int RequiredLevel = 1;
    public PlayerClass RequiredClass = PlayerClass.Novice;
    [Tooltip("켜면 모든 직업이 착용 가능 (야수 서식지 수련 장비)")]
    public bool anyClass;

    [Header("=== 성능 ===")]
    public int attack;      // 무기 공격력
    public int defense;     // 방어구 방어력
    public bool dragonblood; // 용혈 장비 (제련 1형~9형)

    public bool IsUsableBy(PlayerClass cls) { return anyClass || RequiredClass == cls; }

    /// <summary>착용 가능하면 null, 불가능하면 경고 메시지를 반환합니다.</summary>
    public string GetEquipWarning(int playerLevel, PlayerClass playerClass)
    {
        if (playerLevel < RequiredLevel)
            return $"착용 레벨이 부족합니다. (Lv.{RequiredLevel} 필요, 현재 Lv.{playerLevel})";
        if (!IsUsableBy(playerClass))
            return $"[{RequiredClass.KoreanName()}] 전용 장비라 착용할 수 없습니다. (현재 직업: {playerClass.KoreanName()})";
        return null;
    }
}
