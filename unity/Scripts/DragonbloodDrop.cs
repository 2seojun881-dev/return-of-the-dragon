using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 용혈 무기 (이그니스 전용 드롭)
[Serializable]
public class DragonbloodWeapon
{
    public string name;
    public PlayerClass owner;
    public string effect;
    public int refine = 1;          // 1형 ~ 9형
    public const int MaxRefine = 9;

    public DragonbloodWeapon(string name, PlayerClass owner, string effect)
    {
        this.name = name;
        this.owner = owner;
        this.effect = effect;
    }

    // 제련 비용: n형 → n+1형 = 용의 비늘 5n, 용의 결정 n, 골드 100,000n
    public int ScaleCost => refine * 5;
    public int CrystalCost => refine;
    public int GoldCost => refine * 100000;
}

// 토벌 보상 결과
public class IgnisReward
{
    public DragonbloodWeapon armor;    // null이면 방어구 드롭 없음
    public int gold;
    public int dragonScales;
    public int dragonCrystals;
    public DragonbloodWeapon weapon;   // null이면 무기 드롭 없음
}

/// <summary>
/// 고대 드래곤 이그니스 처치 보상 + 서버 전체 공지.
/// - 확정: 용의 비늘 3~6개, 골드
/// - 35%: 용의 결정 1~2개
/// - dropRate(기본 2%): 직업에 맞는 용혈 무기, 별도로 2% 용혈 방어구 → 서버 상단 띠 공지
/// 이그니스의 사망 처리에서 GrantReward(플레이어 이름, 직업)를 호출하세요.
/// </summary>
public class DragonbloodDrop : MonoBehaviour
{
    public static DragonbloodDrop Instance { get; private set; }

    [Header("드롭 확률")]
    [Range(0f, 100f)] public float dropRate = 2f;           // 기획: 1~3%
    [Range(0f, 100f)] public float crystalRate = 35f;
    public Vector2Int scaleRange = new Vector2Int(3, 6);   // 포함 범위
    public Vector2Int goldRange = new Vector2Int(60000, 120000);

    [Header("서버 공지 UI (상단 띠)")]
    public RectTransform noticeBar;      // 화면 상단 띠 (마스크 영역)
    public Text noticeText;              // 띠 안에서 오른쪽 → 왼쪽으로 흐르는 텍스트
    public float noticeSpeed = 160f;     // px/s
    public AudioSource noticeSound;

    // 서버 공지 이벤트: 실제 서비스에서는 여기서 서버 API/소켓으로 브로드캐스트합니다.
    public event Action<string> OnServerNotice;

    readonly Queue<string> noticeQueue = new Queue<string>();
    bool noticeRunning;

    void Awake()
    {
        Instance = this;
        if (noticeBar != null) noticeBar.gameObject.SetActive(false);
    }

    public static DragonbloodWeapon CreateWeapon(PlayerClass cls)
    {
        switch (cls)
        {
            case PlayerClass.Warrior:
                return new DragonbloodWeapon("용혈 파천검", cls, "기본 공격 시 일정 확률로 전방에 공격력 200% 광역 피해");
            case PlayerClass.Rogue:
                return new DragonbloodWeapon("용혈 섬광단검", cls, "기본 공격 적중 시 일정 확률로 용의 표식: 3초 속박 + 받는 피해 30% 증가");
            case PlayerClass.Merchant:
                return new DragonbloodWeapon("용혈 만복주판", cls, "기본 공격 적중 시 일정 확률로 황금 용 폭발(광역 180%) + 추가 골드");
            default:
                return new DragonbloodWeapon("용혈 멸세령", cls, "스킬 사용 시 일정 확률로 마력 소모 0 + 대상 위치에 용암 폭발");
        }
    }

    public static DragonbloodWeapon CreateArmor(PlayerClass cls)
    {
        switch (cls)
        {
            case PlayerClass.Warrior: return new DragonbloodWeapon("용혈 파천중갑", cls, "방어 +90, 받는 피해 감소");
            case PlayerClass.Rogue: return new DragonbloodWeapon("용혈 섬광가죽갑", cls, "방어 +90, 은신 중 이동 속도 증가");
            case PlayerClass.Merchant: return new DragonbloodWeapon("용혈 만복비단포", cls, "방어 +90, 골드 획득 증가");
            default: return new DragonbloodWeapon("용혈 멸세제사장복", cls, "방어 +90, 보호막 강화");
        }
    }

    public IgnisReward GrantReward(string playerName, PlayerClass cls)
    {
        var r = new IgnisReward
        {
            gold = UnityEngine.Random.Range(goldRange.x, goldRange.y + 1),
            dragonScales = UnityEngine.Random.Range(scaleRange.x, scaleRange.y + 1),
        };
        if (UnityEngine.Random.Range(0f, 100f) < crystalRate)
            r.dragonCrystals = UnityEngine.Random.Range(0f, 100f) < 20f ? 2 : 1;

        // [0,100) 범위에 < 비교 (EnhanceSystem과 같은 규칙)
        // 초보자가 잡았다면 네 직업 중 하나의 장비 (진급 후 사용)
        if (cls == PlayerClass.Novice) cls = (PlayerClass)UnityEngine.Random.Range(1, 5);
        if (UnityEngine.Random.Range(0f, 100f) < dropRate)
        {
            r.weapon = CreateWeapon(cls);
            BroadcastLegendaryDrop(playerName, r.weapon.name);
        }
        if (UnityEngine.Random.Range(0f, 100f) < dropRate)
        {
            r.armor = CreateArmor(cls);
            BroadcastLegendaryDrop(playerName, r.armor.name);
        }
        return r;
    }

    public void BroadcastLegendaryDrop(string playerName, string weaponName)
    {
        string msg = $"[{playerName}] 님이 용혈의 제단에서 전설의 [{weaponName}]을(를) 토벌 보상으로 획득하셨습니다!";
        OnServerNotice?.Invoke(msg);
        ShowNotice(msg);
    }

    // 다른 플레이어의 공지를 서버에서 받았을 때도 이 함수를 호출합니다.
    public void ShowNotice(string msg)
    {
        noticeQueue.Enqueue(msg);
        if (!noticeRunning) StartCoroutine(RunNotices());
    }

    IEnumerator RunNotices()
    {
        noticeRunning = true;
        while (noticeQueue.Count > 0)
        {
            string msg = noticeQueue.Dequeue();
            if (noticeBar == null || noticeText == null) { Debug.Log("[서버 공지] " + msg); continue; }

            noticeBar.gameObject.SetActive(true);
            noticeText.text = "<color=#FFB040><b>서버 공지</b></color>  " + msg;
            if (noticeSound != null) noticeSound.Play();

            var rt = noticeText.rectTransform;
            float barW = noticeBar.rect.width;
            float textW = noticeText.preferredWidth;
            float x = barW;
            while (x > -textW)
            {
                x -= noticeSpeed * Time.unscaledDeltaTime;
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                yield return null;
            }
            noticeBar.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.3f);
        }
        noticeRunning = false;
    }

    // 철화 대장간 용혈 제련 (1형 → 9형). 재료가 모자라면 false.
    public static bool TryRefine(DragonbloodWeapon w, ref int scales, ref int crystals, ref long gold)
    {
        if (w == null || w.refine >= DragonbloodWeapon.MaxRefine) return false;
        if (scales < w.ScaleCost || crystals < w.CrystalCost || gold < w.GoldCost) return false;
        scales -= w.ScaleCost;
        crystals -= w.CrystalCost;
        gold -= w.GoldCost;
        w.refine++;
        return true;
    }
}
