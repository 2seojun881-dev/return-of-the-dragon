using System.Collections.Generic;
using UnityEngine;

namespace Game.ClassSystem
{
    /// <summary>
    /// 직업 데이터: 이름·아이콘·레벨 1 기본 스탯·배울 수 있는 스킬.
    /// 용의 귀환 직업: 초보자 / 전사 / 도적 / 신(치유 신관). 상인은 삭제되었습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewClassData", menuName = "Character/Class Data", order = 1)]
    public class ClassData : ScriptableObject
    {
        [Header("Class Info")]
        public string ClassName; // 초보자, 전사, 도적, 신
        public Sprite ClassIcon;
        [TextArea(3, 10)]
        public string ClassDescription;

        [Header("Base Stats (Level 1)")]
        [Tooltip("직업의 레벨 1 기본 스탯. 전사는 Str·MaxHp, 도적은 Dex·Crit, 신은 Int·MaxMp 위주.")]
        public List<CharacterStat> BaseStats = new List<CharacterStat>();

        [Header("Growth")]
        [Tooltip("레벨당 기본 스탯 증가율 (웹 프로토타입과 같은 0.09 = 레벨마다 +9%)")]
        public float GrowthPerLevel = 0.09f;

        [Header("Available Skills")]
        [Tooltip("이 직업이 배울 수 있는 스킬 데이터 파일")]
        public List<SkillData> AvailableSkills = new List<SkillData>();

        public float GetBaseStatValue(StatType type)
        {
            foreach (var stat in BaseStats)
                if (stat.Type == type) return stat.BaseValue;
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
            var list = new List<SkillData>();
            foreach (var s in AvailableSkills)
                if (s != null && characterLevel >= s.RequiredLevel) list.Add(s);
            return list;
        }
    }
}
