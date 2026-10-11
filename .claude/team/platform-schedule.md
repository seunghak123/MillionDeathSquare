# Platform Work Schedule
> 4시간마다 `/platform` 스킬을 호출해 해당 세션 작업을 진행합니다.
> 현재 날짜: 2026-03-07 | 마지막 실행: 2026-03-07 (Session 9 완료)

---

## 중요 안내
- Claude는 자동 실행되지 않습니다 — **사용자가 4시간마다 `/platform`을 호출**해야 합니다
- 각 세션 시작 시 이 파일을 읽고 `[다음 세션]` 항목을 실행합니다
- 완료 후 상태를 `done`으로 업데이트하고 다음 세션으로 이동합니다

---

## 플랫폼 로드맵

### Phase 1 — Toss 미니게임 완성 (Sessions 1~3)
### Phase 2 — Facebook Instant Games 연동 (Sessions 4~6)
### Phase 3 — 멀티 플랫폼 배포 (Sessions 7~9)
### Phase 4 — 유지보수 & 최적화 사이클 (Session 10+)

---

## Session 목록

### ~~[완료] Session 1 — Toss SDK 로그인 완성~~ ✅ 2026-03-06
**플랫폼**: Toss 미니게임
**목표**:
- [x] `AppsInTossLogin.cs` — Toss 사용자 토큰 획득 및 검증 구현 (기존 완성)
- [x] `AITReflectionHelper.cs` — 런타임 리플렉션 바인딩 완성 (기존 완성)
- [x] `ApplicationManager.cs` — Toss 실패 시 GuestLogin 자동 폴백 추가
- [x] `UserDataManager.cs` — SetDataBaseInfo() 주석 해제 (NullRef 버그 수정)
- [x] `IngameSceneController.cs` — 게임오버 시 Toss 리더보드 점수 자동 제출
**수정 파일**: UserDataManager.cs, ApplicationManager.cs, IngameSceneController.cs

---

### ~~[완료] Session 2 — Toss WebGL 브릿지 & 빌드 최적화~~ ✅ 2026-03-06
**플랫폼**: Toss 미니게임 (WebGL)
**목표**:
- [x] `TossBridge.jslib` 생성 — postMessage 송수신 (GAME_START/GAME_END/BACK_PRESSED/Vibrate)
- [x] `TossBridgeHelper.cs` 생성 — C# [DllImport] 래퍼 (WebGL 안전 가드 포함)
- [x] `ApplicationManager.cs` — visibilitychange/pagehide/pageshow 포커스 처리
- [x] `ApplicationManager.cs` — OnApplicationPause 네이티브 플랫폼 처리
- [x] `IngameSceneController.cs` — GAME_START/GAME_END postMessage 연동
- [x] `index.html` + `appsintoss-unity-bridge.js` — 기존 AITTemplate 확인 (이미 완성)
**수정 파일**: TossBridge.jslib(신규), TossBridgeHelper.cs(신규), ApplicationManager.cs, IngameSceneController.cs

---

### ~~[완료] Session 3 — Toss 스코어보드 & 리워드~~ ✅ 2026-03-06
**플랫폼**: Toss 미니게임
**목표**:
- [x] Toss SDK 점수 제출 API 연동 — Session 1에서 완료 (SubmitScoreAsync + postMessage)
- [x] `PlayerPrefKey.CrossyBestScore` 추가 (4000번)
- [x] `UserDataManager.GetCrossyBestScore() / UpdateCrossyBestScore()` 편의 메서드
- [x] `CrossyRoadGameManager` — 시작 시 저장된 BestScore 로드, 갱신 시 저장
- [x] `AppsInTossLogin.GrantAdReward()` — Gold +100 지급 + SaveUserData 연동
**수정 파일**: UserDataManager.cs, CrossyRoadGameManager.cs, AppsInTossLogin.cs

---

