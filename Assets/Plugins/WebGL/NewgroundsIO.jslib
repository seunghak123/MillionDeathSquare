/**
 * Newgrounds.io SDK Bridge
 *
 * Newgrounds 게임 포털 연동용 jslib.
 * Newgrounds.io API 문서: https://www.newgrounds.io/help/getting-started/
 *
 * 동작 방식:
 *   1. NewgroundsTemplate/index.html에서 ngio.js 로드 + NGIO.init({apiId, encKey}) 호출
 *   2. NGBridge_Init() 호출 → 초기화 완료 대기
 *   3. NGBridge_UnlockMedal() — 메달(업적) 해제
 *   4. NGBridge_PostScore()   — 스코어보드 점수 제출
 *   5. NGBridge_Login()       — Newgrounds 계정 로그인 (선택)
 *
 * SendMessage 콜백:
 *   OnNGInitSuccess ""           — NGIO 준비 완료
 *   OnNGLoginResult "1"/"0"      — 로그인 성공/실패
 *   OnNGMedalResult "id:1"/"0"   — 메달 해제 성공/실패
 *   OnNGScoreResult "1"/"0"      — 점수 제출 성공/실패
 */
mergeInto(LibraryManager.library, {

    // ── 초기화 ─────────────────────────────────────────────────────────────
    // ngio.js는 NewgroundsTemplate/index.html에서 먼저 로드됨.
    // NGIO.init()은 템플릿에서 호출됨.
    // 이 함수는 NGIO 준비 상태를 폴링하여 확인합니다.
    NGBridge_Init: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        window.__ngGoName = goName;

        function checkReady(attempts) {
            if (typeof NGIO !== 'undefined' && NGIO.initialized) {
                unityInstance.SendMessage(goName, 'OnNGInitSuccess', '');
            } else if (typeof NGIO !== 'undefined') {
                // NGIO 존재하지만 아직 초기화 대기
                if (attempts > 0) {
                    setTimeout(function () { checkReady(attempts - 1); }, 200);
                } else {
                    // 타임아웃 — 비로그인 상태로 진행
                    console.warn('[NGBridge] NGIO 초기화 타임아웃 — 비로그인 게임 모드 진행');
                    unityInstance.SendMessage(goName, 'OnNGInitSuccess', '');
                }
            } else {
                // NGIO 없음 (Newgrounds 환경 아님)
                console.warn('[NGBridge] NGIO SDK 없음 — 비 Newgrounds 환경');
                unityInstance.SendMessage(goName, 'OnNGInitSuccess', '');
            }
        }

        checkReady(30); // 최대 6초 대기 (200ms × 30회)
    },

    // ── 로그인 ─────────────────────────────────────────────────────────────
    // Newgrounds 계정과 게임을 연결합니다. (선택적)
    // 로그인하지 않으면 메달/스코어는 기록되지 않습니다.
    NGBridge_Login: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);

        if (typeof NGIO === 'undefined') {
            unityInstance.SendMessage(goName, 'OnNGLoginResult', '0');
            return;
        }

        try {
            NGIO.requestLogin(function (result) {
                var success = (result && result.success) ? '1' : '0';
                unityInstance.SendMessage(goName, 'OnNGLoginResult', success);
            });
        } catch (e) {
            console.warn('[NGBridge] requestLogin 에러:', e);
            unityInstance.SendMessage(goName, 'OnNGLoginResult', '0');
        }
    },

    // ── 현재 유저 정보 ─────────────────────────────────────────────────────
    NGBridge_GetUserName: function () {
        if (typeof NGIO === 'undefined' || !NGIO.user) return 0;
        var name = NGIO.user.name || '';
        var bufSize = lengthBytesUTF8(name) + 1;
        var buf = _malloc(bufSize);
        stringToUTF8(name, buf, bufSize);
        return buf;
    },

    NGBridge_IsLoggedIn: function () {
        return (typeof NGIO !== 'undefined' && NGIO.user && NGIO.user.name) ? 1 : 0;
    },

    // ── 메달(업적) 해제 ────────────────────────────────────────────────────
    // medalId: Newgrounds 개발자 포털에서 생성한 메달 ID (정수)
    NGBridge_UnlockMedal: function (gameObjectNamePtr, medalId) {
        var goName = UTF8ToString(gameObjectNamePtr);

        if (typeof NGIO === 'undefined') {
            unityInstance.SendMessage(goName, 'OnNGMedalResult', '0');
            return;
        }

        try {
            NGIO.unlockMedal(medalId, function (result) {
                // result.medal.unlocked 또는 result.success
                var success = (result && (result.success || (result.medal && result.medal.unlocked))) ? '1' : '0';
                unityInstance.SendMessage(goName, 'OnNGMedalResult', 'id:' + medalId + ':' + success);
            });
        } catch (e) {
            console.warn('[NGBridge] unlockMedal 에러:', e);
            unityInstance.SendMessage(goName, 'OnNGMedalResult', '0');
        }
    },

    // ── 스코어보드 점수 제출 ────────────────────────────────────────────────
    // scoreboardId: Newgrounds 개발자 포털에서 생성한 스코어보드 ID
    // score: 점수 (정수)
    // tag: 선택적 태그 문자열 (빈 문자열 가능)
    NGBridge_PostScore: function (gameObjectNamePtr, scoreboardId, score, tagPtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var tag   = tagPtr ? UTF8ToString(tagPtr) : '';

        if (typeof NGIO === 'undefined') {
            unityInstance.SendMessage(goName, 'OnNGScoreResult', '0');
            return;
        }

        try {
            NGIO.postScore(scoreboardId, score, tag, function (result) {
                var success = (result && result.success) ? '1' : '0';
                unityInstance.SendMessage(goName, 'OnNGScoreResult', success);
            });
        } catch (e) {
            console.warn('[NGBridge] postScore 에러:', e);
            unityInstance.SendMessage(goName, 'OnNGScoreResult', '0');
        }
    },

    // ── 상태 확인 ──────────────────────────────────────────────────────────
    NGBridge_IsAvailable: function () {
        return (typeof NGIO !== 'undefined') ? 1 : 0;
    },
});
