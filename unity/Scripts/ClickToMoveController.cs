using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

/// <summary>
/// 쿼터뷰 터치/클릭 이동 (NavMeshAgent).
/// - 바닥 터치: 해당 지점으로 이동 + 클릭 파티클
/// - 몬스터 터치: 공격 사거리까지 접근 후 멈추고 공격 애니메이션
/// - UI(스킬 버튼 등) 위의 터치는 무시 (EventSystem)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ClickToMoveController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Animator animator;
    public ParticleSystem clickEffectPrefab;

    [Header("Layers")]
    public LayerMask groundMask;
    public LayerMask enemyMask;

    [Header("Combat")]
    public float attackRange = 2.2f;
    public float attackInterval = 0.95f;

    private NavMeshAgent agent;
    private Transform target;
    private float attackTimer;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackHash = Animator.StringToHash("Attack");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        HandleInput();
        HandleCombat();
        if (animator) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
    }

    void HandleInput()
    {
        Vector2 screenPos;
        int pointerId = -1; // 마우스

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase != TouchPhase.Began) return;
            screenPos = t.position;
            pointerId = t.fingerId;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
        }
        else return;

        // UI(스킬 버튼 등)를 터치한 경우 이동 로직 무시
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId)) return;

        Ray ray = cam.ScreenPointToRay(screenPos);

        // 1) 몬스터 터치 → 타겟 지정
        if (Physics.Raycast(ray, out RaycastHit enemyHit, 200f, enemyMask))
        {
            target = enemyHit.transform;
            agent.stoppingDistance = attackRange;
            agent.SetDestination(target.position);
            return;
        }

        // 2) 바닥 터치 → 이동 + 파티클
        if (Physics.Raycast(ray, out RaycastHit groundHit, 200f, groundMask))
        {
            target = null;
            agent.stoppingDistance = 0.1f;
            if (NavMesh.SamplePosition(groundHit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
                if (clickEffectPrefab)
                {
                    ParticleSystem fx = Instantiate(clickEffectPrefab, navHit.position + Vector3.up * 0.05f, Quaternion.identity);
                    Destroy(fx.gameObject, 1.5f);
                }
            }
        }
    }

    void HandleCombat()
    {
        attackTimer -= Time.deltaTime;
        if (target == null) return;
        if (!target.gameObject.activeInHierarchy) { target = null; return; }

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > attackRange + 0.1f)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position); // 움직이는 몬스터 추적
            return;
        }

        // 사거리 안: 멈추고 바라보며 공격
        agent.isStopped = true;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 12f);

        if (attackTimer <= 0f)
        {
            attackTimer = attackInterval;
            if (animator) animator.SetTrigger(AttackHash);
        }
    }

    /// <summary>우측 하단 [공격] 버튼: 가장 가까운 적 자동 타겟팅</summary>
    public void OnAttackButton(float searchRadius = 12f)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, enemyMask);
        float best = float.MaxValue;
        Transform nearest = null;
        foreach (var h in hits)
        {
            float d = (h.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = h.transform; }
        }
        if (nearest == null) return;
        target = nearest;
        agent.isStopped = false;
        agent.stoppingDistance = attackRange;
    }
}