### ~~[완료] Session 4 — Facebook Instant Games SDK 초기 설정~~ ✅ 2026-03-06
**플랫폼**: Facebook Instant Games
**목표**:
- [x] `CommonEnum.cs` — E_LOGIN_TYPE.FACEBOOK_INSTANT_LOGIN 추가
- [x] `FacebookBridge.jslib` 생성 — initializeAsync / startGameAsync / getPlayerId / submitScore / shareAsync
- [x] `FacebookBridgeHelper.cs` 생성 — C# [DllImport] 래퍼 (에디터 dev fallback 포함)
- [x] `FacebookInstantLogin.cs` 생성 — LoginInterface 구현 (init→start→getID 흐름)
- [x] `FacebookCallbackReceiver.cs` 생성 — SendMessage 콜백 수신 MonoBehaviour
- [x] `UserDataManager.cs` — FACEBOOK_INSTANT_LOGIN 케이스 추가
**신규 파일**: FacebookBridge.jslib, FacebookBridgeHelper.cs, FacebookInstantLogin.cs, FacebookCallbackReceiver.cs

---

### ~~[완료] Session 5 — Facebook 리더보드 & 소셜 기능~~ ✅ 2026-03-06
**플랫폼**: Facebook Instant Games
**목표**:
- [x] `FacebookBridge.jslib` 확장 — `getConnectedPlayerEntriesAsync` 친구 점수 조회
- [x] `FacebookCallbackReceiver.cs` 확장 — 친구 점수 콜백 + `FBLeaderboardEntry` 데이터 모델
- [x] `FacebookLeaderboard.cs` 생성 — `SubmitScoreAsync` / `GetFriendScoresAsync` / `ShareResultAsync`
- [x] `IngameSceneController.cs` — Toss + FB 점수 병렬 제출 (`UniTask.WhenAll`)
- [x] `IngameSceneController.cs` — FB 점수 제출 성공 시 자동 공유 연동
- [ ] UI 연동 — 리더보드 팝업 (ui-game 팀에 의뢰 필요)
**신규 파일**: FacebookLeaderboard.cs | **수정**: FacebookBridge.jslib, FacebookCallbackReceiver.cs, IngameSceneController.cs

---

### ~~[완료] Session 6 — Facebook WebGL 빌드 & 검증~~ ✅ 2026-03-06
**플랫폼**: Facebook Instant Games
**목표**:
- [x] `Assets/WebGLTemplates/FacebookTemplate/index.html` 생성 — FBInstant SDK 로드 + Unity 초기화 + 로딩 UI
- [x] `fbapp-config.json` 작성 (orientation: portrait, ko_KR/en_US/ja_JP 번역)
- [x] `FacebookBuildProfile.cs` — 빌드 자동화 (메뉴: Tools/Facebook Instant Games/...)
  - Brotli 압축, IL2CPP, Strip Engine Code, Managed Stripping: High
  - 초기 메모리 32MB, DataCaching 활성화
- [x] `FacebookPostBuild.cs` — IPostprocessBuildWithReport 구현
  - 빌드 후 fbapp-config.json 자동 복사
  - 초기 로딩 파일 5MB 제한 검증 + 크기 리포트
- [x] 번들 사이즈 전략 확인: StreamingAssets/Bundles는 런타임 지연 로드 → 5MB 제한 제외
**신규 파일**: FacebookTemplate/index.html, FacebookTemplate/fbapp-config.json, FacebookBuildProfile.cs, FacebookPostBuild.cs

---

### ~~[완료] Session 7 — GameDistribution / CrazyGames 연동~~ ✅ 2026-03-06
**플랫폼**: GameDistribution, CrazyGames
**목표**:
- [x] `IPlatformAds.cs` 인터페이스 + `NullAds` no-op — IsAvailable/InitAsync/GameplayStart/Stop/ShowInterstitialAd/ShowRewardedAd
- [x] `GameDistributionSDK.jslib` — GDBridge_Init(gdsdk.js 동적 로드) / GameplayStart/Stop / ShowInterstitial / ShowRewarded
- [x] `GameDistributionAds.cs` — IPlatformAds 구현, DllImport 래퍼, OnGDInitSuccess/Failed/AdComplete 콜백
- [x] `CrazyGamesSDK.jslib` — CGBridge_Init(5초 폴링) / GameplayStart/Stop / ShowInterstitial(midgame) / ShowRewarded
- [x] `CrazyGamesAds.cs` — IPlatformAds 구현, OnCGAdStarted(오디오 뮤트)/Finished 콜백
- [x] `AdManager.cs` — UnitySingleton, GAME_DISTRIBUTION/CRAZY_GAMES 심볼 분기, 120초 쿨다운, AudioListener.pause 자동 처리
- [x] `IngameSceneController.cs` — 게임 시작 NotifyGameplayStart, 게임오버 NotifyGameplayStop + 전면 광고 후 팝업 표시
**신규 파일**: IPlatformAds.cs, GameDistributionSDK.jslib, GameDistributionAds.cs, CrazyGamesSDK.jslib, CrazyGamesAds.cs, AdManager.cs
**수정 파일**: IngameSceneController.cs

