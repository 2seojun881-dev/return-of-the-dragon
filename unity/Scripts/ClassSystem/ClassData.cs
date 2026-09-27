using System.Collections.Generic;
using UnityEngine;

namespace Game.ClassSystem
{
    /// <summary>
    /// 직업 데이터: 이름·아이콘·레벨 1 기본 스탯·배울 수 있는 스킬.
    /// 용의 귀환 직업: 초보자 / 전사 / 도적 / 신(치유 신관). 상인은 삭제되었습니다.
    /// Lv.99 각성: 전사 → 다크 나이트, 도적 → 네더 아사신, 신 → 그랜드 오라클 (parentClassData 로 부모 직업 참조).
    /// </summary>
    [CreateAssetMenu(fileName = "NewClassData", menuName = "Character/Class Data", order = 1)]
    public class ClassData : ScriptableObject
    {
        [Header("Class Info")]
        [Tooltip("네트워크로 보내는 짧은 ID (예: CLASS_WARRIOR, CLASS_DARK_KNIGHT). 전체 에셋 대신 이 ID만 동기화합니다.")]
        public string ClassId;
        public string ClassName; // 초보자, 전사, 도적, 신 / 다크 나이트, 네더 아사신, 그랜드 오라클
        public Sprite ClassIcon;
        [TextArea(3, 10)]
        public string ClassDescription;

        [Header("Base Stats (Level 1)")]
        [Tooltip("직업의 레벨 1 기본 스탯. 전사는 Str·MaxHp, 도적은 Dex·Crit, 신은 Int·MaxMp 위주.")]
        public List<CharacterStat> BaseStats = new List<CharacterStat>();

        [Header("Growth")]
        [Tooltip("레벨당 기본 스탯 증가율 (웹 프로토타입과 같은 0.09 = 레벨마다 +9%)")]
        public float GrowthPerLevel = 0.09f;

        [Header("Awakening Parent Class")]
        [Tooltip("각성 직업이면 원래 직업 (예: 다크 나이트 → Class_Warrior). 부모의 스킬을 그대로 물려받습니다.")]
        public ClassData parentClassData;
        [Tooltip("각성에 필요한 레벨")]
        public int AwakeningLevel = 99;
        [Tooltip("각성 시 모든 기본 스탯 배율 (1.2 = +20%)")]
        public float AwakeningStatMultiplier = 1.2f;

        [Header("Awakening Exclusive Skills")]
        public List<SkillData> awakeningSkills = new List<SkillData>();

        public bool IsAwakened => parentClassData != null;

        [Header("Available Skills")]
        [Tooltip("이 직업이 배울 수 있는 스킬 데이터 파일")]
        public List<SkillData> AvailableSkills = new List<SkillData>();

        /// <summary>각성 직업은 자기 BaseStats가 비어 있으면 부모 스탯 × AwakeningStatMultiplier 를 씁니다.</summary>
        public float GetBaseStatValue(StatType type)
        {
            foreach (var stat in BaseStats)
                if (stat.Type == type) return stat.BaseValue;
            if (parentClassData != null)
            {
                float v = parentClassData.GetBaseStatValue(type);
                return type == StatType.MoveSpeed || type == StatType.CriticalChance ? v : v * AwakeningStatMultiplier;
            }
            return 0f;
        }

        /// <summary>레벨 반영 스탯 (이동 속도·치명타 확률은 레벨로 늘지 않음)</summary>
        public float GetStatAtLevel(StatType type, int level)
        {
            float v = GetBaseStatValue(type);
            if (type == StatType.MoveSpeed || type == StatType.CriticalChance) return v;
            return v * (1f + GrowthPerLevel * Mathf.Max(0, level - 1));
        }

        /// <summary>해당 캐릭터 레벨에서 배운 스킬들</summary>
        public List<SkillData> GetUnlockedSkills(int characterLevel)
        {
            var list = parentClassData != null ? parentClassData.GetUnlockedSkills(characterLevel) : new List<SkillData>();
            foreach (var s in AvailableSkills)
                if (s != null && characterLevel >= s.RequiredLevel && !list.Contains(s)) list.Add(s);
            if (characterLevel >= AwakeningLevel)
                foreach (var s in awakeningSkills)
                    if (s != null && !list.Contains(s)) list.Add(s);
            return list;
        }

        /// <summary>스킬창에 보여 줄 전체 목록 (아직 못 배운 것 포함)</summary>
        public List<SkillData> GetAllSkills()
        {
            var list = parentClassData != null ? parentClassData.GetAllSkills() : new List<SkillData>();
            foreach (var s in AvailableSkills) if (s != null && !list.Contains(s)) list.Add(s);
            foreach (var s in awakeningSkills) if (s != null && !list.Contains(s)) list.Add(s);
            return list;
        }
    }
}
