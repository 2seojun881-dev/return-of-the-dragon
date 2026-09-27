namespace Game.ClassSystem
{
    /// <summary>
    /// 능동/지속 구분. (Game.Core.SkillType 은 판정 방식 구분이라 별도 네임스페이스로 분리)
    /// </summary>
    public enum SkillType
    {
        Active,     // 능동 스킬 (마나 소비, 쿨타임)
        Passive,    // 지속 스킬 (조건부 상시 적용)
    }
}
