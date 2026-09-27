using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.ClassSystem;

/// <summary>
/// 스킬창 (UGUI). 캐릭터 직업의 전체 스킬(부모 직업 스킬 + 각성 전용 스킬)을 줄마다 표시하고,
/// 배운 스킬은 버튼으로 UseSkill(index, level) 을 호출합니다. 멀티플레이에서는 NetworkedClassManager.CmdUseSkill 로 보냅니다.
/// 사용법: ScrollView Content 를 listRoot 에, 아이콘·이름·설명·버튼이 있는 줄 프리팹을 rowPrefab 에 연결.
/// </summary>
public class SkillWindowUI : MonoBehaviour
{
    [SerializeField] private ClassCharacterController character;
    [SerializeField] private Transform listRoot;
    [SerializeField] private SkillRowUI rowPrefab;
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("스킬 레벨 (마스터 전까지 1)")]
    [SerializeField] private int skillLevel = 1;

    private readonly List<SkillRowUI> rows = new List<SkillRowUI>();

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        foreach (var r in rows) if (r != null) Destroy(r.gameObject);
        rows.Clear();
        if (character == null || character.ClassData == null) return;
        var data = character.ClassData;
        if (titleText != null)
            titleText.text = data.IsAwakened ? $"{data.ClassName} <size=70%>(각성 · {data.parentClassData.ClassName})</size>" : data.ClassName;

        var unlocked = character.Skills;
        foreach (var skill in data.GetAllSkills())
        {
            var row = Instantiate(rowPrefab, listRoot);
            int index = IndexOf(unlocked, skill);
            bool awakening = data.awakeningSkills.Contains(skill);
            row.Bind(skill, skillLevel, index >= 0, awakening, () => Use(index));
            rows.Add(row);
        }
    }

    private static int IndexOf(IReadOnlyList<SkillData> list, SkillData s)
    {
        for (int i = 0; i < list.Count; i++) if (list[i] == s) return i;
        return -1;
    }

    private void Use(int index)
    {
        if (index < 0) return;
#if MIRROR
        var net = character.GetComponent<Game.Networking.NetworkedClassManager>();
        if (net != null && net.isClient) { net.CmdUseSkill(index, skillLevel, null); return; }
#endif
        character.UseSkill(index, skillLevel);   // 예: UseSkill(0, 1) → 다크 나이트 '마이티 스윙' 1레벨
    }

    private void Update()
    {
        foreach (var r in rows) if (r != null) r.UpdateCooldown(character);
    }
}