---

### ~~[완료] Session 8 — itch.io / Newgrounds 배포 준비~~ ✅ 2026-03-07
**플랫폼**: itch.io, Newgrounds
**목표**:
- [x] `GuestLogin.cs` 강화 — PlayerPrefs(IndexedDB) 기반 GUID 토큰 영속화. 재접속 시 동일 계정 유지
- [x] `NewgroundsIO.jslib` — NGBridge_Init(6초 폴링) / Login / GetUserName / UnlockMedal / PostScore / IsAvailable
- [x] `NewgroundsBridgeHelper.cs` — DllImport 래퍼, 메달/스코어보드 TCS, 점수별 메달 자동 해제 헬퍼
- [x] `ItchTemplate/index.html` — 미니멀 로딩 UI (itch.io 브랜드 레드), standalone
- [x] `NewgroundsTemplate/index.html` — ngio.js 사전 로드 + NGIO.init(), 미로그인 배너 (Unity로 콜백)
- [x] `WebGLBuildTool.cs` — `Tools/WebGL Portals/` 메뉴 (itch.io + Newgrounds 빌드 자동화, NEWGROUNDS 심볼 자동 설정)
- [x] `IngameSceneController.cs` — `#if NEWGROUNDS` 블록으로 게임오버 시 NG 스코어 + 메달 자동 연동
**신규 파일**: NewgroundsIO.jslib, NewgroundsBridgeHelper.cs, ItchTemplate/index.html, NewgroundsTemplate/index.html, WebGLBuildTool.cs
**수정 파일**: GuestLogin.cs, IngameSceneController.cs

---

### ~~[완료] Session 9 — 멀티 플랫폼 입력 최적화~~ ✅ 2026-03-07
**플랫폼**: 전체
**목표**:
- [x] `CrossyInputHelper.cs` 파악 — 기존 구현 검증 (static helper, 30f 고정 임계값 문제 발견)
- [x] `WebGLInput.jslib` 신규 — 캔버스 자동 포커스, 화살표/Space 스크롤 차단, 탭 복귀 재포커스
- [x] `WebGLInputHelper.cs` 신규 — C# DllImport 래퍼, ApplicationManager에서 Init() 호출
- [x] `ApplicationManager.cs` — InitApplicationInfo()에 `#if UNITY_WEBGL` WebGLInputHelper.Init() 추가
- [x] `CrossyRoadPlayerController.cs` — 2가지 수정:
  - `#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL` 로 WebGL 키보드 입력 활성화
  - `swipeThreshold` 고정 픽셀→`swipeThresholdRatio`(화면 단변 ×4%) DPI 정규화
- [x] `VirtualDPad.cs` 신규 — 4방향 버튼 UI, autoHideOnTouch, Move() 직접 호출
**신규 파일**: WebGLInput.jslib, WebGLInputHelper.cs, VirtualDPad.cs
**수정 파일**: CrossyRoadPlayerController.cs, ApplicationManager.cs

---

