#if MIRROR
using Mirror;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// 월드 공용 퀘스트 진행도 (서버 권위). 씬에 하나 두고 NetworkIdentity 를 붙입니다.
    /// 처치는 서버에서만 ReportKill 로 올리고, 모든 클라이언트는 같은 SyncDictionary 를 읽습니다
    /// → 누가 잡았든 모두의 퀘스트 목표가 똑같이 달성됩니다.
    /// </summary>
    public class NetworkQuestTracker : NetworkBehaviour
    {
        public static NetworkQuestTracker Instance { get; private set; }

        public readonly SyncDictionary<string, int> kills = new SyncDictionary<string, int>();

        public event System.Action<string, int> OnProgressChanged;

        private void Awake() => Instance = this;

        public override void OnStartClient()
        {
            kills.Callback += (op, key, value) => OnProgressChanged?.Invoke(key, value);
        }

        [Server]
        public void ReportKill(string mobId)
        {
            kills.TryGetValue(mobId, out int n);
            kills[mobId] = n + 1;
        }

        public int GetKills(string mobId) => kills.TryGetValue(mobId, out int n) ? n : 0;
    }
}
#endif
