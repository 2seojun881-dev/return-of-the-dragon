#if MIRROR
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Game.ClassSystem;

namespace Game.Networking
{
    /// <summary>
    /// 드래곤 보스 멀티플레이 동기화 (Mirror, 서버 권위). BossDragonAI 와 같은 오브젝트에 붙입니다.
    ///  - 브레스 피해: 서버가 0.3초마다 원뿔 안의 모든 플레이어를 판정 (BossDragonAI.serverDrivenDamage = true)
    ///  - 신탁의 예지: 브레스 예고 순간 살아 있는 그랜드 오라클이 있으면 모든 클라이언트에 안전지대 RPC
    ///  - 신성 방패: 서버가 브레스를 결정한 순간(예고 시점) 바로 파티 전원에게 부여 → 피해가 먼저 도착해 전멸하던 문제 해결
    /// </summary>
    [RequireComponent(typeof(BossDragonAI))]
    public class NetworkedBossSync : NetworkBehaviour
    {
        public float breathDamagePerTick = 60f;
        [Range(0f, 1f)] public float divineShieldMitigation = 0.8f;
        public float divineShieldManaCost = 30f;
        public GameObject safeZonePrefab;

        private BossDragonAI boss;
        private Coroutine breathLoop;

        private void Awake() => boss = GetComponent<BossDragonAI>();

        public override void OnStartServer()
        {
            boss.serverDrivenDamage = true;
            boss.OnBreathForecast += ServerOnForecast;
            boss.OnBreathStarted += ServerOnBreathStarted;
            boss.OnBreathEnded += ServerOnBreathEnded;
        }

        public override void OnStopServer()
        {
            boss.OnBreathForecast -= ServerOnForecast;
            boss.OnBreathStarted -= ServerOnBreathStarted;
            boss.OnBreathEnded -= ServerOnBreathEnded;
        }

        private static IEnumerable<NetworkedClassManager> Players()
        {
            foreach (var id in NetworkServer.spawned.Values)
            {
                var p = id != null ? id.GetComponent<NetworkedClassManager>() : null;
                if (p != null && !p.IsDead) yield return p;
            }
        }

        [Server]
        private void ServerOnForecast(BossDragonAI b, float secondsToBreath)
        {
            NetworkedClassManager oracle = null;
            foreach (var p in Players()) if (p.IsGrandOracle) { oracle = p; break; }
            if (oracle == null) return;
            RpcForesight(b.BreathOrigin, b.BreathForward, b.BreathRange, b.BreathHalfAngle, oracle.transform.position, secondsToBreath + b.BreathDuration);
            if (oracle.ServerSpendMana(divineShieldManaCost))
                foreach (var p in Players()) p.ServerGrantDivineShield(secondsToBreath + b.BreathDuration + 0.4f, divineShieldMitigation);
        }

        [ClientRpc]
        private void RpcForesight(Vector3 origin, Vector3 forward, float range, float halfAngle, Vector3 aim, float lifetime)
        {
            OracleForesight.ShowSafeZones(origin, forward, range, halfAngle, aim, lifetime, safeZonePrefab, 3f);
        }

        [Server]
        private void ServerOnBreathStarted(BossDragonAI b)
        {
            if (breathLoop != null) StopCoroutine(breathLoop);
            breathLoop = StartCoroutine(BreathDamage(b));
        }

        [Server]
        private void ServerOnBreathEnded(BossDragonAI b)
        {
            if (breathLoop != null) { StopCoroutine(breathLoop); breathLoop = null; }
        }

        private IEnumerator BreathDamage(BossDragonAI b)
        {
            var wait = new WaitForSeconds(0.3f);
            while (true)
            {
                foreach (var p in Players()) if (b.IsInBreathCone(p.transform.position)) p.ServerTakeDamage(breathDamagePerTick);
                yield return wait;
            }
        }
    }
}
#endif
