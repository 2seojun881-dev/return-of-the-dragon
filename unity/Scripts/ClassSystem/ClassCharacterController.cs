using System.Collections.Generic;
using UnityEngine;

namespace Game.ClassSystem
{
    /// <summary>
    /// 직업 데이터를 캐릭터에 적용하는 컴포넌트.
    /// (UnityEngine.CharacterController 와 이름이 겹치지 않도록 ClassCharacterController 로 명명)
    /// </summary>
    public class ClassCharacterController : MonoBehaviour
    {
        [SerializeField] private ClassData myClassData;
        [SerializeField] private int level = 1;

        private readonly Dictionary<StatType, float> runtimeStats = new Dictionary<StatType, float>();
        private readonly Dictionary<SkillData, float> cooldownEnd = new Dictionary<SkillData, float>();
        private List<SkillData> activeSkills = new List<SkillData>();
        public float CurrentMp { get; private set; }

        public ClassData ClassData => myClassData;
        public IReadOnlyList<SkillData> Skills => activeSkills;

        private void Start()
        {
            if (myClassData == null) { Debug.LogError("직업 데이터를 할당해 주세요."); return; }
            ApplyClass(myClassData, level);
        }

        /// <summary>직업 변경(진급) 또는 레벨업 때 다시 호출합니다.</summary>
        public void ApplyClass(ClassData data, int newLevel)
        {
            myClassData = data; level = newLevel;
            runtimeStats.Clear();
            foreach (var stat in data.BaseStats)
                runtimeStats[stat.Type] = data.GetStatAtLevel(stat.Type, level);
            activeSkills = data.GetUnlockedSkills(level);
            CurrentMp = GetStat(StatType.MaxMp);
            Debug.Log($"--- {data.ClassName} Lv.{level} · HP {GetStat(StatType.MaxHp)} · 스킬 {activeSkills.Count}개 ---");
        }

        public float GetStat(StatType type) => runtimeStats.TryGetValue(type, out var v) ? v : 0f;

        /// <summary>스킬 사용 (마나·쿨타임 확인). 성공하면 true.</summary>
        public bool UseSkill(int index, int skillLevel)
        {
            if (index < 0 || index >= activeSkills.Count) return false;
            SkillData skill = activeSkills[index];
            if (skill.Type == SkillType.Passive) return false;
            if (cooldownEnd.TryGetValue(skill, out var end) && Time.time < end) return false;
            if (CurrentMp < skill.ManaCost) return false;

            CurrentMp -= skill.ManaCost;
            cooldownEnd[skill] = Time.time + skill.Cooldown;
            Debug.Log($"{skill.SkillName} (Lv.{skillLevel}) · {skill.GetDescriptionForLevel(skillLevel)}");
            if (skill.EffectPrefab != null) Instantiate(skill.EffectPrefab, transform.position, transform.rotation);
            if (skill.CastSound != null) AudioSource.PlayClipAtPoint(skill.CastSound, transform.position);
            // 실제 데미지/치유 계산은 여기서 skill.Attribute 와 skill.GetValue(skillLevel) 로 연결합니다.
            return true;
        }

        public float CooldownLeft(SkillData skill) =>
            cooldownEnd.TryGetValue(skill, out var end) ? Mathf.Max(0f, end - Time.time) : 0f;
    }
}
