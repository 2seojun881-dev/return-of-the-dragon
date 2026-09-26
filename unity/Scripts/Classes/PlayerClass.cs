// 용의 귀환 — 직업 정의
// 모든 캐릭터는 초보자(Novice)로 시작해 레벨 15에 천명 성채 '진급의 사당'에서 네 직업 중 하나로 진급합니다.
public enum PlayerClass
{
    Novice,     // 초보자 (Lv.1~14)
    Warrior,    // 전사: 근접 중갑 딜탱
    Rogue,      // 도적: 고속 기습/암살
    Merchant,   // 상인: 보조·재화 특화
    Shaman      // 신(神관/신사): 주술·영력 지원
}

public static class PlayerClassInfo
{
    public const int PromotionLevel = 15;

    public static string KoreanName(this PlayerClass c)
    {
        switch (c)
        {
            case PlayerClass.Warrior: return "전사";
            case PlayerClass.Rogue: return "도적";
            case PlayerClass.Merchant: return "상인";
            case PlayerClass.Shaman: return "신(神관/신사)";
            default: return "초보자";
        }
    }

    // Game.SkillSystem.CharacterClass 와 순서가 같아서 그대로 변환됩니다.
    public static Game.SkillSystem.CharacterClass ToSkillClass(this PlayerClass c)
    {
        return (Game.SkillSystem.CharacterClass)(int)c;
    }
}
