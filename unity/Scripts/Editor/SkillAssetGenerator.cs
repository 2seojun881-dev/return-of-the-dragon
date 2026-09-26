#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.SkillSystem;

/// <summary>
/// 메뉴 [Game System > Generate Skill Assets] 한 번으로 웹 프로토타입과 같은 수치의 스킬 에셋 17개(초보자 1 + 4직업 × 4)를 만듭니다.
/// 만든 뒤에는 인스펙터에서 아이콘·VFX·SFX만 채우면 됩니다.
/// </summary>
public static class SkillAssetGenerator
{
    const string Folder = "Assets/GameData/Skills";

    struct Def
    {
        public string id, name, desc; public CharacterClass cls; public int lv; public SkillType type;
        public float cd; public int mp, gold; public float baseV, scale, dur, range, radius;
        public Def(string id, string name, CharacterClass cls, int lv, SkillType type, float cd, int mp, int gold, float baseV, float scale, float dur, float range, float radius, string desc)
        { this.id = id; this.name = name; this.cls = cls; this.lv = lv; this.type = type; this.cd = cd; this.mp = mp; this.gold = gold; this.baseV = baseV; this.scale = scale; this.dur = dur; this.range = range; this.radius = radius; this.desc = desc; }
    }

    static readonly Def[] Defs =
    {
        new Def("SKILL_NOVICE_01","힘껏 베기",CharacterClass.Novice,1,SkillType.SingleTarget,6,10,0,200,1f,0,3,0,"전방을 힘껏 베어 {value}% 피해 (MP {mpCost})"),

        new Def("SKILL_WARRIOR_01","돌진 베기",CharacterClass.Warrior,15,SkillType.SingleTarget,6,20,0,220,1f,1,11,0,"타겟에게 돌진해 {value}% 피해 + {duration}초 기절"),
        new Def("SKILL_WARRIOR_02","드래곤의 방패",CharacterClass.Warrior,35,SkillType.Buff,14,45,0,50,0,5,0,8,"{duration}초간 방어력 {value}% 증가, 주변 적 도발"),
        new Def("SKILL_WARRIOR_03","회오리 베기",CharacterClass.Warrior,55,SkillType.AreaOfEffect,11,70,0,420,1f,1.6f,0,3.3f,"{duration}초간 회전하며 주변 적에게 총 {value}% 피해"),
        new Def("SKILL_WARRIOR_04","지각 변동",CharacterClass.Warrior,80,SkillType.AreaOfEffect,28,150,0,400,1f,0,0,6,"도약 후 내리찍어 광역 띄움 + {value}% 피해"),

        new Def("SKILL_ROGUE_01","그림자 습격",CharacterClass.Rogue,15,SkillType.SingleTarget,7,20,0,240,1f,0,12,0,"타겟 뒤로 순간 이동해 {value}% 확정 치명타"),
        new Def("SKILL_ROGUE_02","은신",CharacterClass.Rogue,35,SkillType.Buff,16,45,0,300,0,6,0,0,"{duration}초간 은신: 적 추적 해제, 이동 속도 30% 증가, 다음 공격 {value}% 치명타"),
        new Def("SKILL_ROGUE_03","칼날 폭풍",CharacterClass.Rogue,55,SkillType.AreaOfEffect,11,70,0,330,1f,1.1f,0,3.5f,"주변을 여섯 번 베어 총 {value}% 피해"),
        new Def("SKILL_ROGUE_04","암살",CharacterClass.Rogue,80,SkillType.SingleTarget,26,150,0,800,1f,0,10,0,"단일 대상 {value}% 피해, 대상 체력 50% 이하면 두 배"),

        new Def("SKILL_MERCHANT_01","금화 수색",CharacterClass.Merchant,15,SkillType.Passive,0,0,0,25,0,0,0,0,"(패시브) 골드 획득량 {value}% 증가, 장비·재료 드롭률 10% 증가"),
        new Def("SKILL_MERCHANT_02","투전 난사",CharacterClass.Merchant,35,SkillType.AreaOfEffect,6,30,100,150,1.2f,0,9,0,"마력이 깃든 금화를 난사하여 전방 적 3명에게 {value}의 방어 무시 피해 5연타. (소모: {mpCost} MP, {goldCost} Gold)"),
        new Def("SKILL_MERCHANT_03","용병 고용: 묵직한 방패병",CharacterClass.Merchant,55,SkillType.Summon,40,120,0,30,0,180,0,7,"{duration}초간 철갑 용병 소환: 도발, 상인이 받는 피해의 {value}%를 대신 흡수"),
        new Def("SKILL_MERCHANT_04","만복의 상단진",CharacterClass.Merchant,80,SkillType.Buff,90,250,0,15,0,60,0,7,"{duration}초 황금 결계: 공격력 {value}% 증가, 물약 회복량 50% 증가, 처치 경험치 15% 추가"),

        new Def("SKILL_SHAMAN_01","령환의 가호",CharacterClass.Shaman,15,SkillType.Buff,10,40,0,100,2.2f,10,0,0,"체력을 {value} 회복하고 {duration}초간 최대 체력 20% 영혼 보호막"),
        new Def("SKILL_SHAMAN_02","파령 부적",CharacterClass.Shaman,35,SkillType.SingleTarget,6,60,0,260,1.5f,5,13,0,"붉은 뇌전 부적: {value} 피해 + {duration}초간 마법 저하, 이동 속도 40% 감소"),
        new Def("SKILL_SHAMAN_03","아수라 봉인진",CharacterClass.Shaman,55,SkillType.AreaOfEffect,16,150,0,45,0.8f,5,14,5,"{duration}초 주술진: 초당 {value} 피해, 2초마다 1.5초 기절, 언데드/원혼 50% 추가 피해"),
        new Def("SKILL_SHAMAN_04","강신: 천신 강림",CharacterClass.Shaman,85,SkillType.Transformation,120,300,0,50,0,30,0,4,"{duration}초 변신: 모든 스킬 쿨타임 {value}% 감소, 모든 공격에 신성 파동, 사망한 파티원 1명 즉시 부활"),
    };

    [MenuItem("Game System/Generate Skill Assets")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        foreach (var d in Defs)
        {
            string path = $"{Folder}/{d.id}.asset";
            var s = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            bool isNew = s == null;
            if (isNew) s = ScriptableObject.CreateInstance<SkillData>();
            s.skillId = d.id; s.skillName = d.name; s.description = d.desc;
            s.requiredClass = d.cls; s.requiredLevel = d.lv; s.skillType = d.type;
            s.cooldown = d.cd; s.mpCost = d.mp; s.goldCost = d.gold;
            s.baseValue = d.baseV; s.scalingFactor = d.scale; s.duration = d.dur; s.range = d.range; s.areaRadius = d.radius;
            if (isNew) AssetDatabase.CreateAsset(s, path); else EditorUtility.SetDirty(s);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"스킬 에셋 {Defs.Length}개 생성/갱신: {Folder}");
    }
}
#endif
