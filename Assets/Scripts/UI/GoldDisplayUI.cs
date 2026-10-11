using System;
using System.Collections;
using UnityEngine;
using TMPro;
using Seunghak.Common;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 재화(Gold) 표시 UI. 이 라벨에 숫자를 쓰는 유일한 주인이다.
    /// - UserDataManager.OnGoldChanged 이벤트를 구독해 자동 갱신.
    /// - 증가/감소 시 숫자가 카운팅되는 연출 + 변동량 팝업 텍스트.
    ///
    /// 표시값 = 지갑 보유량 + 보너스(BonusProvider).
    /// 인게임처럼 "아직 지갑에 적립되지 않은 획득분"까지 합쳐 보여줘야 하는 화면은
    /// SetBonusProvider()로 그 값을 읽는 함수만 넘겨준다(값을 캐시하지 않고 그릴 때마다
    /// 다시 읽으므로, 적립 시점에 지갑/획득분이 동시에 바뀌어도 이중 계산되지 않는다).
    ///
    /// 사용법:
    ///   goldText        — 현재 보유량을 표시할 TextMeshProUGUI
    ///   deltaPopupText  — "+N" / "-N" 팝업 텍스트 (선택, 없으면 팝업 생략)
    ///   countDuration   — 카운팅 연출 시간 (기본 0.5초)
    ///   popupRiseHeight — 팝업이 위로 올라가는 거리 (픽셀)
    /// </summary>
    public class GoldDisplayUI : MonoBehaviour
    {
        [Header("텍스트 바인딩")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI deltaPopupText;  // "+N" / "-N" 팝업 (선택)

        [Header("카운팅 연출")]
        [SerializeField] private float countDuration = 0.5f;

        [Header("팝업 연출")]
        [SerializeField] private float popupRiseHeight = 60f;
        [SerializeField] private float popupDuration   = 0.8f;

        private long   _displayedValue;
        private bool   _hasDisplayedValue;   // 한 번이라도 유효한 값을 찍었는지 (초기 0 고착 방지)
        private bool   _subscribed;
        private Coroutine _countCoroutine;
        private Coroutine _popupCoroutine;
        private Func<long> _bonusProvider;

        // ── 생명주기 ───────────────────────────────────────────────

        private void OnEnable()
        {
            EnsureSubscribed();
            Refresh(false);

            // 씬 로드 직후엔 UserDataManager가 아직 없거나 저장 데이터를 복원하기 전에
            // OnEnable이 돌 수 있다. 그 순간 읽은 0이 그대로 굳으면(인게임 진입 시 0골드 표시)
            // 다음 지갑 변경까지 틀린 값이 남으므로, 다음 프레임에 한 번 더 읽어 보정한다.
            if (gameObject.activeInHierarchy)
                StartCoroutine(RefreshDeferred());
        }

        private void OnDisable()
        {
            if (_subscribed && UserDataManager.Instance != null)
                UserDataManager.Instance.OnGoldChanged -= HandleGoldChanged;
            _subscribed = false;
        }

        // ── 외부 API ──────────────────────────────────────────────

        /// <summary>
        /// 지갑 잔액에 더해서 보여줄 값을 읽는 함수를 등록한다(null이면 지갑만 표시).
        /// 값이 아니라 함수를 받는 이유는 클래스 주석 참고 — 항상 최신 값을 다시 읽어야 한다.
        /// </summary>
        public void SetBonusProvider(Func<long> provider)
        {
            _bonusProvider = provider;
        }

        /// <summary>현재 값을 다시 읽어 표시를 갱신한다. animate=false면 즉시 반영.</summary>
        public void Refresh(bool animate)
        {
            if (UserDataManager.Instance == null) return;   // 아직 못 읽는 상태면 0을 쓰지 않는다

            long target = GetTargetValue();

            // 최초 1회는 연출 없이(0에서부터 카운팅되는 것처럼 보이지 않게) 바로 확정한다.
            if (animate && _hasDisplayedValue)
                AnimateTo(target);
            else
                SetImmediate(target);
        }

        /// <summary>연출 없이 즉시 갱신 (초기화용).</summary>
        public void SetImmediate(long value)
        {
            if (_countCoroutine != null) StopCoroutine(_countCoroutine);
            _countCoroutine = null;
            _displayedValue = value;
            _hasDisplayedValue = true;
            UpdateText(value);
        }

        // ── 내부 ─────────────────────────────────────────────────

        private long GetTargetValue()
        {
            long wallet = UserDataManager.Instance != null ? UserDataManager.Instance.GetGold() : 0;
            long bonus  = _bonusProvider != null ? _bonusProvider() : 0;
            return wallet + bonus;
        }

        private void EnsureSubscribed()
        {
            if (_subscribed || UserDataManager.Instance == null) return;
            UserDataManager.Instance.OnGoldChanged += HandleGoldChanged;
            _subscribed = true;
        }

        private void AnimateTo(long target)
        {
            if (_countCoroutine != null) StopCoroutine(_countCoroutine);

            if (!gameObject.activeInHierarchy)
            {
                SetImmediate(target);
                return;
            }

            _countCoroutine = StartCoroutine(CountTo(target, countDuration));
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────

        private void HandleGoldChanged(long prev, long next)
        {
            Refresh(true);

            // 변동량 팝업 (지갑 변동 기준)
            long delta = next - prev;
            if (deltaPopupText != null && delta != 0 && gameObject.activeInHierarchy)
            {
                if (_popupCoroutine != null) StopCoroutine(_popupCoroutine);
                _popupCoroutine = StartCoroutine(ShowDeltaPopup(delta));
            }
        }

        // ── 코루틴 ────────────────────────────────────────────────

        private IEnumerator RefreshDeferred()
        {
            yield return null;
            EnsureSubscribed();
            // 이 시점에 이미 정상 값이면 같은 값을 다시 쓰는 것뿐이라 화면 변화는 없다.
            Refresh(false);
        }

        private IEnumerator CountTo(long target, float duration)
        {
            long start = _displayedValue;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // easeOut 곡선
                float eased = 1f - (1f - t) * (1f - t);
                _displayedValue = (long)Mathf.LerpUnclamped(start, target, eased);
                UpdateText(_displayedValue);
                yield return null;
            }

            _displayedValue = target;
            _hasDisplayedValue = true;
            UpdateText(target);
            _countCoroutine = null;
        }

        private IEnumerator ShowDeltaPopup(long delta)
        {
            if (deltaPopupText == null) yield break;

            // 텍스트 및 색상 설정
            deltaPopupText.text = delta > 0 ? $"+{delta}" : delta.ToString();
            deltaPopupText.color = delta > 0
                ? new Color(0.3f, 1f, 0.3f, 1f)   // 증가 → 초록
                : new Color(1f, 0.3f, 0.3f, 1f);  // 감소 → 빨강
            deltaPopupText.gameObject.SetActive(true);

            var rect = deltaPopupText.rectTransform;
            Vector2 startPos = Vector2.zero;
            rect.anchoredPosition = startPos;

            float elapsed = 0f;
            while (elapsed < popupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / popupDuration;

                // 위로 이동
                rect.anchoredPosition = startPos + Vector2.up * (popupRiseHeight * t);

                // 후반부 페이드 아웃
                float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
                var c = deltaPopupText.color;
                c.a = alpha;
                deltaPopupText.color = c;

                yield return null;
            }

            deltaPopupText.gameObject.SetActive(false);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────

        private void UpdateText(long value)
        {
            if (goldText != null)
                goldText.text = value.ToString("N0");
        }
    }
}
