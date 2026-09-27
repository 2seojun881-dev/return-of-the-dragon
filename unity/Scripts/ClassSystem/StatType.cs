namespace Game.ClassSystem
{
    /// <summary>직업 기본 스탯 종류</summary>
    public enum StatType
    {
        MaxHp,          // 최대 체력
        MaxMp,          // 최대 마나
        Str,            // 근력 (전사 주스탯)
        Dex,            // 민첩 (도적 주스탯)
        Int,            // 지력 (신 주스탯 · 치유량)
        Luk,            // 행운
        Attack,         // 공격력
        Defense,        // 방어력
        MoveSpeed,      // 이동 속도
        CriticalChance, // 치명타 확률 (0~1)
    }
}
