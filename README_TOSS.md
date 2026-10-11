# 토스(Apps In Toss) 개발 가이드

이 프로젝트는 Unity로 개발된 게임을 토스 앱 내 WebGL 미니앱으로 배포하기 위한 설정이 완료되어 있습니다.

---

## 📋 빠른 시작

### 1. 필수 설정
1. [앱인토스 개발자센터](https://developers-apps-in-toss.toss.im/)에서 앱 생성 및 **앱 ID** 발급
2. Unity Editor에서 `Apps in Toss > Build & Deploy Window` 실행
3. **App ID** 및 **Icon URL** 입력
4. `Tools > 토스 빌드 설정` 메뉴로 설정 확인

### 2. 빌드 및 테스트
- **로컬 개발**: `Apps in Toss > Dev Server > Start Server`
- **프로덕션 빌드**: `Apps in Toss > Build & Package`

---

## 🏗️ 프로젝트 구조

### 로그인 시스템
```
Assets/Scripts/Login/
├── LoginInterface.cs          # 플랫폼별 로그인/결제/광고 인터페이스
├── AppsInTossLogin.cs         # 토스 플랫폼 구현 (로그인/결제/광고)
└── GuestLogin.cs             # 게스트 로그인 (테스트용)
```

### 주요 기능
- ✅ **로그인**: 토스 앱 인증 및 사용자 키 획득
- ✅ **결제**: 인앱 결제 (상품 목록 조회, 결제 처리)
- ✅ **광고**: 인앱 광고 (로드, 표시, 보상형 광고)

### 사용 예시
```csharp
// LoginInterface 인스턴스 가져오기
LoginInterface platform = UserDataManager.Instance.GetLoginPlatform();

// 로그인 (자동으로 ApplicationManager에서 처리됨)
platform.PlatformLogin((token, success) => { });

// 상품 목록 가져오기
var products = await platform.GetProductListAsync();

// 결제하기
platform.PurchaseProduct("coin_100", 
    onSuccess: (orderId) => Debug.Log($"결제 성공: {orderId}"),
    onError: (error) => Debug.LogError($"결제 실패: {error}")
);

// 광고 표시
if (platform.IsAdSupported()) {
    platform.LoadAd("ad_group_id", (loaded) => {
        if (loaded) {
            platform.ShowAd("ad_group_id", (shown) => { });
        }
    });
}
```

---

## 📚 문서

- **[토스 설정 가이드](Assets/AppsInToss/Docs/토스_설정_가이드.md)**: 완전한 설정 가이드 및 체크리스트
- **[앱인토스 프로젝트 세팅](Assets/AppsInToss/Docs/앱인토스_프로젝트_세팅.md)**: 기본 프로젝트 설정 가이드
- **[토스 플랫폼 룰](.cursor/rules/unity-toss-platform.mdc)**: 개발 규칙 및 가이드라인
- **[토스 통합 스킬](.cursor/skills/unity-toss-integration/)**: 개발 스킬 및 패턴

---

## 🛠️ 개발 도구

### Unity Editor 메뉴
- `Apps in Toss > Build & Deploy Window`: 빌드 및 배포 설정
- `Apps in Toss > Dev Server > Start Server`: 로컬 개발 서버 실행
- `Tools > 토스 빌드 설정`: 빌드 설정 확인 및 빠른 링크

### MCP 서버 (AI 개발 지원)
- `.cursor/mcp.json`에 앱인토스 MCP 서버 설정됨
- Cursor에서 `@docs` 명령으로 토스 SDK 문서 자동 참조
- 예: `@docs 토스 결제 API 사용법 알려줘`

---

## ⚙️ 설정 체크리스트

### 필수 설정
- [ ] 앱인토스 콘솔에서 앱 생성 및 앱 ID 발급
- [ ] Unity Editor에서 App ID 설정
- [ ] 아이콘 URL 설정
- [ ] 앱 카테고리를 **게임**으로 설정
- [ ] WebGL 빌드 설정 확인

### 결제 기능 (사용 시)
- [ ] 인앱 결제 상품 등록
- [ ] `AppsInTossLogin.GrantProductToServer()` 서버 연동 구현
- [ ] 결제 테스트

### 광고 기능 (사용 시)
- [ ] 광고 그룹 ID 발급
- [ ] 광고 그룹 ID를 코드에 설정
- [ ] `AppsInTossLogin.GrantAdReward()` 보상 지급 로직 구현
- [ ] 광고 테스트

---

## 🔧 문제 해결

### 로그인 실패
- 앱 카테고리가 **게임**으로 설정되었는지 확인
- WebGL 빌드인지 확인
- 콘솔에서 앱 상태 확인

### 결제/광고 미동작
- `IsAdSupported()` 반환값 확인
- 상품 ID/광고 그룹 ID 확인
- 네트워크 연결 확인

---

## 📖 참고 자료

- [앱인토스 개발자센터](https://developers-apps-in-toss.toss.im/)
- [앱인토스 Unity SDK GitHub](https://github.com/toss/apps-in-toss-unity-sdk)
- [AGENTS.md](AGENTS.md): 에이전트 팀 구성 및 역할

---

## 💡 주요 특징

1. **멀티플랫폼 지원**: 기본은 멀티플랫폼, 토스는 별도 빌드/설정으로 분리
2. **인터페이스 기반**: `LoginInterface`로 플랫폼별 구현 분리
3. **자동 플랫폼 선택**: WebGL 빌드 시 자동으로 토스 로그인 사용
4. **완전한 기능 지원**: 로그인, 결제, 광고 모두 구현 완료

---

**마지막 업데이트**: 2026-02-21
