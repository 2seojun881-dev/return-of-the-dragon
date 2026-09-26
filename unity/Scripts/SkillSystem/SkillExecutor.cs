using UnityEngine;
using Game.Core;

namespace Game.SkillSystem
{
    public class SkillExecutor : MonoBehaviour
    {
        [Header("플레이어 현재 스탯 참조")]
        public CharacterClass currentClass = CharacterClass.Merchant;
        public int currentLevel = 35;
        public int currentMp = 200;
        public int currentGold = 5000;
        public int intelligence = 50; // 지능 (신 스킬 계수)

        /// <summary>
        /// 스킬 시전을 시도합니다.
        /// </summary>
        public bool CastSkill(SkillData skill, Vector3 targetPosition)
        {
            // 1. 레벨 및 직업 조건 검증
            if (currentLevel < skill.requiredLevel || currentClass != skill.requiredClass)
            {
                Debug.LogWarning($"[스킬 사용 실패] 요구 레벨({skill.requiredLevel}) 또는 직업 조건이 맞지 않습니다.");
                return false;
            }

            // 2. 자원(MP/골드) 소모 검증
            if (currentMp < skill.mpCost || currentGold < skill.goldCost)
            {
                Debug.LogWarning("[스킬 사용 실패] MP 또는 골드가 부족합니다.");
                return false;
            }

            // 3. 자원 차감
            currentMp -= skill.mpCost;
            currentGold -= skill.goldCost;

            // 4. 스킬 타입별 실제 로직 분기
            ExecuteSkillLogic(skill, targetPosition);

            // 5. 시각/음향 이펙트 연출
            PlayVfxAndSfx(skill, targetPosition);

            return true;
        }

        private void ExecuteSkillLogic(SkillData skill, Vector3 targetPosition)
        {
            switch (skill.skillType)
            {
                case SkillType.Passive:
                    // 패시브 효과 등록 로직
                    break;

                case SkillType.SingleTarget:
                case SkillType.AreaOfEffect:
                    // 범위/단일 타격 판정 및 데미지 전달 로직
                    float totalDamage = skill.baseValue + (intelligence * skill.scalingFactor);
                    Debug.Log($"<color=orange>{skill.skillName}</color> 발동! 데미지: {totalDamage}");
                    break;

                case SkillType.Summon:
                    // 용병/하수인 소환 로직 (예: 상인의 묵직한 방패병)
                    Debug.Log($"<color=yellow>{skill.skillName}</color>! 용병 소환 완료 (지속시간: {skill.duration}초)");
                    break;

                case SkillType.Buff:
                case SkillType.Transformation:
                    // 상태 이상 및 변신/버프 적용 로직
                    Debug.Log($"<color=green>{skill.skillName}</color> 버프 적용!");
                    break;
            }
        }

        private void PlayVfxAndSfx(SkillData skill, Vector3 targetPosition)
        {
            if (skill.vfxPrefab != null)
            {
                Instantiate(skill.vfxPrefab, targetPosition, Quaternion.identity);
            }

            if (skill.sfxClip != null)
            {
                AudioSource.PlayClipAtPoint(skill.sfxClip, targetPosition);
            }
        }
    }
}
