using Cysharp.Threading.Tasks;
using Seunghak.Manager;
using Seunghak.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 멀티방 (기획서 7장, 11장 화면 3): 방 코드 · Steam 친구 초대, 참가자 4칸, 맵 미리보기, 방식(레이스·협동·생존),
    /// 방장만 시작. 비공개 방만 — 방 코드나 친구 초대로만 들어온다.
    /// </summary>
    public sealed class MDSRoomWindow : BaseUIWindow
    {
        [Header("방 만들기 / 참가")]
        [SerializeField] private GameObject lobbyRoot;
        [SerializeField] private Button createButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button joinButton;
        [Header("방 안")]
        [SerializeField] private GameObject roomRoot;
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private Button copyRoomCodeButton;
        [SerializeField] private Button inviteButton;
        [SerializeField] private TMP_Text[] memberSlots = new TMP_Text[Rules.ROOM_MAX_PLAYERS];
        [SerializeField] private TMP_Text countText;
        [SerializeField] private RawImage mapPreview;
        [SerializeField] private TMP_Text mapText;
        [SerializeField] private TMP_InputField mapCodeInput;
        [SerializeField] private Button selectMapButton;
        [SerializeField] private Button[] modeButtons = new Button[3];
        [SerializeField] private Button startButton;
        [SerializeField] private Button botButton;
        [SerializeField] private Button leaveButton;
        [Header("공통")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button backButton;

        private Texture2D _previewTex;
        private uint _previewHash;
        private bool _busy;
        private MDSMultiplayer _mp;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MDSRoomWindow;
            _mp = MDSGameDirector.Instance.Multiplayer;
            MDSUi.Bind(createButton, () => RunAsync(_mp.CreateRoomAsync()).Forget());
            MDSUi.Bind(joinButton, () => RunAsync(_mp.JoinRoomAsync(joinCodeInput != null ? joinCodeInput.text : "")).Forget());
            MDSUi.Bind(copyRoomCodeButton, () => { if (_mp.Session != null) MDSUi.CopyToClipboard(_mp.Session.Room.Code); SetStatus("방 코드를 복사했어요"); });
            MDSUi.Bind(inviteButton, () => { if (!_mp.InviteFriends()) SetStatus("친구 초대는 Steam에서만 돼요 — 방 코드를 보내 주세요"); });
            MDSUi.Bind(selectMapButton, () => RunAsync(_mp.SelectMapAsync(MapRefFromInput(), MDSGameDirector.Instance.CampaignAsset)).Forget());
            for (int i = 0; i < modeButtons.Length; i++)
            {
                MatchMode m = (MatchMode)i;
                MDSUi.Bind(modeButtons[i], () => { _mp.SelectMode(m); Refresh(); });
                MDSUi.SetLabel(modeButtons[i], MDSPlayWindow.ModeLabel(m));
            }
            MDSUi.Bind(startButton, () => { if (!_mp.StartMatch()) SetStatus("2명 이상 모이고 맵을 골라야 시작할 수 있어요"); });
            MDSUi.Bind(botButton, () => { if (!_mp.AddDebugBot()) SetStatus("봇은 오프라인 방장만 넣을 수 있어요"); });
            MDSUi.Bind(leaveButton, () => { _mp.Leave(); Refresh(); });
            MDSUi.Bind(backButton, ExitWindow);
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            _mp.RoomChanged -= Refresh;
            _mp.RoomChanged += Refresh;
            if (SoundManager.Instance != null) SoundManager.Instance.PlayBGM(SoundKeys.MDS_BGM_MULTI);
            MDSAnalytics.Screen("room");
            Refresh();
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            Refresh();
        }

        public override void ExitWindow()
        {
            _mp.RoomChanged -= Refresh;
            // 방은 나가지 않고 메뉴로 — 다시 들어오면 그대로 (명시적으로 "방 나가기"를 눌러야 나감)
            base.ExitWindow();
        }

        /// <summary>입력칸: 맵 코드 / 공유 코드 / "1-3" 캠페인 스테이지</summary>
        private string MapRefFromInput()
        {
            string s = mapCodeInput != null ? mapCodeInput.text.Trim() : "";
            int w, st;
            if (Campaign.TryParseStageId(s, out w, out st)) return MDSMapResolver.CampaignRef(w, st);
            return s;
        }

        private async UniTaskVoid RunAsync(UniTask<bool> task)
        {
            if (_busy) return;
            _busy = true;
            SetStatus("잠시만요…");
            bool ok = await task;
            _busy = false;
            if (this == null) return;
            SetStatus(ok ? "" : (_mp.LastError ?? "실패했어요"));
            Refresh();
        }

        /// <summary>디버그 봇은 에디터/개발 빌드에서만</summary>
        private static bool BotAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        private void SetStatus(string s) { MDSUi.SetText(statusText, s); }

        private void Refresh()
        {
            if (this == null) return;
            bool inRoom = _mp.InRoom && _mp.Session.Room != null;
            MDSUi.SetActive(lobbyRoot, !inRoom);
            MDSUi.SetActive(roomRoot, inRoom);
            if (!inRoom)
            {
                if (_mp.LastError != null) SetStatus(_mp.LastError);
                return;
            }
            Room room = _mp.Session.Room;
            bool host = _mp.Session.IsHost;
            MDSUi.SetText(roomCodeText, "방 코드  " + room.Code);
            MDSUi.SetActive(inviteButton, _mp.SupportsFriendInvite);
            for (int i = 0; i < memberSlots.Length; i++)
            {
                RoomMember mem = null;
                foreach (RoomMember m in room.Members) if (m.Slot == i) mem = m;
                string label = mem == null ? "빈자리" : mem.Name + (mem.PlayerId == room.HostId ? " (방장)" : "") + (mem.PlayerId == _mp.LocalId ? " — 나" : "");
                MDSUi.SetText(memberSlots[i], label);
                if (memberSlots[i] != null) memberSlots[i].color = mem == null ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
            }
            MDSUi.SetText(countText, room.Count + "/" + Rules.ROOM_MAX_PLAYERS + "명");

            ResolvedMap sel = _mp.SelectedMap;
            MDSUi.SetText(mapText, sel != null ? "맵: " + sel.Title : (string.IsNullOrEmpty(room.MapRef) ? "맵을 골라 주세요 (맵 코드 / 공유 코드 / 1-3)" : "맵 받는 중…"));
            if (mapPreview != null && sel != null && sel.Map.ContentHash() != _previewHash)
            {
                if (_previewTex != null) Destroy(_previewTex);
                _previewTex = MDSMapPreview.Render(sel.Map, MDSPaletteColors.FromCore(Palettes.Get(sel.Map.PaletteId)));
                mapPreview.texture = _previewTex;
                _previewHash = sel.Map.ContentHash();
            }
            MDSUi.SetActive(mapPreview, sel != null);

            for (int i = 0; i < modeButtons.Length; i++)
            {
                MDSUi.Tint(modeButtons[i], (int)room.Mode == i ? MDSUi.Selected : MDSUi.Normal);
                MDSUi.SetInteractable(modeButtons[i], host);
            }
            MDSUi.SetActive(mapCodeInput, host);
            MDSUi.SetActive(selectMapButton, host);
            MDSUi.SetActive(startButton, host);
            MDSUi.SetInteractable(startButton, _mp.CanStart);
            MDSUi.SetActive(botButton, host && BotAllowed);
            if (!host) SetStatus(room.State == RoomState.Results ? "방장이 다음 판을 고르는 중" : "방장이 시작하기를 기다리는 중");
        }
    }
}
