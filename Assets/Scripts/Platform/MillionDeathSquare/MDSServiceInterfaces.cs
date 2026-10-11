using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Platform
{
    // ───────────────────────────── 창작마당 (기획서 6장) ─────────────────────────────

    /// <summary>공개 범위 (기획서 6.1). 1단계 출시 = Unlisted (맵 코드를 아는 사람만), 미지원 시 FriendsOnly</summary>
    public enum WorkshopVisibility
    {
        Unlisted,
        FriendsOnly,
        Public,
        Private,
    }

    public sealed class WorkshopUploadRequest
    {
        public MapData Map;
        public string Title;
        public string Description = "";
        public WorkshopVisibility Visibility = WorkshopVisibility.Unlisted;
        /// <summary>만든 사람의 클리어 기록 (함께 공개, 기획서 5.4)</summary>
        public int CreatorDeaths;
        public int CreatorTicks;
        /// <summary>만든 사람이 직접 깬 입력 기록 (업로드 전 다시 검증)</summary>
        public InputRecording CreatorRecording;
        /// <summary>미리보기 PNG (없으면 생략)</summary>
        public byte[] PreviewPng;
        /// <summary>이미 올린 항목을 고칠 때 (0 = 새 항목)</summary>
        public ulong UpdateItemId;
    }

    public sealed class WorkshopUploadResult
    {
        public bool Success;
        public ulong ItemId;
        public string MapCode;
        public string Error;
        /// <summary>Steam 창작마당 약관 동의가 필요함 (오버레이로 안내)</summary>
        public bool NeedsLegalAgreement;
        /// <summary>요청한 공개 범위 대신 실제로 쓴 범위 (Unlisted 미지원 → FriendsOnly 등)</summary>
        public WorkshopVisibility AppliedVisibility;
    }

    public sealed class WorkshopItem
    {
        public ulong ItemId;
        public string MapCode;
        public string Title;
        public ulong CreatorId;
        public string CreatorName;
        public MapData Map;
        public int CreatorDeaths = -1;
        public int CreatorTicks = -1;
        public long Likes;
        public WorkshopVisibility Visibility;
    }

    public interface IWorkshopService
    {
        string ServiceName { get; }
        bool IsAvailable { get; }

        /// <summary>업로드 (호출 전 MapEditorModel.CanUpload 확인 — 서비스도 리플레이로 다시 검증한다)</summary>
        UniTask<WorkshopUploadResult> UploadAsync(WorkshopUploadRequest request);

        /// <summary>맵 코드 → 항목 + 맵 데이터 (없거나 실패하면 null)</summary>
        UniTask<WorkshopItem> ResolveMapCodeAsync(string mapCode);

        UniTask<bool> SubscribeAsync(ulong itemId);

        /// <summary>좋아요 (싫어요는 두지 않는다)</summary>
        UniTask<bool> LikeAsync(ulong itemId);

        /// <summary>후기 남기기 (체감 난이도 + 정해진 문구)</summary>
        UniTask<bool> SubmitReviewAsync(ulong itemId, Review review);

        /// <summary>시도 기록 (클리어율 분모). 맵을 처음 시작할 때 한 번</summary>
        UniTask RecordAttemptAsync(ulong itemId);

        /// <summary>후기·클리어율 요약 (맵 상세 화면)</summary>
        UniTask<ReviewSummary> GetSummaryAsync(ulong itemId);

        /// <summary>내가 올린 맵 (제작자 프로필)</summary>
        UniTask<List<WorkshopItem>> GetMyItemsAsync();
    }

    // ───────────────────────────── 순위표 (기획서 6.4, 8.2) ─────────────────────────────

    public interface ILeaderboardService
    {
        /// <summary>이 판에서 제출하는 기록의 플랫폼 (PC/모바일 분리 집계)</summary>
        RecordPlatform Platform { get; }

        /// <summary>
        /// 기록 제출. map + proof로 먼저 리플레이 검증하고, 통과해야만 올린다 (기획서 7.3 기록 조작 방지).
        /// mapKey는 RecordBoards.WorkshopKey/CampaignKey/HashKey.
        /// </summary>
        UniTask<bool> SubmitAsync(string mapKey, MapData map, InputRecording proof, int deaths, int ticks);

        UniTask<List<RecordEntry>> GetTopAsync(string mapKey, RecordKind kind, int count);

        UniTask<RecordEntry> GetMineAsync(string mapKey, RecordKind kind);
    }

    /// <summary>제출 전 공통 검증</summary>
    public static class LeaderboardGuard
    {
        public static bool Verify(MapData map, InputRecording proof, int deaths, int ticks)
        {
            if (map == null || proof == null) return false;
            return ReplayVerifier.Verify(map, proof, deaths, ticks).IsValid;
        }
    }

    // ───────────────────────────── 멀티방 (기획서 7장) ─────────────────────────────

    /// <summary>방장 중계용 메시지 전송 (Steam 네트워킹 메시지 / 로컬 루프백)</summary>
    public interface INetTransport : IRoomTransport
    {
        ulong LocalId { get; }
        /// <summary>받은 메시지를 모두 꺼내 콜백 (메인 스레드에서 매 프레임)</summary>
        void Poll(Action<ulong, byte[]> onReceive);
        /// <summary>위치처럼 잃어도 되는 메시지 (최신 값만 의미 있음)</summary>
        void SendUnreliable(ulong peerId, byte[] data);
        void Close();
    }

    public sealed class LobbyResult
    {
        public bool Success;
        public string RoomCode;
        public ulong HostId;
        public ulong LobbyId;
        public string Error;
    }

    public interface ILobbyService
    {
        ulong LocalPlayerId { get; }
        string LocalPlayerName { get; }
        INetTransport Transport { get; }
        bool SupportsFriendInvite { get; }

        /// <summary>비공개 방 만들기 (코드 없이는 목록에 보이지 않음)</summary>
        UniTask<LobbyResult> CreateRoomAsync(string roomCode);

        UniTask<LobbyResult> JoinByCodeAsync(string roomCode);

        /// <summary>친구 초대 창 (Steam 오버레이)</summary>
        bool InviteFriends();

        void LeaveRoom();

        /// <summary>친구 초대를 수락해 들어온 경우 (방 코드로 다시 참가하도록 알림)</summary>
        event Action<LobbyResult> InviteAccepted;

        /// <summary>매 프레임 (콜백 처리)</summary>
        void Update();
    }
}
