using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 씬 도착 지점. ZoneGate가 넘겨준 ID와 일치하면 씬 시작 시 플레이어를 이 위치로 옮긴다.
    /// isDefault 지점은 ID가 없을 때(첫 입장, 부활 등) 쓰인다.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        public static string pendingId;

        public string id;
        public bool isDefault;

        void Start()
        {
            bool match = string.IsNullOrEmpty(pendingId) ? isDefault : pendingId == id;
            if (!match) return;
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;   // CharacterController가 순간이동을 되돌리지 않도록
            player.transform.SetPositionAndRotation(transform.position, transform.rotation);
            if (cc != null) cc.enabled = true;
            pendingId = null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = isDefault ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 1f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 2f);
        }
    }
}
