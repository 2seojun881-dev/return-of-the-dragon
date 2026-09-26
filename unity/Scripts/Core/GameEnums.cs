namespace Game.Core
{
    public enum CharacterClass
    {
        Novice,     // 초보자 (Lv.1~14)
        Warrior,    // 전사
        Rogue,      // 도적
        Shaman      // 신 (치유 신관 · 힐러)
    }

    public enum ItemRarity
    {
        Normal,     // 일반 (흰색)
        Advanced,   // 고급 (파랑)
        Legendary,  // 전설 (노랑)
        Heroic      // 영웅 (빨강)
    }

    public enum SkillType
    {
        Passive,
        SingleTarget,
        AreaOfEffect,
        Buff,
        Summon,
        Transformation,
        Heal        // 치유 (신)
    }
}
