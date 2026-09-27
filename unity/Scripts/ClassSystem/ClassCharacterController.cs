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
        public float CurrentHp { get; private set; }
        public int Level => level;

        // 신성 방패 (그랜드 오라클): 남은 시간 동안 받는 피해를 mitigation 비율만큼 줄입니다 (0.8 = 80% 감소)
        private float shieldEnd, shieldMitigation;
        public bool HasDivineShield => Time.time < shieldEnd;

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
            foreach (StatType t in System.Enum.GetValues(typeof(StatType)))   // 각성 직업은 부모 스탯을 물려받으므로 모든 종류를 조회
            {
                float v = data.GetStatAtLevel(t, level);
                if (v != 0f) runtimeStats[t] = v;
            }
            activeSkills = data.GetUnlockedSkills(level);
            CurrentMp = GetStat(StatType.MaxMp);
            CurrentHp = GetStat(StatType.MaxHp);
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

        /// <summary>네트워크 서버가 계산한 수치를 그대로 반영할 때 (NetworkedClassManager)</summary>
        public void SetStatFromNetwork(StatType type, float value) => runtimeStats[type] = value;
        public void SetVitalsFromNetwork(float hp, float mp) { CurrentHp = hp; CurrentMp = mp; }

        public void ActivateDivineShield(float duration, float mitigation)
        {
            shieldEnd = Mathf.Max(shieldEnd, Time.time + duration);
            shieldMitigation = Mathf.Max(HasDivineShield ? shieldMitigation : 0f, mitigation);
        }

        /// <summary>BossDragonAI 가 SendMessage("TakeDamage") 로 호출합니다. 신성 방패가 있으면 감소.</summary>
        public float TakeDamage(float amount)
        {
            if (HasDivineShield) amount *= 1f - shieldMitigation;
            float def = GetStat(StatType.Defense);
            float dealt = Mathf.Max(1f, amount * (100f / (100f + def * 4f)));
            CurrentHp = Mathf.Max(0f, CurrentHp - dealt);
            return dealt;
        }

        public void Heal(float amount) => CurrentHp = Mathf.Min(GetStat(StatType.MaxHp), CurrentHp + amount);

        public float CooldownLeft(SkillData skill) =>
            cooldownEnd.TryGetValue(skill, out var end) ? Mathf.Max(0f, end - Time.time) : 0f;
    }
}
