namespace Game.SkillSystem
{
    // 직업 열거형
    public enum CharacterClass
    {
        Novice,     // 초보자 (Lv.1~14)
        Warrior,    // 전사
        Rogue,      // 도적
        Merchant,   // 상인
        Shaman      // 신 (신관/신사)
    }

    // 스킬 발동 메커니즘 타입
    public enum SkillType
    {
        Passive,        // 지속 효과 (패시브)
        SingleTarget,   // 단일 대상 공격/스킬
        AreaOfEffect,   // 범위 공격/스킬 (AoE)
        Buff,           // 이로운 버프
        Summon,         // 용병/하수인 소환
        Transformation  // 강신/변신 궁극기
    }
}
