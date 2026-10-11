// ─────────────────────────────────────────────────────────────────────────────
// Steamworks.NET 구현 (창작마당 / 순위표 / 로비 / 네트워킹 메시지).
//
// ⚠ 이 파일은 Steamworks.NET이 설치되고 Scripting Define Symbol STEAMWORKS_NET이 켜졌을 때만 컴파일된다.
//   이 저장소의 작업 환경에는 Unity/Steamworks가 없어 mcs 문법 검사만 거쳤다 — 실제 빌드에서 반드시 확인할 것
//   (Docs/TODO.md "mcs로만 확인한 것"). API는 Steamworks.NET 20.x 기준.
//   설치: Packages/manifest.json에
//     "com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2024.8.0"
//   그리고 Player Settings > Scripting Define Symbols (Standalone)에 STEAMWORKS_NET 추가.
//   steam_appid.txt(개발 중)와 SteamPlatform.AppId 값을 실제 앱 ID로 바꿀 것.
// ─────────────────────────────────────────────────────────────────────────────
#if STEAMWORKS_NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Seunghak.Game.MillionDeathSquare;
using Steamworks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>Steam 콜 결과를 UniTask로 기다리기</summary>
    internal static class SteamCall
    {
        public static UniTask<T> Await<T>(SteamAPICall_t call) where T : struct
        {
            var tcs = new UniTaskCompletionSource<T>();
            if (call == SteamAPICall_t.Invalid)
            {
                tcs.TrySetException(new InvalidOperationException("Steam API 호출 실패: " + typeof(T).Name));
                return tcs.Task;
            }
            CallResult<T> holder = null;
            holder = CallResult<T>.Create((result, ioFailure) =>
            {
                if (ioFailure) tcs.TrySetException(new IOException("Steam IO 실패: " + typeof(T).Name));
                else tcs.TrySetResult(result);
                holder.Dispose();
            });
            holder.Set(call);
            return tcs.Task;
        }
    }

    // ───────────────────────────── 창작마당 ─────────────────────────────

    /// <summary>
    /// Steam 창작마당 (기획서 6장).
    ///   - 업로드: CreateItem → StartItemUpdate(제목·설명·메타데이터=공유 코드·태그·키값 태그·내용 폴더·미리보기·공개 범위) → SubmitItemUpdate
    ///   - 1단계 공개 범위: k_ERemoteStoragePublishedFileVisibilityUnlisted ("목록에 표시 안 함"). 실패하면 FriendsOnly로 다시 시도 (6.1)
    ///   - 맵 코드 = 창작마당 항목 번호의 MapCode. 조회는 CreateQueryUGCDetailsRequest + 메타데이터(공유 코드)라 다운로드 없이 바로 플레이
    ///   - 좋아요 = SetUserItemVote(true). 후기(체감 난이도·문구)와 시도 수는 맵별 순위표에 한 사람당 한 줄로 저장해 모은다
    ///     (창작마당 댓글 API가 없어서 — "mds_{key}_review"의 점수 = 난이도×100 + 문구+1, 상세값[0] = 좋아요·클리어)
    /// </summary>
    public sealed class SteamWorkshopService : IWorkshopService
    {
        public string ServiceName { get { return "Steam 창작마당"; } }
        public bool IsAvailable { get { return SteamPlatform.IsReady; } }

        private static AppId_t App { get { return SteamUtils.GetAppID(); } }

        public async UniTask<WorkshopUploadResult> UploadAsync(WorkshopUploadRequest request)
        {
            var result = new WorkshopUploadResult();
            if (!IsAvailable) { result.Error = "Steam에 연결되지 않았습니다"; return result; }
            ValidationResult v = MapValidator.Validate(request.Map);
            if (!v.IsValid) { result.Error = v.ToString(); return result; }
            if (!LeaderboardGuard.Verify(request.Map, request.CreatorRecording, request.CreatorDeaths, request.CreatorTicks))
            {
                result.Error = "직접 클리어한 기록이 확인되지 않았습니다";
                return result;
            }

            try
            {
                PublishedFileId_t fileId;
                if (request.UpdateItemId != 0)
                {
                    fileId = new PublishedFileId_t(request.UpdateItemId);
                }
                else
                {
                    CreateItemResult_t created = await SteamCall.Await<CreateItemResult_t>(SteamUGC.CreateItem(App, EWorkshopFileType.k_EWorkshopFileTypeCommunity));
                    if (created.m_bUserNeedsToAcceptWorkshopLegalAgreement) result.NeedsLegalAgreement = true;
                    if (created.m_eResult != EResult.k_EResultOK) { result.Error = "항목 생성 실패: " + created.m_eResult; return result; }
                    fileId = created.m_nPublishedFileId;
                }

                MapData map = request.Map.Clone();
                if (!string.IsNullOrEmpty(request.Title)) map.Name = request.Title;
                string shareCode = MapSerializer.ToShareCode(map);
                string folder = WriteContentFolder(fileId, map, request);

                WorkshopVisibility applied = request.Visibility;
                EResult submit = await Submit(fileId, map, request, shareCode, folder, applied);
                if (submit != EResult.k_EResultOK && applied == WorkshopVisibility.Unlisted)
                {
                    // "목록에 표시 안 함" 미지원/거부 → 친구만 보기로 (기획서 6.1)
                    applied = WorkshopVisibility.FriendsOnly;
                    submit = await Submit(fileId, map, request, shareCode, folder, applied);
                }
                if (submit != EResult.k_EResultOK) { result.Error = "업로드 실패: " + submit; return result; }

                result.Success = true;
                result.ItemId = fileId.m_PublishedFileId;
                result.MapCode = MapCode.Encode(result.ItemId);
                result.AppliedVisibility = applied;
                return result;
            }
            catch (Exception e)
            {
                result.Error = e.Message;
                return result;
            }
        }

        private async UniTask<EResult> Submit(PublishedFileId_t fileId, MapData map, WorkshopUploadRequest request, string shareCode, string folder, WorkshopVisibility visibility)
        {
            UGCUpdateHandle_t h = SteamUGC.StartItemUpdate(App, fileId);
            SteamUGC.SetItemTitle(h, string.IsNullOrEmpty(map.Name) ? "Untitled" : map.Name);
            SteamUGC.SetItemDescription(h, request.Description ?? "");
            // 메타데이터 최대 5000바이트 — 32×18 + 장애물 60개 공유 코드도 들어간다
            SteamUGC.SetItemMetadata(h, shareCode);
            SteamUGC.SetItemTags(h, new List<string> { "map", "palette" + map.PaletteId });
            SteamUGC.AddItemKeyValueTag(h, "creator_deaths", request.CreatorDeaths.ToString());
            SteamUGC.AddItemKeyValueTag(h, "creator_ticks", request.CreatorTicks.ToString());
            SteamUGC.AddItemKeyValueTag(h, "rules", Rules.RULES_VERSION.ToString());
            SteamUGC.SetItemContent(h, folder);
            string preview = Path.Combine(folder, "preview.png");
            if (File.Exists(preview)) SteamUGC.SetItemPreview(h, preview);
            SteamUGC.SetItemVisibility(h, ToSteam(visibility));
            SubmitItemUpdateResult_t r = await SteamCall.Await<SubmitItemUpdateResult_t>(SteamUGC.SubmitItemUpdate(h, "Million Death Square map"));
            return r.m_eResult;
        }

        private static string WriteContentFolder(PublishedFileId_t id, MapData map, WorkshopUploadRequest request)
        {
            string folder = Path.Combine(Application.temporaryCachePath, "mds_upload_" + id.m_PublishedFileId);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "map.mds"), MapSerializer.Serialize(map));
            if (request.CreatorRecording != null) File.WriteAllBytes(Path.Combine(folder, "creator.mdr"), request.CreatorRecording.Serialize());
            if (request.PreviewPng != null) File.WriteAllBytes(Path.Combine(folder, "preview.png"), request.PreviewPng);
            return folder;
        }

        private static ERemoteStoragePublishedFileVisibility ToSteam(WorkshopVisibility v)
        {
            switch (v)
            {
                case WorkshopVisibility.Public: return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic;
                case WorkshopVisibility.FriendsOnly: return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly;
                case WorkshopVisibility.Private: return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate;
                default: return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted;
            }
        }

        public async UniTask<WorkshopItem> ResolveMapCodeAsync(string mapCode)
        {
            ulong id;
            if (!IsAvailable || !MapCode.TryDecode(mapCode, out id)) return null;
            List<WorkshopItem> items = await QueryDetails(new[] { new PublishedFileId_t(id) });
            return items.Count > 0 ? items[0] : null;
        }

        private async UniTask<List<WorkshopItem>> QueryDetails(PublishedFileId_t[] ids)
        {
            var list = new List<WorkshopItem>();
            UGCQueryHandle_t q = SteamUGC.CreateQueryUGCDetailsRequest(ids, (uint)ids.Length);
            SteamUGC.SetReturnMetadata(q, true);
            SteamUGC.SetReturnKeyValueTags(q, true);
            try
            {
                SteamUGCQueryCompleted_t done = await SteamCall.Await<SteamUGCQueryCompleted_t>(SteamUGC.SendQueryUGCRequest(q));
                if (done.m_eResult != EResult.k_EResultOK) return list;
                for (uint i = 0; i < done.m_unNumResultsReturned; i++)
                {
                    WorkshopItem item = ReadResult(q, i);
                    if (item != null) list.Add(item);
                }
            }
            finally
            {
                SteamUGC.ReleaseQueryUGCRequest(q);
            }
            return list;
        }

        private static WorkshopItem ReadResult(UGCQueryHandle_t q, uint index)
        {
            SteamUGCDetails_t d;
            if (!SteamUGC.GetQueryUGCResult(q, index, out d) || d.m_eResult != EResult.k_EResultOK) return null;
            string meta;
            if (!SteamUGC.GetQueryUGCMetadata(q, index, out meta, (uint)Constants.k_cchDeveloperMetadataMax)) return null;
            MapData map;
            string error;
            if (!MapSerializer.TryParseShareCode(meta, out map, out error)) return null;
            var item = new WorkshopItem
            {
                ItemId = d.m_nPublishedFileId.m_PublishedFileId,
                MapCode = MapCode.Encode(d.m_nPublishedFileId.m_PublishedFileId),
                Title = d.m_rgchTitle,
                CreatorId = d.m_ulSteamIDOwner,
                CreatorName = SteamFriends.GetFriendPersonaName(new CSteamID(d.m_ulSteamIDOwner)),
                Map = map,
                Likes = d.m_unVotesUp,
            };
            uint tags = SteamUGC.GetQueryUGCNumKeyValueTags(q, index);
            for (uint t = 0; t < tags; t++)
            {
                string key, value;
                if (!SteamUGC.GetQueryUGCKeyValueTag(q, index, t, out key, 256, out value, 256)) continue;
                int n;
                if (!int.TryParse(value, out n)) continue;
                if (key == "creator_deaths") item.CreatorDeaths = n;
                else if (key == "creator_ticks") item.CreatorTicks = n;
            }
            return item;
        }

        public async UniTask<bool> SubscribeAsync(ulong itemId)
        {
            if (!IsAvailable) return false;
            RemoteStorageSubscribePublishedFileResult_t r = await SteamCall.Await<RemoteStorageSubscribePublishedFileResult_t>(SteamUGC.SubscribeItem(new PublishedFileId_t(itemId)));
            return r.m_eResult == EResult.k_EResultOK;
        }

        public async UniTask<bool> LikeAsync(ulong itemId)
        {
            if (!IsAvailable) return false;
            SetUserItemVoteResult_t r = await SteamCall.Await<SetUserItemVoteResult_t>(SteamUGC.SetUserItemVote(new PublishedFileId_t(itemId), true));
            return r.m_eResult == EResult.k_EResultOK;
        }

        private static string ReviewBoard(ulong itemId) { return "mds_" + RecordBoards.WorkshopKey(itemId) + "_review"; }
        private static string AttemptBoard(ulong itemId) { return "mds_" + RecordBoards.WorkshopKey(itemId) + "_attempt"; }

        public async UniTask<bool> SubmitReviewAsync(ulong itemId, Review review)
        {
            if (!IsAvailable || review == null || !review.IsValid) return false;
            if (review.Liked) await LikeAsync(itemId);
            SteamLeaderboard_t board = await SteamLeaderboardService.FindBoard(ReviewBoard(itemId));
            int score = (int)review.Difficulty * 100 + (review.PhraseId + 1);
            int[] details = { (review.Liked ? 1 : 0) | (review.Cleared ? 2 : 0) };
            LeaderboardScoreUploaded_t up = await SteamCall.Await<LeaderboardScoreUploaded_t>(
                SteamUserStats.UploadLeaderboardScore(board, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate, score, details, details.Length));
            return up.m_bSuccess != 0;
        }

        public async UniTask RecordAttemptAsync(ulong itemId)
        {
            if (!IsAvailable) return;
            SteamLeaderboard_t board = await SteamLeaderboardService.FindBoard(AttemptBoard(itemId));
            await SteamCall.Await<LeaderboardScoreUploaded_t>(
                SteamUserStats.UploadLeaderboardScore(board, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, 1, new int[0], 0));
        }

        public async UniTask<ReviewSummary> GetSummaryAsync(ulong itemId)
        {
            var s = new ReviewSummary();
            if (!IsAvailable) return s;
            SteamLeaderboard_t attempts = await SteamLeaderboardService.FindBoard(AttemptBoard(itemId));
            s.Attempts = SteamUserStats.GetLeaderboardEntryCount(attempts);
            SteamLeaderboard_t reviews = await SteamLeaderboardService.FindBoard(ReviewBoard(itemId));
            LeaderboardScoresDownloaded_t dl = await SteamCall.Await<LeaderboardScoresDownloaded_t>(
                SteamUserStats.DownloadLeaderboardEntries(reviews, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, 1, 1000));
            int[] details = new int[1];
            for (int i = 0; i < dl.m_cEntryCount; i++)
            {
                LeaderboardEntry_t e;
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(dl.m_hSteamLeaderboardEntries, i, out e, details, details.Length)) continue;
                var r = new Review
                {
                    Difficulty = (PerceivedDifficulty)Mathf.Clamp(e.m_nScore / 100, 0, 3),
                    PhraseId = (e.m_nScore % 100) - 1,
                    Liked = e.m_cDetails > 0 && (details[0] & 1) != 0,
                    Cleared = e.m_cDetails > 0 && (details[0] & 2) != 0,
                };
                if (r.Cleared) s.Clears++;
                s.Add(r);
            }
            // 클리어 수는 순위표(최소 데스) 인원이 더 정확하다 — 리뷰를 안 남긴 클리어도 포함
            SteamLeaderboard_t clears = await SteamLeaderboardService.FindBoard(RecordBoards.BoardName(RecordBoards.WorkshopKey(itemId), SteamPlatform.RecordPlatformOfThisBuild, RecordKind.Deaths));
            s.Clears = Mathf.Max(s.Clears, SteamUserStats.GetLeaderboardEntryCount(clears));
            List<WorkshopItem> items = await QueryDetails(new[] { new PublishedFileId_t(itemId) });
            if (items.Count > 0) s.Likes = (int)items[0].Likes;
            return s;
        }

        public async UniTask<List<WorkshopItem>> GetMyItemsAsync()
        {
            var list = new List<WorkshopItem>();
            if (!IsAvailable) return list;
            UGCQueryHandle_t q = SteamUGC.CreateQueryUserUGCRequest(SteamUser.GetSteamID().GetAccountID(), EUserUGCList.k_EUserUGCList_Published,
                EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items, EUserUGCListSortOrder.k_EUserUGCListSortOrder_CreationOrderDesc, App, App, 1);
            SteamUGC.SetReturnMetadata(q, true);
            SteamUGC.SetReturnKeyValueTags(q, true);
            try
            {
                SteamUGCQueryCompleted_t done = await SteamCall.Await<SteamUGCQueryCompleted_t>(SteamUGC.SendQueryUGCRequest(q));
                if (done.m_eResult != EResult.k_EResultOK) return list;
                for (uint i = 0; i < done.m_unNumResultsReturned; i++)
                {
                    WorkshopItem item = ReadResult(q, i);
                    if (item != null) list.Add(item);
                }
            }
            finally
            {
                SteamUGC.ReleaseQueryUGCRequest(q);
            }
            return list;
        }
    }

    // ───────────────────────────── 순위표 ─────────────────────────────

    /// <summary>
    /// Steam 순위표 (맵별 최소 데스 / 최단 시간, 오름차순, 개인 최고만). 이름은 RecordBoards 규칙 — 플랫폼별로 따로.
    /// 입력 기록은 Steam 클라우드 파일로 올려 AttachLeaderboardUGC로 붙인다 (다른 사람이 받아 재생 검증 가능).
    /// </summary>
    public sealed class SteamLeaderboardService : ILeaderboardService
    {
        private static readonly Dictionary<string, SteamLeaderboard_t> Cache = new Dictionary<string, SteamLeaderboard_t>();

        public RecordPlatform Platform { get { return SteamPlatform.RecordPlatformOfThisBuild; } }

        internal static async UniTask<SteamLeaderboard_t> FindBoard(string name)
        {
            SteamLeaderboard_t board;
            if (Cache.TryGetValue(name, out board)) return board;
            LeaderboardFindResult_t r = await SteamCall.Await<LeaderboardFindResult_t>(
                SteamUserStats.FindOrCreateLeaderboard(name, ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending, ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric));
            if (r.m_bLeaderboardFound == 0) throw new InvalidOperationException("순위표를 찾거나 만들 수 없습니다: " + name);
            Cache[name] = r.m_hSteamLeaderboard;
            return r.m_hSteamLeaderboard;
        }

        public async UniTask<bool> SubmitAsync(string mapKey, MapData map, InputRecording proof, int deaths, int ticks)
        {
            if (!SteamPlatform.IsReady || !LeaderboardGuard.Verify(map, proof, deaths, ticks)) return false;
            try
            {
                SteamLeaderboard_t deathsBoard = await FindBoard(RecordBoards.BoardName(mapKey, Platform, RecordKind.Deaths));
                SteamLeaderboard_t timeBoard = await FindBoard(RecordBoards.BoardName(mapKey, Platform, RecordKind.Time));
                LeaderboardScoreUploaded_t a = await SteamCall.Await<LeaderboardScoreUploaded_t>(SteamUserStats.UploadLeaderboardScore(
                    deathsBoard, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, deaths, new[] { ticks }, 1));
                LeaderboardScoreUploaded_t b = await SteamCall.Await<LeaderboardScoreUploaded_t>(SteamUserStats.UploadLeaderboardScore(
                    timeBoard, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, ticks, new[] { deaths }, 1));
                if (a.m_bScoreChanged != 0) await AttachProof(deathsBoard, mapKey + "_deaths", proof);
                if (b.m_bScoreChanged != 0) await AttachProof(timeBoard, mapKey + "_time", proof);
                return a.m_bSuccess != 0 && b.m_bSuccess != 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SteamLeaderboard] 제출 실패: " + e.Message);
                return false;
            }
        }

        private static async UniTask AttachProof(SteamLeaderboard_t board, string name, InputRecording proof)
        {
            byte[] data = proof.Serialize();
            string file = "replay_" + name + ".mdr";
            if (!SteamRemoteStorage.FileWrite(file, data, data.Length)) return;
            RemoteStorageFileShareResult_t share = await SteamCall.Await<RemoteStorageFileShareResult_t>(SteamRemoteStorage.FileShare(file));
            if (share.m_eResult != EResult.k_EResultOK) return;
            await SteamCall.Await<LeaderboardUGCSet_t>(SteamUserStats.AttachLeaderboardUGC(board, share.m_hFile));
        }

        public async UniTask<List<RecordEntry>> GetTopAsync(string mapKey, RecordKind kind, int count)
        {
            var list = new List<RecordEntry>();
            if (!SteamPlatform.IsReady) return list;
            SteamLeaderboard_t board = await FindBoard(RecordBoards.BoardName(mapKey, Platform, kind));
            LeaderboardScoresDownloaded_t dl = await SteamCall.Await<LeaderboardScoresDownloaded_t>(
                SteamUserStats.DownloadLeaderboardEntries(board, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, 1, count));
            ReadEntries(dl, list);
            return list;
        }

        public async UniTask<RecordEntry> GetMineAsync(string mapKey, RecordKind kind)
        {
            if (!SteamPlatform.IsReady) return null;
            SteamLeaderboard_t board = await FindBoard(RecordBoards.BoardName(mapKey, Platform, kind));
            LeaderboardScoresDownloaded_t dl = await SteamCall.Await<LeaderboardScoresDownloaded_t>(
                SteamUserStats.DownloadLeaderboardEntriesForUsers(board, new[] { SteamUser.GetSteamID() }, 1));
            var list = new List<RecordEntry>();
            ReadEntries(dl, list);
            return list.Count > 0 ? list[0] : null;
        }

        private static void ReadEntries(LeaderboardScoresDownloaded_t dl, List<RecordEntry> list)
        {
            int[] details = new int[1];
            for (int i = 0; i < dl.m_cEntryCount; i++)
            {
                LeaderboardEntry_t e;
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(dl.m_hSteamLeaderboardEntries, i, out e, details, details.Length)) continue;
                list.Add(new RecordEntry
                {
                    PlayerId = e.m_steamIDUser.m_SteamID.ToString(),
                    PlayerName = SteamFriends.GetFriendPersonaName(e.m_steamIDUser),
                    Score = e.m_nScore,
                    Detail = e.m_cDetails > 0 ? details[0] : 0,
                });
            }
        }
    }

    // ───────────────────────────── 로비 + 네트워킹 ─────────────────────────────

    /// <summary>
    /// Steam 네트워킹 메시지 전송 (SteamNetworkingMessages, 채널 0). 연결 관리 없이 SteamID로 바로 보낸다.
    /// 위치는 비신뢰(Unreliable), 나머지는 신뢰(Reliable) 전송.
    /// </summary>
    public sealed class SteamNetTransport : INetTransport
    {
        private const int CHANNEL = 0;
        private const int MAX_MESSAGES = 64;
        private readonly IntPtr[] _messages = new IntPtr[MAX_MESSAGES];
        private Callback<SteamNetworkingMessagesSessionRequest_t> _sessionRequest;

        public SteamNetTransport()
        {
            LocalId = SteamUser.GetSteamID().m_SteamID;
            // 같은 방 사람의 세션 요청만 받아 준다 (방 목록은 SteamLobbyService가 앎)
            _sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
        }

        public ulong LocalId { get; private set; }
        public Func<ulong, bool> IsRoomMember;

        private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t req)
        {
            SteamNetworkingIdentity id = req.m_identityRemote;
            ulong who = id.GetSteamID().m_SteamID;
            if (IsRoomMember == null || IsRoomMember(who)) SteamNetworkingMessages.AcceptSessionWithUser(ref id);
        }

        public void Send(ulong peerId, byte[] data) { SendInternal(peerId, data, Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession); }

        public void SendUnreliable(ulong peerId, byte[] data) { SendInternal(peerId, data, Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession); }

        private static void SendInternal(ulong peerId, byte[] data, int flags)
        {
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(peerId));
            GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                SteamNetworkingMessages.SendMessageToUser(ref identity, pin.AddrOfPinnedObject(), (uint)data.Length, flags, CHANNEL);
            }
            finally
            {
                pin.Free();
            }
        }

        public void Poll(Action<ulong, byte[]> onReceive)
        {
            int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(CHANNEL, _messages, MAX_MESSAGES);
            for (int i = 0; i < n; i++)
            {
                SteamNetworkingMessage_t msg = SteamNetworkingMessage_t.FromIntPtr(_messages[i]);
                var data = new byte[msg.m_cbSize];
                Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
                ulong from = msg.m_identityPeer.GetSteamID().m_SteamID;
                SteamNetworkingMessage_t.Release(_messages[i]);
                onReceive(from, data);
            }
        }

        public void Close()
        {
            if (_sessionRequest != null) { _sessionRequest.Dispose(); _sessionRequest = null; }
        }
    }

    /// <summary>
    /// Steam 로비 (기획서 7.1). 방은 "코드 없이는 보이지 않는" 로비: 로비 데이터 code=방 코드로 만들고,
    /// 참가는 같은 code 필터로 RequestLobbyList → JoinLobby. 친구 초대는 오버레이 초대 창.
    /// 게임 데이터는 로비 채팅이 아니라 SteamNetTransport(방장 중계)로 주고받는다.
    /// </summary>
    public sealed class SteamLobbyService : ILobbyService
    {
        private const string KEY_CODE = "mds_code";
        private const string KEY_GAME = "mds_game";
        private CSteamID _lobby = CSteamID.Nil;
        private SteamNetTransport _transport;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;

        public SteamLobbyService()
        {
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        }

        public ulong LocalPlayerId { get { return SteamUser.GetSteamID().m_SteamID; } }
        public string LocalPlayerName { get { return SteamFriends.GetPersonaName(); } }
        public bool SupportsFriendInvite { get { return true; } }
        public event Action<LobbyResult> InviteAccepted;

        public INetTransport Transport { get { EnsureTransport(); return _transport; } }

        private void EnsureTransport()
        {
            if (_transport != null) return;
            _transport = new SteamNetTransport();
            _transport.IsRoomMember = IsMember;
        }

        private bool IsMember(ulong id)
        {
            if (_lobby == CSteamID.Nil) return false;
            int n = SteamMatchmaking.GetNumLobbyMembers(_lobby);
            for (int i = 0; i < n; i++) if (SteamMatchmaking.GetLobbyMemberByIndex(_lobby, i).m_SteamID == id) return true;
            return false;
        }

        public async UniTask<LobbyResult> CreateRoomAsync(string roomCode)
        {
            LobbyCreated_t created = await SteamCall.Await<LobbyCreated_t>(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, Rules.ROOM_MAX_PLAYERS));
            if (created.m_eResult != EResult.k_EResultOK) return new LobbyResult { Success = false, Error = "방 만들기 실패: " + created.m_eResult };
            _lobby = new CSteamID(created.m_ulSteamIDLobby);
            string code = Room.NormalizeCode(roomCode);
            SteamMatchmaking.SetLobbyData(_lobby, KEY_CODE, code);
            SteamMatchmaking.SetLobbyData(_lobby, KEY_GAME, "mds" + NetCodec.PROTOCOL_VERSION);
            EnsureTransport();
            return new LobbyResult { Success = true, RoomCode = code, HostId = LocalPlayerId, LobbyId = _lobby.m_SteamID };
        }

        public async UniTask<LobbyResult> JoinByCodeAsync(string roomCode)
        {
            string code = Room.NormalizeCode(roomCode);
            SteamMatchmaking.AddRequestLobbyListStringFilter(KEY_CODE, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(KEY_GAME, "mds" + NetCodec.PROTOCOL_VERSION, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
            LobbyMatchList_t found = await SteamCall.Await<LobbyMatchList_t>(SteamMatchmaking.RequestLobbyList());
            if (found.m_nLobbiesMatching == 0) return new LobbyResult { Success = false, Error = "방을 찾을 수 없습니다" };
            return await JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
        }

        private async UniTask<LobbyResult> JoinLobby(CSteamID lobby)
        {
            LobbyEnter_t enter = await SteamCall.Await<LobbyEnter_t>(SteamMatchmaking.JoinLobby(lobby));
            if (enter.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                return new LobbyResult { Success = false, Error = "입장 실패: " + (EChatRoomEnterResponse)enter.m_EChatRoomEnterResponse };
            _lobby = new CSteamID(enter.m_ulSteamIDLobby);
            EnsureTransport();
            return new LobbyResult
            {
                Success = true,
                RoomCode = SteamMatchmaking.GetLobbyData(_lobby, KEY_CODE),
                HostId = SteamMatchmaking.GetLobbyOwner(_lobby).m_SteamID,
                LobbyId = _lobby.m_SteamID,
            };
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t req)
        {
            JoinFromInvite(req.m_steamIDLobby).Forget();
        }

        private async UniTaskVoid JoinFromInvite(CSteamID lobby)
        {
            LobbyResult r = await JoinLobby(lobby);
            if (InviteAccepted != null) InviteAccepted(r);
        }

        public bool InviteFriends()
        {
            if (_lobby == CSteamID.Nil) return false;
            SteamFriends.ActivateGameOverlayInviteDialog(_lobby);
            return true;
        }

        public void LeaveRoom()
        {
            if (_lobby != CSteamID.Nil) SteamMatchmaking.LeaveLobby(_lobby);
            _lobby = CSteamID.Nil;
            if (_transport != null) { _transport.Close(); _transport = null; }
        }

        public void Update() { }
    }
}
#endif
