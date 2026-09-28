# 용의 바다 — 엔드게임 기획 (게임 적용판)

## 월드 구조
- **서쪽 사냥터 (천명 성채 서문)** — 서쪽으로 갈수록 강해지는 한 줄 연결
  - 야수 서식지 Lv.1~15 → 원혼의 묘지 Lv.15~40 → 태엽 미로 Lv.40~70 → 아수라 흑야 협곡 Lv.70~99 → 용혈의 제단 → 멸세의 둥지
  - 각 사냥터는 동쪽이 입구, 서쪽 끝이 다음 사냥터로 가는 출구 (원혼의 묘지는 좌우 반전, 태엽 미로 · 아수라 협곡은 90° 회전)
- **동문 → 천명 항구** — 선장 하람에게서 출항 (Lv.90 이상)

## 1. 영원의 방주 호위전 (해상 디펜스 레이드)
- 배 위 전투. 방주 내구도가 0이 되면 실패하고 천명 항구로 돌아감
- 1단계 · 심해의 습격: 갑판 양옆으로 기어오르는 **심해의 기어오르는 자** 무리가 주돛대를 노림
  (방주 내구도 감소), 10초마다 **심해 촉수 내려치기** (붉은 띠 표시)
- 2단계 · **심해의 인어 괴물 세이렌** 부상 (70초 또는 7파)
  - 먹물 (원형 장판 · 둔화)
  - 뱃머리 물어뜯기: 7초간 약점 노출, 받는 피해 +50%
  - **뱃머리 대포**: 옆에 1.2초 서 있으면 장전 → 자동 포격 (최대 체력 5%)
- 3단계 · 체력 30% 이하: 촉수가 5초마다 내려침
- 대폭풍: 8분이 지나면 매초 방주 -1%
- 촉수를 잘라내면 세이렌 최대 체력 4% 피해, 7초 안에 못 자르면 방주를 조여 -4%
- 직업 역할
  - **전사**: 촉수 · 물어뜯기 표시 안에서 몸으로 막으면 방주 피해가 없음 (분노 획득), 갑판 괴물이 더 멀리서 전사에게 달려듦
  - **도적**: 갑판 괴물 · 촉수 피해 +50%, 대포 피해 2배 (10%)
  - **신관**: 스킬마다 방주에 신성 결계 +6% (최대 30%, 10초마다 +2%), 결계가 방주 피해를 먼저 흡수 (황금 돔)
- 승리: 최상급 강화석, 신대륙 항로 개방 → 잊혀진 산호 항구 도착

## 2. 신대륙 「용의 바다」 · 잊혀진 산호 항구
- 빛나는 산호 숲, 거대한 용 화석, 무너진 신전 기둥, 해룡 (Ichthyotitan)이 바다를 헤엄침
- 몬스터 (Lv.99)
  - 산호 등껍질 괴수
  - 익사한 옛 기사
  - 심연의 부름꾼 (원거리)
  - 보스: 산호 여왕 네레이스 (5분)
- **잊혀진 신의 제단** — 망각의 순례자 이오의 퀘스트
  - [고대 신의 파편] 3개 (산호 몬스터 20%, 네레이스 100%)를 제단에 바침
  - 보상: [신성 각인] 공격력 · 체력 · 마력 +5% (신관은 치유량 +10%), 칭호 「잊혀진 신의 계승자」
  - 신관에게는 "진정한 신으로 깨어날 날"을 암시하는 전용 대사
- 선장 하람과 대화하면 천명 항구로 귀항. 한 번 클리어하면 항구에서 신대륙으로 바로 항해 가능

## 3. 3직업 시너지 — 암흑 × 신성 × 강철
1. 도적의 공격이 적에게 **암흑 표식** (5초, 보라 고리)을 남긴다
2. 신관의 공격이 표식 걸린 적에 닿으면 **성암 폭발** (반경 4m, 150% 광역 신성 피해)
3. 폭발 근처 파티원은 **공명**을 얻는다 (6초, 받는 피해 -20%)
4. 전사는 추가로 **공명 흡수** (최대 체력 10% 보호막, 분노 +20)

몬스터는 플레이어마다 따로이므로, 도적의 표식은 파티원 화면에 "표식 지대"(반경 4m 보라 원)로 전해집니다.

## 4. 2D 픽셀 × 3D 융합
이 게임은 전부 3D (Three.js)라서 해당하지 않습니다. 거대 보스는 Meshy 3D 모델을 그대로 씁니다.
- 세이렌: Icebound Empress
- 해룡: Ichthyotitan
- 방주: Pirate Ship

## 콘셉트 아트 프롬프트 (Midjourney / DALL-E 3)
1. **액션** — Epic 2.5D boss battle on a wooden ship deck during a stormy night, MapleStory style ultra-detailed pixel art characters fighting a massive 3D-rendered kraken with pixelation shader. A heavy armored warrior blocking a giant tentacle, a dark assassin striking with purple glowing daggers leaving dark marks, and a divine oracle casting a blinding holy light explosion that interacts with the dark marks. Dynamic combat synergy effects, deep ocean, dramatic lighting, high fantasy game environment, UI elements conceptually implied --v 6.0
2. **배경** — MapleStory style dark fantasy new continent harbor, ultra-detailed 2D pixel isometric view. An ancient abandoned port overgrown with glowing bioluminescent coral and giant fossilized dragon bones. A broken stone altar of a forgotten god radiating a faint golden divine aura in the center. Dark emerald water, misty atmosphere, eerie yet majestic exploration environment, high fantasy game asset --v 6.0
3. **시네마틱** — Cinematic game cutscene frame, 2D pixel art style mixed with 3D elements. A massive golden fantasy ship sailing away from an eastern harbor into a stormy dark sea. Beneath the crashing waves, a terrifyingly huge 3D kraken silhouette with glowing red eyes is approaching the ship. Epic scale, tense atmosphere, dramatic storm clouds, lightning, high fantasy --v 6.0

## Meshy 모델 교체 키
| 키 | 모델 |
|---|---|
| `meshy_ship` | 방주 |
| `meshy_kraken` | 세이렌 (해상 보스) |
| `meshy_ichthyo` | 해룡 |

새 모델을 릴리스에 올리면 같은 키로 바꿀 수 있습니다.