### ~~[완료] Session 10 — 유지보수 사이클 Phase A~~ ✅ 2026-03-07
**플랫폼**: Toss 미니게임 핫픽스
**목표**:
- [x] `AppsInTossLogin.cs` — `SystemInfo.deviceUniqueIdentifier` → PlayerPrefs GUID 폴백 (WebGL IndexedDB 영속화)
- [x] `ApplicationManager.cs` — `IsInsideTossApp()` 체크로 비Toss WebGL 환경에서 AIT 로그인 시도 스킵
- [x] `IngameSceneController.cs` — Toss GameCenter.SubmitScoreAsync를 IsInsideTossApp() guard로 보호
- [x] `WebGLBuildTool.cs` — Toss 미니게임 빌드 프로파일 추가 (`Tools/WebGL Portals/Toss 미니게임/`)
**수정 파일**: AppsInTossLogin.cs, ApplicationManager.cs, IngameSceneController.cs, WebGLBuildTool.cs

---

### ~~[완료] Session 11 — 유지보수 사이클 Phase B~~ ✅ 2026-03-07
**플랫폼**: Facebook Instant Games 핫픽스
**목표**:
- [x] `ApplicationManager.cs` — FB 환경(`FacebookBridgeHelper.IsAvailable()`) 감지 → `FACEBOOK_INSTANT_LOGIN` 라우팅 추가 (기존엔 GuestLogin으로 떨어짐)
- [x] `FacebookTemplate/index.html` — SDK `fbinstant.6.3.js` → `7.1.js` 업그레이드
- [x] `FacebookTemplate/index.html` — `FBInstant.setLoadingProgress()` 연동 (Unity 로딩 중 5→99% 보고, FB 검수 필수)
- [x] `FacebookInstantLogin.cs` — 에디터 fallback `deviceUniqueIdentifier` → PlayerPrefs GUID 패턴 통일
**수정 파일**: ApplicationManager.cs, FacebookTemplate/index.html, FacebookInstantLogin.cs

---

### ~~[완료] Session 12 — 유지보수 사이클 Phase C~~ ✅ 2026-03-08
**플랫폼**: 광고 수익 최적화 (보상형 광고 부활 시스템)
**목표**:
- [x] `CrossyRoadGameManager.cs` — `Revive()` 메서드 추가 (세션당 1회, GAME_OVER → PLAYING 복귀, 점수 유지)
- [x] `CrossyRoadGameManager.cs` — `CanRevive` 프로퍼티 + `_hasUsedRevive` 플래그
- [x] `GameOverPopup.cs` — `reviveButton`(광고 보고 부활) + `goldEarnedText`(Gold 획득 표시) 추가
- [x] `GameOverPopup.cs` — `ReviveWithAdAsync()` → `AdManager.ShowRewardedAd` → 성공 시 `CrossyRoadGameManager.Revive()` 호출
- [x] `CrossyRoadPrefabGenerator.cs` — `CreateGameOverPopupPrefab`에 부활 버튼 + Gold 텍스트 추가
- [x] `CrossyRoadPrefabGenerator.cs` — `ForceUpdateGameOverPopup` 메뉴 추가 (기존 프리팹 강제 재생성)
**수정 파일**: CrossyRoadGameManager.cs, GameOverPopup.cs, CrossyRoadPrefabGenerator.cs

---

### ~~[완료] Session 13 — 유지보수 사이클 Phase D~~ ✅ 2026-03-08
**플랫폼**: 전체 (빌드 자동화)
**목표**:
- [x] `WebGLBuildTool.cs` — GameDistribution 빌드 메뉴 추가 (빌드 설정 적용 + 빌드 실행, GAME_DISTRIBUTION 심볼)
- [x] `WebGLBuildTool.cs` — CrazyGames 빌드 메뉴 추가 (빌드 설정 적용 + 빌드 실행, CRAZY_GAMES 심볼)
- [x] `WebGLBuildTool.cs` — "전체 포털 빌드 실행" 메뉴 (6개 플랫폼 순차 빌드)
- [x] `WebGLBuildTool.cs` — `BuildCI()` 커맨드라인 진입점 (-platform 인수로 플랫폼 지정)
- [x] `WebGLBuildTool.cs` — 전체 빌드 사이즈 리포트에 Facebook/GD/CG 포함
- [x] CI 배치 모드(`Application.isBatchMode`)에서 다이얼로그 자동 스킵
**수정 파일**: WebGLBuildTool.cs

