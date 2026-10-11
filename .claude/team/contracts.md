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

### Game Events (Logic Team 소유) — Fossil Merge
```csharp
// Logic → UI: SuikaGameManager
OnStateChanged(E_SUIKA_GAME_STATE)   // NONE(로비) / READY / PLAYING / PAUSED / LEVEL_UP / GAME_OVER
OnScoreChanged(int) / OnTimeBonus(float) / OnTimeWarning()   // 남은 10초
OnLivesChanged(int) / OnLifeLost(Vector3) / OnDanger(bool)
OnXpChanged(float xp, float need, int level) / OnLevelUp()   // → LevelUpPopup
OnMerged(int tier, Vector3) / OnChain(int chain, Vector3)    // chain >= 2
OnDinoRevival(Vector3) / OnDinoRevivalEnd()
OnNextTierChanged(int) / OnGameOver() / OnTectonicUnlocked() / OnDifficultyChanged(E_SUIKA_DIFFICULTY)
// SuikaSkillSystem
OnInventoryChanged / OnPendingChanged(E_FOSSIL_SKILL) / OnSkillUsed(E_FOSSIL_SKILL)
OnOfferChanged / OnNeedReplace(E_FOSSIL_SKILL) / OnOfferClosed
// SuikaQuakeDirector (지각 변동)
OnWarning(float) / OnQuakeStart / OnQuakeEnd(bool survived, int bonus)

// UI → Logic
SuikaGameManager.RestartGame() / ExitGame() / Pause() / Resume() / SetDifficulty(mode) / IsModeAvailable(mode)
SuikaGameManager.Continue()                          // 광고는 SuikaPlatformBridge.ReviveWithAdAsync()
SuikaGameManager.ConsumeDoubleGoldBonus()            // 코인 2배, 지급(AddGold)은 호출부
SuikaSkillSystem.ActivateSlot(i) / CancelPending()
SuikaSkillSystem.ChooseOffer(i) / ReplaceHeld(i) / DiscardOverflow()   // 다시 뽑기: SuikaPlatformBridge.RerollSkillsWithAdAsync()
SuikaProgress.TryBuySkill(skill) / SuikaPlatformBridge.GetRandomSkillWithAdAsync()   // 로비 장착
SuikaPlatformBridge.OnLeaveResultToLobbyAsync()      // 결과 → 로비 (전면 광고 규칙)
SuikaPlatformBridge.ShowRewardedAsync(duringRun, placement)   // placement: lobby_skill / reroll / continue / coin_double

// 플레이 데이터 (토스 이벤트 로그) — 게임 코드는 SuikaAnalytics만 호출
SuikaAnalytics.RunStarted / RunEnded / RunAbandoned / DinoRevived / LifeLost / Continued
SuikaAnalytics.LevelUpResolved / SkillUsed / SkillAcquiredInLobby / Ad / ModeUnlocked / Screen(name)
```

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

### CommonEnum.cs
- `E_SUIKA_GAME_STATE` (Suika3D/SuikaEnums.cs): NONE, READY, PLAYING, PAUSED, LEVEL_UP, GAME_OVER
- `E_SUIKA_GAMEOVER_CAUSE`: NONE, LIVES_OUT, TIME_OUT
- `E_SUIKA_DIFFICULTY`: NORMAL(일반), TECTONIC(지각 변동)
- `E_FOSSIL_SKILL`: HAMMER, BRUSH, EARTHQUAKE, MAGNET, EVOLVE
- `UI_TYPE.LevelUpPopup` (1007, InGamePause 뒤에 추가)
- `UI_TYPE`: 모든 UI 화면 타입
- `E_LOGIN_TYPE`: GUEST_LOGIN, APPS_IN_TOSS_LOGIN

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
- 점수 제출: 게임오버당 1회, `PlatformFactory.Current.SubmitScoreAsync` 경유. **일반 모드 점수만** 제출 (토스 순위표는 게임당 1개).
  토스는 `AIT.GetGameCenterGameProfile`로 프로필을 먼저 확인하고, 없으면 점수를 `FossilPendingLeaderboardScore`(로컬)에 보관했다가 순위표를 연 뒤 제출.
- 지각 변동 최고 기록: 자체 서버 없음 → `FossilTectonicBest`(토스 저장소 스냅샷)에만 저장. 토스 앱을 지우면 사라짐.

---

## 변경 이력
| 날짜 | 변경자 | 내용 |
|------|--------|------|
| (초기화) | setup | 초기 계약서 작성 |
| 2026-03-10 | platform | BundleManager Addressables API 계약 추가 |
