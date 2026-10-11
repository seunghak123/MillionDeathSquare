using Seunghak.Game.MillionDeathSquare;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 베이스 프로젝트에서 넘어온 범용 인게임 HUD (레거시).
    ///
    /// 백만 번 죽는 네모의 플레이 화면 HUD는 MDSPlayWindow(데스 수 상단 가운데 크게)이다.
    /// 이 창은 기존 프리팹/에디터 툴 호환용 최소 구현으로, 일시정지 버튼만 동작한다.
    /// </summary>
    public class InGameHUDWindow : BaseUIWindow
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI bestScoreText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private TextMeshProUGUI noticeText;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.InGameHUDWindow;
            if (noticeText != null) noticeText.gameObject.SetActive(false);
        }

        /// <summary>점수 텍스트 갱신 (외부에서 필요할 때만 사용)</summary>
        public void SetScore(int score)
        {
            if (scoreText != null) scoreText.text = score.ToString();
        }

        public void OnClickPause()
        {
            MDSSession.Pause();
            UIManager.Instance.PushUI(UI_TYPE.InGamePause);
        }
    }
}
