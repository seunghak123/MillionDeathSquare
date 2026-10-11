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
}
