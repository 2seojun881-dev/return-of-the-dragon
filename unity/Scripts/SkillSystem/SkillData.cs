using UnityEngine;
using Game.Core;

namespace Game.SkillSystem
{
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "Game System/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("=== 기본 정보 ===")]
        public string skillId;
        public string skillName;
        [TextArea(3, 5)]
        public string description;
        public Sprite icon;

        [Header("=== 습득 및 요구 조건 ===")]
        public CharacterClass requiredClass;
        public int requiredLevel;
        public SkillType skillType;
        public float cooldown = 1.0f;
        public int mpCost = 0;
        public int goldCost = 0;

        [Header("=== 계수 및 효과 수치 ===")]
        public float baseValue;
        public float scalingFactor;
        public float duration;
        public float range;

        [Header("=== 이펙트 ===")]
        public GameObject vfxPrefab;
        public AudioClip sfxClip;

        public string GetFormattedDescription(int casterStat)
        {
            float totalValue = baseValue + (casterStat * scalingFactor);
            return description
                .Replace("{value}", totalValue.ToString("F0"))
                .Replace("{duration}", duration.ToString("F1"))
                .Replace("{mpCost}", mpCost.ToString())
                .Replace("{goldCost}", goldCost.ToString());
        }
    }
}
