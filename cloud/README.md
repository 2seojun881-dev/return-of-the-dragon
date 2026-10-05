# 용의 귀환 · 계정 서버 (Cloudflare Worker + D1)

캐릭터를 브라우저가 아니라 이 서버에 저장합니다. 어느 폰에서든 같은 캐릭터 이름과 비밀번호로 접속합니다.

- 주소: `https://api.faceforking.com` (Cloudflare 무료 요금제)
- 비밀번호는 PBKDF2-SHA256 + 계정별 salt 로만 저장, 10번 연속 틀리면 10분 잠금
- 한 캐릭터는 한 기기에서만 접속 (다른 폰에서 접속하면 먼저 접속한 기기는 끊김)
- 서버에 연결할 수 없으면 게임은 예전처럼 브라우저에 저장합니다

## 자리 비움 사냥 (`hunt.js`, Durable Object)
AI를 켠 채 게임을 끄면 캐릭터가 서버에서 계속 사냥합니다 (최대 24시간, 게임과 같은 전투 공식).
- 사냥 중인 캐릭터가 있을 때만 2초마다 깨어나고, 없으면 잠듭니다
- 게임 브로커(`broker.emqx.io:8883`, TCP+TLS)에 위치·공격을 보내 다른 플레이어에게 「이름·AI」로 보입니다
- 게임이 만든 비밀값이 있어야 결과를 받거나 사냥을 바꿀 수 있습니다
- `/idle/start` `/idle/stop` `/idle/peek` `/idle/health`

## 배포 (자동)
`return-of-the-dragon` 저장소의 `cloud/` 가 바뀌면 GitHub Actions(`.github/workflows/cloud.yml`)가 배포합니다.
저장소 Settings → Secrets and variables → Actions 에 두 값이 있어야 합니다.
- `CLOUDFLARE_API_TOKEN`: Cloudflare → My Profile → API Tokens → 「Edit Cloudflare Workers」 템플릿 + 권한 추가 `Account · D1 · Edit`, `Zone · DNS · Edit` (Zone: faceforking.com)
- `CLOUDFLARE_ACCOUNT_ID`: Cloudflare 대시보드 오른쪽의 Account ID

## 직접 시험
```
npm i -g wrangler@4
wrangler d1 migrations apply rotd --local   # database_id 자리에 아무 UUID
wrangler dev --local
```
