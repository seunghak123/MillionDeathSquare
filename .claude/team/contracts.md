# Interface Contracts (팀 간 인터페이스 계약서)

> 팀 간 공유되는 인터페이스, 이벤트, 데이터 구조를 여기에 문서화합니다.
> 변경 시 반드시 관련 팀에 board.md를 통해 알립니다.

---

## 1. UI ↔ Logic 인터페이스

### UIManager (UI Team 소유)
```csharp
// Logic → UI: 게임 상태에 따른 UI 전환
UIManager.Instance.PushUI(UI_TYPE type)
UIManager.Instance.PopUI()
```

### 판 흐름 (Logic Team 소유) — 백만 번 죽는 네모
```csharp
// 메뉴 → 플레이 (씬 전환 INGAME → InGameSceneController가 BeginPendingPlay 호출)
MDSGameDirector.Instance.RequestPlay(PlayRequest.Campaign(MDSMapResolver.ResolveCampaign(w, s, asset)));
MDSGameDirector.Instance.RequestPlay(PlayRequest.User(await MDSMapResolver.ResolveAsync(mapCodeOrShareCode)));
// 에디터 시험 플레이 (씬 전환 없음, 플레이 창 Push → 끝나면 Pop)
MDSGameDirector.Instance.StartEditorTest(map, recording => editorModel.SubmitTestPlay(recording));
// 결과 화면
MDSGameDirector.Instance.Retry() / PlayNextStage() / ExitToMenu()
// 공용 UI(일시정지·설정)는 디렉터 타입 대신 MDSSession만 사용
MDSSession.Pause() / Resume() / Restart() / ExitToMenu() / IsPlaying / IsPaused / CurrentDeaths
// HUD는 매 프레임 MDSGameDirector.Instance.Sim(Simulation)을 읽는다 (데스·동전·틱·카운트다운)
event Action<int> DeathsChanged;   // 데스 펀치 연출
event Action<PlayResult> Finished; // 클리어 (캠페인 기록·순위표 제출은 디렉터가 처리)
MDSGameDirector.ConsumeReturnTarget(out UI_TYPE)   // LobbySceneController: 판에서 나오면 원래 화면으로
```

### 결정적 코어 (Logic Team 소유, `Core/` asmdef — UnityEngine 금지, mcs C# 7)
```csharp
new Simulation(map).Step(InputBits)          // 60틱/초, 이벤트: GetEvent(i) (Death/Respawn/Coin/Key/DoorOpen/Checkpoint/Clear/Restart/Bump/SlideStart)
ObstacleMotion.Evaluate(def, tick, out x, out y)  // 장애물 위치 = 틱의 순수 함수
MapValidator.Validate(map) / MapSerializer.ToShareCode(map) / MapSerializer.TryParseShareCode(code, out map, out err)
MapCode.Encode(workshopId) / MapCode.TryDecode(code, out id)
InputRecording (RLE 직렬화) / ReplayVerifier.Run(map, rec) / Verify(map, rec, deaths, ticks)
CampaignProgress.RecordClear(w, s, deaths, ticks) → ClearOutcome (월드·장치 해금)
MapEditorModel: Apply/Erase/AddPathPoint/Undo/Redo/SubmitTestPlay/CanUpload
RoomSession(호스트 중계) / MatchTracker(레이스·협동·생존 결과) / NetCodec
```
- 1칸 = `Fixed.CELL`(1200) 단위, 플레이어 초당 4칸 = 틱당 80, 장애물 2/4/6칸 = 40/80/120
- 규칙 수치를 바꾸면 `Rules.RULES_VERSION`을 올린다 (기존 리플레이·순위 기록과 분리)

## 2. UI ↔ Platform 인터페이스

### LoginInterface (Platform Team 소유)
```csharp
// UI에서 호출: 로그인 처리
LoginInterface.Login()
LoginInterface.GetUserInfo()
```

## 3. Logic ↔ Platform 인터페이스

### UserDataManager (공유)
```csharp
// 유저 데이터 저장/로드
UserDataManager.Instance.SaveData()
UserDataManager.Instance.LoadData()
```

## 4. 공통 Enum (Common Team 소유)

