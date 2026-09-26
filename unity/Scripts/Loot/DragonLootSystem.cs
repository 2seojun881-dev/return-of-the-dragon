using UnityEngine;
using Game.Core;

namespace Game.LootSystem
{
    public class DragonLootSystem : MonoBehaviour
    {
        [Header("=== 드래곤 드롭 확률 (%) ===")]
        [Range(0f, 100f)] public float heroicRate = 1.0f;     // 영웅 (1%)
        [Range(0f, 100f)] public float legendaryRate = 9.0f;  // 전설 (9%)
        [Range(0f, 100f)] public float advancedRate = 90.0f;  // 고급 (90%)

        public ItemRarity DropDragonEquipment()
        {
            float randomValue = Random.Range(0f, 100f);

            // 1. 영웅 등급 판정
            if (randomValue <= heroicRate)
            {
                OnGetHeroicItem();
                return ItemRarity.Heroic;
            }

            // 2. 전설 등급 판정
            if (randomValue <= (heroicRate + legendaryRate))
            {
                OnGetLegendaryItem();
                return ItemRarity.Legendary;
            }

            // 3. 고급 등급 판정
            OnGetAdvancedItem();
            return ItemRarity.Advanced;
        }

        private void OnGetHeroicItem()
        {
            Debug.Log("<color=#FF2222>[전서구] 대륙에 영웅의 장비가 모습을 드러냈습니다! (영웅 등급 획득!)</color>");
        }

        private void OnGetLegendaryItem()
        {
            Debug.Log("<color=#FFD700>[보상] 황금빛 기운이 감도는 전설 장비를 획득했습니다!</color>");
        }

        private void OnGetAdvancedItem()
        {
            Debug.Log("<color=#3388FF>[보상] 푸른 마력이 깃든 고급 장비를 획득했습니다.</color>");
        }
    }
}
