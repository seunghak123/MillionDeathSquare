using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 도형 스프라이트 생성기 (흰색 + 알파, 색은 SpriteRenderer.color로 입힌다).
    /// 셋업 툴은 이걸로 PNG를 만들어 에셋으로 저장하고, 에셋이 없을 때는 런타임에 바로 만든다.
    /// 스프라이트 1개 = 1유닛 = 1칸 (pixelsPerUnit = 크기).
    /// </summary>
    public static class MDSShapes
    {
        public enum Shape
        {
            Square,
            RoundedSquare,
            Circle,
            Ring,
            /// <summary>웃는 눈 ^ (반가움/만세)</summary>
            Arc,
            /// <summary>가로 막대 (눈썹, 감은 눈)</summary>
            Bar,
            Arrow,
            Key,
            Lock,
            Flag,
            Glow,
            Sparkle,
            IceShine,
        }

        public const int SIZE = 64;

        public static Sprite CreateSprite(Shape shape)
        {
            Texture2D tex = CreateTexture(shape, SIZE);
            Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
            s.name = "MDS_" + shape;
            return s;
        }

        public static Texture2D CreateTexture(Shape shape, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "MDS_" + shape;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // -1..1 좌표, y 위쪽
                    float u = ((x + 0.5f) * inv) * 2f - 1f;
                    float v = ((y + 0.5f) * inv) * 2f - 1f;
                    float a = Alpha(shape, u, v, 2f * inv);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>부호 거리 → 가장자리 부드럽게</summary>
        private static float Fill(float sd, float aa) { return Mathf.Clamp01(0.5f - sd / aa); }

        private static float Box(float u, float v, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(u) - hx + r;
            float qy = Mathf.Abs(v) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float CircleSd(float u, float v, float cx, float cy, float r)
        {
            float dx = u - cx, dy = v - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        private static float Alpha(Shape shape, float u, float v, float aa)
        {
            switch (shape)
            {
                case Shape.Square:
                    return Fill(Box(u, v, 1f, 1f, 0f), aa);
                case Shape.RoundedSquare:
                    return Fill(Box(u, v, 0.98f, 0.98f, 0.28f), aa);
                case Shape.Circle:
                    return Fill(CircleSd(u, v, 0, 0, 0.97f), aa);
                case Shape.Ring:
                    return Fill(Mathf.Abs(CircleSd(u, v, 0, 0, 0.8f)) - 0.14f, aa);
                case Shape.Arc:
                {
                    // ^ 모양: 위로 볼록한 두꺼운 호
                    float d = Mathf.Abs(CircleSd(u, v, 0, -0.55f, 0.8f)) - 0.17f;
                    if (v < -0.2f) d = Mathf.Max(d, (-0.2f - v) * 3f);
                    return Fill(d, aa);
                }
                case Shape.Bar:
                    return Fill(Box(u, v, 0.95f, 0.28f, 0.25f), aa);
                case Shape.Arrow:
                {
                    // 오른쪽을 가리키는 화살표 (몸통 + 삼각형)
                    float body = Box(u + 0.25f, v, 0.45f, 0.18f, 0.05f);
                    float tri = Mathf.Max(Mathf.Abs(v) * 1.1f + (u - 0.85f), -(u - 0.05f));
                    return Fill(Mathf.Min(body, tri), aa);
                }
                case Shape.Key:
                {
                    float ring = Mathf.Abs(CircleSd(u, v, -0.45f, 0f, 0.38f)) - 0.13f;
                    float shaft = Box(u - 0.3f, v, 0.5f, 0.1f, 0.03f);
                    float tooth1 = Box(u - 0.55f, v + 0.2f, 0.07f, 0.16f, 0.02f);
                    float tooth2 = Box(u - 0.75f, v + 0.2f, 0.07f, 0.12f, 0.02f);
                    return Fill(Mathf.Min(Mathf.Min(ring, shaft), Mathf.Min(tooth1, tooth2)), aa);
                }
                case Shape.Lock:
                {
                    float body = Box(u, v + 0.25f, 0.6f, 0.45f, 0.12f);
                    float shackle = Mathf.Abs(CircleSd(u, v, 0f, 0.25f, 0.38f)) - 0.11f;
                    if (v < 0.25f) shackle = Mathf.Max(shackle, 1f);
                    float hole = CircleSd(u, v, 0f, -0.2f, 0.12f);
                    return Fill(Mathf.Max(Mathf.Min(body, shackle), -hole), aa);
                }
                case Shape.Flag:
                {
                    float pole = Box(u + 0.55f, v, 0.07f, 0.9f, 0.03f);
                    float cloth = Box(u + 0.05f, v - 0.45f, 0.5f, 0.35f, 0.06f);
                    return Fill(Mathf.Min(pole, cloth), aa);
                }
                case Shape.Glow:
                {
                    float d = Mathf.Sqrt(u * u + v * v);
                    return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) ;
                }
                case Shape.Sparkle:
                {
                    // 4갈래 반짝임
                    float au = Mathf.Abs(u), av = Mathf.Abs(v);
                    float d = Mathf.Min(au * 4f + av, av * 4f + au) - 0.9f;
                    return Fill(d, aa * 2f);
                }
                case Shape.IceShine:
                {
                    // 대각선 줄무늬 2개 (얼음 무늬)
                    float s1 = Mathf.Abs(u + v - 0.5f) - 0.08f;
                    float s2 = Mathf.Abs(u + v + 0.4f) - 0.05f;
                    float d = Mathf.Min(s1, s2);
                    d = Mathf.Max(d, Box(u, v, 0.85f, 0.85f, 0f));
                    return Fill(d, aa) * 0.8f;
                }
            }
            return 0f;
        }
    }
}
