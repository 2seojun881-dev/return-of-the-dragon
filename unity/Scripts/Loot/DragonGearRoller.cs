using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Core;

namespace Game.LootSystem
{
    // 부옵션 풀 (주요 / 전투 / 특수 스탯)
    public enum SubStat { Str, Dex, Int, Vit, Attack, Defense, MaxHp, MaxMp, CritChance, CritDamage, CooldownReduction, AttackSpeed, Accuracy }

    // 영웅(RED) 전용 고유 옵션 (1번 슬롯 확정)
    public enum HeroicUnique { DragonSlayer, BloodDrain, DragonFury, IgnisAegis }

    // 영웅 방어구 세트
    public enum DragonSet { None, DragonbloodConqueror, PhantomGhoul, DivineDescent }

    [Serializable]
    public class SubOption
    {
        public bool isUnique;
        public HeroicUnique unique;
        public SubStat stat;
        public float value;
        public char grade;      // C / B / A / S
    }

    [Serializable]
    public class RolledGear
    {
        public string itemName;
        public ItemRarity rarity;
        public int baseStat;                 // 기본 공격력(무기) 또는 방어력(방어구), 등급 배율 적용 후
        public DragonSet set;                // 영웅 방어구만
        public List<SubOption> options = new List<SubOption>();
    }

    /// <summary>
    /// 드래곤(이그니스) 장비 등급·부옵션 굴림 (웹 프로토타입과 같은 표).
    /// 고급 100% / 부옵션 2줄 (C~B) · 전설 140% / 3줄 (B~A) · 영웅 180% / 4줄 (A~S, 1번 슬롯 고유 옵션)
    /// </summary>
    public static class DragonGearRoller
    {
        public static readonly float[] BaseMultiplier = { 1f, 1f, 1.4f, 1.8f };
        public static readonly int[] SubOptionCount = { 0, 2, 3, 4 };
        static readonly string[] Grades = { "  ", "CB", "BA", "AS" };

        // [stat] -> 고급/전설/영웅 범위
        static readonly Dictionary<SubStat, Vector2[]> Ranges = new Dictionary<SubStat, Vector2[]>
        {
            { SubStat.Str, Main() }, { SubStat.Dex, Main() }, { SubStat.Int, Main() }, { SubStat.Vit, Main() },
            { SubStat.Attack, R(20, 50, 51, 100, 101, 180) },
            { SubStat.Defense, R(10, 25, 26, 50, 51, 90) },
            { SubStat.MaxHp, R(300, 700, 701, 1500, 1501, 3000) },
            { SubStat.MaxMp, R(300, 700, 701, 1500, 1501, 3000) },
            { SubStat.CritChance, R(1f, 2.5f, 2.6f, 5f, 5.1f, 8f) },
            { SubStat.CritDamage, R(5f, 10f, 10.1f, 20f, 20.1f, 35f) },
            { SubStat.CooldownReduction, R(1f, 2f, 2.1f, 4f, 4.1f, 7f) },
            { SubStat.AttackSpeed, R(1f, 3f, 3.1f, 6f, 6.1f, 10f) },
            { SubStat.Accuracy, R(1f, 3f, 3.1f, 6f, 6.1f, 10f) },
        };
        static Vector2[] Main() { return R(10, 20, 21, 40, 41, 70); }
        static Vector2[] R(float a, float b, float c, float d, float e, float f) { return new[] { new Vector2(a, b), new Vector2(c, d), new Vector2(e, f) }; }
        static bool IsPercent(SubStat s) { return s >= SubStat.CritChance; }

        public static RolledGear Roll(string name, ItemRarity rarity, int tierBaseStat, DragonSet set = DragonSet.None)
        {
            var g = new RolledGear { itemName = name, rarity = rarity, baseStat = Mathf.RoundToInt(tierBaseStat * BaseMultiplier[(int)rarity]) };
            if (rarity == ItemRarity.Heroic) g.set = set;
            Reroll(g, null);
            return g;
        }

        /// <summary>옵션 재마법 부여. lockedIndices(최대 2개)는 그대로 두고 나머지만 다시 굴립니다.</summary>
        public static void Reroll(RolledGear g, IList<int> lockedIndices)
        {
            int r = (int)g.rarity, n = SubOptionCount[r];
            var old = g.options; var result = new SubOption[n];
            if (lockedIndices != null)
                foreach (int i in lockedIndices) if (i >= 0 && i < n && old != null && i < old.Count) result[i] = old[i];

            if (g.rarity == ItemRarity.Heroic && result[0] == null)
                result[0] = new SubOption { isUnique = true, unique = (HeroicUnique)UnityEngine.Random.Range(0, 4), grade = 'S' };

            for (int i = 0; i < n; i++)
            {
                if (result[i] != null) continue;
                var pool = new List<SubStat>();
                foreach (SubStat s in Enum.GetValues(typeof(SubStat)))
                {
                    bool used = false;
                    foreach (var o in result) if (o != null && !o.isUnique && o.stat == s) used = true;
                    if (!used) pool.Add(s);
                }
                result[i] = RollOne(pool[UnityEngine.Random.Range(0, pool.Count)], r);
            }
            g.options = new List<SubOption>(result);
        }

