using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 발굴대 테두리 벽 (지진 스킬 동안 솟아올라 화석이 떨어지지 않게 막는다).
    /// 발굴대와 같은 위치/회전을 따라가는 별도 kinematic 바디이며, 자식에 박스 콜라이더 링 + 시각 오브젝트를 둔다.
    /// 내려가 있을 때는 콜라이더를 끄고 발굴대 아래로 숨긴다.
    /// (Tools/Fossil Merge 메뉴가 생성)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SuikaPlateWalls : MonoBehaviour
    {
        [Tooltip("솟아오르는 부분 (콜라이더 + 시각 오브젝트의 부모)")]
        [SerializeField] private Transform ring;
        [SerializeField] private float raisedLocalY = 0.45f;
        [SerializeField] private float loweredLocalY = -0.6f;
        [SerializeField] private float riseSpeed = 6f;

        private Rigidbody _rb;
        private Collider[] _colliders;
        private bool _raised;
        private float _y;

        public bool IsRaised => _raised;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (ring == null && transform.childCount > 0) ring = transform.GetChild(0);
            _colliders = ring != null ? ring.GetComponentsInChildren<Collider>(true) : new Collider[0];
            SetRaised(false, instant: true);
        }

        public void SetRaised(bool raised, bool instant)
        {
            _raised = raised;
            // 올라갈 때는 즉시 막아야 하므로 콜라이더를 바로 켜고, 내려갈 때는 다 내려간 뒤 끈다
            if (raised) SetCollidersEnabled(true);
            if (instant)
            {
                _y = raised ? raisedLocalY : loweredLocalY;
                ApplyRing();
                if (!raised) SetCollidersEnabled(false);
                if (ring != null) ring.gameObject.SetActive(raised);
            }
            else if (raised && ring != null)
            {
                ring.gameObject.SetActive(true);
            }
        }

        /// <summary>발굴대 위치/회전을 따라간다 (SuikaPlate.FixedUpdate에서 호출)</summary>
        public void Follow(Vector3 position, Quaternion rotation)
        {
            if (_rb == null) return;
            _rb.MovePosition(position);
            _rb.MoveRotation(rotation);
        }

        private void Update()
        {
            float target = _raised ? raisedLocalY : loweredLocalY;
            if (Mathf.Approximately(_y, target)) return;
            _y = Mathf.MoveTowards(_y, target, riseSpeed * Time.deltaTime);
            ApplyRing();
            if (!_raised && Mathf.Approximately(_y, loweredLocalY))
            {
                SetCollidersEnabled(false);
                if (ring != null) ring.gameObject.SetActive(false);
            }
        }

        private void ApplyRing()
        {
            if (ring == null) return;
            Vector3 p = ring.localPosition;
            p.y = _y;
            ring.localPosition = p;
        }

        private void SetCollidersEnabled(bool on)
        {
            if (_colliders == null) return;
            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = on;
        }
    }
}
