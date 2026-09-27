using System.Collections.Generic;
using UnityEngine;

namespace Game.ClassSystem
{
    /// <summary>
    /// 그랜드 오라클 전용 (싱글플레이):
    ///  - 신탁의 예지: 용이 이륙해 브레스를 예고하는 순간(브레스 약 2초 전) 원뿔 밖의 안전지대를 바닥에 표시
    ///  - 신성 방패: 브레스가 시작되는 순간 반경 안의 아군 모두에게 피해 감소(기본 80%)
    /// 멀티플레이에서는 NetworkedBossSync 가 서버에서 같은 일을 하고 ShowSafeZones 만 각 클라이언트에서 호출합니다.
    /// </summary>
    [RequireComponent(typeof(ClassCharacterController))]
    public class OracleForesight : MonoBehaviour
    {
        public const string GrandOracleId = "CLASS_GRAND_ORACLE";

        [Header("신탁의 예지")]
        [Tooltip("안전지대 표시 프리팹 (초록 원). 비워 두면 기본 원판을 만듭니다.")]
        public GameObject safeZonePrefab;
        public float safeZoneMargin = 3f;

        [Header("신성 방패")]
        [Range(0f, 1f)] public float mitigation = 0.8f;
        public float manaCost = 30f;
        public float partyRadius = 25f;

        private ClassCharacterController me;
        private readonly List<BossDragonAI> bosses = new List<BossDragonAI>();

        private void Awake() => me = GetComponent<ClassCharacterController>();

        private void OnEnable()
        {
            foreach (var b in FindObjectsOfType<BossDragonAI>())
            {
                b.OnBreathForecast += OnForecast; b.OnBreathStarted += OnBreathStarted; bosses.Add(b);
            }
        }

        private void OnDisable()
        {
            foreach (var b in bosses) if (b != null) { b.OnBreathForecast -= OnForecast; b.OnBreathStarted -= OnBreathStarted; }
            bosses.Clear();
        }

        private bool IsOracle => me != null && me.ClassData != null && me.ClassData.ClassId == GrandOracleId && me.CurrentHp > 0f;

        private void OnForecast(BossDragonAI boss, float secondsToBreath)
        {
            if (!IsOracle) return;
            ShowSafeZones(boss.BreathOrigin, boss.BreathForward, boss.BreathRange, boss.BreathHalfAngle,
                          transform.position, secondsToBreath + boss.BreathDuration, safeZonePrefab, safeZoneMargin);
        }

        private void OnBreathStarted(BossDragonAI boss)
        {
            if (!IsOracle || me.CurrentMp < manaCost) return;
            foreach (var ally in FindObjectsOfType<ClassCharacterController>())
                if (Vector3.Distance(ally.transform.position, transform.position) <= partyRadius)
                    ally.ActivateDivineShield(boss.BreathDuration + 0.4f, mitigation);
        }

        /// <summary>브레스 원뿔 바깥의 안전지대 3곳 (조준 대상의 양옆, 용의 뒤)을 표시합니다.</summary>
        public static void ShowSafeZones(Vector3 origin, Vector3 forward, float range, float halfAngle, Vector3 aimPoint,
                                         float lifetime, GameObject prefab, float margin)
        {
            forward.y = 0f; forward.Normalize();
            Vector3 side = new Vector3(forward.z, 0f, -forward.x);
            float dist = Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(aimPoint.x, 0f, aimPoint.z));
            float halfWidth = Mathf.Tan(halfAngle * Mathf.Deg2Rad) * Mathf.Max(dist, 1f);
            var spots = new[] { aimPoint + side * (halfWidth + margin), aimPoint - side * (halfWidth + margin), origin - forward * (margin + 4f) };
            foreach (var p in spots)
            {
                var pos = new Vector3(p.x, origin.y + 0.05f, p.z);
                GameObject z;
                if (prefab != null) z = Instantiate(prefab, pos, Quaternion.identity);
                else
                {
                    z = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    z.transform.position = pos; z.transform.localScale = new Vector3(3.5f, 0.02f, 3.5f);
                    var col = z.GetComponent<Collider>(); if (col != null) Destroy(col);
                    z.GetComponent<Renderer>().material.color = new Color(0.35f, 1f, 0.6f, 0.6f);
                }
                Destroy(z, lifetime);
            }
        }
    }
}
