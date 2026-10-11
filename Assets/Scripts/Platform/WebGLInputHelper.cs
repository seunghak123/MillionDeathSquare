using System.Runtime.InteropServices;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// WebGL 입력 포커스 관리 헬퍼.
    /// WebGLInput.jslib과 통신합니다.
    ///
    /// 해결 문제:
    ///   - 캔버스 자동 포커스 (페이지 로드 및 클릭 시)
    ///   - 화살표키/Space 키의 브라우저 기본 스크롤 차단
    ///   - 탭 복귀 시 자동 재포커스
    ///
    /// ApplicationManager.InitApplicationInfo()에서 Init() 호출.
    /// </summary>
    public static class WebGLInputHelper
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void WebGLInput_Init();
        [DllImport("__Internal")] private static extern void WebGLInput_FocusCanvas();
#endif

        /// <summary>
        /// WebGL 입력 시스템을 초기화합니다.
        /// 캔버스 포커스 자동화 및 키 기본 동작 차단.
        /// </summary>
        public static void Init()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput_Init();
#else
            Debug.Log("[WebGLInputHelper] Init (dev 모드 — 에디터에서는 no-op)");
#endif
        }

        /// <summary>캔버스 포커스를 수동으로 요청합니다.</summary>
        public static void FocusCanvas()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput_FocusCanvas();
#endif
        }
    }
}
