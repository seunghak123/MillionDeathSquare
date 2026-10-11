using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 인게임 일시정지 팝업. 계속하기/재시작(처음부터)/메인 화면 이동 버튼 처리.
    /// 판 제어는 MDSSession(→ MDSGameDirector)을 통한다.
    /// </summary>
    public class InGamePause : BaseUIPopup
    {
        [Header("바인딩")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button retryButton;
        [Tooltip("아직 프리팹에 버튼이 없음 — 추가되면 Inspector에서 연결")]
        [SerializeField] private Button homeButton;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.InGamePause;
        }

        public override void EnterWindow()
        {
            RegistEvent();
            base.EnterWindow();

            if (scoreText != null)
                scoreText.text = MDSSession.CurrentDeaths.ToString();
        }

        public override void ExitWindow()
        {
            DeleteRegistedEvent();
            base.ExitWindow();
        }

        // continueButton/homeButton은 프리팹 Inspector에서 OnClick이 이미 연결되어 있음.
        // 여기서 AddListener까지 하면 클릭 1회에 핸들러가 2번 호출되어
        // OnClickContinue()의 두 번째 호출이 UIManager.currentUI를 윈도우로 오인해
        // InGameHUDWindow까지 pop되고 로비로 폴백되는 버그가 있었다 (retryButton은 미연결 상태라 유지).
        public override void RegistEvent()
        {
            if (retryButton != null) retryButton.onClick.AddListener(OnClickRetry);
        }

        public override void DeleteRegistedEvent()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(OnClickRetry);
        }

        // 계속하기 — 일시정지 해제
        public void OnClickContinue()
        {
            ExitWindow();
            MDSSession.Resume();
        }

        // 재시작 — 현재 맵을 처음부터 (R 키와 같음)
        public void OnClickRetry()
        {
            ExitWindow();
            MDSSession.Restart();
        }

        // 로비로 이동
        public void OnClickHome()
        {
            ExitWindow();
            // 인게임 BGM이 로비까지 새어 나오지 않게 명시적으로 끊는다
            if (Seunghak.Manager.SoundManager.Instance != null) Seunghak.Manager.SoundManager.Instance.StopBGM();
            // 판 정리 + 메인 화면 이동은 디렉터가 처리
            MDSSession.ExitToMenu();
        }
    }
}
