/**
 * CrazyGames SDK Bridge
 *
 * CrazyGames HTML5 게임 포털 연동용 jslib.
 * CrazyGames SDK 문서: https://docs.crazygames.com/sdk/unity/
 *
 * 동작 방식:
 *   CrazyGames SDK는 index.html에 자동 삽입된 스크립트로 제공됩니다.
 *   (빌드 시 CrazyGames 포털이 자동 주입 — 별도 script 로드 불필요)
 *   window.CrazyGames.SDK 가 있으면 사용 가능.
 *
 * SendMessage 콜백:
 *   OnCGInitSuccess  ""       — 초기화 완료
 *   OnCGAdStarted    ""       — 광고 시작 (오디오 뮤트 필요)
 *   OnCGAdFinished   "1"/"0"  — 광고 완료/에러 (1=시청, 0=스킵/에러)
 */
mergeInto(LibraryManager.library, {

    // ── SDK 초기화 ─────────────────────────────────────────────────────────
    CGBridge_Init: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        window.__cgGoName = goName;

        // SDK 준비 대기 (CrazyGames 포털이 주입하는 시점이 다를 수 있음)
        function tryInit(attempts) {
            if (typeof window.CrazyGames !== 'undefined' && window.CrazyGames.SDK) {
                window.__cgInitDone = true;
                unityInstance.SendMessage(goName, 'OnCGInitSuccess', '');
            } else if (attempts > 0) {
                setTimeout(function () { tryInit(attempts - 1); }, 200);
            } else {
                // CrazyGames 환경이 아님 — 에디터 테스트 등에서 정상
                console.warn('[CGBridge] CrazyGames SDK 없음 — 비 CrazyGames 환경으로 간주');
                unityInstance.SendMessage(goName, 'OnCGInitSuccess', '');
            }
        }

        tryInit(25); // 최대 5초 대기 (200ms × 25회)
    },

    // ── 로딩 상태 알림 ─────────────────────────────────────────────────────

    /**
     * SDK.game.sdkGameLoadingStart() — [필수] Unity 번들 로드 시작 시 호출
     * CrazyGames 포털이 로딩 화면을 표시합니다.
     */
    CGBridge_LoadingStart: function () {
        try {
            if (window.CrazyGames && window.CrazyGames.SDK && window.CrazyGames.SDK.game)
                window.CrazyGames.SDK.game.sdkGameLoadingStart();
        } catch (e) { console.warn('[CGBridge] sdkGameLoadingStart error', e); }
    },

    /**
     * SDK.game.sdkGameLoadingStop() — [필수] Unity 로드 완료 시 호출
     * CrazyGames 포털 로딩 화면을 해제합니다.
     */
    CGBridge_LoadingStop: function () {
        try {
            if (window.CrazyGames && window.CrazyGames.SDK && window.CrazyGames.SDK.game)
                window.CrazyGames.SDK.game.sdkGameLoadingStop();
        } catch (e) { console.warn('[CGBridge] sdkGameLoadingStop error', e); }
    },

    // ── 게임플레이 상태 알림 ────────────────────────────────────────────────

    CGBridge_GameplayStart: function () {
        try {
            if (window.CrazyGames && window.CrazyGames.SDK && window.CrazyGames.SDK.game)
                window.CrazyGames.SDK.game.gameplayStart();
        } catch (e) { console.warn('[CGBridge] gameplayStart error', e); }
    },

    CGBridge_GameplayStop: function () {
        try {
            if (window.CrazyGames && window.CrazyGames.SDK && window.CrazyGames.SDK.game)
                window.CrazyGames.SDK.game.gameplayStop();
        } catch (e) { console.warn('[CGBridge] gameplayStop error', e); }
    },

    /**
     * SDK.game.happytime() — 플레이어가 기쁜 순간(레벨업, 고득점 등)에 호출
     * CrazyGames 포털이 confetti 등 축하 연출을 트리거합니다.
     */
    CGBridge_HappyTime: function () {
        try {
            if (window.CrazyGames && window.CrazyGames.SDK && window.CrazyGames.SDK.game)
                window.CrazyGames.SDK.game.happytime();
        } catch (e) { console.warn('[CGBridge] happytime error', e); }
    },

    // ── 전면 광고 ──────────────────────────────────────────────────────────

    CGBridge_ShowInterstitial: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);

        if (!window.CrazyGames || !window.CrazyGames.SDK) {
            unityInstance.SendMessage(goName, 'OnCGAdFinished', '0');
            return;
        }

        try {
            window.CrazyGames.SDK.ad.requestAd('midgame', {
                adStarted: function () {
                    unityInstance.SendMessage(goName, 'OnCGAdStarted', '');
                },
                adFinished: function () {
                    unityInstance.SendMessage(goName, 'OnCGAdFinished', '1');
                },
                adError: function (error) {
                    console.warn('[CGBridge] 광고 에러:', error);
                    unityInstance.SendMessage(goName, 'OnCGAdFinished', '0');
                },
            });
        } catch (e) {
            console.warn('[CGBridge] requestAd(midgame) 예외:', e);
            unityInstance.SendMessage(goName, 'OnCGAdFinished', '0');
        }
    },

    // ── 보상형 광고 ────────────────────────────────────────────────────────

    CGBridge_ShowRewarded: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);

        if (!window.CrazyGames || !window.CrazyGames.SDK) {
            unityInstance.SendMessage(goName, 'OnCGAdFinished', '0');
            return;
        }

        try {
            window.CrazyGames.SDK.ad.requestAd('rewarded', {
                adStarted: function () {
                    unityInstance.SendMessage(goName, 'OnCGAdStarted', '');
                },
                adFinished: function () {
                    unityInstance.SendMessage(goName, 'OnCGAdFinished', '1');
                },
                adError: function (error) {
                    console.warn('[CGBridge] 보상 광고 에러:', error);
                    unityInstance.SendMessage(goName, 'OnCGAdFinished', '0');
                },
            });
        } catch (e) {
            console.warn('[CGBridge] requestAd(rewarded) 예외:', e);
            unityInstance.SendMessage(goName, 'OnCGAdFinished', '0');
        }
    },

    // ── 상태 확인 ──────────────────────────────────────────────────────────

    CGBridge_IsAvailable: function () {
        return (typeof window.CrazyGames !== 'undefined' && window.CrazyGames.SDK) ? 1 : 0;
    },
});
