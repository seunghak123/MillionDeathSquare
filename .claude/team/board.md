# 🎮 Fossil Merge (가제) — 팀 작업 보드

> 마지막 업데이트: 2026-10-09 (claude)
> 기획서: `Docs/Fossil_Merge_기획서.md` (기획: 민수님 👑) | 아래 🔴🟠🟡🟢 표와 오래된 완료 기록은 이전 게임(길건너) 기록
> (이전) [프리팹/사운드 시트](https://docs.google.com/spreadsheets/d/1AxuorzcWUBpHMmUpBJsrHJeaRcS7-h6w/edit?gid=1112271618) | [이슈 트래킹](https://docs.google.com/spreadsheets/d/1yYvdby_Oih7VJJysdi6ezJ0dIx0KzBgtU4c4-qNEZ7k/edit) | [회의록](https://docs.google.com/document/d/1HmjL13UJoAjziWRrl-J4aUm6cq_FgrpWXWAkxqCUaB8/edit)

---

## 🦴 Fossil Merge 1차 구현 (2026-10-09, claude — 기획: 민수님, Unity 컴파일/플레이 미검증)
기획서의 P0 전부 + P1 대부분을 코드로 구현. 화석은 임시 그래픽(단계별 색/크기 노듈 구체).

| 기획서 | 구현 | 위치 |
|---|---|---|
| 3장 발굴대 쌓기 | 탭 = 그 위치 낙하, 좌우 드래그 = 회전, 낙하 가이드(수직선+그림자), 장외 낙하 = 라이프 -1, 화석 상한 40 | `SuikaDropper`, `SuikaGameManager` |
| 4장 11단계 | 모래알 → … → 공룡 골격, 1~5단계만 드롭 | `Tools/Fossil Merge/1`, `SuikaFruitTable` |
| 5장 타임어택 | 120초, 라이프 3, 마일스톤 500/1500/3000 → +20/+15/+10초, 이후 간격×1.5 +5초, 이어하기(라이프+1·브러시 / +30초) | `SuikaRules`, `SuikaGameManager` |
| 합체/연쇄 | n(n+1)/2×10점, 연쇄 ×1.2/×1.5/×2 (2초 창) | `SuikaRules.MergeScore` |
| 6장 공룡 부활 | 1.5초 타이머 정지, 1~3단계 제거, +2000점 +15초, 경험치 가득 → 레벨업 | `SuikaGameManager` |
| 7장 스킬 5종 | 망치/브러시/지진(벽+방향 스와이프·기울기)/자석/진화, 최대 4, 1회용 | `SuikaSkillSystem`, `SuikaPlateWalls` |
| 8장 레벨업/장착 | 300×1.6^레벨, 3개 중 1개, 광고 다시 뽑기 1회, 가득 차면 교체 / 로비 장착 2칸, 코인 150 확정, 광고 랜덤 하루 10회 | `SuikaSkillSystem`, `LevelUpPopup`, `SuikaProgress`, `GameStartWindow` |
| 9장 지각 변동 | 폰 기울기(기준 자세·데드존 ±3°·보정·감도·최대 15°), 지진 이벤트(30/20/15초, 5/10/15°, 경보 2초, 버티기 보너스), 보상 ×1.5, 굴리기 ×1.5, 5000점 해금, 카메라 고정 | `SuikaTiltInput`(+`FossilTilt.jslib`), `SuikaQuakeDirector`, `SuikaPlate` |
| 10장 재화/광고 | 코인 = 점수÷50(×1.5), 게임 중 광고 없음, 보상형 4곳, 전면 3판마다(첫 판·보상형 본 판 제외, 결과→로비), 배너 결과 화면만 | `SuikaPlatformBridge` |
| 11장 화면 | HUD(시간/라이프/경험치/스킬4/알림), 레벨업 팝업, 결과(이어하기 5초), 로비(모드 잠금·장착), 설정(감도/흔들림 강도/기준 자세) | UI 스크립트 + `SuikaTestHUD`(테스트 씬) |

- **기획과 다르게 한 것**: ① 발굴대 회전은 카메라 공전으로 구현(화석이 올라간 물리 판을 직접 돌리면 마찰로 미끄러짐, 화면은 동일) ② URP/툰 셰이더·LevelPlay/AdMob 대신 현재 프로젝트(빌트인 + Toss 광고) 유지 ③ 리더보드는 플랫폼에 1개라 **일반 모드 점수만 제출**
- **남은 것 (에디터 작업)**: Tools/Fossil Merge/1 → 6 → 5 실행, HUD/로비/결과/설정 프리팹에 새 필드 배치(시간·라이프·경험치·스킬 4칸·알림, 모드 잠금·장착 2칸·구매 패널, 이어하기 카운트다운, 감도·흔들림·재설정)
- **플레이 데이터 (기획서 13.6)**: 토스 이벤트 로그로 수집 — `SuikaAnalytics` → `TossPlatform.LogEventParams`(AIT.EventLog) / 화면은 `LogScreen`(AIT.AnalyticsScreen). 이벤트: fossil_run_start/run_end/run_abandon/dino_revival/level_up/skill_use/life_lost/continue/ad/skill_acquire/mode_unlock, 화면 lobby/ingame/level_up/result. run_end에 도달 단계·스킬 획득/사용 수·공룡 부활 수 등 밸런스 지표 포함 (이어하기 후 다시 끝나면 run_end가 한 번 더 감 — continued=true). ※ SDK v3.5.0에서 EventLog는 deprecated 표시, 후속 AnalyticsLog는 C# 미구현 → SDK 올릴 때 TossPlatform.SendEventLogAsync 한 곳만 교체
- **남은 것 (P1/P2)**: 공룡 부활/스킬 연출·이펙트·사운드 전용 리소스, 실제 화석 모델, 지진 경보 사운드, 순위표 모드 분리
- **이름 정리 (2026-10-09)**: 저장 키·분석 이벤트·번들 ID·씬·에셋·씬 오브젝트 이름에서 Suika 제거 — 저장 키 `SuikaDifficulty`→`FossilMode`, 에셋 폴더 `BaseSources/Suika3D`→`BaseSources/FossilMerge`(FossilTable/Fossil/FossilPlate/FossilLandingMarker, GUID 유지), 테스트 씬 `FossilMergeTest.unity`, 씬 오브젝트 `FossilMergeGame`, 메뉴 `Tools/Fossil Merge`. 캐릭터 기반 구 과일 프리팹(Suika_0x_*) 삭제, FossilTable은 비워 둠 → **Tools/Fossil Merge/1 실행 필요**
- **📋 출시 전 정리 작업 — 클래스 이름 변경**: `Suika*` 클래스 / `Seunghak.Game.Suika3D` 네임스페이스 / `Assets/Scripts/Game/Suika3D` 폴더 / `SoundKeys.SUIKA_*` / `Tools`의 `Suika3DSetupTool` → Fossil 계열로. 스크립트는 .cs와 .meta를 함께 옮겨 GUID 유지(씬·프리팹 참조 보존), 직렬화 필드명은 그대로 두거나 `[FormerlySerializedAs]` 사용. 로그 태그 `[Suika…]`도 함께 변경
- **기획서 개정 반영 (2026-10-09, 토스 출시 확정판)**: 토스 순위표는 게임 프로필 확인 후 제출(없으면 보관 → 순위표 연 뒤 제출), 지각 변동 최고 기록은 **자체 서버가 없어 토스 저장소에만 저장**(기획서 12.1과 다름, 토스 앱 삭제 시 기록 사라짐), 결과 화면에 최고 기록 표시(지각 변동은 "지각 변동 내 최고 기록"을 크게, `GameOverPopup` 프리팹의 `BestObject`에 배치 완료 — 위치는 에디터에서 확인). 카메라 회전·화면 기준 기울기/스와이프는 이미 구현돼 있음
- **결정 필요 (기획서 15.1)**: 토스 앱 안 기울기 센서 실기기 확인(안 되면 대체 조작 vs 모드 보류), 렌더링 방식, 5번째 스킬(현재 진화), 정식 이름, 공룡 종류, Fossil Slash 에셋 공유, 토스 광고 노출 빈도 정책

## 🔴 Critical
| # | 이슈 | 상태 |
|---|------|------|
| B-09 | 물길 사망 안 됨 / 안전 발판 누락 | ✅ 완료 |
| B-11 | 충돌형 함정 이동 작동 안 함 | ✅ 완료 (WoodTrap1_Pre → ObstacleCollisionTrap, pool category 8, collisionTrapKeys 등록) |
| B-12a | 후진 시 SCREEN_OUT 강제 사망 | ✅ 완료 (`_screenOutGraceTimer` 0.2s) |
| B-12b | 배경 삭제 간격 빠름 | ✅ 완료 (`rowsBehind` fallback 5→8) |

## 🟠 High
| # | 이슈 | 상태 |
|---|------|------|
| B-02 | 리프보트 겹침 | ✅ 완료 (`logMinGapX` 조정) |
| B-07 | 충돌 함정 경로 밖 노출 | ✅ 완료 (`obstacleBoundX` 12→10) |
| B-10 | 버섯 1x1 그리드 오정렬 | ✅ 완료 (그리드 스냅 추가) |
| B-13 | 아웃라인 계단 현상 | ✅ 완료 (`normalThreshold` 조정) |

## 🟡 Medium
| # | 이슈 | 담당 | 상태 |
|---|------|------|------|
| B-03 | 잔디 기반 타일 레이어링 | dev-logic-core | 📋 대기 |
| B-08 | 물길 폭포 + 발판 구현 | design-level | 📋 대기 |
| P-06 | WrapPipe 씬 Entry/Exit 배치 | dev-logic-gameplay | 📋 대기 |
| UI-01~03 | Popup UIManager 연결 3종 | dev-ui-game | ✅ 완료 |
| SND-01 | SoundManager 클립 등록 | 개발팀 | ✅ 완료 (BGM 5개 + SFX 24개 Base.unity에 등록) |

## 🟢 Art/Editor (영서, 수민)
| 항목 | 담당 | 상태 |
|------|------|------|
| LotusLeaf_Pre → ObstacleWaterSafe | 영서 | ✅ (claude — ObstacleWaterStatic→WaterSafe 교체) |
| WoodBoard_Pre → ObstacleWaterSafe | 영서 | ✅ (claude — ObstacleWaterStatic→WaterSafe 교체) |
| Sponge_Pre → ObstacleWaterTrap | 영서 | ✅ (ObstacleWaterStatic 유지, waterTrapKeys 등록됨) |
| Turtle_Pre → ObstacleLog | 영서 | ✅ (claude — ObstacleWaterStatic 제거, ObstacleLog 추가 완료) |
| Tramplin_Pre warningDuration=0 | 영서 | ✅ (claude — 0.6→0 수정) |
| RewardPopup/SettingPopup/UserInfoPopup 연결 | 수민 | ✅ (claude — 기본 UI 구축 완료, 디자인 폴리싱 필요) |
| Coin_Pre 획득 이펙트 | 수민 | 📋 |
| 나무 색상 (경계=청록, 내부=초록) | 영서 | 📋 |

## 🍉 3D 수박게임 전환 (2026-10-09, claude — Unity 컴파일/플레이 미검증)
- **길건너 코드 삭제 완료**: `Scripts/Game/CrossyRoad`, 길건너 전용 에디터 툴, Mockups, CrossyInputHelper, 길건너 enum, 사운드/난이도/패턴/존 테이블 에셋
- **유지**: 플랫폼(Toss/FB/포털), UI 프레임워크와 창 전부, UserDataManager(골드/아이템/최고점/플레이 횟수), CharacterManager(보유 캐릭터 = 스킨 판매용), 상점, IAP, PlatformSaveSync
- **UI 재연결**: InGameHUDWindow, GameOverPopup(골드/광고 부활, 광고 골드 2배), InGamePause, SettingPopup(+햅틱 토글), SettingsWindow, GameStartWindow, In/Lobby SceneController → `SuikaGameManager`
- **아이템**: `ShopItemData`(구 CrossyRoadItemData의 GUID를 이어받아 기존 ItemData_*.asset과 상점 참조 유지). 인게임 효과는 `SuikaGameManager.ItemUseHandler`에 등록해야 함 (**기획 필요**)
- **저장 키 이름 변경**: PlayerPrefKey `CrossyBestScore/Gold/PlayCount` → `BestScore/Gold/PlayCount` (기존 로컬 저장값은 이어지지 않음). 스냅샷 키 `CrossySaveSnapshot` → `MergeGameSaveSnapshot` (2026-10-09, 출시 전이라 테스트 저장분은 이어지지 않음)
- **난이도 (2026-10-09)**: 노멀 = 접시 완전 고정(코드로 고정) / 하드 = 무게 쪽으로 기울기만 (데드존 0.4, 최대 4°, 초당 3° 제한, 굴러다니는 과일은 계산 제외). **흔들림(상시 wobble·N드롭마다 흔들림)은 전 모드에서 제거**. 카메라/드롭 높이는 멈춘 과일 기준 0.5m 단위로만 갱신(화면 출렁임 방지). 마지막 선택은 PlayerPrefs `FossilMode`. 로비 버튼: `OnClickPlayNormal/Hard` 또는 `OnClickSelectNormal/Hard` + `OnClickPlay`. 리더보드는 아직 모드 공용
- **길건너 잔재 정리 (2026-10-09)**: 로비 캐릭터 선택 제거(코드 + 프리팹 `Character card` 비활성), Base.unity `[CrossyRoad]` 루트 삭제, 장애물/맵 타일/아이템 픽업 프리팹·모델·머티리얼·텍스처·애니메이터 192개(~57MB) 삭제, Addressables `CrossyRoad` 그룹 삭제, `Test Scene.unity`·길건너 GDD 삭제, 공유/토너먼트/부트 타이틀 문구 "3D 수박게임"으로 변경. 유지: 스카이박스·VFX(FX_Hit/Heart, 미사용이지만 범용), 캐릭터 아트(과일/스킨), 아이템 Clock/Shield/Wing(슬롯 UI 자리 표시용, 효과 기획 필요)
- **외부 식별자 mergegame으로 변경 (2026-10-09)**: APK 패키지 `com.seunghak.mergegame`, 제품명 `Fossil Merge`, 키스토어 `keystore/mergegame.keystore`(별칭 `mergegame`, 새로 생성 필요), FB 리더보드 `mergegame_score`(FB 콘솔에서 같은 이름으로 생성 필요), Newgrounds 상수 `SCOREBOARD_MAIN`(ID 1001 유지), Addressables 라벨 `mergegame`, 저장 스냅샷 키 `MergeGameSaveSnapshot`
- **모바일 조작**: 상대 드래그 조준(손가락이 과일을 가리지 않음), 상단까지 끌어 올리면 드롭 취소, 하단 회전 영역 한 손 회전 + 관성, 두 손가락 회전, 세로 화면 거리 보정, 착지 예상 라인 + 마커
- **햅틱**: `SuikaHaptics` — 드롭 tap, 합치기 단계별 tick/medium, 이번 판 첫 단계 success, 최고 단계 confetti, 위기 softMedium, 게임오버 error, 가장자리 tickWeak. 간격/우선순위 제한, 설정 토글
- **셋업 순서**: Tools/Fossil Merge/1 → 5(Base 씬 연결: Missing Script 정리 + 게임 오브젝트 + 컨트롤러 연결) → Play. 빈 오브젝트 목록은 Console 로그 확인 후 수동 삭제
- **남은 것**: Unity 컴파일 확인 → Base 씬 연결 실행 → HUD 프리팹에 회전 버튼/다음 과일/위험 표시 배치(에디터) → 밸런스 → 아이템 효과 기획 → FB 리더보드·키스토어 생성

## 🚀 토스 출시 작업 (2026-10-03, claude — 진행 중, Unity 컴파일/실기기 미검증)
- **SDK**: apps-in-toss-unity-sdk 2.0.0 → v3.5.0 (`Packages/manifest.json` 고정). appName `gotohome`
- **플랫폼 핸들러 구조** (contracts.md 6장): `IPlatform.Storage/Ads/IAP` — 토스 = `TossStorage`/`TossAds`/`TossIAP`
- **저장**: PlayerPrefs = 로컬 캐시, 플랫폼 Storage = 원본 (`PlatformSaveSync`, 스냅샷 키 `MergeGameSaveSnapshot`)
- **로그인**: `GetUserKeyForGame`만 사용 (서버 없음), 점수 제출 게임오버당 1회, 토스 자동 공유 제거
- **dev/prod**: `TossConfig`(Resources) + 심볼 `TOSS_DEV`/`TOSS_PROD`. dev=테스트 광고·프로모션, prod=실제
- **배포**: `ci/toss/deploy-toss.ps1 -Env dev|prod|all`, 디스코드 봇 "토스 배포해줘" (`discord-bot/tossDeploy.js`, 봇 재시작 필요)
- **남은 것**: Unity 재시작 후 컴파일 확인 → UI 에디터 배치(로비 dev 라벨, 리더보드/초대/공유 버튼, 골드 구매 슬롯) → 빌드·ait 업로드
- **사용자 준비물**: `AITConfig.iconUrl`, `AITCredentials.deploymentKey`, prod 광고 그룹 ID, (선택) 프로모션 코드·초대 moduleId·IAP sku

## 🧪 검증 대기 (2026-09-19, claude — 브랜치 bot/task-1789820005876-8405b4, 컴파일/플레이 미검증)
- **구조물 복합 군집 배치**: MapGenerator.SpawnStaticObstaclesAtLane — 앵커+팔레트(2~4종) 기반 군집, 레인 간 드리프트, 1x1 90° 랜덤 회전. Inspector: `staticCluster*`, `staticPalette*`, `staticRandomYaw`
- **코인/아이템 겹침 수정**: 겹침 조회 전 `Physics.SyncTransforms()` (같은 프레임 스폰 장애물 미반영이 원인), `IsPickupCellBlocked` 신규(정적 장애물+고정 함정+직전 코인), 아이템 스폰에도 적용

## ✅ 완료 (2026-05-22)
- **SND-01**: SoundManager bgmClips(5개) + sfxClips(24개) Base.unity에 등록
- **B-11**: WoodTrap1_Pre → ObstacleCollisionTrap 전환, pool category 8, collisionTrapKeys=[WoodTrap1]
- **Player tag 버그 수정**: Player.prefab m_TagString Untagged→Player (9개 시스템 일제 복구)

## ✅ 완료 (2026-05-21)
- LotusLeaf_Pre, WoodBoard_Pre → ObstacleWaterSafe 교체 (WaterStatic 제거)
- Tramplin_Pre warningDuration 0.6→0
- Base.unity logObstacleKeys에 Turtle, Tramplin 추가 (LeafBoat만 등록되어 있었음)
- **장치 중간 생성 버그**: logObstacleKeys에서 `Tramplin` 제거 — ILogObstacle 미구현으로 강 위에 정적 생성됨. `["LeafBoat","Turtle"]`로 수정 (코드 defaults + 씬 Inspector)
- **carObstacleKeys** Inspector 값 수정: `["WoodCart","WoodCart2"]` (잘못된 WoodCar1/WoodBoat/WoodTrap1 제거)
- **ObstacleWaterStatic 물 기믹 5종 버그 수정**: OnDisable 정리, 침수 시작 시 SetRidingWaterTrap 조기 해제 제거, 부유 아이템 bail-out 완전 상태 복구, SetRidingWaterTrap→TriggerGameOver 직전으로 이동, catch 블록 SetInputLocked 해제 추가
- **ObstacleWaterTrap** 동일 5종 버그 수정

## ✅ 완료 (이번 세션)
- SCREEN_OUT 오탐 수정, 버섯 포물선 호, 아이들 타이머, ObstaclePipe 보호
- ObstacleWaterSafe.cs 신규, MapGenerator 분류 수정
- CrossySoundKeys.cs + 사운드 통합 8개 파일
- CrossySoundSetup.cs 에디터 자동 매핑 툴
- 5시간 기획서 체크 SessionStart 훅
- `_screenOutGraceTimer` 후진 사망 방지
- `obstacleBoundX` 12→10, `rowsBehind` fallback 8, 정적 장애물 그리드 스냅

## ✅ 완료 (2026-05-19)
- UIManager Stack empty 완전 수정 (while-loop Count guard, currentUI null check)
- GameStartWindow: MyInfo→UserInfoPopup, Settings→SettingPopup 연결
- InGameHUDWindow: Pause→SettingPopup 연결
- CrossyAnimatorSetupEditor.cs 생성 (FBX 클립 → AnimatorController 자동 생성)
- ObstacleMushroom SCREEN_OUT 버그 확인 (`!_ridingLogJump` 이미 적용됨)

## ✅ 완료 (2026-05-18)
- IDLE_TIMEOUT 사망 제거 (`idleTimeout` 필드 + Update 로직 삭제)
- ObstaclePipe `exitOffsetCells` (1~3칸) 추가
- ObstacleLog wrap-around halfW 기준 수정 (통나무 경계 이탈 방지)
- UIManager Stack empty 크래시 3곳 수정 (`windowStack.Peek()` → Count 체크)
- 물 사망 race condition 수정 (`_ridingWaterTrap` bool → `_waterSupportCount` 카운터)
- FallTrap 빈도 감소 (`maxFallTrapsPerRow` 7→3)
- planning_docs.txt 회의록 링크 추가

## Decision Log
| 날짜 | 결정 |
|------|------|
| 2026-05-17 | Tramplin=logObstacleKeys, warningDuration=0 프리팹 설정 |
| 2026-05-17 | LotusLeaf=WaterSafe(영구), LotusLeafTrap=WaterTrap(가라앉음) |
| 2026-05-17 | Sponge→waterTrapKeys 재분류 |
| 2026-05-17 | BGM: READY→game_ui_bgm, PLAYING→sound_magic_forest |
