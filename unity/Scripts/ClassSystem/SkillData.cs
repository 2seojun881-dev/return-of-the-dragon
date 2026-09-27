using System.Collections.Generic;
using UnityEngine;

namespace Game.ClassSystem
{
    /// <summary>
    /// 개별 스킬 데이터. Create > Character > Skill Data 로 에셋을 만들고 인스펙터에서 수치를 채웁니다.
    /// (Game.SkillSystem.SkillData 와 이름이 같지만 네임스페이스가 달라 함께 쓸 수 있습니다)
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "Character/Skill Data", order = 2)]
    public class SkillData : ScriptableObject
    {
        [Header("Basic Info")]
        public string SkillName;
        public Sprite SkillIcon;
        public SkillType Type;
        public SkillAttribute Attribute;
        [Tooltip("이 스킬을 배우는 캐릭터 레벨 (웹 프로토타입: Lv.15/35/55/80~85)")]
        public int RequiredLevel = 1;

        [Header("Skill Settings")]
        [Tooltip("스킬의 마스터 레벨")]
        public int MaxLevel = 1;
        public float ManaCost = 0f;
        [Tooltip("기본 쿨타임 (초)")]
        public float Cooldown = 0f;
        [Tooltip("스킬 사정거리")]
        public float Range = 0f;

        [Header("Level Data Table")]
        [Tooltip("스킬 레벨별 수치 (예: 1레벨 데미지%, 2레벨 데미지%...)\nMaxLevel이 5라면 리스트 크기도 5여야 합니다.")]
        public List<float> LevelTable = new List<float>();

        [Header("Description")]
        [TextArea(5, 10)]
        public string Description = "스킬 설명을 입력하세요.\n{0}레벨: {1} 데미지"; // {0}=Level, {1}=LevelTable 값

        [Header("Visual & Audio")]
        public GameObject EffectPrefab; // 발사체, 마법진 등
        public AudioClip CastSound;

        /// <summary>해당 스킬 레벨의 수치 (범위 밖이면 0)</summary>
        public float GetValue(int level)
        {
            if (LevelTable == null || level <= 0 || level > LevelTable.Count) return 0f;
            return LevelTable[level - 1];
        }

        public string GetDescriptionForLevel(int level)
        {
            if (LevelTable == null || level <= 0 || level > LevelTable.Count) return "설명 불가 (레벨 초과)";
            return string.Format(Description, level, LevelTable[level - 1]);
        }

        private void OnValidate()
        {
            // MaxLevel 과 LevelTable 길이를 맞춰 둡니다 (부족하면 마지막 값을 복사)
            if (MaxLevel < 1) MaxLevel = 1;
            if (LevelTable == null) LevelTable = new List<float>();
            while (LevelTable.Count < MaxLevel) LevelTable.Add(LevelTable.Count > 0 ? LevelTable[LevelTable.Count - 1] : 0f);
            while (LevelTable.Count > MaxLevel) LevelTable.RemoveAt(LevelTable.Count - 1);
        }
    }
}
