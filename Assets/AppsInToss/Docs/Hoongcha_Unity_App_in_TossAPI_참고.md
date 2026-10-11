# Hoongcha Unity-App-in-TossAPI 참고 문서

[Hoongcha/Unity-App-in-TossAPI](https://github.com/Hoongcha/Unity-App-in-TossAPI)는 **Vite Warp** 방식으로 토스 API를 유니티에서 쓰기 위한 **커뮤니티 브릿지** 프로젝트입니다. 이 문서는 해당 프로젝트와 **우리 프로젝트(공식 앱인토스 SDK)** 의 차이, 그리고 “그거 보고 작업”할 때 선택지를 정리합니다.

---

## 1. 우리 프로젝트 vs Hoongcha 프로젝트

| 항목 | 우리 프로젝트 (BaseProject) | Hoongcha Unity-App-in-TossAPI |
|------|-----------------------------|-------------------------------|
| **빌드/배포** | **공식 앱인토스 유니티 SDK** (`im.toss.apps-in-toss-unity-sdk`) · `Apps in Toss > Build & Deploy` · ait-build | **Vite Warp**: Unity WebGL 빌드 → Vite(React) 프로젝트에 넣고, [UNITY-TOSS-VITE_WARPPER](https://github.com/Hoongcha/UNITY-TOSS-VITE_WARPPER) 등으로 래핑 |
| **JS 브릿지** | SDK 내장 C# ↔ .jslib (앱인토스 공식) | 자체 .jslib + **Vite 쪽 전역 함수** (`window.TossGetUserKeyForGame` 등) 선언 |
| **리시버 오브젝트** | **불필요**. `await AIT.GetUserKeyForGame()` 등은 SDK 내부에서 콜백 처리 | **필수**. MonoBehaviour 브릿지(GameObject) + `gameObject.name` + 콜백 메서드 이름을 JS에 넘기고, JS가 `SendMessage(objName, methodName, json)` 으로 C# 호출 |
| **기능** | 로그인, 결제(IAP), 광고 (공식 API) | 로그인(getUserKeyForGame), 리더보드, 네이티브 저장소, 백그라운드/포그라운드 감지, 인앱 광고 |

---

## 2. Hoongcha 쪽 “리시버” 패턴 (자바/JS 오브젝트 받을 오브젝트)

Hoongcha는 **Vite(JS)에서 유니티로 결과를 돌려줄 때** Unity `SendMessage`를 사용합니다. 그래서 **받을 GameObject와 메서드**를 반드시 세팅해야 합니다.

- C#에서 JS 호출 시 **GameObject 이름**과 **콜백 메서드 이름**을 인자로 넘김.  
  예: `RequestUserKey(gameObject.name, "OnUserKeyCallback");`
- JS(Vite)에서 API 호출 후 `unityInstance.SendMessage(gameObjectName, methodName, json)` 으로 C# 콜백 호출.
- C# 브릿지 클래스는 **MonoBehaviour**이고, 씬에 올라간 **GameObject에 붙여서** 그 이름이 JS에 전달됨.

예시 (Hoongcha `TossGetUserKeyJSBridge.cs` 요약):

```csharp
// JS 쪽에 콜백 대상 전달
RequestUserKey(gameObject.name, "OnUserKeyCallback");

// JS가 SendMessage(gameObject.name, "OnUserKeyCallback", json) 호출 시 실행되는 메서드
public void OnUserKeyCallback(string jsonResponse)
{
    var response = JsonUtility.FromJson<JSBridgeResponse>(jsonResponse);
    if (response.status == "success")
        onGetUserKeyReceived(response.hash);
    else
        onGetUserKeyError(response.message);
}
```

즉, **“자바(JS) 오브젝트를 받을 오브젝트” 세팅이 필요한 구조**는 **Hoongcha처럼 Vite Warp + 자체 jslib**를 쓸 때이고, 우리처럼 **공식 SDK만** 쓰면 SDK가 내부에서 처리하므로 별도 리시버 세팅은 없습니다.

---

## 3. 우리 프로젝트에서 Hoongcha를 “보고” 할 수 있는 작업

### A) 그대로 두고 참고만 하기 (권장)

- **현재**: 공식 SDK만 사용. 빌드는 `Apps in Toss > Build & Deploy`, `TossBuildHelper` 등으로 진행.
- **Hoongcha**는 “Vite Warp + 리시버 GameObject 패턴” 참고용으로만 활용.
- **주의**: Hoongcha README의 “절대로 같은 JS 브릿지 여러 개 쓰지 마세요”에 따라, **공식 SDK와 Hoongcha 패키지를 동시에 쓰면 안 됨.**

### B) 공식 SDK로 Hoongcha와 유사 기능 추가

Hoongcha에 있는 기능 중 **공식 앱인토스 SDK**에 해당 API가 있으면, 우리 쪽에서만 래퍼를 추가할 수 있습니다.

- **리더보드**: 공식 SDK `AIT.GameCenter` 등 (패키지에 `AIT.GameCenter.cs` 존재) → 필요 시 `AppsInTossLogin` 또는 별도 스크립트에서 공식 API 호출 래퍼 추가.
- **네이티브 저장소**: 공식 SDK `AIT.Storage` (`AIT.Storage.cs`) → getItem/setItem/removeItem 등 공식 API로 동일 기능 구현 가능.
- **백그라운드/포그라운드 감지**: 공식 문서의 Visibility/앱 생명주기 API가 있다면 동일하게 C#에서만 연동.

이때는 **Hoongcha 패키지를 설치하지 않고**, 우리 프로젝트의 `AppsInTossLogin` / `AITReflectionHelper` 패턴처럼 **공식 AIT API만** 호출하도록 구현하면 됩니다.

### C) Vite Warp로 완전 전환 후 Hoongcha 패키지 사용

- 빌드/배포를 **Vite Warp** 방식으로 바꾸고, **공식 앱인토스 유니티 SDK는 제거**한 뒤, Hoongcha 패키지만 사용.
- 이 경우 **리시버 오브젝트 세팅이 필수**: 씬에 브릿지용 GameObject를 두고, Hoongcha 위키처럼 Vite 쪽에 `window.TossGetUserKeyForGame` 등 전역 메서드 선언이 필요합니다.
- [Hoongcha UNITY-TOSS-VITE_WARPPER](https://github.com/Hoongcha/UNITY-TOSS-VITE_WARPPER) 및 [위키(토스 로그인 등)](https://github.com/Hoongcha/Unity-App-in-TossAPI/wiki) 참고.

---

## 4. 정리

- **Hoongcha** = Vite Warp + **자체 jslib** + **GameObject 리시버**로 JS → C# 콜백. “자바(JS) 오브젝트를 받을 오브젝트” 세팅이 **있음**.
- **우리 프로젝트** = **공식 SDK**만 사용 → 리시버 세팅 **없음**, SDK 내부에서 처리.
- “그거 보고 작업” 시:
  - **지금처럼 공식 SDK만 쓸 때**: Hoongcha는 패턴/기능 참고만 하고, 동일 기능은 **공식 AIT API**로 우리 쪽에 구현 (B).
  - **Vite Warp로 바꿀 때만**: Hoongcha 패키지 도입 가능 (C), 이때는 리시버 오브젝트 세팅과 Vite 전역 선언이 필요합니다.

---

## 5. 참고 링크

- [Hoongcha/Unity-App-in-TossAPI](https://github.com/Hoongcha/Unity-App-in-TossAPI)
- [Hoongcha UNITY-TOSS-VITE_WARPPER](https://github.com/Hoongcha/UNITY-TOSS-VITE_WARPPER)
- [앱인토스 Vite Unity 연동 (공식)](https://developers-apps-in-toss.toss.im/porting_tutorials/vite_unity.html)
- [앱인토스 유니티 SDK (공식)](https://github.com/toss/apps-in-toss-unity-sdk)
