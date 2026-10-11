/**
 * WebGL 입력 포커스 관리 브릿지
 *
 * 해결하는 문제:
 *   1. 캔버스 자동 포커스 — 페이지 로드 및 클릭 시 즉시 포커스
 *   2. 키보드 기본 동작 차단 — 화살표키 스크롤, Space 스크롤 방지
 *   3. 포커스 상실 감지 — 캔버스 클릭 리스너로 재포커스
 *
 * 사용법: ApplicationManager.InitApplicationInfo() 에서
 *   WebGLInput_Init() 한 번 호출
 */
mergeInto(LibraryManager.library, {

    WebGLInput_Init: function () {
        var canvas = document.getElementById('unity-canvas');
        if (!canvas) {
            console.warn('[WebGLInput] unity-canvas 요소를 찾을 수 없음');
            return;
        }

        // ── 1. 즉시 포커스 ──────────────────────────────────────────────
        function focusCanvas() {
            if (document.activeElement !== canvas) {
                canvas.focus();
            }
        }

        // tabindex가 없으면 포커스 불가 — 보장
        if (canvas.getAttribute('tabindex') === null) {
            canvas.setAttribute('tabindex', '-1');
        }

        // 로드 즉시 포커스
        focusCanvas();

        // ── 2. 클릭 시 포커스 복구 (body 전체) ─────────────────────────
        // 사용자가 외부를 클릭해도 canvas로 포커스 반환
        document.addEventListener('click', function (e) {
            // 입력 요소(input, textarea 등) 클릭은 제외
            var tag = e.target && e.target.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
            focusCanvas();
        }, false);

        // 터치 시작 시에도 포커스 (모바일)
        document.addEventListener('touchstart', function () {
            focusCanvas();
        }, { passive: true });

        // ── 3. 게임에서 필요한 키 기본 동작 차단 ───────────────────────
        // 화살표키/Space 가 페이지 스크롤하는 것을 방지
        var BLOCKED_KEYS = {
            'ArrowUp': true,
            'ArrowDown': true,
            'ArrowLeft': true,
            'ArrowRight': true,
            'Space': true,
            ' ': true,
        };

        canvas.addEventListener('keydown', function (e) {
            if (BLOCKED_KEYS[e.key] || BLOCKED_KEYS[e.code]) {
                e.preventDefault();
            }
        }, false);

        // ── 4. visibilitychange: 탭 복귀 시 재포커스 ───────────────────
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden) {
                setTimeout(focusCanvas, 100);
            }
        }, false);

        console.log('[WebGLInput] 초기화 완료 — 캔버스 자동 포커스 활성화');
    },

    // 외부에서 수동 포커스 요청 (디버그용)
    WebGLInput_FocusCanvas: function () {
        var canvas = document.getElementById('unity-canvas');
        if (canvas) canvas.focus();
    },
});
