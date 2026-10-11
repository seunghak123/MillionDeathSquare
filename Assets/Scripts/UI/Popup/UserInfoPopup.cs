using Seunghak.Common;
using Seunghak.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 유저 정보 팝업 (MyInfoWindow의 팝업 버전).
    /// 어느 화면에서도 오버레이로 표시 가능.
    ///
    /// 표시 정보:
    ///   - 최고 점수 (Best Score)
    ///   - 플레이 횟수 (Play Count)
    ///   - 대표 캐릭터
    ///
    /// [에디터 세팅]
    ///   - bestScoreText, playCountText, topCharacterText, closeButton 인스펙터 연결
    /// </summary>
    public class UserInfoPopup : BaseUIPopup
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
            ui_Type = UI_TYPE.UserInfoPopup;
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
            base.ExitWindow();
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
                bestScoreText.text = $"Best Score : {best}";
            }

            if (playCountText != null)
            {
                int count = UserDataManager.Instance.GetPlayCount();
                playCountText.text = $"Play Count : {count}";
            }

            if (topCharacterText != null && CharacterManager.Instance != null)
            {
                var id   = CharacterManager.Instance.SelectedCharacterId;
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
