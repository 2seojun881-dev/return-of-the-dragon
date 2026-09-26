using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 직업별 스탯 (전사: HP/STR, 도적: DEX/CRI, 상인: GOLD_BONUS/LUK, 신: MP/INT)
[Serializable]
public class PlayerStats
{
    public int level = 1;
    public float maxHp = 230f;
    public float maxMp = 100f;
    public float str = 10f;          // 힘: 물리 공격력
    public float dex = 10f;          // 민첩: 공격·이동 속도
    public float intel = 10f;        // 지능: 주술 위력, 회복량
    public float luk = 10f;          // 행운: 드롭률
    public float critChance = 0.12f; // 치명타 확률 (0~1)
    public float goldBonus = 0f;     // 추가 골드 획득률 (0.25 = +25%)
}

/// <summary>
/// 레벨 15 진급(전직) 시스템.
/// PromoteClass(newClass): 레벨 15 이상 + 현재 직업이 Novice일 때만 진급 가능.
/// 진급하면 직업별 스탯 보너스를 지급하고 UI 알림을 띄웁니다.
/// </summary>
public class ClassPromotion : MonoBehaviour
{
    [Header("플레이어 상태")]
    public PlayerClass currentClass = PlayerClass.Novice;
    public PlayerStats stats = new PlayerStats();

    [Header("UI 알림")]
    public GameObject noticePanel;     // 화면 중앙 알림 패널
    public Text noticeText;
    public float noticeSeconds = 3f;
    public AudioSource promoteSound;

    // 진급 성공 시 (이전 직업, 새 직업). 스킬 해금·장비 지급·모델 교체 등을 여기에 연결합니다.
    public event Action<PlayerClass, PlayerClass> OnPromoted;

    Coroutine noticeRoutine;

    /// <summary>진급 가능 여부와 불가 사유를 반환합니다.</summary>
    public bool CanPromote(PlayerClass newClass, out string reason)
    {
        if (newClass == PlayerClass.Novice) { reason = "초보자로는 진급할 수 없습니다."; return false; }
        if (currentClass != PlayerClass.Novice) { reason = $"이미 [{currentClass.KoreanName()}](으)로 진급했습니다. 진급은 한 번만 할 수 있습니다."; return false; }
        if (stats.level < PlayerClassInfo.PromotionLevel) { reason = $"레벨 {PlayerClassInfo.PromotionLevel} 이상만 진급할 수 있습니다. (현재 Lv.{stats.level})"; return false; }
        reason = null;
        return true;
    }

    public bool PromoteClass(PlayerClass newClass)
    {
        if (!CanPromote(newClass, out string reason))
        {
            ShowNotice(reason);
            Debug.LogWarning("[진급 실패] " + reason);
            return false;
        }

        PlayerClass before = currentClass;
        currentClass = newClass;
        string bonus = ApplyClassBonus(newClass);

        ShowNotice($"진급 완료! [{newClass.KoreanName()}]\n{bonus}");
        if (promoteSound != null) promoteSound.Play();
        OnPromoted?.Invoke(before, newClass);
        return true;
    }

    // 직업별 보너스 (웹 프로토타입과 같은 비율)
    string ApplyClassBonus(PlayerClass c)
    {
        switch (c)
        {
            case PlayerClass.Warrior:
                stats.maxHp *= 1.40f; stats.str *= 1.10f;
                return "최대 체력 +40% · 힘 +10%";
            case PlayerClass.Rogue:
                stats.dex *= 1.45f; stats.critChance = Mathf.Max(stats.critChance, 0.28f);
                return "민첩 +45% · 치명타 확률 28%";
            case PlayerClass.Merchant:
                stats.goldBonus += 0.25f; stats.luk *= 1.10f;
                return "골드 획득 +25% · 행운 +10%";
            case PlayerClass.Shaman:
                stats.maxMp *= 2.20f; stats.intel *= 1.23f;
                return "최대 마력 +120% · 지능 +23%";
        }
        return "";
    }

    public void ShowNotice(string msg)
    {
        if (noticePanel == null || noticeText == null) { Debug.Log("[알림] " + msg); return; }
        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeRoutine = StartCoroutine(NoticeRoutine(msg));
    }

    IEnumerator NoticeRoutine(string msg)
    {
        noticeText.text = msg;
        noticePanel.SetActive(true);
        yield return new WaitForSecondsRealtime(noticeSeconds);
        noticePanel.SetActive(false);
        noticeRoutine = null;
    }
}
