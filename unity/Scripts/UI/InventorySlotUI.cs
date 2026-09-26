using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image rarityBorder;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private GameObject equippedBadge;
    [SerializeField] private GameObject heroicVfx;

    // 슬롯 바인딩 및 데이터 갱신 메서드
    public void SetSlot(Sprite icon, Color borderColors, int quantity, bool isEquipped, bool isHeroic)
    {
        iconImage.sprite = icon;
        rarityBorder.color = borderColors;
        quantityText.text = quantity > 1 ? quantity.ToString() : string.Empty;
        equippedBadge.SetActive(isEquipped);
        heroicVfx.SetActive(isHeroic);
    }
}
