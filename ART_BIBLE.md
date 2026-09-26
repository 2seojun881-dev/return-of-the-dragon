# 아트 바이블 — 용의 귀환

## 핵심 원칙

1. **배경은 누른다.** 맵 전체는 어둡고 차가운 푸른빛/잿빛 환경광, 채도 낮은 지면.
2. **핵심만 빛난다.** 무기 강화 광채, 스킬 이펙트, 적의 눈, 횃불·용암·포탈만 채도 높은 붉은색/황금색(또는 마력의 푸른색)으로 Emission + Bloom.
3. **키 라이트는 따뜻하게, 그림자는 차갑게.** 주광은 저녁 햇빛 같은 주황빛, 그림자와 하늘은 푸른 잿빛.

### 팔레트

| 역할 | 색 | 사용처 |
|---|---|---|
| 환경광 (하늘) | `#8898B8` | Hemisphere / Ambient Sky |
| 환경광 (지면) | `#2A221E` | Ambient Ground |
| 키 라이트 | `#FFAE78` | Directional Light |
| 안개 · 마을 | `#2E2A36` | 잿빛 황혼 |
| 안개 · 평원 | `#3A4450` | 푸른 잿빛 |
| 안개 · 성채 | `#404C66` | 차가운 은청색 |
| 강조 · 불 | `#FF4A12` ~ `#FFA040` | 용암, 횃불, 메테오, +10 이상 무기 |
| 강조 · 황금 | `#FFB040` / `#FFD070` | +4 이상 무기, 드래곤의 방패, 레벨 업 |
| 강조 · 마력 | `#4A9AFF` / `#9FE0FF` | +7 이상 무기, 서리 폭발, 포탈, 성채 가로등 |
| 위험 | `#FF3A2A` | 보스 공격 범위(장판), 적의 눈 |

### 천명 성채 사냥터 톤

| 구역 | 안개/하늘 | 키 라이트 | 강조색 |
|---|---|---|---|
| 서문 · 야수 서식지 | `#3A4450` 푸른 잿빛 | `#FFCF98` | 늑대의 붉은 눈 |
| 동문 · 원혼의 묘지 | `#1E1A2A` 보랏빛 어둠 + 흐르는 지면 안개 | `#A8B0FF` 차가운 달빛 | 반투명 원혼의 푸른 빛, 제단 촛불 |
| 남문 · 태엽 미로 | `#1C1814` 녹슨 갈색 | `#FFC890` | 금속 톱니바퀴 `#B8883A` (회전), 횃불 |
| 북문 · 아수라 흑야 협곡 | `#2A0C0A` 핏빛 | `#FF7A4A` | 용암 균열 `#FF4A10`, 도깨비의 붉은 눈 |
| 용혈의 제단 | `#140606` 칠흑 | `#FF6A3A` | 용암 고리, 불기둥 10개, 흑요석 비늘 틈의 마그마 |
| 용혈 무기 | – | – | 핏빛 `#FF2A06` Emission, 제련 단계마다 강해짐 (강화 광채보다 우선) |

이그니스가 카메라와 플레이어 사이에 오면 반투명(28%)으로 바뀌어 전투 장판이 가려지지 않습니다.

### 무기 강화 광채 규칙

| 강화 단계 | Emission | 세기 |
|---|---|---|
| +0 ~ +3 | 없음 | – |
| +4 ~ +6 | 황금 `#FFB040` | 낮음 |
| +7 ~ +9 | 마력 푸른빛 `#4A9AFF` | 중간 |
| +10 이상 | 불꽃 `#FF4A12` | 강함 (Bloom 확실히 발생) |

## 웹 프로토타입 적용 내역 (`index.html`)

- **후처리:** UnrealBloom (threshold 0.9, 강도 0.6~0.7) → ACES 필름 톤 + 스플릿 토닝(그림자 푸르게, 하이라이트 따뜻하게) + 채도 소폭 상승 → 비네트
- **조명:** 존별 환경광·안개·주광을 위 팔레트로 재조정, 소프트 그림자 (높음: 2048, 보통/낮음: 1024)
- **그래픽 품질 옵션 (메뉴):** 높음 / 보통 (모바일 기본값, 해상도 1.5배) / 낮음 (후처리 끔, 발열 최소)

