using Seunghak.Common;
using Seunghak.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 내 정보 윈도우 (기획서 슬라이드5).
    ///
    /// 표시 정보:
    ///   - 최고 점수 (High Score)
    ///   - 플레이 횟수 (Play Count)
    ///   - 제일 많이 플레이한 캐릭터
    /// </summary>
    public class MyInfoWindow : BaseUIWindow
    {
        [Header("정보 텍스트")]
        [SerializeField] private TextMeshProUGUI bestScoreText;
        [SerializeField] private TextMeshProUGUI playCountText;
        [SerializeField] private TextMeshProUGUI topCharacterText;

        [Header("닫기")]
        [SerializeField] private Button closeButton;

        // ── 생명주기 ────────────────────────────────────────────────────────

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MyInfoWindow;
        }

        public override void EnterWindow()
        {
            RegistEvent();
            base.EnterWindow();
            Refresh();
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            Refresh();
        }

        public override void ExitWindow()
        {
            DeleteRegistedEvent();
            UIManager.Instance.PopUI();
        }

        public override void RegistEvent()
        {
            closeButton?.onClick.AddListener(OnClickClose);
        }

        public override void DeleteRegistedEvent()
        {
            closeButton?.onClick.RemoveListener(OnClickClose);
        }

        // ── 데이터 갱신 ─────────────────────────────────────────────────────

        private void Refresh()
        {
            if (bestScoreText != null)
            {
                int best = UserDataManager.Instance.GetBestScore();
                bestScoreText.text = $"High Score : {best}";
            }

            if (playCountText != null)
            {
                int count = UserDataManager.Instance.GetPlayCount();
                playCountText.text = $"Play Time : {count}";
            }

            if (topCharacterText != null && CharacterManager.Instance != null)
            {
                var id = CharacterManager.Instance.SelectedCharacterId;
                var data = CharacterManager.Instance.GetCharacterData(id);
                topCharacterText.text = data != null ? data.characterName : id.ToString();
            }
        }

        // ── 버튼 핸들러 ─────────────────────────────────────────────────────

        public void OnClickClose()
        {
            ExitWindow();
        }
    }
}
