using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI subOptionsText;
    [SerializeField] private TextMeshProUGUI setEffectText;
    [SerializeField] private RectTransform tooltipRect;

    public void ShowTooltip(string name, string subOpts, string setOpts, Vector2 touchPosition)
    {
        itemNameText.text = name;
        subOptionsText.text = subOpts;
        setEffectText.text = setOpts;

        // UI Layout 갱신 후 위치 조정 (화면 밖으로 나가는 현상 방지)
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        
        // 터치 좌표 기준으로 피벗(Pivot) 변경 알고리즘 적용
        Vector2 pivot = new Vector2(
            touchPosition.x > Screen.width * 0.5f ? 1f : 0f,
            touchPosition.y > Screen.height * 0.5f ? 1f : 0f
        );
        tooltipRect.pivot = pivot;
        transform.position = touchPosition;
    }
}
