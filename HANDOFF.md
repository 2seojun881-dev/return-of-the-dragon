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

## 남은 일
1. **구글 플레이 등록**: 사용자가 개발자 계정(25달러) 아직 없음. PWABuilder 옵션은 정해 둠(패키지 com.faceforking.dragon, Fullscreen sticky, 새 서명 키, 국가 KR) — 다운로드 전에 사용자 확인 필요. 개발자 계정(25달러) → PWABuilder 에서 `https://game.faceforking.com` 으로 Android 패키지(.aab) 생성 → 플레이 콘솔 비공개 테스트(테스터 12명·14일) → 앱 서명 SHA-256 지문과 패키지 이름을 받아 `.well-known/assetlinks.json` 채우기 → 스토어 등록정보(이미지는 `dragon-raid/store/`) → 프로덕션 출시
2. 사용자 보고: 가방에서 무기 터치가 안 된다는 문제. 테스트(폰 크기·터치)에서는 재현 안 됨. 정보창이 안 뜨는지 / 장착이 안 되는지 확인 필요
3. 사용자 캐릭터 「예서」: 서버에 낮은 레벨로 새로 만들어졌을 수 있음. 원래 폰에서 접속하면 어느 쪽을 쓸지 묻는 창이 뜸
4. 드래곤 모델 4개(Meshy 릴리스 595112790, 595112651, 595112521, 595112889)는 나중 업데이트용으로 남겨 둠

## 주의
- API 키·토큰은 채팅으로 받지 말 것 (GitHub Secrets 에 사용자가 직접 넣음)
- 사용자는 한국어, 폰은 가로 770×355 화면
