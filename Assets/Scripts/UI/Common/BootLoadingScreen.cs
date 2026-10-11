using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 앱 시작부터 로비 UI가 처음 뜰 때까지 화면을 덮는 부트 로딩 화면.
    /// Addressables 번들 밖(Base.unity 씬에 직접 배치)이라 번들 로드 전 구간도 덮을 수 있다.
    /// 에디터 메뉴 Tools/UI/Setup Boot Loading Screen 으로 생성/연결.
    /// </summary>
    public class BootLoadingScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image progressFill;              // 9-slice 둥근 바 (Sliced, 좌측 앵커) — 폭으로 진행률 표시
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private RectTransform spinner;

        [Header("설정")]
        [SerializeField] private float fillSpeed = 1.5f;          // 초당 진행 바 증가량
        [SerializeField] private float spinnerSpeed = -360f;      // 초당 회전 각도 (음수 = 시계 방향)
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private float maxShowSeconds = 30f;      // 안전장치: 이 시간 후 자동 Hide
        [SerializeField] private float fillInset = 4f;            // 진행 바 배경 안쪽 여백(px)

        private float targetProgress;
        private float shownProgress;
        private float shownTime;
        private float fadeElapsed;
        private bool isHiding;
        private string currentStatus;

        private void OnEnable()
        {
            shownTime = 0f;
            isHiding = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }
        }

        /// <summary>진행률(0~1)과 상태 문구 갱신. 진행 바는 뒤로 가지 않는다.</summary>
        public void SetProgress(float progress01, string status)
        {
            if (isHiding) return;

            targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(progress01));

            // 문구는 바뀔 때만 대입 (TMP 리빌드 방지)
            if (statusText != null && status != null && status != currentStatus)
            {
                currentStatus = status;
                statusText.text = status;
            }
        }

        /// <summary>진행 바를 100%로 채운 뒤 페이드아웃하고 비활성. 중복 호출 안전.</summary>
        public void Hide()
        {
            if (isHiding || !gameObject.activeSelf) return;

            isHiding = true;
            fadeElapsed = 0f;
            targetProgress = 1f;
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = false;
        }

        private void Start()
        {
            ApplyFill(shownProgress);
        }

        /// <summary>
        /// Filled 타입은 9-slice가 안 돼 둥근 끝이 찌그러지므로, Sliced 이미지의 폭을 바꿔 진행률을 표시한다.
        /// 최소 폭 = 바 높이 (0%일 때도 둥근 점 모양 유지).
        /// </summary>
        private void ApplyFill(float progress01)
        {
            if (progressFill == null) return;

            var fillRt = progressFill.rectTransform;
            var parentRt = fillRt.parent as RectTransform;
            if (parentRt == null) return;

            Rect parentRect = parentRt.rect;
            float height = parentRect.height - fillInset * 2f;
            float maxWidth = parentRect.width - fillInset * 2f;
            float width = Mathf.Lerp(height, maxWidth, progress01);
            fillRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (spinner != null)
                spinner.Rotate(0f, 0f, spinnerSpeed * dt);

            if (shownProgress < targetProgress)
            {
                shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, fillSpeed * dt);
                ApplyFill(shownProgress);
            }

            if (!isHiding)
            {
                shownTime += dt;
                if (shownTime >= maxShowSeconds)
                {
                    Debug.LogWarning($"[BootLoadingScreen] {maxShowSeconds}초 동안 로비 UI가 뜨지 않아 로딩 화면을 강제로 닫습니다.");
                    Hide();
                }
                return;
            }

            // 진행 바가 거의 찼을 때부터 페이드 시작
            if (shownProgress < 0.999f) return;

            fadeElapsed += dt;
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - t;

            if (t >= 1f)
                gameObject.SetActive(false); // Update 중단
        }
    }
}
