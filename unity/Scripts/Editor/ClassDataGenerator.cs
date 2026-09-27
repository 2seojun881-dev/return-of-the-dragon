#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.ClassSystem;

/// <summary>
/// 메뉴 [Game System > Generate Class Data] : 웹 프로토타입과 같은 수치로
/// Class_Novice / Class_Warrior / Class_Rogue / Class_God 와 스킬 에셋 13개를 만듭니다.
/// 스킬은 5레벨까지, 레벨마다 기본 수치의 +15%.
/// </summary>
public static class ClassDataGenerator
{
    const string Folder = "Assets/GameData/Classes";

    struct Sk { public string id, name, desc; public int lv; public SkillAttribute attr; public float cd, mp, range, value; public bool passive; }
    static Sk S(string id, string n, int lv, SkillAttribute a, float cd, float mp, float range, float value, string d) =>
        new Sk { id = id, name = n, lv = lv, attr = a, cd = cd, mp = mp, range = range, value = value, desc = d };

    [MenuItem("Game System/Generate Class Data")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GameData")) AssetDatabase.CreateFolder("Assets", "GameData");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/GameData", "Classes");

        Make("Class_Novice", "초보자", "모든 모험가는 초보자로 시작합니다. 레벨 15에 천명 성채 진급의 사당에서 전사·도적·신 중 하나로 진급합니다.",
            new[] { St(StatType.MaxHp, 230), St(StatType.MaxMp, 100), St(StatType.Str, 10), St(StatType.Dex, 10), St(StatType.Int, 10), St(StatType.Luk, 10),
                    St(StatType.Attack, 13), St(StatType.Defense, 9), St(StatType.MoveSpeed, 6.4f), St(StatType.CriticalChance, 0.1f) },
            new[] { S("NOVICE_01", "힘껏 베기", 1, SkillAttribute.Physical, 6, 10, 3, 200, "전방을 힘껏 베어 {1}% 피해 (Lv.{0})") });

        Make("Class_Warrior", "전사", "근접 · 중갑 딜탱. 대검으로 앞에서 적을 막고 베어 넘긴다.",
            new[] { St(StatType.MaxHp, 330), St(StatType.MaxMp, 110), St(StatType.Str, 14), St(StatType.Dex, 8), St(StatType.Int, 6), St(StatType.Luk, 8),
                    St(StatType.Attack, 14), St(StatType.Defense, 15), St(StatType.MoveSpeed, 6.3f), St(StatType.CriticalChance, 0.1f) },
            new[] { S("WARRIOR_01", "돌진 베기", 15, SkillAttribute.Physical, 6, 20, 11, 220, "타겟에게 돌진해 {1}% 피해 + 1초 기절 (Lv.{0})"),
                    S("WARRIOR_02", "드래곤의 방패", 35, SkillAttribute.Buff, 14, 45, 8, 50, "5초간 방어력 {1}%↑, 주변 적 도발 (Lv.{0})"),
                    S("WARRIOR_03", "회오리 베기", 55, SkillAttribute.Physical, 11, 70, 3.3f, 420, "1.6초간 회전하며 주변 적에게 총 {1}% 피해 (Lv.{0})"),
                    S("WARRIOR_04", "지각 변동", 80, SkillAttribute.Physical, 28, 150, 6, 400, "도약 후 내리찍어 광역 띄움 + {1}% 피해 (Lv.{0})") });