### CommonEnum.cs / BaseUI.cs
- `UI_TYPE` 백만 번 죽는 네모 화면: `MDSMainWindow`, `MDSCampaignWindow`, `MDSPlayWindow`, `MDSEditorWindow`, `MDSMapFindWindow`, `MDSRoomWindow` (윈도우), `MDSMapDetailPopup`, `MDSResultPopup` (팝업). 프리팹 이름 = 타입 이름 (셋업 툴이 생성)
- 레거시(베이스 프로젝트) 창 `GameStartWindow` / `InGameHUDWindow` / `GameOverPopup`은 기존 프리팹 호환용 최소 구현만 남음
- `E_LOGIN_TYPE`: GUEST_LOGIN(Steam·에디터), APPS_IN_TOSS_LOGIN(모바일판), FACEBOOK_INSTANT_LOGIN
- 코어 enum: `TileType`, `PathMode`, `KeyColor`, `InputBits`, `SimState`, `SimEventType`, `EditorTool`, `EditorDevice`, `MatchMode`, `PerceivedDifficulty`, `RecordPlatform`, `RecordKind`

---

## 5. BundleManager API (Platform Team 소유)

### 에셋 로드 (Addressables 기반)
```csharp
// 앱 시작 시 1회 호출 (Main label 자동 로드)
await BundleManager.Instance.InitializeAsync(ct);

// 씬별 번들 로드 (SceneController.InitSceneController()에서 호출)
await BundleManager.Instance.LoadBundleAsync(E_BUNDLE_TYPE.LOBBY, ct: ct);
await BundleManager.Instance.LoadBundleAsync(E_BUNDLE_TYPE.INGAME, ct: ct);
await BundleManager.Instance.LoadBundleAsync(E_BUNDLE_TYPE.CHARACTER, suffix: "cat", ct: ct);

// 에셋 가져오기 (로드 완료 후)
GameObject prefab = BundleManager.Instance.GetAsset("prefabName");
T asset = BundleManager.Instance.LoadAsset<T>(E_BUNDLE_TYPE.MAIN, "assetName");

// 번들 언로드
BundleManager.Instance.UnloadBundle(E_BUNDLE_TYPE.INGAME);

// 상태 확인
bool loaded = BundleManager.Instance.IsBundleLoaded(E_BUNDLE_TYPE.LOBBY);
```

### Addressables 레이블 규칙
| E_BUNDLE_TYPE | Addressables Label |
|---------------|-------------------|
| MAIN | "main" |
| LOBBY | "lobby" |
| INGAME | "ingame" |
| CHARACTER | "character_{suffix}" (예: "character_cat") |

### 에디터 폴백
- 에디터에서 레이블 로드 실패 시 `AssetDatabase.FindAssets()` 자동 폴백
- 런타임에서는 레이블 미로드 시 `LogError` 출력

---

## 6. 플랫폼 핸들러 계약 (Platform Team 소유) — 2026-10-03 토스 출시 작업

플랫폼별 기능은 `IPlatform`이 핸들러로 노출한다. 새 플랫폼은 핸들러만 구현하면 된다.
토스 연동은 공식 Unity SDK(`AppsInToss.AIT.*`)만 사용한다 (`window.__TossSDK` jslib 경로 금지).

```csharp
// IPlatform 추가 멤버
IPlatformStorage Storage { get; }   // 기본 PlayerPrefsStorage.Instance, 토스 = TossStorage
IPlatformAds     Ads     { get; }   // 기본 NullAds.Instance,           토스 = TossAds
IPlatformIAP     IAP     { get; }   // 기본 NullIAP.Instance,           토스 = TossIAP
bool SupportsLeaderboardUI { get; }
UniTask<bool> OpenLeaderboardAsync();
bool SupportsInvite { get; }
UniTask<int> InviteFriendsAsync();  // 지급된 리워드 수량 합계 (없으면 0)
```

| 파일 | 타입 | 비고 |
|------|------|------|
| `Platform/IPlatformStorage.cs` | `IPlatformStorage`, `PlayerPrefsStorage` | 키-값 문자열 |
| `Platform/IPlatformIAP.cs` | `IPlatformIAP`, `PurchaseResult`, `NullIAP` | 서버 없음 → 클라이언트 지급 |
| `Platform/TossConfig.cs` | `TossConfig.Instance` | 광고/초대/프로모션/상품 ID. `Resources/TossConfig.asset` |
| `Platform/TossStorage.cs` | `public sealed class TossStorage : IPlatformStorage` | 기본 생성자 |
| `Platform/TossAds.cs` | `public sealed class TossAds : IPlatformAds` | 기본 생성자, 일반 C# 클래스 |
| `Platform/TossIAP.cs` | `public sealed class TossIAP : IPlatformIAP` | 기본 생성자, 일반 C# 클래스 |

