using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 광고 보상 상자깡 연출 팝업. 상자를 누르면 뚜껑(box up)이 왼쪽으로 살짝 기울며 위로 열리고
    /// 골드가 팡팡 터지는 연출 후, 호출부가 넘겨준 보상 지급 콜백을 실행하고 잠시 후 자동으로 닫힌다.
    /// 실제 광고 시청/보상 지급 로직은 호출부(CharacterShopWindow 등)가 담당하고,
    /// 이 팝업은 순수하게 "상자 열기" 연출만 책임진다.
    /// </summary>
    public class AdGachaBox : BaseUIPopup
    {
        [Header("상자 (box up = 뚜껑, box_under = 받침)")]
        [SerializeField] private RectTransform boxUpTransform;

        [Header("연출 — 뚜껑 이동/회전")]
        [SerializeField] private float openMoveDistance = 400f;
        [SerializeField] private float openTiltAngle = 45f; // 열릴 때 왼쪽으로 기우는 각도(도)
        [SerializeField] private float openDuration = 0.3f;
        [SerializeField] private float autoCloseDelay = 1f;

        [Header("골드 팡팡 이펙트 (에디터에서 미리 배치된 코인 조각들)")]
        [Tooltip("상자 중심에 미리 배치해둔 코인 이미지들 — 런타임 Instantiate 없이 이 오브젝트들을 재사용해 사방으로 튀긴다.")]
        [SerializeField] private RectTransform[] goldBurstPieces;
        [SerializeField] private float burstRadius = 300f;
        [SerializeField] private float burstDuration = 0.5f;

        [Header("보상 텍스트 (선택 — 열렸을 때 표시)")]
        [SerializeField] private TextMeshProUGUI rewardText;

        private Action _onOpened;
        private Action _onClosed;
        private bool _opened;
        private Vector2 _boxUpOriginalPos;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.AdGachaBox;
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            _opened = false;

            if (boxUpTransform != null)
            {
                _boxUpOriginalPos = boxUpTransform.anchoredPosition;
                boxUpTransform.localRotation = Quaternion.identity;
            }
            if (rewardText != null)
                rewardText.gameObject.SetActive(false);

            ResetGoldBurstPieces();
        }

        public override void ExitWindow()
        {
            // ESC 등으로 상자를 열지 않고 취소한 경우에도(연출 도중 강제 종료 포함) 호출부가
            // "진행 중" 플래그를 반드시 풀 수 있도록, 열림 성공 여부와 무관하게 항상 알려준다.
            var closed = _onClosed;
            _onOpened = null;
            _onClosed = null;
            base.ExitWindow();
            closed?.Invoke();
        }

        /// <summary>팝업을 띄우면서, 상자를 눌렀을 때 실행할 보상 지급 콜백을 등록한다.
        /// onClosed는 보상 지급 여부와 무관하게 팝업이 실제로 닫히는 시점(연출 종료든, ESC 취소든)에 항상 호출된다 —
        /// 호출부가 "진행 중" 플래그를 안전하게 풀 수 있도록 하기 위함.</summary>
        public void Open(Action onOpened, Action onClosed = null)
        {
            _onOpened = onOpened;
            _onClosed = onClosed;
        }

        // box up / box_under 버튼 OnClick은 프리팹 Inspector에서 이 메서드에 직접 연결됨.
        public void OnClickBox()
        {
            if (_opened) return;
            _opened = true;
            OpenSequenceAsync().Forget();
        }

        private async UniTaskVoid OpenSequenceAsync()
        {
            if (boxUpTransform != null)
            {
                Vector2 startPos = boxUpTransform.anchoredPosition;
                Vector2 endPos = startPos + new Vector2(0f, openMoveDistance);
                float elapsed = 0f;
                while (elapsed < openDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / openDuration);
                    boxUpTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                    // 위로 올라가면서 왼쪽으로 살짝 기울어 뚜껑이 열리는 느낌을 준다.
                    boxUpTransform.localRotation = Quaternion.Euler(0f, 0f, openTiltAngle * t);
                    await UniTask.Yield(PlayerLoopTiming.Update, this.destroyCancellationToken);
                }
                boxUpTransform.anchoredPosition = endPos;
                boxUpTransform.localRotation = Quaternion.Euler(0f, 0f, openTiltAngle);
            }

            if (rewardText != null)
                rewardText.gameObject.SetActive(true);

            PlayGoldBurst().Forget();

            // 실제 보상 지급(골드 등)은 호출부 콜백이 담당 — 여기서는 열리는 연출과 동시에 실행만 시켜준다.
            _onOpened?.Invoke();

            await UniTask.Delay((int)(autoCloseDelay * 1000f), cancellationToken: this.destroyCancellationToken);

            if (boxUpTransform != null)
            {
                boxUpTransform.anchoredPosition = _boxUpOriginalPos;
                boxUpTransform.localRotation = Quaternion.identity;
            }

            ExitWindow();
        }

        /// <summary>골드 조각들을 상자 중심에서 사방으로 팡팡 튀기고 사라지게 한다.
        /// 새 오브젝트를 만들지 않고, 에디터에서 미리 배치해둔 goldBurstPieces를 재사용한다.</summary>
        private async UniTaskVoid PlayGoldBurst()
        {
            if (goldBurstPieces == null || goldBurstPieces.Length == 0) return;

            int count = goldBurstPieces.Length;
            var starts = new Vector2[count];
            var ends = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                var piece = goldBurstPieces[i];
                if (piece == null) continue;

                // 사방이 아니라 위쪽으로만 팡팡 튀도록, 90도(정위쪽) 중심으로 부채꼴(±60도)
                // 범위 안에서만 방향을 고르게 퍼뜨리고 살짝 랜덤을 섞는다.
                const float upCenterAngle = 90f;
                const float upSpread = 60f;
                float baseAngle = upCenterAngle - upSpread + (upSpread * 2f) / Mathf.Max(1, count - 1) * i;
                float angle = baseAngle + UnityEngine.Random.Range(-8f, 8f);
                float radius = burstRadius * UnityEngine.Random.Range(0.7f, 1f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                // 모든 조각은 항상 부모(GoldBurstFX)의 로컬 원점(0,0)에서 출발한다 — 저번에
                // 튀어나간 위치를 "원래 자리"로 착각하고 거기서 또 튀어서 매번 위치가
                // 누적되어 올라가던 버그가 있었다. 원점을 항상 고정해서 고쳤다.
                starts[i] = Vector2.zero;
                ends[i] = starts[i] + dir * radius;

                piece.anchoredPosition = starts[i];
                piece.localScale = Vector3.one * 0.5f;
                piece.gameObject.SetActive(true);
            }

            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);
                // ease-out: 처음엔 빠르게 튀어나갔다가 천천히 멈춤
                float moveT = 1f - (1f - t) * (1f - t);
                float scaleT = Mathf.Sin(t * Mathf.PI); // 커졌다가 다시 작아짐(팡 터지는 느낌)

                for (int i = 0; i < count; i++)
                {
                    var piece = goldBurstPieces[i];
                    if (piece == null) continue;
                    piece.anchoredPosition = Vector2.Lerp(starts[i], ends[i], moveT);
                    piece.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, scaleT);
                    var cg = piece.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f - t;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, this.destroyCancellationToken);
            }

            ResetGoldBurstPieces();
        }

        /// <summary>골드 조각들을 비활성화하고 부모 로컬 원점(0,0)·기본 크기·불투명으로 되돌린다.
        /// 팝업이 열릴 때와 버스트 연출이 끝났을 때 모두 호출해, 다음 번 버스트가 항상 같은
        /// 자리에서 시작하도록 보장한다.</summary>
        private void ResetGoldBurstPieces()
        {
            if (goldBurstPieces == null) return;
            for (int i = 0; i < goldBurstPieces.Length; i++)
            {
                var piece = goldBurstPieces[i];
                if (piece == null) continue;
                piece.gameObject.SetActive(false);
                piece.anchoredPosition = Vector2.zero;
                piece.localScale = Vector3.one;
                var cg = piece.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }
    }
}
