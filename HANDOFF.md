# 용의 귀환 · 인수인계 (2026-10-05)

새 대화에서 이 파일을 먼저 읽고 이어서 작업하세요. 이전 대화: https://claude.ai/code/session_01XckkXwdwY1VrnaF4JNgRLQ

## 주소
- 게임: https://game.faceforking.com (GitHub Pages, 저장소 `2seojun881-dev/return-of-the-dragon`, `main` 과 `gh-pages` 둘 다 푸시)
- 계정·사냥 서버: https://api.faceforking.com (Cloudflare Worker `rotd-api` + D1 `rotd` + Durable Object `Hunt`)
- 개인정보처리방침: https://game.faceforking.com/privacy.html
- 도메인 faceforking.com 은 Cloudflare 에 있음 (`game` CNAME → 2seojun881-dev.github.io, DNS only)

## 저장소
- `2seojun881-dev/trading-bot` 의 `dragon-raid/` (작업 브랜치 `claude/kind-hamilton-8or90c`): 게임 빌드 `index.html`, `cloud/`(Worker), `server/`(Node 버전 broker·idle, 지금은 안 씀), `store/`(스토어 이미지), `docs/`
- `2seojun881-dev/return-of-the-dragon`: 실제 서비스되는 사이트. `index.html`, `cloud/`, `.github/workflows/cloud.yml`(cloud/ 바뀌면 Cloudflare 자동 배포, Secrets: CLOUDFLARE_API_TOKEN), `CNAME`, `.well-known/assetlinks.json`(템플릿)
- 게임 코드는 단일 HTML(Three.js r128). 배포 = 버전 문자열(`ver …`, `const cur='…'`) 올리고 index.html 을 두 저장소에 커밋·푸시

## 지금까지 만든 것 (최근)
- AI 자동 진행: 퀘스트 → 레벨 맞는 사냥터 사냥, 진급(직업은 사용자 선택), 장비, 물약 구매, 부활
- 절전 모드(달 버튼), 화면 꺼짐 방지, 게임 종료 버튼(메뉴), 메뉴 창은 게임을 멈추지 않음
- 서버 계정: 캐릭터는 서버에 저장, 어느 폰에서든 이름+비밀번호로 접속, 한 번에 한 기기. 브라우저에 남은 옛 캐릭터는 첫 접속 때 서버로 옮김 (양쪽 진행이 다르면 어느 쪽을 쓸지 물어봄)
- 자리 비움 사냥: AI 켠 채 종료하면 서버(Durable Object)가 최대 24시간 사냥, 다른 플레이어에게 「이름·AI」로 보임, 재접속 시 결과 지급. 서버가 없으면 자리 비운 시간 × 최근 AI 사냥 속도 70% (최대 8시간)
- 시스템 가이드 터치하면 닫힘, 메시지(toast)가 창 위에 표시
- (ver 2026.10.05-7) 스킬 업그레이드: ① 화려한 스킬 연출(스킬명 배너, 섬광, 충격파, 룬 진, 빛기둥) ② 스킬 강화 +1~+10 (피해 +10%/단계, 재사용 -1.5%, +5·+10에서 연출 강화, 골드+비급 조각: 몬스터 5%·보스 5개·자리비움 처치당 0.05) ③ 도깨비 비급 스킬덱: 4계열(화염·뇌전·빙결·도깨비)×3개, 4칸 장착, 같은 계열 2개 피해 +15%, 3개면 계열 궁극기. 마력 사용, AI도 사용. 스킬 창 = 오른쪽 위 책 버튼(K). 저장 필드 S.skUp, S.deck, S.skp
- (ver 2026.10.06-2~4) AI 물약 보충(마을 귀환/걷기 → 잡화점 → 지름길로 복귀), 위험하면 출구로 후퇴, 연속 사망 시 쉬운 사냥터 / 「자동」 사냥 버튼(그 자리에서만, 보스 포함) / 마을 귀환 버튼(V, 3초 시전, 30초 재사용)
- (ver 2026.10.10-1) 화면 설정(메뉴 › 화면): 시야 거리 슬라이더(VIEW.zoom 0.6~2.2, 기본 1.5, 핀치 줌과 같은 값, localStorage ignis.zoom)·밝기(ignis.bright). 시야를 멀리 하면 안개·그림자 범위도 함께 늘림. 보스 연출 카메라는 최대 2.6배
- (ver 2026.10.07-3) 바라카스 2페이즈 자동전투: 무적 보스 대신 제단 주술사(e.channel)를 가까운 순으로 공격(autoPick), 메테오 회피 때 목표 유지하고 주술사 쪽으로 비켜섬. 착지 후 다시 바라카스
- (ver 2026.10.07-2) 밝은 지역(마을·평원·성채 등) 스킬 연출: 바닥 장식이 스킬 위에 덮이던 문제(renderOrder 5) + 가산 혼합 대신 일반 혼합·진한 색 (fxBright, BRIGHT_Z). 바라카스 크기 2.4→1.8배, 지상전에서 카메라 2.1배 후퇴
- (ver 2026.10.06-1) 길찾기: 벽(W.cols)으로 1m 격자를 만들어 A* 로 돌아감 (navGrid / navPath / navWay, stepToward 에서 사용). 태엽 미로에서 AI가 벽에 막히던 문제 해결

## 남은 일
1. **구글 플레이 등록**: 사용자가 개발자 계정(25달러) 아직 없음. PWABuilder 옵션은 정해 둠(패키지 com.faceforking.dragon, Fullscreen sticky, 새 서명 키, 국가 KR) — 다운로드 전에 사용자 확인 필요. 개발자 계정(25달러) → PWABuilder 에서 `https://game.faceforking.com` 으로 Android 패키지(.aab) 생성 → 플레이 콘솔 비공개 테스트(테스터 12명·14일) → 앱 서명 SHA-256 지문과 패키지 이름을 받아 `.well-known/assetlinks.json` 채우기 → 스토어 등록정보(이미지는 `dragon-raid/store/`) → 프로덕션 출시
2. (해결, ver 2026.10.07-1) 가방 무기 터치 문제: 가방 버튼이 손가락을 누를 때 창을 열어서, 같은 터치가 새 창의 아이템 칸에 눌려 정보창이 저절로 뜨던 것. 창이 열린 뒤 0.7초 안의 클릭은 무시 (PANEL_T)
3. 사용자 캐릭터 「예서」: 서버에 낮은 레벨로 새로 만들어졌을 수 있음. 원래 폰에서 접속하면 어느 쪽을 쓸지 묻는 창이 뜸
4. 드래곤 모델 4개(Meshy 릴리스 595112790, 595112651, 595112521, 595112889)는 나중 업데이트용으로 남겨 둠

## 주의
- **배포 전에 항상 `return-of-the-dragon` 의 최신 `index.html`(gh-pages) 을 기준으로 고칠 것.** 두 대화가 따로 고치면 한쪽 작업이 지워짐 (10.05-7 스킬 업데이트가 이렇게 덮일 뻔했음)
- API 키·토큰은 채팅으로 받지 말 것 (GitHub Secrets 에 사용자가 직접 넣음)
- 사용자는 한국어, 폰은 가로 770×355 화면
