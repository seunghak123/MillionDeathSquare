/**
 * GameDistribution SDK Bridge
 *
 * GameDistribution HTML5 게임 포털 연동용 jslib.
 * GD SDK 문서: https://gamedistribution.com/sdk/html5
 *
 * 동작 방식:
 *   1. GDBridge_Init() 호출 → gdsdk.js 동적 로드 → GD.init({...})
 *   2. 게임 시작 전 GDBridge_GameplayStart(), 일시정지/종료 시 GDBridge_GameplayStop()
 *   3. GDBridge_ShowInterstitial() / GDBridge_ShowRewarded() → 광고 표시
 *
 * SendMessage 콜백 (goName → 메서드 → 파라미터):
 *   OnGDInitSuccess  ""       — 초기화 완료
 *   OnGDInitFailed   "error"  — 초기화 실패
 *   OnGDAdComplete   "1"/"0"  — 전면/보상 광고 완료 (1=시청, 0=스킵/에러)
 */
mergeInto(LibraryManager.library, {

    // ── 내부 상태 ──────────────────────────────────────────────────────────
    _gdInitialized: false,
    _gdGoName: '',
    _gdAdCallback: null,

    // ── GD SDK 동적 로드 ───────────────────────────────────────────────────
    GDBridge_Init: function (gameObjectNamePtr, gameIdPtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var gameId = UTF8ToString(gameIdPtr);

        // 이미 초기화됨
        if (window.__gdInitDone) {
            unityInstance.SendMessage(goName, 'OnGDInitSuccess', '');
            return;
        }

        window.__gdInitGoName = goName;
        window.__gdAdCallback = null;

        // gdsdk.js 로드
        var script = document.createElement('script');
        script.src = 'https://html5.api.gamedistribution.com/main.min.js';
        script.onload = function () {
            if (typeof GD === 'undefined') {
                unityInstance.SendMessage(goName, 'OnGDInitFailed', 'GD SDK 로드 실패');
                return;
            }

            GD.init({
                gameId: gameId,
                onEvent: function (event) {
                    switch (event.name) {
                        case 'SDK_READY':
                            window.__gdInitDone = true;
                            unityInstance.SendMessage(window.__gdInitGoName, 'OnGDInitSuccess', '');
                            break;
                        case 'SDK_ERROR':
                            unityInstance.SendMessage(window.__gdInitGoName, 'OnGDInitFailed',
                                event.message || 'SDK_ERROR');
                            break;
                        case 'ADVERTISEMENT_STARTED':
                            // 광고 시작 — Unity 오디오/게임 일시정지는 AdManager가 처리
                            break;
                        case 'ADVERTISEMENT_FINISHED':
                            if (window.__gdAdCallback) {
                                window.__gdAdCallback('1'); // 시청 완료
                                window.__gdAdCallback = null;
                            }
                            break;
                        case 'ADVERTISEMENT_ERROR':
                        case 'ADVERTISEMENT_CANCELLED':
                            if (window.__gdAdCallback) {
                                window.__gdAdCallback('0'); // 실패/스킵
                                window.__gdAdCallback = null;
                            }
                            break;
                    }
                }
            });
        };
        script.onerror = function () {
            unityInstance.SendMessage(goName, 'OnGDInitFailed', 'gdsdk.js 로드 실패 (네트워크 오류)');
        };
        document.head.appendChild(script);
    },

    // ── 게임플레이 상태 알림 ────────────────────────────────────────────────
    // 필수: 광고 빈도 계산에 사용됨. 호출 누락 시 광고 수익 감소.

    GDBridge_GameplayStart: function () {
        if (typeof GD !== 'undefined' && GD.gameplayStart) {
            try { GD.gameplayStart(); } catch(e) {}
        }
    },

    GDBridge_GameplayStop: function () {
        if (typeof GD !== 'undefined' && GD.gameplayStop) {
            try { GD.gameplayStop(); } catch(e) {}
        }
    },

    // ── 전면 광고 ──────────────────────────────────────────────────────────

    GDBridge_ShowInterstitial: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);

        if (typeof GD === 'undefined' || !window.__gdInitDone) {
            unityInstance.SendMessage(goName, 'OnGDAdComplete', '0');
            return;
        }

        window.__gdAdCallback = function (result) {
            unityInstance.SendMessage(goName, 'OnGDAdComplete', result);
        };

        try {
            GD.showAd('interstitial');
        } catch (e) {
            if (window.__gdAdCallback) { window.__gdAdCallback('0'); window.__gdAdCallback = null; }
        }
    },

    // ── 보상형 광고 ────────────────────────────────────────────────────────

    GDBridge_ShowRewarded: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);

        if (typeof GD === 'undefined' || !window.__gdInitDone) {
            unityInstance.SendMessage(goName, 'OnGDAdComplete', '0');
            return;
        }

        window.__gdAdCallback = function (result) {
            unityInstance.SendMessage(goName, 'OnGDAdComplete', result);
        };

        try {
            GD.showAd('rewarded');
        } catch (e) {
            if (window.__gdAdCallback) { window.__gdAdCallback('0'); window.__gdAdCallback = null; }
        }
    },

    // ── 상태 확인 ──────────────────────────────────────────────────────────

    GDBridge_IsAvailable: function () {
        return (window.__gdInitDone === true) ? 1 : 0;
    },
});
