/**
 * TossBridge.jslib
 * Unity WebGL ↔ Toss 웹뷰 환경 브릿지 (환경 감지 / 포커스 콜백 전용)
 *
 * Toss SDK 기능(유저 키, 리더보드, 공유, 친구 초대, 프로모션, 햅틱, 저장소, 광고, 결제)은
 * 모두 공식 Unity SDK(AppsInToss.AIT.*)를 C#에서 직접 호출합니다.
 * 여기에는 SDK가 제공하지 않는 것만 남깁니다.
 */
mergeInto(LibraryManager.library, {

    // =========================================================================
    // 환경 감지
    // =========================================================================

    TossBridge_IsInsideTossApp: function() {
        return (window.ReactNativeWebView && typeof window.ReactNativeWebView.postMessage === 'function') ? 1 : 0;
    },

    // =========================================================================
    // WebView 포커스 / 가시성 콜백
    // =========================================================================

    TossBridge_RegisterFocusCallback: function(gameObjectName) {
        var goName = UTF8ToString(gameObjectName);

        document.addEventListener('visibilitychange', function() {
            var focused = !document.hidden;
            if (typeof unityInstance !== 'undefined' && unityInstance)
                unityInstance.SendMessage(goName, 'OnWebViewFocusChanged', focused ? 'true' : 'false');
            console.log('[TossBridge] visibilitychange →', focused ? 'foreground' : 'background');
        });
        window.addEventListener('pagehide', function() {
            if (typeof unityInstance !== 'undefined' && unityInstance)
                unityInstance.SendMessage(goName, 'OnWebViewFocusChanged', 'false');
        });
        window.addEventListener('pageshow', function() {
            if (typeof unityInstance !== 'undefined' && unityInstance)
                unityInstance.SendMessage(goName, 'OnWebViewFocusChanged', 'true');
        });
        console.log('[TossBridge] 포커스 콜백 등록 완료 → GameObject:', goName);
    }

});
