using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
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
