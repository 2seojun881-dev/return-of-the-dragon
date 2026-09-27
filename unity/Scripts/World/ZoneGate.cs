using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.World
{
    /// <summary>
    /// 성문 트리거. 플레이어가 들어오면 대상 씬을 불러오고, 도착 지점 ID를 넘겨준다.
    /// 게이트 오브젝트에 BoxCollider(isTrigger)와 함께 붙인다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ZoneGate : MonoBehaviour
    {
        [Tooltip("불러올 씬 이름 (Build Settings에 등록되어 있어야 함)")]
        public string targetScene;
        [Tooltip("대상 씬에서 도착할 SpawnPoint ID")]
        public string targetSpawnId;
        [Tooltip("표시 이름 (예: 서문 · 야수 서식지 Lv.1~15)")]
        public string label;

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") || string.IsNullOrEmpty(targetScene)) return;
            SpawnPoint.pendingId = targetSpawnId;
            SceneManager.LoadScene(targetScene);
        }
    }
}