        Make("Class_Rogue", "도적", "근접 · 고속 기습/암살. 쌍검과 은신으로 적의 급소를 노린다.",
            new[] { St(StatType.MaxHp, 240), St(StatType.MaxMp, 120), St(StatType.Str, 9), St(StatType.Dex, 15), St(StatType.Int, 7), St(StatType.Luk, 12),
                    St(StatType.Attack, 16), St(StatType.Defense, 8), St(StatType.MoveSpeed, 7.1f), St(StatType.CriticalChance, 0.28f) },
            new[] { S("ROGUE_01", "그림자 습격", 15, SkillAttribute.Physical, 7, 20, 12, 240, "타겟 뒤로 순간 이동해 {1}% 확정 치명타 (Lv.{0})"),
                    S("ROGUE_02", "은신", 35, SkillAttribute.Buff, 16, 45, 0, 300, "6초 은신, 다음 공격 {1}% 치명타 (Lv.{0})"),
                    S("ROGUE_03", "칼날 폭풍", 55, SkillAttribute.Physical, 11, 70, 3.5f, 330, "주변을 여섯 번 베어 총 {1}% 피해 (Lv.{0})"),
                    S("ROGUE_04", "암살", 80, SkillAttribute.Physical, 26, 150, 10, 800, "단일 대상 {1}% 피해, 체력 50% 이하면 두 배 (Lv.{0})") });

        Make("Class_God", "신(치유 신관)", "원거리 · 치유/신성 (힐러). 상처를 메우고 보호막을 두르며 성역으로 적을 태운다.",
            new[] { St(StatType.MaxHp, 220), St(StatType.MaxMp, 220), St(StatType.Str, 6), St(StatType.Dex, 8), St(StatType.Int, 16), St(StatType.Luk, 10),
                    St(StatType.Attack, 16), St(StatType.Defense, 8), St(StatType.MoveSpeed, 6.5f), St(StatType.CriticalChance, 0.1f) },
            new[] { S("GOD_01", "치유의 빛", 15, SkillAttribute.Heal, 8, 40, 0, 18, "최대 체력 {1}% 즉시 회복 + 6초간 초당 3% 회복 (Lv.{0})"),
                    S("GOD_02", "신성 보호막", 35, SkillAttribute.Buff, 14, 60, 4, 30, "10초간 최대 체력 {1}% 보호막 + 주변 160% 신성 피해 (Lv.{0})"),
                    S("GOD_03", "성역", 55, SkillAttribute.Heal, 18, 120, 5, 5, "6초 성역: 초당 체력 {1}% 회복, 적은 지속 신성 피해 + 기절 (Lv.{0})"),
                    S("GOD_04", "신의 강림", 85, SkillAttribute.Buff, 120, 300, 4, 50, "체력 완전 회복 + 30초 치유량 {1}%↑, 쿨타임 50%↓, 부활 1회 (Lv.{0})") });

        AssetDatabase.SaveAssets();
        Debug.Log("직업 데이터 4종 + 스킬 13개 생성 완료: " + Folder);
    }

    static CharacterStat St(StatType t, float v) => new CharacterStat(t, v);

    static void Make(string file, string name, string desc, CharacterStat[] stats, Sk[] skills)
    {
        var skillAssets = new List<SkillData>();
        foreach (var s in skills)
        {
            string path = $"{Folder}/Skill_{s.id}.asset";
            var sd = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (sd == null) { sd = ScriptableObject.CreateInstance<SkillData>(); AssetDatabase.CreateAsset(sd, path); }
            sd.SkillName = s.name; sd.Type = s.passive ? SkillType.Passive : SkillType.Active; sd.Attribute = s.attr;
            sd.RequiredLevel = s.lv; sd.Cooldown = s.cd; sd.ManaCost = s.mp; sd.Range = s.range;
            sd.MaxLevel = 5; sd.LevelTable = new List<float>();
            for (int i = 0; i < 5; i++) sd.LevelTable.Add(Mathf.Round(s.value * (1f + 0.15f * i) * 10f) / 10f);
            sd.Description = s.desc;
            EditorUtility.SetDirty(sd);
            skillAssets.Add(sd);
        }
        string cpath = $"{Folder}/{file}.asset";
        var cd = AssetDatabase.LoadAssetAtPath<ClassData>(cpath);
        if (cd == null) { cd = ScriptableObject.CreateInstance<ClassData>(); AssetDatabase.CreateAsset(cd, cpath); }
        cd.ClassName = name; cd.ClassDescription = desc; cd.BaseStats = new List<CharacterStat>(stats); cd.AvailableSkills = skillAssets;
        EditorUtility.SetDirty(cd);
    }
}
#endif
