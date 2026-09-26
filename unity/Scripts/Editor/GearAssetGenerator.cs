#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴 [Game System > Generate Gear & Loot Tables] 로 기획서의 사냥터별 무기/방어구 테이블 전체와
/// 사냥터별 LootTable 5개를 에셋으로 만듭니다. (웹 프로토타입과 같은 공격력·방어력)
/// </summary>
public static class GearAssetGenerator
{
    const string Folder = "Assets/GameData/Equipment";
    static readonly PlayerClass[] C = { PlayerClass.Warrior, PlayerClass.Rogue, PlayerClass.Merchant, PlayerClass.Shaman };

    struct Tier
    {
        public int lv, atk, def; public HuntingZone zone; public bool db; public string[] w, a;
        public Tier(HuntingZone zone, int lv, int atk, int def, bool db, string[] w, string[] a) { this.zone = zone; this.lv = lv; this.atk = atk; this.def = def; this.db = db; this.w = w; this.a = a; }
    }

    // 무기/방어구 이름 순서: 전사, 도적, 상인, 신
    static readonly Tier[] Tiers =
    {
        new Tier(HuntingZone.VengefulGraveyard,15,20,10,false,new[]{"강철 대검","암살자의 비가","금화 주판","령환의 방울"},new[]{"강철 판금","날렵한 가죽갑","상단 비단포","령사 로브"}),
        new Tier(HuntingZone.VengefulGraveyard,30,34,18,false,new[]{"원혼을 베는 자","원혼의 뼈단검","백은 주판","혼령의 신봉"},new[]{"백골 판금","칠흑 가죽갑","화려한 상단복","영혼 주술포"}),
        new Tier(HuntingZone.ClockworkMaze,45,52,28,false,new[]{"태엽 거대도","미로의 톱니검","청동 계산기","톱니 부적"},new[]{"태엽 강화갑","정밀 가죽갑","상인 대장의 도포","환영의 신사복"}),
        new Tier(HuntingZone.ClockworkMaze,60,74,40,false,new[]{"마력 기계대검","붉은 태엽 단도","황금 제왕 주판","천상의 령봉"},new[]{"미로 수호판금","그림자 수호갑","황금 비단 의복","성스러운 신사 로브"}),
        new Tier(HuntingZone.AsuraCanyon,75,100,55,false,new[]{"아수라 파천도","흑야의 암귀검","만복 주판","강신 무당령"},new[]{"아수라 중갑","흑야의 야사갑","천명 상단 비단포","천신 주술의복"}),
        new Tier(HuntingZone.AsuraCanyon,90,130,72,false,new[]{"멸악 패왕검","시귀의 환영검","만수무강 주판","신통 만신봉"},new[]{"멸악 패왕갑","시귀의 은신갑","만수 거상포","신통 만신 로브"}),
        new Tier(HuntingZone.DragonbloodAltar,99,150,90,true,new[]{"용혈 파천검","용혈 섬광단검","용혈 만복주판","용혈 멸세령"},new[]{"용혈 파천중갑","용혈 섬광가죽갑","용혈 만복비단포","용혈 멸세제사장복"}),
    };

    [MenuItem("Game System/Generate Gear & Loot Tables")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        var tables = new Dictionary<HuntingZone, LootTable>();

        // 야수 서식지: 공용 수련 장비
        var beast = Table(tables, HuntingZone.BeastHabitat, 1, 15, 0.035f);
        beast.items.Add(Make("수련용 목검", ItemType.Weapon, 1, PlayerClass.Novice, true, 6, 0, false));
        beast.items.Add(Make("목판 갑옷", ItemType.Armor, 1, PlayerClass.Novice, true, 0, 5, false));
        beast.items.Add(Make("가죽 톳옷", ItemType.Armor, 1, PlayerClass.Novice, true, 0, 4, false));

        foreach (var t in Tiers)
        {
            var table = t.zone == HuntingZone.VengefulGraveyard ? Table(tables, t.zone, 15, 40, 0.035f)
                      : t.zone == HuntingZone.ClockworkMaze ? Table(tables, t.zone, 40, 70, 0.035f)
                      : t.zone == HuntingZone.AsuraCanyon ? Table(tables, t.zone, 70, 99, 0.035f)
                      : Table(tables, t.zone, 99, 99, 0f);
            if (t.db) { table.bossDropChance = 0.02f; } // 이그니스: 부위별 2% (기획 1~3%)
            for (int i = 0; i < 4; i++)
            {
                table.items.Add(Make(t.w[i], ItemType.Weapon, t.lv, C[i], false, t.atk, 0, t.db));
                table.items.Add(Make(t.a[i], ItemType.Armor, t.lv, C[i], false, 0, t.def, t.db));
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"장비·드롭 테이블 생성 완료: {Folder}");
    }

    static LootTable Table(Dictionary<HuntingZone, LootTable> tables, HuntingZone z, int min, int max, float chance)
    {
        LootTable t;
        if (tables.TryGetValue(z, out t)) return t;
        string p = Folder + "/Loot_" + z + ".asset";
        t = AssetDatabase.LoadAssetAtPath<LootTable>(p);
        if (t == null) { t = ScriptableObject.CreateInstance<LootTable>(); AssetDatabase.CreateAsset(t, p); }
        t.zone = z; t.minLevel = min; t.maxLevel = max; t.dropChance = chance; t.items.Clear(); tables[z] = t; EditorUtility.SetDirty(t);
        return t;
    }

    static EquipmentItem Make(string name, ItemType type, int lv, PlayerClass cls, bool any, int atk, int def, bool db)
    {
        string path = $"{Folder}/{name}.asset";
        var it = AssetDatabase.LoadAssetAtPath<EquipmentItem>(path);
        bool isNew = it == null;
        if (isNew) it = ScriptableObject.CreateInstance<EquipmentItem>();
        it.itemName = name; it.ItemType = type; it.RequiredLevel = lv; it.RequiredClass = cls; it.anyClass = any;
        it.attack = atk; it.defense = def; it.dragonblood = db;
        it.description = (any ? "공용" : cls.KoreanName() + " 전용") + $" · Lv.{lv} " + (type == ItemType.Weapon ? $"무기 (공격력 +{atk})" : $"방어구 (방어 +{def})");
        if (isNew) AssetDatabase.CreateAsset(it, path); else EditorUtility.SetDirty(it);
        return it;
    }
}
#endif