        static SubOption RollOne(SubStat s, int r)
        {
            Vector2 range = Ranges[s][r - 1];
            float v = UnityEngine.Random.Range(range.x, range.y);
            v = IsPercent(s) ? Mathf.Round(v * 10f) / 10f : Mathf.Round(v);
            float q = (v - range.x) / Mathf.Max(0.001f, range.y - range.x);
            return new SubOption { stat = s, value = v, grade = Grades[r][q < 0.5f ? 0 : 1] };
        }

        /// <summary>재설정 비용: 용의 비늘 + 골드, 잠금 1줄마다 용의 결정 추가</summary>
        public static void RerollCost(ItemRarity rarity, int lockCount, out int scales, out int gold, out int crystals)
        {
            int r = (int)rarity;
            scales = new[] { 0, 5, 10, 20 }[r];
            gold = new[] { 0, 50000, 150000, 500000 }[r];
            crystals = lockCount * new[] { 0, 1, 2, 3 }[r];
        }

        public static string Describe(RolledGear g)
        {
            var sb = new StringBuilder();
            foreach (var o in g.options)
            {
                if (o.isUnique) { sb.AppendLine("<color=#FF2222>★ " + UniqueName(o.unique) + "</color>"); continue; }
                sb.AppendLine(o.stat + " +" + (IsPercent(o.stat) ? o.value.ToString("F1") + "%" : o.value.ToString("F0")) + " (" + o.grade + ")");
            }
            return sb.ToString();
        }

        public static string UniqueName(HeroicUnique u)
        {
            switch (u)
            {
                case HeroicUnique.DragonSlayer: return "용살자의 위엄: 보스 피해 +15%";
                case HeroicUnique.BloodDrain: return "용혈의 흡수: 처치 시 최대 체력 5% 회복";
                case HeroicUnique.DragonFury: return "용의 분노: 공격 시 5% 확률로 3초간 치명타 +100% (재사용 30초)";
                default: return "이그니스의 가호: 피격 시 3% 확률로 5초 용암 보호막 (재사용 60초)";
            }
        }

        // 직업별 영웅 세트 (3부위 / 5부위)
        public static DragonSet SetFor(CharacterClass c)
        {
            switch (c)
            {
                case CharacterClass.Warrior: return DragonSet.DragonbloodConqueror;
                case CharacterClass.Rogue: return DragonSet.PhantomGhoul;
                case CharacterClass.Shaman: return DragonSet.DivineDescent;
                default: return DragonSet.None;
            }
        }

        public static string SetEffect(DragonSet s, int equippedCount)
        {
            string three, five;
            switch (s)
            {
                case DragonSet.DragonbloodConqueror:
                    three = "[용의 역린] 피격·공격 시 15% 확률로 10초간 공격력 +25%, 받는 피해 -15% (최대 3중첩)";
                    five = "[불사신: 용혈 강림] 치명상 시 5초 무적 + 주변 3초 기절 + HP 50% 회복 (180초) / 근접 공격 20% 확률 500% 용의 충격파"; break;
                case DragonSet.PhantomGhoul:
                    three = "[그림자 습격] 은신 첫 타격 치명타 100%, 치명타 피해 +60%, 회피 +25%";
                    five = "[환영 처형 & 연쇄 암살] 공격 시 10% 확률 그림자 분신 2명 800% 연타 / 처치 시 모든 쿨타임 초기화"; break;
                case DragonSet.DivineDescent:
                    three = "[신성 가호] 치유량 +35%, 신성 피해 +35%, MP 초당 4% 회복";
                    five = "[천신 심판 & 부활의 기적] 신의 강림 60초 + 초당 350% 신성 결계 / 사망 시 즉시 100% 부활 (3회)"; break;
                default: return "";
            }
            return (equippedCount >= 3 ? "<color=#FFB0A0>" : "<color=#808080>") + "3세트 " + three + "</color>\n" +
                   (equippedCount >= 5 ? "<color=#FFB0A0>" : "<color=#808080>") + "5세트 " + five + "</color>";
        }
    }
}
