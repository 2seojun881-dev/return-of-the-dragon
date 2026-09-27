#if MIRROR
using Mirror;
using UnityEngine;
using Game.ClassSystem;

namespace Game.Networking
{
    /// <summary>
    /// 플레이어 직업/스탯 동기화 (Mirror). 서버가 권위를 가집니다.
    ///  - ClassData 에셋 대신 ClassId 문자열과 필요한 수치(float)만 보냅니다 → 클라이언트가 에셋을 못 찾아 스탯 0/투명화되는 문제 방지
    ///  - 힐·피해·신성 방패는 모두 서버에서 계산하고 SyncVar 로 모든 화면에 같은 값을 보여 줍니다 (Desync 방지)
    /// 플레이어 프리팹에 ClassCharacterController 와 함께 붙입니다.
    /// </summary>
    [RequireComponent(typeof(ClassCharacterController))]
    public class NetworkedClassManager : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnClassIdSynced))] private string networkedClassId = "CLASS_NONE";
        [SyncVar(hook = nameof(OnLevelSynced))] private int networkedLevel = 1;

        [SyncVar] public float currentSyncedMaxHp;
        [SyncVar] public float currentSyncedMaxMp;
        [SyncVar(hook = nameof(OnVitalsSynced))] public float currentSyncedHp;
        [SyncVar(hook = nameof(OnVitalsSynced))] public float currentSyncedMp;
        [SyncVar] private double shieldUntil;      // NetworkTime 기준 신성 방패 만료 시각
        [SyncVar] private float shieldMitigation;

        public string ClassId => networkedClassId;
        public int Level => networkedLevel;
        public bool IsDead => currentSyncedHp <= 0f;
        public bool HasDivineShield => NetworkTime.time < shieldUntil;
        public bool IsGrandOracle => networkedClassId == OracleForesight.GrandOracleId;

        private ClassCharacterController controller;
        private void Awake() => controller = GetComponent<ClassCharacterController>();

        // ---------------------------------------------------------------- 초기화 / 각성

        /// <summary>로컬 플레이어가 캐릭터 선택 후 호출</summary>
        [Command]
        public void CmdInitializeClass(string classId, int level)
        {
            var data = ClassDatabase.Instance != null ? ClassDatabase.Instance.Find(classId) : null;
            if (data == null) { Debug.LogWarning($"알 수 없는 직업 ID: {classId}"); return; }
            ServerApply(data, Mathf.Clamp(level, 1, 99));
        }

        /// <summary>Lv.99 에서 각성 (서버가 조건 검사)</summary>
        [Command]
        public void CmdAwaken()
        {
            var db = ClassDatabase.Instance; if (db == null) return;
            var current = db.Find(networkedClassId);
            if (current == null || current.IsAwakened || networkedLevel < 99) return;
            var awakened = db.FindAwakeningOf(current);
            if (awakened != null) ServerApply(awakened, networkedLevel);
        }

        [Server]
        private void ServerApply(ClassData data, int level)
        {
            controller.ApplyClass(data, level);
            networkedClassId = data.ClassId;
            networkedLevel = level;
            currentSyncedMaxHp = controller.GetStat(StatType.MaxHp);
            currentSyncedMaxMp = controller.GetStat(StatType.MaxMp);
            currentSyncedHp = currentSyncedMaxHp;
            currentSyncedMp = currentSyncedMaxMp;
            // 전체 스탯 표는 주인 클라이언트에게만 (UI 표시용)
            var types = (StatType[])System.Enum.GetValues(typeof(StatType));
            var values = new float[types.Length];
            for (int i = 0; i < types.Length; i++) values[i] = controller.GetStat(types[i]);
            if (connectionToClient != null) TargetRpcInitializeStats(connectionToClient, types, values);
        }

        [TargetRpc]
        private void TargetRpcInitializeStats(NetworkConnectionToClient target, StatType[] types, float[] values)
        {
            for (int i = 0; i < types.Length && i < values.Length; i++) controller.SetStatFromNetwork(types[i], values[i]);
        }

        private void OnClassIdSynced(string oldId, string newId)
        {
            if (isServer) return;   // 서버는 이미 적용함
            var data = ClassDatabase.Instance != null ? ClassDatabase.Instance.Find(newId) : null;
            if (data != null) controller.ApplyClass(data, networkedLevel);   // 모델/스킬 목록은 각자 에셋으로, 수치는 SyncVar 로
        }

        private void OnLevelSynced(int oldLv, int newLv) => OnClassIdSynced(networkedClassId, networkedClassId);
        private void OnVitalsSynced(float oldV, float newV) => controller.SetVitalsFromNetwork(currentSyncedHp, currentSyncedMp);

        // ---------------------------------------------------------------- 스킬 / 힐 / 피해

        /// <summary>스킬 사용 요청. 힐 계열은 target 에게 서버가 적용합니다.</summary>
        [Command]
        public void CmdUseSkill(int index, int skillLevel, NetworkIdentity target)
        {
            if (IsDead) return;
            var skills = controller.Skills;
            if (index < 0 || index >= skills.Count) return;
            var skill = skills[index];
            if (currentSyncedMp < skill.ManaCost || controller.CooldownLeft(skill) > 0f) return;
            if (!controller.UseSkill(index, skillLevel)) return;
            currentSyncedMp -= skill.ManaCost;
            if (skill.Attribute == SkillAttribute.Heal)
            {
                var t = target != null ? target.GetComponent<NetworkedClassManager>() : this;
                if (t != null && Vector3.Distance(t.transform.position, transform.position) <= Mathf.Max(skill.Range, 12f))
                    t.ServerHeal(t.currentSyncedMaxHp * skill.GetValue(skillLevel) / 100f);
            }
            RpcSkillFx(index);
        }

        [ClientRpc]
        private void RpcSkillFx(int index)
        {
            var skills = controller.Skills;
            if (index < 0 || index >= skills.Count) return;
            var s = skills[index];
            if (s.EffectPrefab != null) Instantiate(s.EffectPrefab, transform.position, transform.rotation);
            if (s.CastSound != null) AudioSource.PlayClipAtPoint(s.CastSound, transform.position);
        }

        [Server]
        public void ServerHeal(float amount)
        {
            if (IsDead) return;
            currentSyncedHp = Mathf.Min(currentSyncedMaxHp, currentSyncedHp + amount);   // SyncVar → 모든 화면에서 같은 체력
        }

        [Server]
        public float ServerTakeDamage(float amount)
        {
            if (IsDead) return 0f;
            if (HasDivineShield) amount *= 1f - shieldMitigation;
            float def = controller.GetStat(StatType.Defense);
            float dealt = Mathf.Max(1f, amount * (100f / (100f + def * 4f)));
            currentSyncedHp = Mathf.Max(0f, currentSyncedHp - dealt);
            return dealt;
        }

        [Server]
        public void ServerGrantDivineShield(float duration, float mitigation)
        {
            shieldUntil = System.Math.Max(shieldUntil, NetworkTime.time + duration);
            shieldMitigation = Mathf.Max(shieldMitigation, mitigation);
            RpcDivineShieldFx(duration);
        }

        [Server]
        public bool ServerSpendMana(float amount)
        {
            if (currentSyncedMp < amount) return false;
            currentSyncedMp -= amount; return true;
        }

        [ClientRpc]
        private void RpcDivineShieldFx(float duration)
        {
            controller.ActivateDivineShield(duration, shieldMitigation);   // 로컬 표시용 (실제 감소는 서버 계산)
        }
    }
}
#endif
