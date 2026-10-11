using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Seunghak.Game.MillionDeathSquare;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// 오프라인/에디터용 창작마당 — 이 기기의 PlayerPrefs에 저장 (키 MDSLocalWS:*).
    /// 맵 코드 = 항목 번호(1000부터)의 MapCode. Steam 없이 업로드 → 맵 코드 → 플레이 → 후기 흐름 전체를 시험할 수 있다.
    /// </summary>
    public sealed class LocalWorkshopService : IWorkshopService
    {
        private const string PREFIX = "MDSLocalWS:";
        private const ulong FIRST_ID = 1000;
        private readonly string _localPlayerId;
        private readonly string _localPlayerName;

        public LocalWorkshopService(string localPlayerId, string localPlayerName)
        {
            _localPlayerId = localPlayerId ?? "local";
            _localPlayerName = localPlayerName ?? "Local";
        }

        public string ServiceName { get { return "로컬 창작마당 (오프라인)"; } }
        public bool IsAvailable { get { return true; } }

        private static List<ulong> LoadIndex()
        {
            var list = new List<ulong>();
            foreach (string s in PlayerPrefs.GetString(PREFIX + "index", "").Split(','))
            {
                ulong id;
                if (ulong.TryParse(s, out id)) list.Add(id);
            }
            return list;
        }

        private static void SaveIndex(List<ulong> ids)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ids.Count; i++) { if (i > 0) sb.Append(','); sb.Append(ids[i]); }
            PlayerPrefs.SetString(PREFIX + "index", sb.ToString());
        }

        public UniTask<WorkshopUploadResult> UploadAsync(WorkshopUploadRequest request)
        {
            var result = new WorkshopUploadResult();
            if (request == null || request.Map == null) { result.Error = "맵이 없습니다"; return UniTask.FromResult(result); }
            ValidationResult v = MapValidator.Validate(request.Map);
            if (!v.IsValid) { result.Error = v.ToString(); return UniTask.FromResult(result); }
            // 직접 깬 기록인지 다시 확인 (기획서 5.4)
            if (!LeaderboardGuard.Verify(request.Map, request.CreatorRecording, request.CreatorDeaths, request.CreatorTicks))
            {
                result.Error = "직접 클리어한 기록이 확인되지 않았습니다";
                return UniTask.FromResult(result);
            }
            List<ulong> ids = LoadIndex();
            int mine = 0;
            foreach (ulong existing in ids) if (PlayerPrefs.GetString(PREFIX + existing + ":creator", "") == _localPlayerId) mine++;
            if (request.UpdateItemId == 0 && mine >= Rules.MAX_UPLOADS_PER_USER) { result.Error = "업로드는 1인당 " + Rules.MAX_UPLOADS_PER_USER + "개까지입니다"; return UniTask.FromResult(result); }

            ulong id = request.UpdateItemId;
            if (id == 0)
            {
                id = FIRST_ID;
                foreach (ulong existing in ids) if (existing >= id) id = existing + 1;
                ids.Add(id);
                SaveIndex(ids);
            }
            MapData map = request.Map.Clone();
            if (!string.IsNullOrEmpty(request.Title)) map.Name = request.Title;
            PlayerPrefs.SetString(PREFIX + id + ":code", MapSerializer.ToShareCode(map));
            PlayerPrefs.SetString(PREFIX + id + ":creator", _localPlayerId);
            PlayerPrefs.SetString(PREFIX + id + ":creatorName", _localPlayerName);
            PlayerPrefs.SetInt(PREFIX + id + ":deaths", request.CreatorDeaths);
            PlayerPrefs.SetInt(PREFIX + id + ":ticks", request.CreatorTicks);
            PlayerPrefs.SetInt(PREFIX + id + ":vis", (int)request.Visibility);
            PlayerPrefs.Save();

            result.Success = true;
            result.ItemId = id;
            result.MapCode = MapCode.Encode(id);
            result.AppliedVisibility = request.Visibility;
            return UniTask.FromResult(result);
        }

        public UniTask<WorkshopItem> ResolveMapCodeAsync(string mapCode)
        {
            ulong id;
            if (!MapCode.TryDecode(mapCode, out id)) return UniTask.FromResult<WorkshopItem>(null);
            return UniTask.FromResult(Load(id));
        }

        private WorkshopItem Load(ulong id)
        {
            string code = PlayerPrefs.GetString(PREFIX + id + ":code", "");
            MapData map;
            string error;
            if (string.IsNullOrEmpty(code) || !MapSerializer.TryParseShareCode(code, out map, out error)) return null;
            ulong creator;
            ulong.TryParse(PlayerPrefs.GetString(PREFIX + id + ":creator", "0"), out creator);
            return new WorkshopItem
            {
                ItemId = id,
                MapCode = MapCode.Encode(id),
                Title = map.Name,
                Map = map,
                CreatorId = creator,
                CreatorName = PlayerPrefs.GetString(PREFIX + id + ":creatorName", ""),
                CreatorDeaths = PlayerPrefs.GetInt(PREFIX + id + ":deaths", -1),
                CreatorTicks = PlayerPrefs.GetInt(PREFIX + id + ":ticks", -1),
                Likes = PlayerPrefs.GetInt(PREFIX + id + ":likes", 0),
                Visibility = (WorkshopVisibility)PlayerPrefs.GetInt(PREFIX + id + ":vis", 0),
            };
        }

        public UniTask<bool> SubscribeAsync(ulong itemId) { return UniTask.FromResult(Load(itemId) != null); }

        public UniTask<bool> LikeAsync(ulong itemId)
        {
            string likedKey = PREFIX + itemId + ":liked:" + _localPlayerId;
            if (PlayerPrefs.GetInt(likedKey, 0) == 1) return UniTask.FromResult(true);
            PlayerPrefs.SetInt(likedKey, 1);
            PlayerPrefs.SetInt(PREFIX + itemId + ":likes", PlayerPrefs.GetInt(PREFIX + itemId + ":likes", 0) + 1);
            return UniTask.FromResult(true);
        }

        public UniTask<bool> SubmitReviewAsync(ulong itemId, Review review)
        {
            if (review == null || !review.IsValid) return UniTask.FromResult(false);
            // 한 사람당 하나 (덮어쓰기)
            PlayerPrefs.SetString(PREFIX + itemId + ":review:" + _localPlayerId, review.Encode());
            string who = PlayerPrefs.GetString(PREFIX + itemId + ":reviewers", "");
            if (("," + who + ",").IndexOf("," + _localPlayerId + ",", StringComparison.Ordinal) < 0)
                PlayerPrefs.SetString(PREFIX + itemId + ":reviewers", who.Length == 0 ? _localPlayerId : who + "," + _localPlayerId);
            if (review.Liked) LikeAsync(itemId).Forget();
            if (review.Cleared) PlayerPrefs.SetInt(PREFIX + itemId + ":clears", PlayerPrefs.GetInt(PREFIX + itemId + ":clears", 0) + 1);
            return UniTask.FromResult(true);
        }

        public UniTask RecordAttemptAsync(ulong itemId)
        {
            PlayerPrefs.SetInt(PREFIX + itemId + ":attempts", PlayerPrefs.GetInt(PREFIX + itemId + ":attempts", 0) + 1);
            return UniTask.CompletedTask;
        }

        public UniTask<ReviewSummary> GetSummaryAsync(ulong itemId)
        {
            var s = new ReviewSummary();
            s.Attempts = PlayerPrefs.GetInt(PREFIX + itemId + ":attempts", 0);
            s.Clears = PlayerPrefs.GetInt(PREFIX + itemId + ":clears", 0);
            foreach (string who in PlayerPrefs.GetString(PREFIX + itemId + ":reviewers", "").Split(','))
            {
                if (who.Length == 0) continue;
                Review r;
                if (Review.TryDecode(PlayerPrefs.GetString(PREFIX + itemId + ":review:" + who, ""), out r)) s.Add(r);
            }
            s.Likes = PlayerPrefs.GetInt(PREFIX + itemId + ":likes", 0);
            return UniTask.FromResult(s);
        }

        public UniTask<List<WorkshopItem>> GetMyItemsAsync()
        {
            var list = new List<WorkshopItem>();
            foreach (ulong id in LoadIndex())
            {
                if (PlayerPrefs.GetString(PREFIX + id + ":creator", "") != _localPlayerId) continue;
                WorkshopItem item = Load(id);
                if (item != null) list.Add(item);
            }
            return UniTask.FromResult(list);
        }
    }

    /// <summary>오프라인/에디터용 순위표 — 맵별·플랫폼별 RecordTable을 PlayerPrefs에 (키 MDSLocalLB:*)</summary>
    public sealed class LocalLeaderboardService : ILeaderboardService
    {
        private const string PREFIX = "MDSLocalLB:";
        private readonly string _playerId;
        private readonly string _playerName;

        public LocalLeaderboardService(RecordPlatform platform, string playerId, string playerName)
        {
            Platform = platform;
            _playerId = playerId ?? "local";
            _playerName = playerName ?? "Local";
        }

        public RecordPlatform Platform { get; private set; }

        private RecordTable Load(string board) { return RecordTable.Parse(PlayerPrefs.GetString(PREFIX + board, ""), 100); }

        public UniTask<bool> SubmitAsync(string mapKey, MapData map, InputRecording proof, int deaths, int ticks)
        {
            if (!LeaderboardGuard.Verify(map, proof, deaths, ticks)) return UniTask.FromResult(false);
            string deathsBoard = RecordBoards.BoardName(mapKey, Platform, RecordKind.Deaths);
            string timeBoard = RecordBoards.BoardName(mapKey, Platform, RecordKind.Time);
            RecordTable d = Load(deathsBoard);
            RecordTable t = Load(timeBoard);
            d.Submit(_playerId, _playerName, deaths, ticks);
            t.Submit(_playerId, _playerName, ticks, deaths);
            PlayerPrefs.SetString(PREFIX + deathsBoard, d.Serialize());
            PlayerPrefs.SetString(PREFIX + timeBoard, t.Serialize());
            return UniTask.FromResult(true);
        }

        public UniTask<List<RecordEntry>> GetTopAsync(string mapKey, RecordKind kind, int count)
        {
            RecordTable table = Load(RecordBoards.BoardName(mapKey, Platform, kind));
            var list = new List<RecordEntry>();
            for (int i = 0; i < table.Count && i < count; i++) list.Add(table[i]);
            return UniTask.FromResult(list);
        }

        public UniTask<RecordEntry> GetMineAsync(string mapKey, RecordKind kind)
        {
            RecordTable table = Load(RecordBoards.BoardName(mapKey, Platform, kind));
            int i = table.IndexOf(_playerId);
            return UniTask.FromResult(i >= 0 ? table[i] : null);
        }
    }

    /// <summary>
    /// 같은 프로세스 안 전송 (에디터 시험용). 피어마다 받은 편지함 큐. 메인 스레드 전용.
    /// </summary>
    public sealed class LoopbackTransport : INetTransport
    {
        private static readonly Dictionary<ulong, Queue<KeyValuePair<ulong, byte[]>>> Inboxes = new Dictionary<ulong, Queue<KeyValuePair<ulong, byte[]>>>();

        public LoopbackTransport(ulong localId)
        {
            LocalId = localId;
            Inboxes[localId] = new Queue<KeyValuePair<ulong, byte[]>>();
        }

        public ulong LocalId { get; private set; }

        public void Send(ulong peerId, byte[] data)
        {
            Queue<KeyValuePair<ulong, byte[]>> q;
            if (Inboxes.TryGetValue(peerId, out q)) q.Enqueue(new KeyValuePair<ulong, byte[]>(LocalId, data));
        }

        public void SendUnreliable(ulong peerId, byte[] data) { Send(peerId, data); }

        public void Poll(Action<ulong, byte[]> onReceive)
        {
            Queue<KeyValuePair<ulong, byte[]>> q;
            if (!Inboxes.TryGetValue(LocalId, out q)) return;
            while (q.Count > 0)
            {
                KeyValuePair<ulong, byte[]> m = q.Dequeue();
                onReceive(m.Key, m.Value);
            }
        }

        public void Close() { Inboxes.Remove(LocalId); }
    }

    /// <summary>
    /// 오프라인/에디터용 로비 — 이 프로세스 안에서만 방을 만든다. 방 코드 → 방장 ID 표.
    /// 혼자서도 시험할 수 있게 디버그 봇(MDSRoomBot)이 같은 방에 들어올 수 있다.
    /// </summary>
    public sealed class LocalLobbyService : ILobbyService
    {
        private static readonly Dictionary<string, ulong> Rooms = new Dictionary<string, ulong>();
        private LoopbackTransport _transport;
        private string _code;

        public LocalLobbyService(ulong localId, string name)
        {
            LocalPlayerId = localId;
            LocalPlayerName = name;
        }

        public ulong LocalPlayerId { get; private set; }
        public string LocalPlayerName { get; private set; }
        public INetTransport Transport { get { EnsureTransport(); return _transport; } }

        private void EnsureTransport()
        {
            if (_transport == null) _transport = new LoopbackTransport(LocalPlayerId);
        }
        public bool SupportsFriendInvite { get { return false; } }
        public event Action<LobbyResult> InviteAccepted;

        public static bool TryFindHost(string code, out ulong hostId) { return Rooms.TryGetValue(Room.NormalizeCode(code), out hostId); }

        public UniTask<LobbyResult> CreateRoomAsync(string roomCode)
        {
            _code = Room.NormalizeCode(roomCode);
            Rooms[_code] = LocalPlayerId;
            EnsureTransport();
            return UniTask.FromResult(new LobbyResult { Success = true, RoomCode = _code, HostId = LocalPlayerId });
        }

        public UniTask<LobbyResult> JoinByCodeAsync(string roomCode)
        {
            ulong host;
            string code = Room.NormalizeCode(roomCode);
            if (!Rooms.TryGetValue(code, out host)) return UniTask.FromResult(new LobbyResult { Success = false, Error = "방을 찾을 수 없습니다" });
            _code = code;
            EnsureTransport();
            return UniTask.FromResult(new LobbyResult { Success = true, RoomCode = code, HostId = host });
        }

        public bool InviteFriends() { return false; }

        public void LeaveRoom()
        {
            if (_code != null)
            {
                ulong host;
                if (Rooms.TryGetValue(_code, out host) && host == LocalPlayerId) Rooms.Remove(_code);
            }
            _code = null;
            if (_transport != null) { _transport.Close(); _transport = null; }
        }

        public void Update()
        {
            if (InviteAccepted == null) return; // 로컬에는 초대가 없다 (이벤트 미사용 경고 방지)
        }
    }
}