---

### ~~[완료] Session 14 — 유지보수 사이클 Phase A (반복)~~ ✅ 2026-03-08
**플랫폼**: Toss SDK 핫픽스
**목표**:
- [x] `ApplicationManager.cs` — `AdManager.Instance.InitAsync().Forget()` 추가 (광고 SDK가 초기화되지 않아 전체 광고 미동작 버그 수정)
- [x] `AppsInTossLogin.cs` — `GrantAdReward()`에서 내부 `AddItem()` 직접 호출 → `UserDataManager.AddGold()`로 변경 (PlayerPrefs 저장 누락 버그 수정)
**수정 파일**: ApplicationManager.cs, AppsInTossLogin.cs

---

### [다음 세션] Session 15 — 유지보수 사이클 Phase B (반복)
**4시간마다 순환**:

| 순서 | 작업 |
|------|------|
| ~~A~~ | ~~Toss SDK 핫픽스~~ ✅ |
| **B** | **Facebook SDK 변경사항 대응** |
| C | 광고 수익 최적화 |
| D | 빌드 자동화 개선 |

---

## 플랫폼별 현재 상태

| 플랫폼 | 상태 | 완료율 | 다음 작업 |
|--------|------|--------|-----------|
| **Toss 미니게임** | 🟢 완료 | 100% | 유지보수 |
| **Facebook Instant Games** | 🟢 완료 | 100% | 유지보수 |
| **GameDistribution** | 🟢 완료 | 100% | 유지보수 |
| **CrazyGames** | 🟢 완료 | 100% | 유지보수 |
| **itch.io** | 🟢 완료 | 100% | 유지보수 |
| **Newgrounds** | 🟢 완료 | 100% | 유지보수 |
| **Android** | 🟡 기본만 됨 | 20% | - |
| **iOS** | 🟡 기본만 됨 | 20% | - |

---

## 세션 실행 방법

```
1. 4시간 경과 후 대화창에 "/platform" 입력
2. "platform-schedule.md의 [다음 세션]을 실행해줘" 추가
3. 세션 완료 후 이 파일의 상태 자동 업데이트됨
```

---

## 변경 이력
| 날짜 | 내용 |
|------|------|
| 2026-03-05 | 스케줄 초안 작성 |
| 2026-03-06 | Session 1 완료: 로그인 버그 3건 수정, Toss 점수 제출 연동 |
| 2026-03-06 | Session 2 완료: TossBridge.jslib + TossBridgeHelper.cs 신규, WebView 포커스 처리 |
| 2026-03-06 | Session 3 완료: BestScore 영속화, 광고 리워드 Gold +100 구현 |
| 2026-03-06 | Session 4 완료: Facebook Instant Games SDK 초기 설정 (jslib + 로그인 클래스 4종) |
| 2026-03-06 | Session 5 완료: FacebookLeaderboard.cs 생성, 친구 점수, IngameSceneController 병렬 제출 |
| 2026-03-06 | Session 6 완료: FacebookTemplate(index.html+fbapp-config), FacebookBuildProfile.cs, FacebookPostBuild.cs |
| 2026-03-06 | Session 7 완료: IPlatformAds + AdManager + GD/CrazyGames jslib 2종 + 광고 타이밍 연동 |
| 2026-03-07 | Session 8 완료: GuestLogin 강화 + NewgroundsIO.jslib + NG메달/스코어 + ItchTemplate + NGTemplate + WebGLBuildTool |
| 2026-03-07 | Session 9 완료: WebGLInput.jslib + WebGLInputHelper + WebGL 키보드 활성화 + DPI 스와이프 + VirtualDPad |
| 2026-03-07 | Session 10-A 완료: Toss SDK 핫픽스 3건 + WebGLBuildTool Toss 프로파일 |
| 2026-03-07 | Session 11-B 완료: FB 로그인 라우팅 버그 수정 + SDK 7.1 + setLoadingProgress + GUID 폴백 |
| 2026-03-08 | Session 12-C 완료: 보상형 광고 부활 시스템 (Revive + GameOverPopup 부활버튼 + PrefabGenerator) |