## 4대 직업 인게임 모델 (KayKit)

| 직업 | 모델 | 색 | 무기 |
|---|---|---|---|
| 초보자 | Rogue (망토 없음) | 기본 가죽·녹색 | 기사의 한손검 |
| 전사 | Knight | 차가운 다크 스틸, 금 테두리 | 대검 (강화 광채) |
| 도적 | Rogue_Hooded | 녹색 → 그림자 보라 가죽 | 쌍단검 (보라 광채) |
| 상인 | Barbarian (모자 없음) | 갈색 → 금빛, 청·보라 → 진홍 비단 | 지팡이(상단봉) + 주위를 도는 금화 3개 |
| 신(神관/신사) | Mage | 로브·모자 → 흰 제사장 로브, 붉은 장식 | 지팡이 끝 흰 영혼불, 펼친 주문서 |

## (이전) 캐릭터 디자인 (직업별 프롬프트 → 인게임 3D 모델)

| 직업 | 프롬프트 핵심 | 인게임 모델 적용 |
|---|---|---|
| 뱅가드 나이트 | 중갑, 거대한 대검, 금장식 다크 스틸, 찢어진 망토 | 반사되는 다크 스틸 판금(흉갑·3단 견갑·건틀릿·경갑·허리 갑주), 금 테두리, 투구 눈구멍의 불씨, 붉은 깃털, 7갈래로 찢어진 진홍 망토(바람에 흔들림), 금 가드의 거대한 대검(룬 홈이 강화 단계별로 빛남) |
| 섀도우 레인저 | 다크 엘프, 빛나는 마법 활, 짙은 녹색 나뭇잎 망토, 가죽 갑옷 | 잿빛 보랏빛 피부, 긴 뾰족 귀, 은발, 빛나는 보라 눈, 둥근 녹색 두건, 잎사귀 수십 장으로 만든 망토, 가죽 갑옷·스트랩·팔보호대, 빛나는 화살통, 녹청색 마력 테두리가 빛나는 활 |
| 엘리멘탈 소서러 | 보라색 마법 불꽃, 룬이 빛나는 어두운 로브, 뼈 지팡이, 떠다니는 마법 구슬 | 칠흑 로브와 높은 옷깃, 가슴·밑단·소매의 빛나는 보라 룬, 해골과 뿔이 달린 뼈 지팡이(보라 불꽃), 손에 타오르는 보라 불꽃, 주위를 도는 마법 구슬 3개, 보라 불꽃 기본 공격 |

직업 선택 화면은 어두운 무대, 스포트라이트, 푸른 역광, 떠오르는 불씨를 쓴 3D 쇼케이스로, 컨셉아트의 연출(dynamic pose, cinematic lighting)을 따릅니다.

### 직업별 컨셉아트 프롬프트 (세로 9:16)

```
Dark fantasy, a heavy armored Vanguard Knight holding a massive greatsword, intricate dark steel armor with gold accents, torn cape, dynamic pose, dark atmospheric background, epic cinematic lighting, highly detailed concept art, 8k resolution --ar 9:16
```
```
Dark fantasy, an agile dark elf Shadow Ranger holding a glowing magical bow, wearing a deep green leafy cloak and leather armor, standing on a misty cliff, mysterious atmosphere, dynamic lighting, highly detailed character concept art, 8k --ar 9:16
```
```
Dark fantasy, a powerful Elemental Sorcerer casting vibrant purple magical flames, wearing intricate dark robes with glowing runic patterns, holding a mystical bone staff, floating magical orbs, dramatic cinematic lighting, highly detailed concept art, 8k --ar 9:16
```

## 타이틀 화면 원화 시안 (Midjourney)

