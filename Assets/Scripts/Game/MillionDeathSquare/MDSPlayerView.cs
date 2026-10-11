using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 플레이어 네모 (기획서 3.1): 눈이 이동 방향을 따라봄, 장애물이 가까우면 눈이 커지고 떨림,
    /// 출발·멈춤·부딪힘에 늘어났다 줄어드는 움직임 (기획서 3.4). 골인하면 만세.
    /// 위치는 디렉터가 고정 스텝 사이를 보간해서 넣는다.
    /// </summary>
    public sealed class MDSPlayerView : MonoBehaviour
    {
        private SpriteRenderer _body;
        private MDSEyes _eyes;
        private Transform _visual;
        private Vector3 _scale = Vector3.one;
        private Vector3 _scaleVel;
        private Vector2 _lastVel;
        private float _happyTimer;
        private float _cheerTimer;
        private float _alpha = 1f;
        private bool _ghost;

        public float Size { get; private set; }

        public static MDSPlayerView Create(Transform parent, MDSSpriteLibrary sprites, Color body, Color eye, float size, int sortingOrder, bool ghost)
        {
            var go = new GameObject(ghost ? "Ghost" : "Player");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<MDSPlayerView>();
            v.Size = size;
            v._ghost = ghost;
            v._visual = new GameObject("Visual").transform;
            v._visual.SetParent(go.transform, false);
            v._body = new GameObject("Body").AddComponent<SpriteRenderer>();
            v._body.transform.SetParent(v._visual, false);
            v._body.transform.localScale = Vector3.one * size;
            v._body.sprite = sprites.roundedSquare;
            v._body.color = body;
            v._body.sortingOrder = sortingOrder;
            v._eyes = new MDSEyes(v._visual, sprites, size * 0.2f, size * 0.26f, eye, sortingOrder + 1);
            if (ghost) v.SetAlpha(0.45f);
            return v;
        }

        public void SetColors(Color body, Color eye)
        {
            body.a = _alpha;
            _body.color = body;
            _eyes.SetEyeColor(eye);
            _eyes.SetAlpha(_alpha);
        }

        public void SetAlpha(float a)
        {
            _alpha = a;
            Color c = _body.color; c.a = a; _body.color = c;
            _eyes.SetAlpha(a);
        }

        public void SetVisible(bool v) { _visual.gameObject.SetActive(v); }

        /// <summary>
        /// 매 프레임 표시 갱신. velocity: 단위/틱, dangerDistance: 가장 가까운 장애물까지 거리(칸, 큰 값 = 없음)
        /// </summary>
        public void Tick(Vector3 position, Vector2 velocity, float dangerDistance, bool onIce, float dt)
        {
            transform.localPosition = position;

            // 출발/멈춤 순간 늘어남·찌그러짐
            bool moving = velocity.sqrMagnitude > 0.01f;
            bool wasMoving = _lastVel.sqrMagnitude > 0.01f;
            if (moving && !wasMoving) Squash(Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y) ? new Vector2(1.25f, 0.8f) : new Vector2(0.8f, 1.25f));
            else if (!moving && wasMoving) Squash(Mathf.Abs(_lastVel.x) > Mathf.Abs(_lastVel.y) ? new Vector2(0.8f, 1.2f) : new Vector2(1.2f, 0.8f));
            _lastVel = velocity;

            // 스프링으로 원래 크기 복귀
            _scale = Vector3.SmoothDamp(_scale, Vector3.one, ref _scaleVel, 0.07f, Mathf.Infinity, dt);
            _visual.localScale = _scale;

            // 표정
            Vector2 look = moving ? new Vector2(velocity.x, -velocity.y).normalized : Vector2.zero;
            _eyes.Look(look);
            if (_cheerTimer > 0f)
            {
                _cheerTimer -= dt;
                _eyes.SetExpression(EyeExpression.Cheer);
                float hop = Mathf.Abs(Mathf.Sin(_cheerTimer * 14f)) * Size * 0.25f;
                _visual.localPosition = new Vector3(0f, hop, 0f);
                _eyes.Jitter(Vector2.zero);
                return;
            }
            _visual.localPosition = Vector3.zero;
            if (_happyTimer > 0f)
            {
                _happyTimer -= dt;
                _eyes.SetExpression(EyeExpression.Happy);
                _eyes.Jitter(Vector2.zero);
            }
            else if (!_ghost && dangerDistance < 1.6f)
            {
                _eyes.SetExpression(EyeExpression.Surprised);
                float amp = Mathf.Lerp(0.05f, 0.012f, Mathf.Clamp01((dangerDistance - 0.6f) / 1.0f)) * Size;
                _eyes.Jitter(new Vector2(Random.Range(-amp, amp), Random.Range(-amp, amp)));
            }
            else
            {
                _eyes.SetExpression(onIce ? EyeExpression.Focus : EyeExpression.Normal);
                _eyes.Jitter(Vector2.zero);
            }
        }

        public void Squash(Vector2 s)
        {
            _scale = new Vector3(s.x, s.y, 1f);
            _visual.localScale = _scale;
        }

        /// <summary>벽에 부딪힘 (axis 0 = 가로, 1 = 세로)</summary>
        public void Bump(int axis) { Squash(axis == 0 ? new Vector2(0.75f, 1.2f) : new Vector2(1.2f, 0.75f)); }

        public void Happy(float seconds) { _happyTimer = seconds; }

        public void Cheer(float seconds) { _cheerTimer = seconds; }

        public void ResetPose()
        {
            _cheerTimer = 0f;
            _happyTimer = 0f;
            _scale = Vector3.one;
            _scaleVel = Vector3.zero;
            _lastVel = Vector2.zero;
            _visual.localScale = Vector3.one;
            _visual.localPosition = Vector3.zero;
            _eyes.SetExpression(EyeExpression.Normal);
        }
    }
}
