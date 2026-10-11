using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 화석(노듈) 1개. 같은 단계 화석과 닿으면 SuikaGameManager에 합체를 요청한다.
    /// 프리팹 루트에 Rigidbody + SphereCollider(radius 0.5)와 함께 붙으며, 실제 크기는 localScale로 조절한다.
    /// (클래스 이름은 초기 프로토타입의 "Fruit"를 유지한다)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class SuikaFruit : MonoBehaviour
    {
        private const float POP_DURATION = 0.15f;
        private const float POP_START_SCALE = 0.6f;

        private Rigidbody _rb;
        private SphereCollider _col;
        private float _baseScale = 1f;
        private float _popTimer = -1f;

        public int Tier { get; private set; }
        /// <summary>합치기가 예약되어 곧 풀로 돌아갈 과일</summary>
        public bool IsMerging { get; set; }
        /// <summary>드롭 대기(미리보기) 중이면 false — 물리/합치기 대상 아님</summary>
        public bool IsDropped { get; private set; }
        /// <summary>드롭 후 무언가에 한 번이라도 닿았는지</summary>
        public bool HasLanded { get; private set; }
        public Rigidbody Body => _rb;
        public float Mass => _rb.mass;
        public float Radius => _baseScale * 0.5f;

        /// <summary>연쇄 단계. 떨어뜨린 화석 = 0, 합체로 생긴 화석 = (합쳐진 두 화석 중 큰 연쇄) + 1</summary>
        public int Chain { get; private set; }
        private float _chainTime;
        /// <summary>연쇄가 유효한지 (합체로 생긴 뒤 window 초 이내)</summary>
        public int ActiveChain(float window) => Chain > 0 && Time.time - _chainTime <= window ? Chain : 0;

        /// <summary>연출 등으로 물리를 잠시 멈춘 상태 (합체 대상 아님)</summary>
        public bool IsHeld { get; private set; }

        private SuikaGameManager _manager;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<SphereCollider>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        /// <summary>풀에서 꺼낼 때 호출. 미리보기 상태(물리 꺼짐)로 초기화된다.</summary>
        public void Setup(SuikaGameManager manager, int tier, SuikaFruitTable.Tier data)
        {
            _manager = manager;
            Tier = tier;
            IsMerging = false;
            IsDropped = false;
            HasLanded = false;
            IsHeld = false;
            Chain = 0;
            _popTimer = -1f;

            _baseScale = data.radius * 2f;
            transform.localScale = Vector3.one * _baseScale;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            _rb.mass = data.mass;
            _rb.isKinematic = true;
            _col.enabled = false;
        }

        /// <summary>드롭 — 물리 활성화</summary>
        public void Drop()
        {
            IsDropped = true;
            _col.enabled = true;
            _rb.isKinematic = false;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        /// <summary>합체로 생성된 화석 — 바로 물리 활성 + 커지는 연출</summary>
        public void SpawnAsMerged(Vector3 velocity, int chain)
        {
            Drop();
            HasLanded = true;
            Chain = chain;
            _chainTime = Time.time;
            _rb.velocity = velocity;
            _popTimer = 0f;
            transform.localScale = Vector3.one * (_baseScale * POP_START_SCALE);
        }

        /// <summary>제자리에 고정 (공룡 부활 연출 등). Release()로 해제.</summary>
        public void Hold()
        {
            IsHeld = true;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        public void Release()
        {
            IsHeld = false;
            if (IsDropped) _rb.isKinematic = false;
        }

        private void Update()
        {
            if (_popTimer < 0f) return;
            _popTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_popTimer / POP_DURATION);
            // EaseOutBack — 살짝 커졌다가 제자리
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            float ease = 1f + c3 * u * u * u + c1 * u * u;
            float s = Mathf.LerpUnclamped(POP_START_SCALE, 1f, ease);
            if (t >= 1f) { s = 1f; _popTimer = -1f; }
            transform.localScale = Vector3.one * (_baseScale * s);
        }

        private void OnCollisionEnter(Collision collision) => HandleContact(collision);
        private void OnCollisionStay(Collision collision) => HandleContact(collision);

        private void HandleContact(Collision collision)
        {
            if (!IsDropped || IsMerging || IsHeld || _manager == null) return;
            HasLanded = true;

            Rigidbody otherRb = collision.rigidbody;
            if (otherRb == null) return;
            if (!otherRb.TryGetComponent(out SuikaFruit other)) return;
            if (other.IsMerging || other.IsHeld || !other.IsDropped || other.Tier != Tier) return;

            // 양쪽 모두 콜백을 받으므로 한쪽(InstanceID가 작은 쪽)만 처리
            if (GetInstanceID() > other.GetInstanceID()) return;

            _manager.RequestMerge(this, other);
        }
    }
}
