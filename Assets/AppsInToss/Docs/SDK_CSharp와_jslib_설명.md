# C# API Wrapper와 .jslib 브릿지 — 직접 구현할 필요 없음

## 결론: **직접 작업하지 않아도 됩니다**

[앱인토스 유니티 SDK](https://developers-apps-in-toss.toss.im/unity/porting-tutorials/unity-sdk.html) 패키지를 설치하면 **1. C# API Wrapper**와 **2. JavaScript Bridge(.jslib)** 가 **패키지 안에 이미 포함**되어 있습니다.

---

## 1. C# Header (C# API Wrapper)

- **역할**: Unity C#에서 앱인토스 API를 직접 호출할 수 있게 해주는 레이어.
- **제공처**: 앱인토스 유니티 패키지 (`im.toss.apps-in-toss-unity-sdk`).
- **위치**: 패키지 설치 후  
  `Packages/Apps In Toss SDK/Runtime/SDK/`  
  (또는 `Library/PackageCache/.../Runtime/SDK/`)  
  아래에 `AIT.cs`, `AIT.IAP.cs` 등 **C# API 파일**이 들어옵니다.
- **내부 동작**: WebGL 빌드 시 `DllImport("__Internal")`로 .jslib의 JS 함수와 연결됩니다.
- **우리 프로젝트**: `Assets/Scripts/Login/AppsInTossLogin.cs`에서 이 C# API를 사용합니다.  
  (직접 `AIT.xxx` 호출 또는 `AITReflectionHelper`를 통해 호출)

→ **별도로 C# API Wrapper를 새로 만들 필요 없습니다.** 패키지 것이 그대로 쓰입니다.

---

## 2. JavaScript Bridge (.jslib)

- **역할**: WebGL 빌드에서 C#과 JavaScript를 이어주고, 앱인토스 WebView SDK와 통신합니다.
- **제공처**: 같은 앱인토스 유니티 패키지.
- **위치**: 패키지 내  
  `Runtime/SDK/Plugins/*.jslib`  
  (또는 PackageCache 내 동일 구조).
- **내부 동작**: C#에서 호출하는 JS 함수가 여기에 정의되고, 앱인토스 WebView SDK와의 실제 통신도 여기서 처리됩니다.

→ **별도로 .jslib 브릿지를 새로 작성할 필요 없습니다.** 패키지에 포함된 것을 그대로 사용합니다.

---

## 3. 우리가 한 작업

- **C# Wrapper / .jslib를 “만든” 것이 아니라**,  
  **이미 패키지로 제공되는 C# API를 게임 코드에서 어떻게 부를지**만 정리했습니다.
- `AppsInTossLogin.cs`: 로그인/결제/광고 흐름에서 `AIT.AppLogin`, `AIT.GetUserKeyForGame`, IAP, 광고 API를 사용.
- `AITReflectionHelper.cs`: SDK 버전/타입 차이를 흡수하기 위해 리플렉션으로 `AIT.IAPGetProductItemList()`, `AIT.IAPCreateOneTimePurchaseOrder()` 등 **패키지가 제공하는 C# API**를 호출.

즉, **“C# Wrapper + .jslib 구현” 작업은 패키지가 하고, 우리는 “그 API를 어떻게 쓸지”만 구현한 상태입니다.**

---

## 4. 설치 확인 방법

다음이 맞다면 C# API와 .jslib는 이미 들어온 것입니다.

1. **Package Manager**  
   - `Window → Package Manager`  
   - 목록에 **Apps In Toss SDK** (또는 `im.toss.apps-in-toss-unity-sdk`) 가 있음.
2. **메뉴**  
   - Unity 상단 메뉴에 **AIT** 메뉴가 있음.
3. **manifest.json**  
   - `Packages/manifest.json`에  
     `"im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git"`  
     가 있음. (현재 프로젝트에는 이미 있음.)

패키지가 없다면:

- **Package Manager → + → Add package from git URL...**
- URL: `https://github.com/toss/apps-in-toss-unity-sdk.git`
- **Add** 후 재생성되면 C# API와 .jslib가 함께 들어옵니다.

---

## 5. 정리

| 항목 | 직접 구현 필요? | 실제 제공처 |
|------|------------------|-------------|
| C# API Wrapper (DllImport 등) | **아니요** | 앱인토스 유니티 패키지 |
| JavaScript Bridge (.jslib) | **아니요** | 앱인토스 유니티 패키지 |
| 게임에서 API 호출 (로그인/결제/광고) | **예** | `AppsInTossLogin.cs`, `AITReflectionHelper.cs` |

**C# Header와 .jslib 작업은 패키지가 담당하고, 우리는 패키지가 제공하는 C# API를 호출하는 쪽만 구현하면 됩니다.**

---

## 6. “자바(JS) 오브젝트를 받을 오브젝트” 세팅이 필요한가?

### 결론: **앱인토스 SDK API를 쓸 때는 별도 리시버 GameObject 세팅이 없어도 됩니다.**

- **앱인토스는 WebGL 미니앱**이라 통신 상대는 **Java가 아니라 JavaScript**입니다.  
  (Android 네이티브 플러그인처럼 `AndroidJavaObject` / `UnitySendMessage` 리시버를 세팅할 필요 없음.)

- **JS ↔ C# “왔다갔다”는 SDK 안에서 이미 처리됩니다.**
  - C#에서 `await AIT.AppLogin()`, `await AIT.GetUserKeyForGame()` 등을 호출하면,
  - 패키지 내부의 .jslib / TypeScript 브릿지가 JS API를 호출하고,
  - 결과는 **Promise/콜백 ID** 같은 방식으로 C# 쪽 Task/콜백에 전달됩니다.
  - 이 과정은 **SDK가 내부적으로 갖춘 통신 구조**에서 이루어지므로,  
    **게임 쪽에서 “콜백을 받을 GameObject”를 만들어 둘 필요가 없습니다.**

- 따라서 **리시버 오브젝트를 세팅하고 그걸 왔다갔다 쓰는 코드는 우리 프로젝트에 없어도 됩니다.**  
  `AppsInTossLogin`에서는 그냥 `AIT.xxx` / `AITReflectionHelper`를 호출하기만 하면 됩니다.

### 예외: 웹뷰에서 Unity로 직접 메시지를 보낼 때

- **앱인토스 SDK가 아닌**, 예를 들어 **자체 웹페이지(React 등)에서**  
  `unityInstance.SendMessage(objectName, methodName, value)` 로 유니티에 메시지를 보내고 싶다면,
- 그때는 **해당 이름의 GameObject**와 그 오브젝트에 붙은 **public 메서드**를 씬에 두어야 합니다.  
  이건 “앱인토스 API 호출”이 아니라 **커스텀 웹–유니티 연동**일 때만 필요한 설정입니다.