```
Title screen concept art for a high-end mobile dark fantasy MMORPG. Top-down isometric perspective overlooking a ruined stone fortress cliff. A massive active volcano glowing with red magma in the far background under dark ash clouds. Glowing embers floating in the air, cinematic lightning, epic scale, dark and moody atmosphere, Unreal Engine 5 render, highly detailed, 8k resolution --ar 16:9 --v 6.0
```

게임 안 타이틀은 이 구도(벼랑 위 폐허 → 성채 → 화산, 번개와 불씨)를 3D로 재현합니다.

## Unity 적용

`unity/Scripts/DarkFantasyGraphicsSetup.cs` 를 씬의 빈 오브젝트에 붙이면 아래 값이 한 번에 들어갑니다.

| 항목 | 값 |
|---|---|
| Tonemapping | ACES |
| Bloom | Threshold 1.0 (HDR 1 이상 = Emission만), Intensity 0.7~0.9, Scatter 0.65, High Quality Filtering 끔 |
| Vignette | Intensity 0.32, Smoothness 0.45, 색 거의 검정 (보라 기운) |
| Color Adjustments | Post Exposure 0.1, Contrast +12, Saturation −6 |
| Shadows/Midtones/Highlights | 그림자 푸르게, 하이라이트 따뜻하게 |
| 그림자 | Shadow Distance 35~45, Cascade 1~2, Soft Shadows, 해상도 1024~2048 |
| Render Scale | 높음 1.0 / 보통 0.85 / 낮음 0.7 |
| 안개 | Exponential Squared, Density 0.018, `#2E2A36` 계열 |
| 환경광 | Trilight (하늘 `#8898B8` / 적도 `#403D45` / 지면 `#2A221E`) |
| Emission | `DarkFantasyGraphicsSetup.SetEmission(mat, color, 2~4)`: HDR 배율로 Bloom threshold를 넘김 |
| 금속 질감 | `SetMetal(mat, 0.9, 0.72)`: 칼날은 Smoothness 높게, 손잡이는 0.3 정도 |

## AI 이미지 생성용 공통 스타일 (Midjourney / DALL-E)

모든 에셋 프롬프트 끝에 붙입니다.

```
, isometric top-down gameplay view, dark fantasy MMORPG aesthetic, cinematic volumetric lighting, high-end AAA mobile game graphic, Unreal Engine 5 render, highly detailed, sharp focus, 8k resolution, trending on ArtStation --v 6.0
```

게임 팔레트와 맞추려면 공통 스타일 앞에 다음 문구를 추가하면 좋습니다.

```
, desaturated cold blue-grey ambient, warm orange key light, only weapons, spell effects and monster eyes glowing in saturated red and gold
```

### 에셋별 예시

- **필드 배경:** `Environment design of a charred volcanic wasteland filled with glowing magma rivers and ruined dark stone pillars` + 공통 스타일 `--ar 16:9`
- **캐릭터:** `Character design of an elegant female Elemental Sorcerer wearing dark blue robes with glowing magical runes, holding a crystal staff, casting a frost spell` + 공통 스타일 `--ar 16:9`
- **보스:** `Monster design of a massive corrupted dire wolf with glowing red eyes and black smoke emanating from its fur, roaring fiercely` + 공통 스타일 `--ar 16:9`
- **스킬 이펙트:** `VFX concept art of a massive blazing meteor crashing into the ground, huge fiery explosion, flying embers, high contrast glowing effects` + 공통 스타일 `--ar 16:9`
- **최종 보스:** `Concept art of a massive, ancient volcanic dragon, the final boss of a mobile MMORPG. The dragon has dark obsidian scales glowing with magma from underneath, huge intimidating wings, breathing fire` + 공통 스타일 `--ar 16:9`
- **모바일 UI:** `Game UI design for a mobile MMORPG. Top-down isometric view gameplay in the background showing a fantasy forest. The UI includes a character health bar on top left, a minimap on top right, and 4 circular fantasy skill buttons glowing with magic on the bottom right corner. Clean and modern UI, high quality, fantasy aesthetic` `--ar 16:9`
