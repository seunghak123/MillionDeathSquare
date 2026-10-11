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

        // ── 백만 번 죽는 네모 화면 (기획서 11장) ── 프리팹은 Tools/Million Death Square 메뉴가 생성
        /// <summary>메인 화면 (캠페인/에디터/맵 찾기/멀티방/설정)</summary>
        MDSMainWindow,
        /// <summary>캠페인 선택 (월드 5 × 스테이지 10, 기록 표시)</summary>
        MDSCampaignWindow,
        /// <summary>플레이 HUD (데스 수 상단 가운데 크게, 월드-스테이지, 동전 n/m)</summary>
        MDSPlayWindow,
        /// <summary>맵 에디터 (왼쪽 도구, 가운데 칸, 위쪽 시험 플레이/업로드)</summary>
        MDSEditorWindow,
        /// <summary>맵 찾기 (맵 코드 입력, 공유 코드 붙여넣기, 플레이한 맵 목록)</summary>
        MDSMapFindWindow,
        /// <summary>멀티방 (참가자, 맵 미리보기, 레이스/협동/생존, 방장만 시작)</summary>
        MDSRoomWindow,

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
        /// <summary>맵 상세·후기 (클리어율, 최소 데스 보유자, 체감 난이도 분포, 좋아요, 한 줄 후기)</summary>
        MDSMapDetailPopup,
        /// <summary>결과 (데스 수, 시간, 0데스, 후기 남기기, 멀티는 가장 많이 죽은 장소)</summary>
        MDSResultPopup,
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