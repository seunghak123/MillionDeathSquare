using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 눈 표정 (기획서 12.1: 평소, 놀람, 반가움, 집중, 만세). 프레임 애니메이션 없이 눈 스프라이트·크기만 코드로 바꾼다.
    /// </summary>
    public enum EyeExpression
    {
        Normal,
        /// <summary>놀람: 장애물이 가까움 — 눈이 커지고 떨림</summary>
        Surprised,
        /// <summary>반가움: 동전/체크포인트 — ^ ^</summary>
        Happy,
        /// <summary>집중: 얼음 위/좁은 길 — 가늘게 뜬 눈</summary>
        Focus,
        /// <summary>만세: 골인</summary>
        Cheer,
        /// <summary>장애물: 화난 눈 (눈썹은 따로)</summary>
        Angry,
    }

    /// <summary>두 눈 (흰자 + 눈동자). 몸(네모/동그라미)의 자식으로 붙는다.</summary>
    public sealed class MDSEyes
    {
        private readonly Transform _root;
        private readonly SpriteRenderer[] _whites = new SpriteRenderer[2];
        private readonly SpriteRenderer[] _pupils = new SpriteRenderer[2];
        private readonly MDSSpriteLibrary _sprites;
        private readonly float _spacing;
        private readonly float _size;
        private Vector2 _look;
        private EyeExpression _expr = (EyeExpression)(-1);

        public MDSEyes(Transform parent, MDSSpriteLibrary sprites, float spacing, float size, Color eyeColor, int sortingOrder)
        {
            _sprites = sprites;
            _spacing = spacing;
            _size = size;
            _root = new GameObject("Eyes").transform;
            _root.SetParent(parent, false);
            for (int i = 0; i < 2; i++)
            {
                var w = new GameObject(i == 0 ? "EyeL" : "EyeR").AddComponent<SpriteRenderer>();
                w.transform.SetParent(_root, false);
                w.sortingOrder = sortingOrder;
                w.color = Color.white;
                _whites[i] = w;
                var p = new GameObject("Pupil").AddComponent<SpriteRenderer>();
                p.transform.SetParent(w.transform, false);
                p.sprite = sprites.pupil;
                p.color = eyeColor;
                p.sortingOrder = sortingOrder + 1;
                _pupils[i] = p;
            }
            SetExpression(EyeExpression.Normal);
        }

        public void SetEyeColor(Color c)
        {
            for (int i = 0; i < 2; i++) _pupils[i].color = c;
        }

        public void SetExpression(EyeExpression e)
        {
            if (e == _expr) return;
            _expr = e;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform w = _whites[i].transform;
                w.localPosition = new Vector3(side * _spacing, 0.08f, 0f);
                w.localRotation = Quaternion.identity;
                bool showPupil = true;
                switch (e)
                {
                    case EyeExpression.Surprised:
                        _whites[i].sprite = _sprites.eyeWhite;
                        w.localScale = Vector3.one * _size * 1.45f;
                        _pupils[i].transform.localScale = Vector3.one * 0.32f;
                        break;
                    case EyeExpression.Happy:
                    case EyeExpression.Cheer:
                        _whites[i].sprite = _sprites.eyeHappy;
                        _whites[i].color = _pupils[i].color;
                        w.localScale = new Vector3(_size * 1.2f, _size * 0.8f, 1f);
                        showPupil = false;
                        break;
                    case EyeExpression.Focus:
                        _whites[i].sprite = _sprites.eyeWhite;
                        w.localScale = new Vector3(_size * 1.1f, _size * 0.55f, 1f);
                        _pupils[i].transform.localScale = new Vector3(0.5f, 0.9f, 1f);
                        break;
                    case EyeExpression.Angry:
                        _whites[i].sprite = _sprites.eyeWhite;
                        w.localScale = new Vector3(_size, _size * 0.8f, 1f);
                        _pupils[i].transform.localScale = Vector3.one * 0.55f;
                        break;
                    default:
                        _whites[i].sprite = _sprites.eyeWhite;
                        w.localScale = Vector3.one * _size;
                        _pupils[i].transform.localScale = Vector3.one * 0.5f;
                        break;
                }
                if (e != EyeExpression.Happy && e != EyeExpression.Cheer) _whites[i].color = Color.white;
                _pupils[i].enabled = showPupil;
            }
            ApplyLook();
        }

        /// <summary>바라보는 방향 (-1..1). 눈동자를 그쪽으로 옮긴다.</summary>
        public void Look(Vector2 dir)
        {
            _look = Vector2.ClampMagnitude(dir, 1f);
            ApplyLook();
        }

        /// <summary>놀람 떨림 등 눈 전체 흔들기 (로컬 오프셋)</summary>
        public void Jitter(Vector2 offset) { _root.localPosition = offset; }

        private void ApplyLook()
        {
            for (int i = 0; i < 2; i++) _pupils[i].transform.localPosition = new Vector3(_look.x * 0.25f, _look.y * 0.25f, 0f);
        }

        public void SetVisible(bool v) { _root.gameObject.SetActive(v); }

        public void SetAlpha(float a)
        {
            for (int i = 0; i < 2; i++)
            {
                Color w = _whites[i].color; w.a = a; _whites[i].color = w;
                Color p = _pupils[i].color; p.a = a; _pupils[i].color = p;
            }
        }
    }

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

    /// <summary>장애물 동그라미 (기획서 3.1): 화난 눈썹 + 움직이는 방향으로 눈을 흘김</summary>
    public sealed class MDSObstacleView : MonoBehaviour
    {
        private SpriteRenderer _body;
        private MDSEyes _eyes;
        private Transform[] _brows = new Transform[2];

        public static MDSObstacleView Create(Transform parent, MDSSpriteLibrary sprites, float diameter, int sortingOrder)
        {
            var go = new GameObject("Obstacle");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<MDSObstacleView>();
            v._body = new GameObject("Body").AddComponent<SpriteRenderer>();
            v._body.transform.SetParent(go.transform, false);
            v._body.transform.localScale = Vector3.one * diameter;
            v._body.sprite = sprites.circle;
            v._body.sortingOrder = sortingOrder;
            v._eyes = new MDSEyes(go.transform, sprites, diameter * 0.2f, diameter * 0.28f, Color.black, sortingOrder + 1);
            v._eyes.SetExpression(EyeExpression.Angry);
            for (int i = 0; i < 2; i++)
            {
                var b = new GameObject(i == 0 ? "BrowL" : "BrowR").AddComponent<SpriteRenderer>();
                b.transform.SetParent(go.transform, false);
                b.sprite = sprites.brow;
                b.sortingOrder = sortingOrder + 3;
                float side = i == 0 ? -1f : 1f;
                b.transform.localPosition = new Vector3(side * diameter * 0.2f, diameter * 0.26f, 0f);
                b.transform.localScale = new Vector3(diameter * 0.3f, diameter * 0.12f, 1f);
                // 화난 눈썹: 가운데로 내려옴 (\ /)
                b.transform.localRotation = Quaternion.Euler(0f, 0f, side * -22f);
                v._brows[i] = b.transform;
            }
            return v;
        }

        public void SetColors(Color body, Color eye)
        {
            _body.color = body;
            _eyes.SetEyeColor(eye);
            for (int i = 0; i < 2; i++) _brows[i].GetComponent<SpriteRenderer>().color = eye;
        }

        public void Tick(Vector3 position, Vector2 dir)
        {
            transform.localPosition = position;
            _eyes.Look(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.zero);
        }
    }
}
