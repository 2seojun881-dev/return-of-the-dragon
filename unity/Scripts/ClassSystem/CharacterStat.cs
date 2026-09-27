using System;

namespace Game.ClassSystem
{
    /// <summary>직업의 레벨 1 기본 스탯 한 줄 (인스펙터 리스트용)</summary>
    [Serializable]
    public class CharacterStat
    {
        public StatType Type;
        public float BaseValue; // 레벨 1일 때의 기본 스탯 값

        public CharacterStat(StatType type, float value)
        {
            Type = type;
            BaseValue = value;
        }
    }
}
