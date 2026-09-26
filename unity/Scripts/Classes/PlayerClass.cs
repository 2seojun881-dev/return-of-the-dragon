// 용의 귀환 — 직업 정의
// 모든 캐릭터는 초보자(Novice)로 시작해 레벨 15에 천명 성채 '진급의 사당'에서 세 직업(전사 · 도적 · 신) 중 하나로 진급합니다.
public enum PlayerClass
{
    Novice,     // 초보자 (Lv.1~14)
    Warrior,    // 전사: 근접 중갑 딜탱
    Rogue,      // 도적: 고속 기습/암살
    Shaman      // 신(치유 신관): 치유·신성 (힐러)
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
            case PlayerClass.Shaman: return "신(치유 신관)";
            default: return "초보자";
        }
    }

    // Game.Core.CharacterClass 와 순서가 같아서 그대로 변환됩니다.
    public static Game.Core.CharacterClass ToSkillClass(this PlayerClass c)
    {
        return (Game.Core.CharacterClass)(int)c;
    }
}
