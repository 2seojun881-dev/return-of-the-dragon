using UnityEngine;

namespace Game.SkillSystem
{
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "Game System/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("=== 기본 정보 (Basic Info) ===")]
        public string skillId;             // 스킬 고유 ID (예: SKILL_MERCHANT_01)
        public string skillName;           // 스킬 이름
        [TextArea(3, 5)]
        public string description;         // 스킬 설명 (툴팁 표시용)
        public Sprite icon;                // UI 스킬 아이콘

        [Header("=== 습득 및 사용 조건 (Requirements & Costs) ===")]
        public CharacterClass requiredClass; // 착용/사용 가능 직업
        public int requiredLevel;            // 습득 요구 레벨
        public SkillType skillType;          // 스킬 형태
        public float cooldown = 1.0f;        // 재사용 대기시간 (초)
        public int mpCost = 0;               // 소모 MP
        public int goldCost = 0;             // 소모 골드 (상인 스킬 등)

        [Header("=== 수치 수량 및 계수 (Skill Values) ===")]
        public float baseValue;              // 기본 피해량 / 회복량 / 버프율
        public float scalingFactor;          // 지능/힘 등 주요 스탯 비례 계수
        public float duration;               // 버프/소환/지속 시간
        public float range;                  // 사거리
        public float areaRadius;             // 범위 스킬 장판 반지름

        [Header("=== 연출 및 이펙트 (Visual & Audio) ===")]
        public GameObject vfxPrefab;         // 시전 시 생성될 시각 이펙트 프리팹
        public AudioClip sfxClip;            // 효과음

        /// <summary>
        /// 스킬 툴팁 UI에 표시할 동적 설명 텍스트를 반환합니다.
        /// </summary>
        public string GetFormattedDescription(int casterStat)
        {
            float totalValue = baseValue + (casterStat * scalingFactor);
            string result = description
                .Replace("{value}", totalValue.ToString("F0"))
                .Replace("{duration}", duration.ToString("F1"))
                .Replace("{mpCost}", mpCost.ToString())
                .Replace("{goldCost}", goldCost.ToString());

            return result;
        }
    }
}
