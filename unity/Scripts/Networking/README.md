# 멀티플레이 · 각성 (Mirror)

이 폴더의 스크립트는 [Mirror](https://mirror-networking.gitbook.io/) 가 설치되어 있을 때만 컴파일됩니다
(Mirror 는 설치 시 `MIRROR` 스크립팅 심볼을 자동으로 추가합니다). 설치 전에도 프로젝트는 그대로 빌드됩니다.

## 준비
1. Package Manager 또는 Asset Store 에서 Mirror 설치
2. 메뉴 **Game System > Generate Class Data** 실행
   - 기본 직업 4종 + 각성 직업 3종 (다크 나이트 / 네더 아사신 / 그랜드 오라클)
   - `Assets/Resources/ClassDatabase.asset` (ClassId → ClassData 조회표)
3. 플레이어 프리팹: `NetworkIdentity` + `ClassCharacterController` + `NetworkedClassManager`
   (싱글플레이 테스트에는 그랜드 오라클에 `OracleForesight` 를 붙이면 됩니다)
4. 드래곤 보스: `NetworkIdentity` + `BossDragonAI` + `NetworkedBossSync`
5. 씬에 빈 오브젝트 하나: `NetworkIdentity` + `NetworkQuestTracker`

## 동기화 원칙 (Desync 해결)
| 문제 | 해결 |
| --- | --- |
| 힐이 시전자 화면에서만 보임 | `CmdUseSkill` → 서버가 `ServerHeal` → 체력은 `SyncVar` 라 모든 화면이 같은 값 |
| 보스 방에서 ClassData 참조가 사라져 스탯 0 · 투명화 | 에셋 대신 `ClassId` 문자열만 동기화하고 각자 `ClassDatabase` 에서 찾음. 수치는 서버가 `TargetRpc` 로 전송 |
| 신성 방패가 브레스 피해보다 늦게 도착해 전멸 | 서버가 브레스를 **예고하는 순간**(`OnBreathForecast`) 방패를 먼저 부여, 피해 판정도 서버(`NetworkedBossSync`)가 함 |
| 신탁의 예지가 보스 데이터를 못 읽음 | 보스가 예고 이벤트로 원뿔 정보(원점·방향·사거리·각도)를 넘기고 서버가 `RpcForesight` 로 안전지대 표시 |
| 누가 잡아도 퀘스트가 안 오름 | 처치는 서버에서 `NetworkQuestTracker.ReportKill` → `SyncDictionary` 로 전원 공유 |

## 각성
- 로컬 플레이어가 Lv.99 에서 `CmdAwaken()` → 서버가 `ClassDatabase.FindAwakeningOf` 로 각성 직업을 찾아 적용
- 각성 직업은 `parentClassData` 의 스탯 × 1.2 와 부모 스킬 전체 + `awakeningSkills` 를 가짐
- 스킬창: `UI/SkillWindowUI` 가 전체 스킬을 줄마다 표시, 버튼이 `UseSkill(index, level)` (멀티플레이는 `CmdUseSkill`)
