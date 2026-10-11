/**
 * FacebookBridge.jslib
 * Unity WebGL ↔ Facebook Instant Games SDK (FBInstant 8.0) 브릿지
 *
 * 전제조건: index.html에 FBInstant SDK 스크립트가 로드되어 있어야 합니다.
 *   <script src="https://connect.facebook.net/en_US/fbinstant.8.0.js"></script>
 *
 * 비동기 패턴: JS Promise 완료 시 unityInstance.SendMessage(goName, method, data) 로 C#에 콜백.
 */
mergeInto(LibraryManager.library, {

    // =========================================================================
    // 초기화 / 필수 API
    // =========================================================================

    /** FBInstant.initializeAsync() — 가장 먼저 호출 */
    FBBridge_Initialize: function(gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined') {
            console.warn('[FacebookBridge] FBInstant SDK를 찾을 수 없습니다.');
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBInitFailed', 'FBInstant not loaded');
            return;
        }
        FBInstant.initializeAsync()
            .then(function() {
                console.log('[FacebookBridge] 초기화 성공');
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBInitSuccess', '');
            })
            .catch(function(err) {
                console.error('[FacebookBridge] 초기화 실패:', err);
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBInitFailed', err ? err.toString() : 'init error');
            });
    },

    /**
     * FBInstant.setLoadingProgress(percent) — [필수] 로딩 진행률 보고
     * @param {number} percent 0~100
     */
    FBBridge_SetLoadingProgress: function(percent) {
        if (typeof FBInstant === 'undefined') return;
        FBInstant.setLoadingProgress(percent);
    },

    /** FBInstant.startGameAsync() — 로딩 완료 선언 */
    FBBridge_StartGame: function(gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined') {
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBStartGameFailed', 'FBInstant not loaded');
            return;
        }
        FBInstant.startGameAsync()
            .then(function() {
                console.log('[FacebookBridge] startGame 성공');
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBStartGameSuccess', '');
            })
            .catch(function(err) {
                console.error('[FacebookBridge] startGame 실패:', err);
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBStartGameFailed', err ? err.toString() : 'startGame error');
            });
    },

    /**
     * FBInstant.onPause(callback) — [필수] 앱 일시정지 콜백 등록
     * 전화, 알림 등으로 앱이 중단될 때 C#으로 'OnFBPause("")' 전송
     */
    FBBridge_RegisterPauseCallback: function(gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined') return;
        FBInstant.onPause(function() {
            console.log('[FacebookBridge] onPause 발생');
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBPause', '');
        });
        console.log('[FacebookBridge] onPause 콜백 등록 완료');
    },

    // =========================================================================
    // 플레이어 정보
    // =========================================================================

    FBBridge_GetPlayerId: function() {
        if (typeof FBInstant === 'undefined' || !FBInstant.player) return null;
        var id = FBInstant.player.getID();
        if (!id) return null;
        var buffer = _malloc(lengthBytesUTF8(id) + 1);
        stringToUTF8(id, buffer, lengthBytesUTF8(id) + 1);
        return buffer;
    },

    FBBridge_GetPlayerName: function() {
        if (typeof FBInstant === 'undefined' || !FBInstant.player) return null;
        var name = FBInstant.player.getName();
        if (!name) return null;
        var buffer = _malloc(lengthBytesUTF8(name) + 1);
        stringToUTF8(name, buffer, lengthBytesUTF8(name) + 1);
        return buffer;
    },

    /**
     * FBInstant.player.getPhoto() — 플레이어 프로필 사진 URL (동기, null 가능)
     * SDK 8.0: getPhoto()는 동기 API로 즉시 URL 문자열 반환
     */
    FBBridge_GetPlayerPhoto: function() {
        if (typeof FBInstant === 'undefined' || !FBInstant.player) return null;
        var photo = FBInstant.player.getPhoto();
        if (!photo) return null;
        var buffer = _malloc(lengthBytesUTF8(photo) + 1);
        stringToUTF8(photo, buffer, lengthBytesUTF8(photo) + 1);
        return buffer;
    },

    // =========================================================================
    // 플레이어 클라우드 저장소
    // =========================================================================

    /**
     * FBInstant.player.setDataAsync(data) — 플레이어 데이터 저장
     * dataJson: JSON 직렬화된 key-value 객체
     * 성공: OnFBSetDataSuccess(""), 실패: OnFBSetDataFailed(error)
     */
    FBBridge_SetPlayerData: function(gameObjectNamePtr, dataJsonPtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var dataJson = UTF8ToString(dataJsonPtr);
        if (typeof FBInstant === 'undefined') return;
        var data;
        try { data = JSON.parse(dataJson); } catch(e) { data = {}; }
        FBInstant.player.setDataAsync(data)
            .then(function() {
                // SDK 8.0: setDataAsync 후 flushDataAsync 필수 — 서버 동기화 보장
                return FBInstant.player.flushDataAsync();
            })
            .then(function() {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBSetDataSuccess', '');
            })
            .catch(function(err) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBSetDataFailed', err ? err.toString() : 'setData error');
            });
    },

    /**
     * FBInstant.player.getDataAsync(keys) — 플레이어 데이터 로드
     * keysJson: JSON 배열 ex) ["score","level"]
     * 성공: OnFBGetDataLoaded(jsonString), 실패: OnFBGetDataFailed(error)
     */
    FBBridge_GetPlayerData: function(gameObjectNamePtr, keysJsonPtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var keysJson = UTF8ToString(keysJsonPtr);
        if (typeof FBInstant === 'undefined') return;
        var keys;
        try { keys = JSON.parse(keysJson); } catch(e) { keys = []; }
        FBInstant.player.getDataAsync(keys)
            .then(function(data) {
                var json = JSON.stringify(data || {});
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBGetDataLoaded', json);
            })
            .catch(function(err) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBGetDataFailed', err ? err.toString() : 'getData error');
            });
    },

    // =========================================================================
    // 봇 구독 (Bot Subscription)
    // =========================================================================

    /**
     * FBInstant.player.canSubscribeBotAsync()
     * 성공: OnFBCanSubscribeBot("1"/"0")
     */
    FBBridge_CanSubscribeBot: function(gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined' || !FBInstant.player) {
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBCanSubscribeBot', '0');
            return;
        }
        FBInstant.player.canSubscribeBotAsync()
            .then(function(can) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBCanSubscribeBot', can ? '1' : '0');
            })
            .catch(function() {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBCanSubscribeBot', '0');
            });
    },

    /**
     * FBInstant.player.subscribeBotAsync() — 봇 구독 다이얼로그 표시
     * 성공: OnFBSubscribeBotSuccess(""), 실패: OnFBSubscribeBotFailed(error)
     * ※ 주당 1회 다이얼로그 제한
     */
    FBBridge_SubscribeBot: function(gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined' || !FBInstant.player) return;
        FBInstant.player.subscribeBotAsync()
            .then(function() {
                console.log('[FacebookBridge] 봇 구독 완료');
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBSubscribeBotSuccess', '');
            })
            .catch(function(err) {
                var msg = err ? err.toString() : 'subscribe error';
                console.warn('[FacebookBridge] 봇 구독 실패/거부:', msg);
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBSubscribeBotFailed', msg);
            });
    },

    // =========================================================================
    // 홈화면 바로가기 (Shortcut)
    // =========================================================================

    /**
     * FBInstant.canCreateShortcutAsync()
     * 성공: OnFBCanCreateShortcut("1"/"0")
     */
    FBBridge_CanCreateShortcut: function(gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined') {
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBCanCreateShortcut', '0');
            return;
        }
        FBInstant.canCreateShortcutAsync()
            .then(function(can) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBCanCreateShortcut', can ? '1' : '0');
            })
            .catch(function() {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBCanCreateShortcut', '0');
            });
    },

    /**
     * FBInstant.createShortcutAsync() — 홈화면 바로가기 추가 (세션당 1회)
     * 성공: OnFBCreateShortcutSuccess(""), 실패/거부: OnFBCreateShortcutFailed(error)
     */
    FBBridge_CreateShortcut: function(gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined') return;
        FBInstant.createShortcutAsync()
            .then(function() {
                console.log('[FacebookBridge] 바로가기 추가 완료');
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBCreateShortcutSuccess', '');
            })
            .catch(function(err) {
                var msg = err ? err.toString() : 'shortcut error';
                console.warn('[FacebookBridge] 바로가기 추가 실패/거부:', msg);
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBCreateShortcutFailed', msg);
            });
    },

    // =========================================================================
    // 분석 (Analytics)
    // =========================================================================

    /**
     * FBInstant.logEvent(eventName, valueToSum, parameters)
     * paramsJson: JSON 직렬화된 파라미터 (optional, "" 전달 가능)
     */
    FBBridge_LogEvent: function(eventNamePtr, value, paramsJsonPtr) {
        var eventName = UTF8ToString(eventNamePtr);
        var paramsJson = UTF8ToString(paramsJsonPtr);
        if (typeof FBInstant === 'undefined') return;
        var params = null;
        if (paramsJson && paramsJson.length > 0) {
            try { params = JSON.parse(paramsJson); } catch(e) {}
        }
        var err = FBInstant.logEvent(eventName, value > 0 ? value : null, params);
        if (err) console.warn('[FacebookBridge] logEvent 오류:', err);
    },

    // =========================================================================
    // 리더보드
    // =========================================================================

    FBBridge_SubmitScore: function(gameObjectNamePtr, leaderboardNamePtr, score) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var lbName = UTF8ToString(leaderboardNamePtr);
        if (typeof FBInstant === 'undefined') {
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBScoreSubmitFailed', 'FBInstant not loaded');
            return;
        }
        FBInstant.getLeaderboardAsync(lbName)
            .then(function(lb) { return lb.setScoreAsync(score); })
            .then(function(entry) {
                var result = entry ? entry.getScore().toString() : score.toString();
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBScoreSubmitSuccess', result);
            })
            .catch(function(err) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBScoreSubmitFailed', err ? err.toString() : 'submit error');
            });
    },

    FBBridge_GetFriendScores: function(gameObjectNamePtr, leaderboardNamePtr, count) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var lbName = UTF8ToString(leaderboardNamePtr);
        if (typeof FBInstant === 'undefined') {
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBFriendScoresFailed', 'FBInstant not loaded');
            return;
        }
        FBInstant.getLeaderboardAsync(lbName)
            .then(function(lb) { return lb.getConnectedPlayerEntriesAsync(count, 0); })
            .then(function(entries) {
                var result = [];
                for (var i = 0; i < entries.length; i++) {
                    var e = entries[i];
                    result.push({
                        playerId: e.getPlayer().getID(),
                        name: e.getPlayer().getName(),
                        score: e.getScore(),
                        rank: e.getRank()
                    });
                }
                var json = JSON.stringify(result);
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBFriendScoresLoaded', json);
            })
            .catch(function(err) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBFriendScoresFailed', err ? err.toString() : 'load error');
            });
    },

    // =========================================================================
    // 공유 / 초대
    // =========================================================================

    /**
     * FBInstant.shareAsync() — 결과 공유
     * intent: "SHARE"|"CHALLENGE"|"INVITE"|"REQUEST"
     */
    FBBridge_Share: function(gameObjectNamePtr, textPtr, imageUrlPtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var text = UTF8ToString(textPtr);
        var imageUrl = UTF8ToString(imageUrlPtr);
        if (typeof FBInstant === 'undefined') {
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBShareFailed', 'FBInstant not loaded');
            return;
        }
        FBInstant.shareAsync({
            intent: 'CHALLENGE',
            image: imageUrl,
            text: text,
            data: { timestamp: Date.now() }
        })
            .then(function() {
                console.log('[FacebookBridge] 공유 완료');
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBShareSuccess', '');
            })
            .catch(function(err) {
                var msg = err ? err.toString() : 'share cancelled';
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBShareFailed', msg);
            });
    },

    /**
     * FBInstant.inviteAsync() — 친구 초대 다이얼로그
     * 성공: OnFBInviteSuccess(""), 실패/취소: OnFBInviteFailed(error)
     */
    FBBridge_Invite: function(gameObjectNamePtr, textPtr, imageUrlPtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var text = UTF8ToString(textPtr);
        var imageUrl = UTF8ToString(imageUrlPtr);
        if (typeof FBInstant === 'undefined') return;
        FBInstant.inviteAsync({
            image: imageUrl,
            text: text,
            data: { timestamp: Date.now() }
        })
            .then(function() {
                console.log('[FacebookBridge] 초대 완료');
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBInviteSuccess', '');
            })
            .catch(function(err) {
                var msg = err ? err.toString() : 'invite cancelled';
                console.warn('[FacebookBridge] 초대 취소/실패:', msg);
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBInviteFailed', msg);
            });
    },

    // =========================================================================
    // 토너먼트 (Tournament)
    // =========================================================================

    /**
     * FBInstant.tournament.getTournamentAsync() — 현재 Context의 토너먼트 정보 조회
     * ※ SDK 8.0: FBInstant.getTournamentAsync() 는 deprecated → FBInstant.tournament.* 네임스페이스 사용
     * 성공: OnFBTournamentLoaded(jsonString), 실패: OnFBTournamentFailed(error)
     */
    FBBridge_GetTournament: function(gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined' || !FBInstant.tournament) {
            if (typeof unityInstance !== 'undefined')
                unityInstance.SendMessage(goName, 'OnFBTournamentFailed', 'tournament API not available');
            return;
        }
        FBInstant.tournament.getTournamentAsync()
            .then(function(tournament) {
                var info = {
                    id: tournament.getID(),
                    contextID: tournament.getContextID(),
                    endTime: tournament.getEndTime(),
                    title: tournament.getTitle() || ''
                };
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBTournamentLoaded', JSON.stringify(info));
            })
            .catch(function(err) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBTournamentFailed', err ? err.toString() : 'no tournament');
            });
    },

    /**
     * FBInstant.tournament.createAsync() — 토너먼트 생성
     * scorePtr: 초기 점수 문자열
     * titlePtr: 토너먼트 타이틀 (optional)
     */
    FBBridge_CreateTournament: function(gameObjectNamePtr, scorePtr, titlePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        var score = parseInt(UTF8ToString(scorePtr), 10) || 0;
        var title = UTF8ToString(titlePtr);
        if (typeof FBInstant === 'undefined' || !FBInstant.tournament) return;
        var payload = {
            initialScore: score,
            config: { title: title || 'Million Death Square 토너먼트' }
        };
        FBInstant.tournament.createAsync(payload)
            .then(function() {
                console.log('[FacebookBridge] 토너먼트 생성 완료');
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBTournamentCreated', '');
            })
            .catch(function(err) {
                var msg = err ? err.toString() : 'create tournament error';
                console.warn('[FacebookBridge] 토너먼트 생성 실패:', msg);
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBTournamentCreateFailed', msg);
            });
    },

    /**
     * FBInstant.tournament.postScoreAsync(score) — 토너먼트 점수 등록
     * 토너먼트 세션 중 게임 종료 시 호출
     */
    FBBridge_PostTournamentScore: function(gameObjectNamePtr, score) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined' || !FBInstant.tournament) return;
        FBInstant.tournament.postScoreAsync(score)
            .then(function() {
                console.log('[FacebookBridge] 토너먼트 점수 등록 완료');
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBTournamentScorePosted', '');
            })
            .catch(function(err) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBTournamentScorePostFailed', err ? err.toString() : 'post score error');
            });
    },

    /**
     * FBInstant.tournament.shareAsync() — 토너먼트 공유
     * 현재 토너먼트 세션 중에만 호출 가능
     */
    FBBridge_ShareTournament: function(gameObjectNamePtr, score) {
        var goName = UTF8ToString(gameObjectNamePtr);
        if (typeof FBInstant === 'undefined' || !FBInstant.tournament) return;
        FBInstant.tournament.shareAsync({ score: score })
            .then(function() {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBTournamentShared', '');
            })
            .catch(function(err) {
                if (typeof unityInstance !== 'undefined')
                    unityInstance.SendMessage(goName, 'OnFBTournamentShareFailed', err ? err.toString() : 'share error');
            });
    },

    // =========================================================================
    // 환경 감지
    // =========================================================================

    FBBridge_IsAvailable: function() {
        return (typeof FBInstant !== 'undefined') ? 1 : 0;
    }

});
