using UnityEngine;

namespace Seunghak.UIManager
{
    /// <summary>
    /// Safe Area 대응 컴포넌트.
    /// RectTransform을 디바이스 Safe Area에 맞게 조정합니다.
    /// 노치/펀치홀 디바이스, 웹 브라우저 환경 모두 대응.
    ///
    /// 사용법: UI Canvas 하위의 패널(RectTransform)에 이 컴포넌트를 추가.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool applyTop = true;
        [SerializeField] private bool applyBottom = true;
        [SerializeField] private bool applyLeft = true;
        [SerializeField] private bool applyRight = true;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            // 화면 크기 변경 감지 (브라우저 리사이즈, 회전 등)
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width ||
                _lastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            // Safe Area를 0~1 정규화 좌표로 변환
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // 선택적 적용
            if (!applyLeft)   anchorMin.x = 0f;
            if (!applyBottom) anchorMin.y = 0f;
            if (!applyRight)  anchorMax.x = 1f;
            if (!applyTop)    anchorMax.y = 1f;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}
