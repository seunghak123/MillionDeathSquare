/**
 * FossilTilt.jslib
 * Unity WebGL ↔ 브라우저 deviceorientation 브릿지 (지각 변동 모드의 폰 기울기 입력)
 *
 * - beta  : 앞뒤 기울기 (-180~180, 폰 윗부분을 앞으로 숙이면 감소)
 * - gamma : 좌우 기울기 (-90~90, 오른쪽을 내리면 증가)
 * iOS 13+ 는 DeviceOrientationEvent.requestPermission()을 "사용자 제스처 안에서" 불러야 한다.
 * Unity의 버튼 클릭은 다음 프레임에 처리돼 제스처 밖이므로, 첫 터치/클릭 때 권한을 요청하는 리스너를 단다.
 */
mergeInto(LibraryManager.library, {

    FossilTilt_Start: function () {
        if (window.__fossilTilt) return;
        var st = window.__fossilTilt = { beta: 0, gamma: 0, has: 0, denied: 0 };

        function onOrientation(e) {
            if (e.beta === null || e.gamma === null) return;
            st.beta = e.beta;
            st.gamma = e.gamma;
            st.has = 1;
        }

        function listen() {
            window.addEventListener('deviceorientation', onOrientation, true);
        }

        if (typeof DeviceOrientationEvent !== 'undefined' &&
            typeof DeviceOrientationEvent.requestPermission === 'function') {
            // iOS: 사용자 제스처에서 권한 요청
            var ask = function () {
                DeviceOrientationEvent.requestPermission().then(function (r) {
                    if (r === 'granted') listen(); else st.denied = 1;
                }).catch(function () { st.denied = 1; });
                document.removeEventListener('touchend', ask, true);
                document.removeEventListener('click', ask, true);
            };
            document.addEventListener('touchend', ask, true);
            document.addEventListener('click', ask, true);
        } else if (typeof DeviceOrientationEvent !== 'undefined') {
            listen();
        } else {
            st.denied = 1;
        }
    },

    /** 0 = 아직 값 없음, 1 = 수신 중, -1 = 미지원/거부 */
    FossilTilt_State: function () {
        var st = window.__fossilTilt;
        if (!st) return 0;
        if (st.has) return 1;
        return st.denied ? -1 : 0;
    },

    FossilTilt_GetBeta: function () {
        return window.__fossilTilt ? window.__fossilTilt.beta : 0;
    },

    FossilTilt_GetGamma: function () {
        return window.__fossilTilt ? window.__fossilTilt.gamma : 0;
    },
});
