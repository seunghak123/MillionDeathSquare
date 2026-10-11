using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 발굴대 중심을 도는 궤도 카메라 (모바일 우선).
    ///
    /// 기획서는 "화면을 좌우로 드래그하면 발굴대가 돌아간다"인데, 화석이 올라간 물리 판을 직접 돌리면
    /// 마찰로 화석이 미끄러져 결과가 불안정하다. 그래서 카메라가 발굴대 주위를 돌아 같은 화면을 만든다.
    /// 카메라 높이/각도는 고정이고 기울지 않는다 (지각 변동 모드 멀미 방지 — 기울어지는 것은 발굴대뿐).
    ///
    /// 회전 입력:
    ///   - 한 손가락 가로 드래그: SuikaDropper가 탭(드롭)과 구분해 DragRotate()로 전달
    ///   - 두 손가락: 가로 드래그
    ///   - 데스크톱: 우클릭 드래그 / Q·E 키
    ///   - UI 버튼: RotateBy()
    /// 손을 떼면 관성으로 조금 더 돌다가 멈춘다.
    /// </summary>
    public class SuikaCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform pivot;

        [Header("궤도")]
        [SerializeField] private float distance = 11f;
        [SerializeField] private float pitch = 48f;
        [SerializeField] private float pivotHeight = 1.2f;
        [Tooltip("세로 화면(모바일)에서 접시가 화면 폭에 들어오도록 거리 보정 배율 상한")]
        [SerializeField] private float portraitDistanceBoost = 1.35f;

        [Header("회전")]
        [Tooltip("화면 너비 전체를 드래그했을 때 회전 각도")]
        [SerializeField] private float dragDegPerScreen = 220f;
        [SerializeField] private float keyDegPerSec = 120f;
        [Tooltip("드래그를 따라가는 부드러움 (클수록 즉각)")]
        [SerializeField] private float smooth = 14f;
        [Tooltip("손을 뗀 뒤 관성 감쇠 (클수록 빨리 멈춤)")]
        [SerializeField] private float inertiaDamping = 5f;
        [SerializeField] private float maxInertiaDegPerSec = 540f;

        private float _yaw;
        private float _targetYaw;
        private float _velocity; // deg/sec (관성)
        private float _focusHeight;
        private float _targetFocusHeight;

        private bool _externalDrag;
        private Vector2 _lastTwoFingerCenter;
        private bool _twoFingerActive;
        private Vector3 _lastMouse;

        public Camera Camera => targetCamera;
        public float Yaw => _yaw;
        /// <summary>현재 회전 입력 중인지 — 이 동안 드롭 조준은 무시</summary>
        public bool IsRotating { get; private set; }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            _yaw = _targetYaw = transform.eulerAngles.y;
        }

        public void RotateBy(float degrees)
        {
            _targetYaw += degrees;
            _velocity = 0f;
        }

        /// <summary>쌓인 높이에 맞춰 시선 높이를 올린다 (pivotHeight에 더해짐)</summary>
        public void SetFocusHeight(float height) => _targetFocusHeight = Mathf.Max(0f, height);

        public void ResetView()
        {
            _yaw = _targetYaw = 0f;
            _velocity = 0f;
            _focusHeight = _targetFocusHeight = 0f;
            Apply();
        }

        /// <summary>한 손가락 가로 드래그 회전 (SuikaDropper가 탭/드래그를 구분해 넘겨준다)</summary>
        public void DragRotate(float deltaPx)
        {
            if (!_externalDrag) _velocity = 0f;
            _externalDrag = true;
            AddDrag(deltaPx, Time.unscaledDeltaTime);
        }

        /// <summary>드래그 종료 → 관성으로 조금 더 돈다</summary>
        public void EndDragRotate() => _externalDrag = false;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            ReadInput(dt);

            if (!IsRotating && Mathf.Abs(_velocity) > 0.5f)
            {
                _targetYaw += _velocity * dt;
                _velocity *= Mathf.Exp(-inertiaDamping * dt);
            }

            float k = 1f - Mathf.Exp(-smooth * dt);
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, k);
            _focusHeight = Mathf.Lerp(_focusHeight, _targetFocusHeight, k * 0.25f);
            Apply();
        }

        private void AddDrag(float deltaPx, float dt)
        {
            float deg = deltaPx / Mathf.Max(1f, Screen.width) * dragDegPerScreen;
            _targetYaw += deg;
            if (dt > 0f)
            {
                float v = Mathf.Clamp(deg / dt, -maxInertiaDegPerSec, maxInertiaDegPerSec);
                _velocity = Mathf.Lerp(_velocity, v, 0.5f);
            }
        }

        private void ReadInput(float dt)
        {
            IsRotating = _externalDrag;

            if (Input.touchSupported && Input.touchCount > 0)
            {
                ReadTouch(dt);
                return;
            }
            _twoFingerActive = false;

            // 데스크톱: 우클릭 드래그
            if (Input.GetMouseButtonDown(1)) { _lastMouse = Input.mousePosition; _velocity = 0f; }
            if (Input.GetMouseButton(1))
            {
                Vector3 m = Input.mousePosition;
                AddDrag(m.x - _lastMouse.x, dt);
                _lastMouse = m;
                IsRotating = true;
            }

            float key = 0f;
            if (Input.GetKey(KeyCode.Q)) key -= 1f;
            if (Input.GetKey(KeyCode.E)) key += 1f;
            if (key != 0f) { _targetYaw += key * keyDegPerSec * dt; _velocity = 0f; }
        }

        private void ReadTouch(float dt)
        {
            // 두 손가락 — 가로 드래그 회전 (한 손가락 드래그 회전은 SuikaDropper가 DragRotate로 전달)
            if (Input.touchCount >= 2)
            {
                Vector2 center = (Input.GetTouch(0).position + Input.GetTouch(1).position) * 0.5f;
                if (_twoFingerActive) AddDrag(center.x - _lastTwoFingerCenter.x, dt);
                else _velocity = 0f;
                _lastTwoFingerCenter = center;
                _twoFingerActive = true;
                IsRotating = true;
                return;
            }
            _twoFingerActive = false;
        }

        private void Apply()
        {
            if (targetCamera == null) return;

            // 세로 화면일수록 가로 시야가 좁아지므로 거리를 늘려 접시가 잘리지 않게 한다
            float aspect = targetCamera.aspect > 0f ? targetCamera.aspect : 1f;
            float boost = aspect < 1f ? Mathf.Min(portraitDistanceBoost, 1f / Mathf.Sqrt(aspect)) : 1f;

            Vector3 center = (pivot != null ? pivot.position : Vector3.zero) + Vector3.up * (pivotHeight + _focusHeight);
            Quaternion rot = Quaternion.Euler(pitch, _yaw, 0f);
            Transform cam = targetCamera.transform;
            cam.position = center - rot * Vector3.forward * (distance * boost);
            cam.rotation = rot;
        }
    }
}
