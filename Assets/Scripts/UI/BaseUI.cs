using System;
using UnityEngine;

namespace Seunghak.UIManager
{
    public enum UI_TYPE_PROPERTY
    {
        None,
        Window,
        Popup,
        Utils,
    }
    public enum UI_TYPE
    {
        None,
        //1~100까진 기본 Window
        LobbyWindow,
        /// <summary>로비 (게임 시작) 화면</summary>
        GameStartWindow,
        /// <summary>인게임 HUD (점수, 일시정지)</summary>
        InGameHUDWindow,
        /// <summary>상점 (스킨/아이템, Gold·IAP)</summary>
        CharacterShopWindow,
        /// <summary>내 정보 (플레이 횟수, 최고 점수)</summary>
        MyInfoWindow,
        /// <summary>랭킹 시스템 (주간/월간)</summary>
        RankingWindow,
        /// <summary>설정 (오디오, 문의하기)</summary>
        SettingsWindow,

        EventBegin = 100,


        BasePopupWindow = 1000,
        LoadingPopup,
        /// <summary>게임오버 팝업</summary>
        GameOverPopup,
        /// <summary>보상 지급 팝업</summary>
        RewardPopup,
        /// <summary>설정 팝업 (인게임/로비 공용)</summary>
        SettingPopup,
        /// <summary>유저 정보 팝업</summary>
        UserInfoPopup,
        /// <summary>인게임 일시정지 팝업 (계속하기/재시작/로비)</summary>
        InGamePause,
        /// <summary>레벨업 스킬 선택 (3개 중 1개, 광고 다시 뽑기) — 인게임 위에 뜸</summary>
        LevelUpPopup,
        OtherUI = 4000,
        BaseAwnserPopup,
        /// <summary>광고 보상 상자깡 연출 팝업</summary>
        AdGachaBox,

    }
    public interface IBaseUIController
    {
        void EnterWindow();
        void StartWindow();
        void ExitWindow();
        void RestoreWindow();
    }

    public class BaseUI : MonoBehaviour, IBaseUIController
    {
        protected UI_TYPE ui_Type = UI_TYPE.None;
        private Action exitAction = null;
        public UI_TYPE CUR_UI_TYPE
        {
            get { return ui_Type; }
            set { ui_Type = value; }
        }
        public void SetAction(Action exit)
        {
            exitAction = exit;
        }
        public virtual void EnterWindow()
        {
            this.gameObject.SetActive(true);
        }
        public void CloseUI()
        {
            if (exitAction != null)
            {
                exitAction();
            }

            this.gameObject.SetActive(false);
        }
        public virtual void ExitWindow()
        {
            UIManager.Instance.PopUI();
        }

        public virtual void RestoreWindow()
        {
            //파티클 긁어다가 갱신 
            this.gameObject.SetActive(true);
        }

        public virtual void StartWindow()
        {

        }
        public virtual void RegistEvent()
        {
            //버튼 이벤트 등을 등록해주는 함수
        }
        public virtual void DeleteRegistedEvent()
        {
            //버튼 이벤트를 제거해주는 함수
        }
    }
}