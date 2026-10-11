using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 발굴대 (화석을 쌓는 둥근 판, 벽 없음).
    ///
    /// 움직임 원천 (모두 합산 후 최대각으로 제한):
    ///  1. 폰 기울기 — 지각 변동 모드에서만 (SuikaTiltInput, 화면 기준 → 카메라 방향으로 월드 변환)
    ///  2. 지진 이벤트 — 지각 변동 모드에서만 (SuikaQuakeDirector)
    ///  3. 지진 스킬 — 두 모드 공통, 정한 방향으로 3초 흔들고 그동안 테두리 벽이 솟는다
    /// 일반 모드에서 지진 스킬을 쓰지 않으면 발굴대는 완전히 고정이다.
    ///
    /// 멀미 방지(기획서 9.4): 카메라는 기울지 않고 발굴대만 기운다.
    /// kinematic Rigidbody를 MoveRotation으로 돌려 위의 화석과 물리적으로 맞물리게 한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SuikaPlate : MonoBehaviour
    {
        [Header("크기")]
        [Tooltip("발굴대 반지름 (드롭 범위 계산에 사용)")]
        [SerializeField] private float radius = 3.5f;

        [Header("참조 (선택)")]
        [SerializeField] private SuikaPlateWalls walls;
        [SerializeField] private SuikaTiltInput tiltInput;
        [SerializeField] private SuikaQuakeDirector quakeDirector;
        [Tooltip("폰 기울기를 월드 방향으로 바꿀 때 쓰는 카메라 (비우면 Camera.main)")]
        [SerializeField] private Camera viewCamera;

        [Header("기울기 한계")]
        [Tooltip("지각 변동 모드 최대 기울기 (기획서: 15°)")]
        [SerializeField] private float maxTiltDeg = 15f;
        [Tooltip("초당 최대 회전 각도 — 순간적으로 확 꺾여 화석이 튕겨나가는 것 방지")]
        [SerializeField] private float maxTiltSpeedDeg = 180f;

        [Header("지진 스킬 흔들림")]
        [SerializeField] private float skillShakeFrequency = 2.2f;

        private Rigidbody _rb;
        private Quaternion _baseRotation;
        private Vector2 _currentTilt; // (x축 각, z축 각)

        private float _skillQuakeTimer;
        private float _skillQuakeDuration;
        private float _skillQuakeDeg;
        private Vector3 _skillQuakeDir = Vector3.right;
        private bool _skillQuakeFollowTilt;

        public float Radius => radius;
        public E_SUIKA_DIFFICULTY Difficulty { get; private set; } = E_SUIKA_DIFFICULTY.NORMAL;
        public bool IsTectonic => Difficulty == E_SUIKA_DIFFICULTY.TECTONIC;
        public bool IsSkillQuakeActive => _skillQuakeTimer > 0f;
        public SuikaTiltInput TiltInput => tiltInput;
        public SuikaQuakeDirector QuakeDirector => quakeDirector;
        /// <summary>현재 발굴대 기울기 크기(도) — 굴리기 보너스 판정용</summary>
        public float CurrentTiltDeg => _currentTilt.magnitude;
        /// <summary>현재 폰 기울기(도) — 지각 변동 모드에서만 0이 아님</summary>
        public float PlayerTiltDeg => IsTectonic && tiltInput != null ? tiltInput.Tilt.magnitude : 0f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _baseRotation = transform.rotation;
            if (viewCamera == null) viewCamera = Camera.main;
        }

        public void SetDifficulty(E_SUIKA_DIFFICULTY difficulty)
        {
            Difficulty = difficulty;
            if (tiltInput != null)
            {
                if (IsTectonic) tiltInput.Begin(); else tiltInput.End();
            }
            ResetPlate();
        }

        public void ResetPlate()
        {
            _currentTilt = Vector2.zero;
            _skillQuakeTimer = 0f;
            walls?.SetRaised(false, instant: true);
            if (_rb != null) _rb.MoveRotation(_baseRotation);
            transform.rotation = _baseRotation;
            walls?.Follow(transform.position, transform.rotation);
        }

        /// <summary>
        /// 지진 스킬: worldDir 방향으로 duration초 흔든다. 그동안 벽이 솟아 화석이 떨어지지 않는다.
        /// followTilt면(지각 변동 모드) 흔드는 방향을 폰 기울기 방향으로 계속 바꾼다.
        /// </summary>
        public void StartSkillQuake(Vector3 worldDir, float duration, float tiltDeg, bool followTilt)
        {
            worldDir.y = 0f;
            _skillQuakeDir = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized : Vector3.right;
            _skillQuakeDuration = Mathf.Max(0.1f, duration);
            _skillQuakeTimer = _skillQuakeDuration;
            _skillQuakeDeg = tiltDeg;
            _skillQuakeFollowTilt = followTilt;
            walls?.SetRaised(true, instant: false);
        }

        /// <summary>벽만 먼저 올린다 (지진 스킬 방향 입력 대기 중)</summary>
        public void RaiseWalls(bool raised) => walls?.SetRaised(raised, instant: false);

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 target = Vector2.zero;

            if (IsTectonic)
            {
                if (tiltInput != null) target += ScreenTiltToEuler(tiltInput.Tilt);
                if (quakeDirector != null) target += quakeDirector.CurrentTiltEuler;
            }

            if (_skillQuakeTimer > 0f)
            {
                _skillQuakeTimer -= dt;
                if (_skillQuakeFollowTilt && tiltInput != null && tiltInput.Tilt.sqrMagnitude > 0.01f)
                    _skillQuakeDir = ScreenToWorldDir(tiltInput.Tilt);

                float elapsed = _skillQuakeDuration - _skillQuakeTimer;
                float envelope = Mathf.Clamp01(_skillQuakeTimer / 0.4f) * Mathf.Clamp01(elapsed / 0.2f);
                float wave = Mathf.Sin(elapsed * skillShakeFrequency * Mathf.PI * 2f);
                target += DirToEuler(_skillQuakeDir, wave * _skillQuakeDeg * envelope);

                if (_skillQuakeTimer <= 0f) walls?.SetRaised(false, instant: false);
            }

            // 일반 모드에서 아무 효과도 없으면 완전 고정
            if (!IsTectonic && _skillQuakeTimer <= 0f && _currentTilt == Vector2.zero)
                return;

            target = Vector2.ClampMagnitude(target, maxTiltDeg);
            _currentTilt = Vector2.MoveTowards(_currentTilt, target, maxTiltSpeedDeg * dt);
            if (!IsTectonic && _skillQuakeTimer <= 0f && _currentTilt.sqrMagnitude < 0.0001f)
                _currentTilt = Vector2.zero;

            Quaternion rot = _baseRotation * Quaternion.Euler(_currentTilt.x, 0f, _currentTilt.y);
            _rb.MoveRotation(rot);
            walls?.Follow(_rb.position, rot);
        }

        // ── 방향 변환 ──────────────────────────────────────────────────

        /// <summary>화면 기준 기울기(x = 오른쪽 내림, y = 먼 쪽 내림, 도) → 월드 수평 방향 × 도</summary>
        private Vector3 ScreenToWorldVector(Vector2 screenTilt)
        {
            Transform cam = viewCamera != null ? viewCamera.transform : null;
            Vector3 right = cam != null ? cam.right : Vector3.right;
            Vector3 fwd = cam != null ? cam.forward : Vector3.forward;
            right.y = 0f; fwd.y = 0f;
            right.Normalize(); fwd.Normalize();
            return right * screenTilt.x + fwd * screenTilt.y;
        }

        public Vector3 ScreenToWorldDir(Vector2 screenTilt)
        {
            Vector3 v = ScreenToWorldVector(screenTilt);
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.right;
        }

        private Vector2 ScreenTiltToEuler(Vector2 screenTilt)
        {
            Vector3 v = ScreenToWorldVector(screenTilt);
            float deg = v.magnitude;
            return deg > 0.0001f ? DirToEuler(v / deg, deg) : Vector2.zero;
        }

        /// <summary>월드 수평 방향 dir 쪽이 deg만큼 내려가는 (x축 각, z축 각)</summary>
        public static Vector2 DirToEuler(Vector3 dir, float deg)
        {
            // +X 쪽이 내려가려면 z축 음의 회전, +Z 쪽이 내려가려면 x축 양의 회전
            return new Vector2(dir.z * deg, -dir.x * deg);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            const int seg = 32;
            Vector3 c = transform.position;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius,
                                c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius);
            }
        }
    }
}
