using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.ClassSystem;

/// <summary>스킬창 한 줄: 아이콘 · 이름 · 설명 · 사용 버튼 · 쿨타임 표시</summary>
public class SkillRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Button useButton;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private Image cooldownFill;

    private SkillData skill;

    public void Bind(SkillData s, int level, bool learned, bool awakening, System.Action onUse)
    {
        skill = s;
        if (icon != null) { icon.sprite = s.SkillIcon; icon.color = learned ? Color.white : new Color(1f, 1f, 1f, 0.35f); }
        if (nameText != null)
            nameText.text = (awakening ? "<color=#b58aff>[각성]</color> " : "") + s.SkillName +
                            (s.Type == SkillType.Passive ? " <size=75%>(패시브)</size>" : "") + $" <size=75%>Lv.{s.RequiredLevel}</size>";
        if (descText != null) descText.text = s.GetDescriptionForLevel(level);
        bool usable = learned && s.Type != SkillType.Passive;
        if (useButton != null)
        {
            useButton.interactable = usable;
            useButton.onClick.RemoveAllListeners();
            useButton.onClick.AddListener(() => onUse());
        }
        if (buttonText != null) buttonText.text = !learned ? "미습득" : s.Type == SkillType.Passive ? "상시" : "사용";
    }

    public void UpdateCooldown(ClassCharacterController c)
    {
        if (cooldownFill == null || skill == null || skill.Cooldown <= 0f) return;
        cooldownFill.fillAmount = c.CooldownLeft(skill) / skill.Cooldown;
    }
}
