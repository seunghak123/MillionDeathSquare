# 토스(앱인토스) SDK 전체 기능 가이드

**모든 토스 SDK 기능은 `AppsInTossLogin` 한 곳에서 사용합니다.**

- **로그인 / 결제 / 광고**: `LoginInterface` 메서드 (예: `UserDataManager.Instance.GetLoginPlatform()`으로 인스턴스 획득 후 호출)
- **기기 정보, 저장소, 리더보드, 클립보드, 햅틱**: `AppsInTossLogin.Device`, `AppsInTossLogin.Storage` 등 정적 중첩 클래스

---

## 1. 로그인 · 결제 · 광고 (LoginInterface)

`UserDataManager.Instance.GetLoginPlatform()`으로 `LoginInterface`를 가져와 사용합니다.

| 기능 | 사용 예 |
|------|--------|
| 로그인 | `platform.PlatformLogin((token, success) => { ... });` |
| 상품 목록 | `var products = await platform.GetProductListAsync();` |
| 결제 | `platform.PurchaseProduct("sku", onSuccess, onError);` |
| 광고 로드 | `platform.LoadAd("ad_group_id", onLoadComplete);` |
| 광고 표시 | `platform.ShowAd("ad_group_id", onShowComplete);` |
| 광고 지원 여부 | `platform.IsAdSupported();` |

---

## 2. 기기 정보 (Device)

```csharp
#if UNITY_WEBGL || UNITY_EDITOR
using Seunghak.LoginSystem;

string deviceId = await AppsInTossLogin.Device.GetDeviceIdAsync();
string platform = await AppsInTossLogin.Device.GetPlatformOSAsync();
string locale = await AppsInTossLogin.Device.GetLocaleAsync();
object networkStatus = await AppsInTossLogin.Device.GetNetworkStatusAsync();
#endif
```

---

## 3. 네이티브 저장소 (Storage)

클라우드 저장소에 키-값으로 데이터를 저장/조회합니다.

```csharp
// 조회
string value = await AppsInTossLogin.Storage.GetItemAsync("my_key");

// 저장
bool ok = await AppsInTossLogin.Storage.SetItemAsync("my_key", "my_value");

// 삭제
bool removed = await AppsInTossLogin.Storage.RemoveItemAsync("my_key");

// 전체 삭제
bool cleared = await AppsInTossLogin.Storage.ClearAsync();
```

---

## 4. 리더보드 (GameCenter)

```csharp
// 리더보드 UI 열기 (미니앱이 백그라운드로 갈 수 있음)
bool opened = await AppsInTossLogin.GameCenter.OpenLeaderboardAsync();

// 점수 제출 (게임 프로필 생성 후, 게임 진입 직후가 아닌 시점에 호출 권장)
bool submitted = await AppsInTossLogin.GameCenter.SubmitScoreAsync(12345L);
```

**주의**: 게임 프로필 생성 전 점수 제출 시 오류가 날 수 있고, 미니앱당 리더보드는 1개만 제공됩니다.

---

## 5. 클립보드 (Clipboard)

`granite.config.ts` 등에서 클립보드 권한(`clipboard` read/write) 설정이 필요할 수 있습니다.

```csharp
string text = await AppsInTossLogin.Clipboard.GetTextAsync();
bool set = await AppsInTossLogin.Clipboard.SetTextAsync("복사할 텍스트");
```

---

## 6. 햅틱 (Haptic)

```csharp
AppsInTossLogin.Haptic.GenerateFeedback();
```

---

## 7. 인앱 결제 상품 목록 (IAP)

결제 플로우는 `LoginInterface.PurchaseProduct`를 쓰고, 상품 목록만 필요할 때는 `GetLoginPlatform().GetProductListAsync()`를 사용하세요.

---

## 8. SDK 메서드명이 다른 경우

`AITReflectionHelper`는 공식 SDK의 메서드명이 버전별로 다를 수 있어 **여러 후보 이름**을 시도합니다.  
(예: Storage는 `StorageGetItem`, `GetItem`, `StorageGetItemAsync` 등)  
동작하지 않는 API가 있으면 앱인토스 개발자센터/Unity SDK 문서에서 실제 메서드명을 확인한 뒤, `AITReflectionHelper.cs`의 해당 래퍼에서 `alternateNames`에 추가하면 됩니다.

---

## 9. 환경 제한

- 위 API들은 **WebGL 빌드** 및 **Unity Editor**(Mock)에서만 동작합니다.
- 실제 동작 확인은 **토스 앱에 올린 뒤** 테스트하는 것이 좋습니다.