- 저장: PlayerPrefs = 동기 로컬 캐시, `PlatformFactory.Current.Storage` = 원본. 로그인 직후 복원, 변경 시 디바운스 푸시.
- 광고: `AdManager`는 GD/CG 심볼이 없으면 `PlatformFactory.Current.Ads`를 사용.
- 로그인: 서버가 없으므로 `AIT.GetUserKeyForGame()`만 사용 (`AIT.AppLogin()` 호출 금지).
- 점수 제출(토스 모바일판): `PlatformFactory.Current.SubmitScoreAsync` 경유. 토스는 `AIT.GetGameCenterGameProfile`로 프로필을 먼저 확인하고,
  없으면 점수를 `MDSPendingLeaderboardScore`(로컬)에 보관했다가 순위표를 연 뒤 제출. PC(Steam)판의 맵별 기록은 7장 `ILeaderboardService`.
- Steam(PC): `SteamPlatform` (`#if STEAMWORKS_NET`) — Storage = Steam 클라우드(`SteamCloudStorage`), 광고·결제 없음, 초기화 실패 시 DefaultPlatform

---

## 7. 맵 공유·기록·멀티방 서비스 (Platform/Network Team 소유) — 백만 번 죽는 네모

`MDSServices.Workshop / Leaderboards / Lobby` — Steam 준비 시 Steam 구현, 아니면 로컬(오프라인) 구현.

```csharp
IWorkshopService.UploadAsync(WorkshopUploadRequest)   // Unlisted 기본, 실패 시 FriendsOnly. 리플레이로 직접 클리어 재검증
IWorkshopService.ResolveMapCodeAsync(mapCode)         // 메타데이터 = 공유 코드 → 다운로드 없이 플레이
IWorkshopService.LikeAsync / SubmitReviewAsync(Review) / RecordAttemptAsync / GetSummaryAsync / GetMyItemsAsync
ILeaderboardService.SubmitAsync(mapKey, map, proof, deaths, ticks)   // 검증 통과해야 제출, 플랫폼별 보드
ILeaderboardService.GetTopAsync(mapKey, RecordKind, n) / GetMineAsync
ILobbyService.CreateRoomAsync(code) / JoinByCodeAsync(code) / InviteFriends() / LeaveRoom() / Transport(INetTransport)
```
| 항목 | 규칙 |
|------|------|
| 순위표 이름 | `RecordBoards.BoardName(mapKey, platform, kind)` = `mds_{key}_{pc|mo}_{deaths|time}`. key = `w_{맵코드}` / `c_{월드-스테이지}` / `h_{내용해시}` |
| 후기(Steam) | 맵별 순위표 `mds_w_{코드}_review`: 점수 = 난이도×100 + 문구+1, 상세[0] = 좋아요(1)·클리어(2) — 창작마당 댓글 API 대신 |
| 시도 수(Steam) | `mds_w_{코드}_attempt` 인원 = 시도 수 (클리어율 분모) |
| 멀티 전송 | 참가자 → 방장만, 방장이 `RelayRouter` 규칙으로 중계. 위치는 비신뢰(20Hz), 나머지 신뢰 |
| 방 코드 | 6글자 (I/O/0/1 제외), Steam 로비 데이터 `mds_code` 필터로만 찾음 |
| 저장 키 | `MDSCampaignSave`, `MDSEditorDraft`, `MDSPlayedMaps`, `MDSShakePercent`, `MDSRumbleEnabled`, `MDSShowGhosts` (스냅샷 `MillionDeathSquareSaveSnapshot`) |
| 분석 이벤트 | `mds_` 접두사 (`MDSAnalytics`) |

---

## 변경 이력
| 날짜 | 변경자 | 내용 |
|------|--------|------|
| (초기화) | setup | 초기 계약서 작성 |
| 2026-03-10 | platform | BundleManager Addressables API 계약 추가 |
| 2026-10-11 | claude | 백만 번 죽는 네모로 전환: 1·4장 교체, 6장 Steam 추가, 7장(창작마당·순위표·멀티방) 신설 |
