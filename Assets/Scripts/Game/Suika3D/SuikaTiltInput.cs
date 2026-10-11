using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 지각 변동 모드의 폰 기울기 입력 (기획서 9.5).
    ///  - 기준 자세 재설정: Calibrate() 시점의 폰 각도를 "수평"으로 본다 (누워서 해도 OK)
    ///  - 데드존 ±3°, 저역 통과 필터로 튀는 센서 값 보정, 감도 낮음/보통/높음, 최대 15°
    ///  - 입력 경로: WebGL = 브라우저 deviceorientation (FossilTilt.jslib) / 네이티브 = 자이로 중력 → 가속도 센서 / 에디터 = I·J·K·L 키
    ///
    /// 출력 Tilt는 "화면 기준" 기울기(도): x = 오른쪽이 내려간 정도, y = 화면 위쪽(먼 쪽)이 내려간 정도.
    /// 발굴대는 카메라 방향을 반영해 월드 기울기로 바꿔 쓴다.
    /// </summary>
    public class SuikaTiltInput : MonoBehaviour
    {
        [SerializeField] private float deadzoneDeg = 3f;
        [SerializeField] private float maxTiltDeg = 15f;
        [Tooltip("센서 보정 (클수록 즉각, 작을수록 부드러움)")]
        [SerializeField] private float smoothing = 10f;
        [Tooltip("감도 배율: 낮음 / 보통 / 높음")]
        [SerializeField] private float[] sensitivityGains = { 0.6f, 1f, 1.5f };
        [Tooltip("기기마다 앞뒤 방향이 반대로 느껴지면 켠다")]
        [SerializeField] private bool invertForward = false;
        [Tooltip("에디터에서 I/J/K/L 키로 기울기 흉내 (도/초)")]
        [SerializeField] private float editorKeySpeed = 30f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void FossilTilt_Start();
        [DllImport("__Internal")] private static extern int FossilTilt_State();
        [DllImport("__Internal")] private static extern float FossilTilt_GetBeta();
        [DllImport("__Internal")] private static extern float FossilTilt_GetGamma();
#endif

        private Vector2 _raw;        // 센서 원값 (x = 좌우, y = 앞뒤) 도
        private Vector2 _baseline;   // 기준 자세
        private Vector2 _smoothed;
        private bool _started;
        private bool _needsCalibration = true;
#if UNITY_EDITOR
        private Vector2 _editorTilt;
#endif

        /// <summary>화면 기준 기울기(도). 데드존/감도/최대각 적용 후</summary>
        public Vector2 Tilt { get; private set; }

        /// <summary>기울기 입력을 쓸 수 있는지 (미지원/권한 거부면 지각 변동 모드 비활성)</summary>
        public static bool IsSupported
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_WEBGL
                // 데스크톱 브라우저는 기울기 센서가 없다. 모바일은 권한을 첫 터치 때 요청하므로 "거부 확정"만 미지원으로 본다
                return Application.isMobilePlatform && !_webDenied;
#else
                return SystemInfo.supportsGyroscope || SystemInfo.supportsAccelerometer;
#endif
            }
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        private static bool _webDenied;
#endif

        public void Begin()
        {
            if (!_started)
            {
                _started = true;
#if UNITY_WEBGL && !UNITY_EDITOR
                FossilTilt_Start();
#elif !UNITY_EDITOR
                if (SystemInfo.supportsGyroscope) Input.gyro.enabled = true;
#endif
            }
            _needsCalibration = true;
            _smoothed = Vector2.zero;
            Tilt = Vector2.zero;
        }

        public void End()
        {
            Tilt = Vector2.zero;
            _smoothed = Vector2.zero;
        }

        /// <summary>현재 폰 각도를 수평으로 재설정 (설정 팝업 "기준 자세 재설정")</summary>
        public void Calibrate() => _needsCalibration = true;

        private void Update()
        {
            if (!_started) return;

            if (!ReadRaw(out _raw)) return;

            if (_needsCalibration)
            {
                _baseline = _raw;
                _needsCalibration = false;
            }

            Vector2 delta = _raw - _baseline;
            if (invertForward) delta.y = -delta.y;

            // 저역 통과 필터
            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            _smoothed = Vector2.Lerp(_smoothed, delta, k);

            // 데드존 → 감도 → 최대각
            float mag = _smoothed.magnitude;
            if (mag <= deadzoneDeg) { Tilt = Vector2.zero; return; }
            Vector2 dir = _smoothed / mag;
            float deg = (mag - deadzoneDeg) * SensitivityGain();
            Tilt = dir * Mathf.Min(deg, maxTiltDeg);
        }

        private float SensitivityGain()
        {
            int i = Mathf.Clamp(SuikaSettings.TiltSensitivity, 0, sensitivityGains.Length - 1);
            return sensitivityGains[i];
        }

        // x = 좌우(오른쪽 내림 +), y = 앞뒤(위쪽/먼 쪽 내림 +)
        private bool ReadRaw(out Vector2 raw)
        {
#if UNITY_EDITOR
            float dt = Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.L)) _editorTilt.x += editorKeySpeed * dt;
            if (Input.GetKey(KeyCode.J)) _editorTilt.x -= editorKeySpeed * dt;
            if (Input.GetKey(KeyCode.I)) _editorTilt.y += editorKeySpeed * dt;
            if (Input.GetKey(KeyCode.K)) _editorTilt.y -= editorKeySpeed * dt;
            if (!Input.GetKey(KeyCode.I) && !Input.GetKey(KeyCode.J) && !Input.GetKey(KeyCode.K) && !Input.GetKey(KeyCode.L))
                _editorTilt = Vector2.MoveTowards(_editorTilt, Vector2.zero, editorKeySpeed * dt);
            _editorTilt = Vector2.ClampMagnitude(_editorTilt, 30f);
            raw = _editorTilt;
            return true;
#elif UNITY_WEBGL
            int state = FossilTilt_State();
            if (state < 0) { _webDenied = true; raw = Vector2.zero; return false; }
            if (state == 0) { raw = Vector2.zero; return false; }
            // gamma: 오른쪽 내림 +, beta: 윗부분을 앞으로 숙이면 감소 → 부호 반전
            raw = new Vector2(FossilTilt_GetGamma(), -FossilTilt_GetBeta());
            return true;
#else
            Vector3 g = SystemInfo.supportsGyroscope ? Input.gyro.gravity : Input.acceleration;
            if (g.sqrMagnitude < 0.01f) { raw = Vector2.zero; return false; }
            g.Normalize();
            // 기기 좌표: x = 오른쪽, y = 위쪽. 오른쪽을 내리면 g.x 증가, 윗부분을 숙이면 g.y 증가
            raw = new Vector2(Mathf.Asin(Mathf.Clamp(g.x, -1f, 1f)) * Mathf.Rad2Deg,
                              Mathf.Asin(Mathf.Clamp(g.y, -1f, 1f)) * Mathf.Rad2Deg);
            return true;
#endif
        }
    }
}
