using System;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// 플랫폼별 UI 여백 (캔버스 단위, 기준 해상도 720x1280).
    /// 플랫폼 앱의 상단 내비게이션 바 등에 UI가 가리지 않도록 화면 가장자리에 고정된 UI를 안쪽으로 민다.
    /// </summary>
    [Serializable]
    public struct SafeAreaInset
    {
        public float top;
        public float bottom;
        public float left;
        public float right;

        public SafeAreaInset(float top, float bottom = 0f, float left = 0f, float right = 0f)
        {
            this.top = top;
            this.bottom = bottom;
            this.left = left;
            this.right = right;
        }

        public static readonly SafeAreaInset Zero = new SafeAreaInset(0f);

        public static SafeAreaInset operator -(SafeAreaInset a, SafeAreaInset b)
            => new SafeAreaInset(a.top - b.top, a.bottom - b.bottom, a.left - b.left, a.right - b.right);

        public bool IsZero => Mathf.Approximately(top, 0f) && Mathf.Approximately(bottom, 0f)
                           && Mathf.Approximately(left, 0f) && Mathf.Approximately(right, 0f);
    }

    /// <summary>
    /// 현재 플랫폼의 SafeAreaInset을 UI 계층에 적용한다.
    /// 화면 가장자리에 고정된(anchor 0 또는 1) 요소만 이동하고, stretch 요소(배경 등)는 크기를 유지한 채 그 안쪽만 탐색한다.
    /// </summary>
    public static class PlatformSafeArea
    {
        /// <summary>현재 플랫폼의 여백.</summary>
        public static SafeAreaInset Current => PlatformFactory.Current.SafeAreaInset;

        /// <summary>
        /// root 하위에 inset 만큼 적용. 같은 root에 다시 호출할 때는 이전에 적용한 값을 넘겨주면 차이만 반영한다.
        /// </summary>
        /// <returns>적용 후 누적 inset (다음 호출 시 applied로 넘길 값)</returns>
        public static SafeAreaInset Apply(Transform root, SafeAreaInset applied)
        {
            SafeAreaInset target = Current;
            SafeAreaInset delta = target - applied;
            if (root == null || delta.IsZero) return applied;

            ShiftEdgeAnchored(root, delta);
            return target;
        }

        private static void ShiftEdgeAnchored(Transform parent, SafeAreaInset delta)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (child == null) continue;

                Vector2 min = child.anchorMin;
                Vector2 max = child.anchorMax;
                Vector2 shift = Vector2.zero;

                if (IsPinned(min.y, max.y, 1f)) shift.y -= delta.top;
                else if (IsPinned(min.y, max.y, 0f)) shift.y += delta.bottom;

                if (IsPinned(min.x, max.x, 0f)) shift.x += delta.left;
                else if (IsPinned(min.x, max.x, 1f)) shift.x -= delta.right;

                if (shift != Vector2.zero)
                {
                    child.anchoredPosition += shift;
                }
                else if (IsStretch(min.y, max.y) || IsStretch(min.x, max.x))
                {
                    ShiftEdgeAnchored(child, delta);
                }
            }
        }

        private static bool IsPinned(float min, float max, float edge)
            => Mathf.Approximately(min, edge) && Mathf.Approximately(max, edge);

        private static bool IsStretch(float min, float max)
            => Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 1f);
    }
}
