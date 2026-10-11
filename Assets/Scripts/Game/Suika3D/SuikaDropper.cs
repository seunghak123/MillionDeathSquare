using UnityEngine;
using UnityEngine.EventSystems;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 떨어뜨리기 / 회전 / 스킬 대상 입력 (기획서 3장 조작, 7장 스킬).
    ///
    /// 한 손가락(또는 마우스 좌클릭):
    ///   - 탭: 발굴대(또는 쌓인 화석)를 탭한 위치 위에서 화석이 떨어진다.
    ///          누르고 있는 동안 낙하 위치 가이드(수직선 + 그림자 원)가 손가락을 따라간다.
    ///   - 좌우 드래그: 발굴대 회전 (SuikaCameraRig.DragRotate)
    ///   - 스킬 대기 중: 탭 = 대상 화석 지정 (망치/자석/진화), 스와이프 = 지진 방향
    /// 두 손가락: 회전 (SuikaCameraRig가 직접 처리)
    /// 키보드(웹): WASD/방향키로 낙하 위치 이동(카메라 기준), Space 드롭
    /// </summary>
    public class SuikaDropper : MonoBehaviour
    {
        [SerializeField] private SuikaGameManager manager;

        [Tooltip("드롭 후 다음 화석이 나오기까지 대기 시간")]
        [SerializeField] private float dropCooldown = 0.35f;

        [Header("제스처")]
        [Tooltip("이 거리(화면 너비 비율) 이상 움직이면 탭이 아니라 드래그(회전/스와이프)")]
        [SerializeField] private float dragThresholdRatio = 0.03f;
        [Tooltip("스킬 대상 탭 판정 반경 (월드, 손가락 오차 보정)")]
        [SerializeField] private float targetPickRadius = 0.25f;

        [Header("입력 제외 영역 (화면 위/아래 비율 — HUD 영역)")]
        [Range(0f, 0.5f)] [SerializeField] private float ignoreTopRatio = 0.12f;
        [Range(0f, 0.5f)] [SerializeField] private float ignoreBottomRatio = 0.14f;

        [Header("키보드")]
        [SerializeField] private float keyboardMoveSpeed = 4f;

        [Header("낙하 위치 가이드 (기획서 3.1)")]
        [Tooltip("드롭 위치에서 착지 예상 지점까지 그리는 LineRenderer (2점)")]
        [SerializeField] private LineRenderer guideLine;
        [Tooltip("착지 예상 지점에 놓을 원형 그림자 (지름 1 기준, 충돌체 없음)")]
        [SerializeField] private Transform landingMarker;
        [SerializeField] private LayerMask landingMask = ~0;

        [Header("미리보기 연출")]
        [SerializeField] private float followSharpness = 30f;
        [SerializeField] private float bobAmplitude = 0.05f;
        [SerializeField] private float bobFrequency = 1.6f;

        private enum Gesture { None, Press, Rotate, Swipe }

        private SuikaFruit _current;
        private Vector3 _aimLocal;      // 목표 낙하 위치 (발굴대 중심 기준 XZ)
        private Vector3 _shownLocal;    // 미리보기 표시 위치 (부드럽게 따라감)
        private float _cooldown;
        private bool _atEdge;

        private Gesture _gesture;
        private int _fingerId = -1;     // -1 = 마우스
        private Vector2 _downPos;
        private Vector2 _lastPos;

        private void Awake()
        {
            if (manager == null) manager = SuikaGameManager.Instance;
            Input.multiTouchEnabled = true;
        }

        private void OnEnable()
        {
            if (manager != null) manager.OnStateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnStateChanged -= OnStateChanged;
            SetGuideVisible(false);
        }

        private void OnStateChanged(E_SUIKA_GAME_STATE state)
        {
            // 새 판 / 인게임 이탈 → 쥐고 있던 미리보기 화석은 버린다
            if ((state == E_SUIKA_GAME_STATE.READY || state == E_SUIKA_GAME_STATE.NONE) && _current != null && !_current.IsDropped)
            {
                manager.ReleasePreviewFruit(_current);
                _current = null;
                _aimLocal = _shownLocal = Vector3.zero;
                _cooldown = 0f;
            }
            CancelGesture();
        }

        private void Update()
        {
            if (manager == null) return;

            var state = manager.GameState;
            bool active = state == E_SUIKA_GAME_STATE.READY || state == E_SUIKA_GAME_STATE.PLAYING;
            if (!active)
            {
                CancelGesture();
                SetGuideVisible(false);
                return;
            }

            if (_current == null && manager.CanDrop)
            {
                _cooldown -= Time.deltaTime;
                if (_cooldown <= 0f)
                {
                    _current = manager.AcquirePreviewFruit();
                    if (_current != null) { ClampAim(); _shownLocal = _aimLocal; }
                }
            }

            ReadPointer();
            ReadKeyboard();

            _shownLocal = Vector3.Lerp(_shownLocal, _aimLocal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            UpdatePreviewTransform();
        }

        // ── 포인터 (터치 / 마우스) ────────────────────────────────────

        private void ReadPointer()
        {
            if (Input.touchSupported && Input.touchCount > 0)
            {
                if (Input.touchCount >= 2) { CancelGesture(); return; } // 두 손가락 = 카메라 회전
                Touch t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began: PointerDown(t.position, t.fingerId); break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary: if (t.fingerId == _fingerId) PointerMove(t.position); break;
                    case TouchPhase.Ended: if (t.fingerId == _fingerId) PointerUp(t.position); break;
                    case TouchPhase.Canceled: if (t.fingerId == _fingerId) CancelGesture(); break;
                }
                return;
            }

            if (Input.touchSupported && _fingerId >= 0) { CancelGesture(); return; }

            Vector2 m = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) PointerDown(m, -1);
            else if (Input.GetMouseButton(0) && _gesture != Gesture.None && _fingerId < 0) PointerMove(m);
            if (Input.GetMouseButtonUp(0) && _gesture != Gesture.None && _fingerId < 0) PointerUp(m);
        }

        private void PointerDown(Vector2 pos, int fingerId)
        {
            if (!IsValidPointerStart(pos, fingerId)) return;
            _gesture = Gesture.Press;
            _fingerId = fingerId;
            _downPos = _lastPos = pos;
            if (!SkillAwaiting) AimAtScreen(pos);
        }

        private void PointerMove(Vector2 pos)
        {
            if (_gesture == Gesture.None) return;
            Vector2 delta = pos - _lastPos;
            _lastPos = pos;

            if (_gesture == Gesture.Press)
            {
                float threshold = Screen.width * dragThresholdRatio;
                if ((pos - _downPos).sqrMagnitude > threshold * threshold)
                {
                    var skills = manager.Skills;
                    if (skills != null && skills.IsAimingQuake) _gesture = Gesture.Swipe;
                    else
                    {
                        _gesture = Gesture.Rotate;
                        delta = pos - _downPos; // 임계값까지 움직인 만큼도 반영
                    }
                }
                else
                {
                    if (!SkillAwaiting) AimAtScreen(pos); // 누른 채 위치 미세 조정
                    return;
                }
            }

            if (_gesture == Gesture.Rotate) manager.CameraRig?.DragRotate(delta.x);
        }

        private void PointerUp(Vector2 pos)
        {
            Gesture g = _gesture;
            CancelGesture();
            var skills = manager.Skills;

            switch (g)
            {
                case Gesture.Press: // 탭
                    if (skills != null && skills.IsTargeting)
                    {
                        SuikaFruit target = PickFossil(pos);
                        if (target != null) skills.ApplyToTarget(target);
                    }
                    else if (skills == null || !skills.IsAwaitingInput)
                    {
                        AimAtScreen(pos);
                        DropCurrent();
                    }
                    break;

                case Gesture.Swipe:
                    skills?.ApplyQuakeSwipe(pos - _downPos);
                    break;
            }
        }

        private void CancelGesture()
        {
            if (_gesture == Gesture.Rotate) manager?.CameraRig?.EndDragRotate();
            _gesture = Gesture.None;
            _fingerId = -1;
        }

        private bool SkillAwaiting => manager.Skills != null && manager.Skills.IsAwaitingInput;

        private bool IsValidPointerStart(Vector2 screenPos, int fingerId)
        {
            float h = Screen.height;
            if (screenPos.y > h * (1f - ignoreTopRatio)) return false;
            if (screenPos.y < h * ignoreBottomRatio) return false;

            if (EventSystem.current != null)
            {
                bool overUI = fingerId >= 0
                    ? EventSystem.current.IsPointerOverGameObject(fingerId)
                    : EventSystem.current.IsPointerOverGameObject();
                if (overUI) return false;
            }
            return true;
        }

        private Camera Cam => manager.CameraRig != null && manager.CameraRig.Camera != null ? manager.CameraRig.Camera : Camera.main;

        /// <summary>탭한 화면 위치 → 발굴대/쌓인 화석 표면의 수평 위치</summary>
        private void AimAtScreen(Vector2 screenPos)
        {
            Camera cam = Cam;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(screenPos);
            Vector3 center = PlateCenter();

            Vector3 hitPoint;
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, landingMask, QueryTriggerInteraction.Ignore))
                hitPoint = hit.point;
            else
            {
                var plane = new Plane(Vector3.up, center);
                if (!plane.Raycast(ray, out float enter)) return;
                hitPoint = ray.GetPoint(enter);
            }

            _aimLocal = new Vector3(hitPoint.x - center.x, 0f, hitPoint.z - center.z);
            ClampAim();
        }

        /// <summary>스킬 대상 화석 찾기 (손가락 오차를 감안해 굵은 레이)</summary>
        private SuikaFruit PickFossil(Vector2 screenPos)
        {
            Camera cam = Cam;
            if (cam == null) return null;
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.SphereCast(ray, targetPickRadius, out RaycastHit hit, 100f, landingMask, QueryTriggerInteraction.Ignore)
                && hit.rigidbody != null && hit.rigidbody.TryGetComponent(out SuikaFruit f) && f.IsDropped)
                return f;
            return null;
        }

        // ── 키보드 ─────────────────────────────────────────────────────

        private void ReadKeyboard()
        {
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;

            if (h != 0f || v != 0f)
            {
                float yaw = manager.CameraRig != null ? manager.CameraRig.Yaw : 0f;
                Vector3 move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(h, 0f, v);
                _aimLocal += move * (keyboardMoveSpeed * Time.deltaTime);
                ClampAim();
            }

            if (Input.GetKeyDown(KeyCode.Space) && !SkillAwaiting)
                DropCurrent();
        }

        // ── 미리보기 / 가이드 ─────────────────────────────────────────

        private void ClampAim()
        {
            if (manager.Plate == null) return;
            float r = _current != null ? _current.Radius : 0.3f;
            // 가장자리에 살짝 걸치게 놓는 것까지는 허용 (위험 감수 플레이)
            float maxR = Mathf.Max(0f, manager.Plate.Radius - r * 0.5f);
            bool atEdge = _aimLocal.sqrMagnitude > maxR * maxR;
            if (atEdge) _aimLocal = _aimLocal.normalized * maxR;

            if (atEdge && !_atEdge && _gesture != Gesture.None) SuikaHaptics.Play(SuikaHaptics.AIM_EDGE);
            _atEdge = atEdge;
        }

        private Vector3 PlateCenter() => manager.Plate != null ? manager.Plate.transform.position : Vector3.zero;

        private void UpdatePreviewTransform()
        {
            if (_current == null) { SetGuideVisible(false); return; }
            Vector3 c = PlateCenter();
            float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            Vector3 pos = new Vector3(c.x + _shownLocal.x, manager.DropHeight + bob, c.z + _shownLocal.z);
            _current.transform.position = pos;

            // 스킬 대상 지정 중에는 낙하 가이드를 숨겨 헷갈리지 않게
            if (SkillAwaiting) { SetGuideVisible(false); return; }
            UpdateLandingGuide(pos);
        }

        private void UpdateLandingGuide(Vector3 from)
        {
            if (guideLine == null && landingMarker == null) return;

            float radius = _current.Radius;
            Vector3 end;
            Vector3 normal = Vector3.up;
            if (Physics.SphereCast(from, radius * 0.95f, Vector3.down, out RaycastHit hit, 50f, landingMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                normal = hit.normal;
            }
            else
            {
                end = new Vector3(from.x, PlateCenter().y - 2f, from.z);
            }

            SetGuideVisible(true);
            if (guideLine != null)
            {
                guideLine.SetPosition(0, from);
                guideLine.SetPosition(1, end);
            }
            if (landingMarker != null)
            {
                landingMarker.position = end + normal * 0.02f;
                landingMarker.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                float d = radius * 2f;
                landingMarker.localScale = new Vector3(d, landingMarker.localScale.y, d);
            }
        }

        private void DropCurrent()
        {
            if (_current == null || !manager.CanDrop) return;
            _shownLocal = _aimLocal;
            UpdatePreviewTransform();
            manager.RegisterDropped(_current);
            _current = null;
            _cooldown = dropCooldown;
            _atEdge = false;
            SetGuideVisible(false);
        }

        private void SetGuideVisible(bool visible)
        {
            if (guideLine != null && guideLine.enabled != visible) guideLine.enabled = visible;
            if (landingMarker != null && landingMarker.gameObject.activeSelf != visible) landingMarker.gameObject.SetActive(visible);
        }
    }
}
