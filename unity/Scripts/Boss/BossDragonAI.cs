using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 드래곤 공중 브레스 패턴: 비상 → 조준/충전 → 브레스 발사 → 착지 → 쿨타임 (코루틴).
/// 웹 프로토타입의 ignisAirBreath()와 같은 구성입니다 (높이 6.5, 충전 1.5초, 브레스 2.6초, 쿨타임 8초).
///
/// 연출 3단계
///  - 비상(Takeoff): 제자리 상승 + 날개짓 충격파 → 발밑 원형 먼지(takeoffDustVfx) + 지면 균열 데칼(groundCrackDecal)
///  - 충전(Charging): 목을 뒤로 젖힘 → 입 주변 화염 흡입(chargeGlowVfx) + 둔탁한 파장 SFX(chargeSound)
///  - 방출(Fire Breath): 머리를 지상으로 숙임 → 원뿔 화염(breathParticleVfx) + 연소 데칼(burnMarkPrefab)
/// 공중에서는 flyingModel(Meshy "flying pose" 드래곤)을 보여주고 groundModel을 숨길 수 있습니다.
/// </summary>
public class BossDragonAI : MonoBehaviour
{
    [Header("🎯 Target & Origin")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Transform mouthTransform;

    [Header("✈️ Flight Settings")]
    [SerializeField] private float flyHeight = 6.5f;       // 비상 높이
    [SerializeField] private float takeoffSpeed = 6f;      // 상승 속도
    [SerializeField] private float landingSpeed = 7f;      // 착지 속도
    [SerializeField] private float rotationSpeed = 3f;     // 회전 추적 속도 (브레스 중에는 breathTurnFactor 배)
    [SerializeField, Range(0f, 1f)] private float breathTurnFactor = 0.25f;

    [Header("🔥 Breath Attack Settings")]
    [SerializeField] private ParticleSystem breathParticleVfx; // 입에서 나가는 원뿔 화염 파티클
    [SerializeField] private float chargeDuration = 1.5f;      // 충전/조준 시간
    [SerializeField] private float breathDuration = 2.6f;      // 브레스 유지 시간
    [SerializeField] private float attackCooldown = 8.0f;      // 패턴 재사용 대기시간
    [SerializeField] private float breathRange = 20f;          // 지면 기준 사거리
    [SerializeField] private float breathHalfAngle = 12f;      // 원뿔 반각 (도)
    [SerializeField] private float breathDamagePerTick = 70f;  // 0.3초마다
    [SerializeField] private float landingDamage = 150f;       // 착지 충격파
    [SerializeField] private float landingRadius = 8f;

    [Header("🎆 Stage VFX (optional)")]
    [SerializeField] private ParticleSystem takeoffDustVfx;       // 비상: 발밑 원형 먼지
    [SerializeField] private GameObject groundCrackDecal;         // 비상: 지면 균열 데칼 프리팹
    [SerializeField] private ParticleSystem chargeGlowVfx;        // 충전: 입 주변 화염 흡입
    [SerializeField] private GameObject burnMarkPrefab;           // 방출: 바닥 연소 자국 프리팹
    [SerializeField] private ParticleSystem landingShockwaveVfx;  // 착지 충격파
    [SerializeField] private GameObject groundModel;              // 지상 모델
    [SerializeField] private GameObject flyingModel;              // 공중 모델 (Meshy flying pose)

    [Header("🎬 Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip breathSound;
    [SerializeField] private AudioClip chargeSound;

    private Vector3 originalGroundPosition;
    private bool isExecutingPattern = false;
    public bool IsAirborne { get; private set; }

    // 애니메이터 파라미터 캐싱
    private static readonly int AnimTakeoff = Animator.StringToHash("Takeoff");
    private static readonly int AnimBreath = Animator.StringToHash("Breath");
    private static readonly int AnimLand = Animator.StringToHash("Land");

    private void Start()
    {
        // 플레이어 자동 탐색 (Inspector 지정 안 되어있을 시)
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTarget = player.transform;
        }
        SetFlyingModel(false);
    }

    /// <summary>외부(BT, FSM, 또는 테스트)에서 브레스 패턴을 트리거할 때 호출</summary>
    public void ExecuteAirBreathPattern()
    {
        if (!isExecutingPattern) StartCoroutine(AirBreathRoutine());
    }

    private IEnumerator AirBreathRoutine()
    {
        isExecutingPattern = true;
        originalGroundPosition = transform.position;

        // 1단계: 공중 비상 (Takeoff)
        if (animator != null) animator.SetTrigger(AnimTakeoff);
        if (takeoffDustVfx != null) takeoffDustVfx.Play();
        if (groundCrackDecal != null) Destroy(Instantiate(groundCrackDecal, originalGroundPosition, Quaternion.identity), 6f);
        SetFlyingModel(true);
        IsAirborne = true;
        Vector3 targetAirPosition = originalGroundPosition + Vector3.up * flyHeight;
        while (Vector3.Distance(transform.position, targetAirPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetAirPosition, takeoffSpeed * Time.deltaTime);
            RotateTowardsPlayer(1f);
            yield return null;
        }

        // 2단계: 플레이어 조준 및 기 모으기 (Aiming & Charging)
        if (chargeGlowVfx != null) chargeGlowVfx.Play();
        if (audioSource != null && chargeSound != null) audioSource.PlayOneShot(chargeSound);
        float chargeTimer = 0f;
        while (chargeTimer < chargeDuration)
        {
            chargeTimer += Time.deltaTime;
            RotateTowardsPlayer(1f);
            AimMouth();
            yield return null;
        }
        if (chargeGlowVfx != null) chargeGlowVfx.Stop();

        // 3단계: 화염 브레스 발사 (Fire Breath)
        if (animator != null) animator.SetTrigger(AnimBreath);
        if (breathParticleVfx != null) breathParticleVfx.Play();
        if (audioSource != null && breathSound != null) audioSource.PlayOneShot(breathSound);
        float breathTimer = 0f, tick = 0f, burnTimer = 0f;
        while (breathTimer < breathDuration)
        {
            breathTimer += Time.deltaTime;
            RotateTowardsPlayer(breathTurnFactor); // 브레스 중에는 천천히 추적
            AimMouth();
            tick -= Time.deltaTime;
            if (tick <= 0f) { tick = 0.3f; if (PlayerInBreathCone()) DamagePlayer(breathDamagePerTick); }
            burnTimer -= Time.deltaTime;
            if (burnTimer <= 0f && burnMarkPrefab != null)
            {
                burnTimer = 0.25f;
                Vector3 fwd = Flat(transform.forward);
                Vector3 p = originalGroundPosition + fwd * Random.Range(breathRange * 0.25f, breathRange);
                Destroy(Instantiate(burnMarkPrefab, p, Quaternion.identity), 4f);
            }
            yield return null;
        }
        if (breathParticleVfx != null) breathParticleVfx.Stop();

        // 4단계: 지면 착지 (Landing)
        if (animator != null) animator.SetTrigger(AnimLand);
        while (Vector3.Distance(transform.position, originalGroundPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, originalGroundPosition, landingSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = originalGroundPosition;
        IsAirborne = false;
        SetFlyingModel(false);
        if (landingShockwaveVfx != null) landingShockwaveVfx.Play();
        if (playerTarget != null && Vector3.Distance(Flat(playerTarget.position), Flat(transform.position)) < landingRadius)
            DamagePlayer(landingDamage);

        // 5단계: 쿨타임 대기 (Cooldown)
        yield return new WaitForSeconds(attackCooldown);
        isExecutingPattern = false;
    }

    /// <summary>플레이어 위치를 바라보도록 수평 회전 (높이 차이는 무시해서 몸이 기울지 않게)</summary>
    private void RotateTowardsPlayer(float factor)
    {
        if (playerTarget == null) return;
        Vector3 direction = Flat(playerTarget.position - transform.position);
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * factor * Time.deltaTime);
    }

    /// <summary>입(과 브레스 파티클)을 지상의 플레이어 쪽으로 숙임</summary>
    private void AimMouth()
    {
        if (mouthTransform == null || playerTarget == null) return;
        mouthTransform.LookAt(playerTarget.position);
    }

    private bool PlayerInBreathCone()
    {
        if (playerTarget == null) return false;
        Vector3 toPlayer = Flat(playerTarget.position - originalGroundPosition);
        if (toPlayer.magnitude > breathRange) return false;
        return Vector3.Angle(Flat(transform.forward), toPlayer) <= breathHalfAngle;
    }

    private void DamagePlayer(float amount)
    {
        // 플레이어 쪽에 TakeDamage(float) 메서드가 있으면 받습니다 (의존성 없이 연결).
        playerTarget.SendMessage("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
    }

    private void SetFlyingModel(bool flying)
    {
        if (flyingModel == null) return;
        flyingModel.SetActive(flying);
        if (groundModel != null) groundModel.SetActive(!flying);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // 에디터 테스트용 Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 airPos = transform.position + Vector3.up * flyHeight;
        Gizmos.DrawLine(transform.position, airPos);
        Gizmos.DrawWireSphere(airPos, 1f);
        Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
        Vector3 fwd = Flat(transform.forward);
        Gizmos.DrawLine(Flat(transform.position), Flat(transform.position) + fwd * breathRange);
    }
}
