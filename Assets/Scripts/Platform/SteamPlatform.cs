// Steam(PC) 플랫폼 — IPlatform 구현. STEAMWORKS_NET이 정의됐을 때만 컴파일된다 (MDSSteamServices.cs 머리말 참고).
// ⚠ Unity/Steamworks 없이 mcs 문법 검사만 거침 — 실제 빌드에서 확인 필요.
#if STEAMWORKS_NET
using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Seunghak.Game.MillionDeathSquare;
using Steamworks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// Steam 플랫폼. SteamAPI 초기화/콜백 펌프, 플레이어 ID·이름, Steam 클라우드 저장소(PlatformSaveSync 원본).
    /// 광고·인앱결제 없음 (유료 판매, 기획서 9장). 순위표·창작마당·로비는 MDSServices가 Steam 구현으로 연결한다.
    /// </summary>
    public sealed class SteamPlatform : MonoBehaviour, IPlatform
    {
        /// <summary>Steam 앱 ID (MDSSteamAppId — TODO: 실제 ID)</summary>
        public const uint AppId = MDSSteamAppId.Value;

        public static bool IsReady { get; private set; }

        /// <summary>이 빌드가 올리는 기록의 플랫폼 (Steam = PC)</summary>
        public static RecordPlatform RecordPlatformOfThisBuild { get { return RecordPlatform.PC; } }

        private SteamCloudStorage _storage;

        /// <summary>Steam 초기화를 시도해 성공하면 owner에 붙여 반환. 실패하면 null (DefaultPlatform으로 폴백).</summary>
        public static SteamPlatform TryCreate(GameObject owner)
        {
#if !UNITY_EDITOR
            try
            {
                // Steam 클라이언트 밖에서 실행하면 Steam을 통해 다시 실행한다 (출시 빌드)
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
                {
                    Application.Quit();
                    return null;
                }
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError("[SteamPlatform] steam_api 라이브러리를 찾을 수 없습니다: " + e.Message);
                return null;
            }
#endif
            bool ok;
            try { ok = SteamAPI.Init(); }
            catch (Exception e) { Debug.LogWarning("[SteamPlatform] SteamAPI.Init 예외: " + e.Message); ok = false; }
            if (!ok)
            {
                Debug.LogWarning("[SteamPlatform] Steam 초기화 실패 — Steam 클라이언트 실행/steam_appid.txt 확인. 오프라인 모드로 진행합니다.");
                return null;
            }
            IsReady = true;
            var p = owner.AddComponent<SteamPlatform>();
            p._storage = new SteamCloudStorage();
            return p;
        }

        private void Update()
        {
            if (IsReady) SteamAPI.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            if (!IsReady) return;
            IsReady = false;
            SteamAPI.Shutdown();
        }

        public UniTask InitAsync() { return UniTask.CompletedTask; }
        public string GetPlayerId() { return IsReady ? SteamUser.GetSteamID().m_SteamID.ToString() : "steam_offline"; }
        public string GetPlayerName() { return IsReady ? SteamFriends.GetPersonaName() : "Player"; }
        public void OnGameStart() { }
        public void OnGameEnd(int score, int bestScore) { }
        /// <summary>점수 하나짜리 전역 순위표는 쓰지 않는다 — 맵별 순위표는 ILeaderboardService</summary>
        public UniTask<bool> SubmitScoreAsync(int score) { return UniTask.FromResult(false); }
        public UniTask<List<FBLeaderboardEntry>> GetFriendScoresAsync(int count = 10) { return UniTask.FromResult(new List<FBLeaderboardEntry>()); }
        public UniTask<bool> ShareResultAsync(int score, string imageUrl = "") { return UniTask.FromResult(false); }
        public void RequestBack() { }
        public void Vibrate(int type = 0) { }
        public void SetLoadingProgress(float percent) { }
        public void LogEvent(string eventName, float value = 0f, string paramsJson = "")
        {
            // Steam에는 분석 이벤트 API가 없다 — 개발 빌드에서만 로그 (출시 후 서버/분석 도입 시 교체)
            if (Debug.isDebugBuild) Debug.Log("[Analytics] " + eventName + " " + value + " " + paramsJson);
        }
        public void GenerateHaptic(string hapticType) { }
        public IPlatformStorage Storage { get { return _storage != null ? (IPlatformStorage)_storage : PlayerPrefsStorage.Instance; } }
        public IPlatformAds Ads { get { return NullAds.Instance; } }
        public IPlatformIAP IAP { get { return NullIAP.Instance; } }
        public bool SupportsLeaderboardUI { get { return false; } }
        public UniTask<bool> OpenLeaderboardAsync() { return UniTask.FromResult(false); }
        public bool SupportsInvite { get { return IsReady; } }
        public UniTask<int> InviteFriendsAsync()
        {
            if (IsReady) SteamFriends.ActivateGameOverlay("Friends");
            return UniTask.FromResult(0);
        }
        public SafeAreaInset SafeAreaInset { get { return SafeAreaInset.Zero; } }
    }

    /// <summary>Steam 클라우드(ISteamRemoteStorage) 키-값 저장소 — 키마다 파일 하나 ("mds_save_{키}.txt")</summary>
    public sealed class SteamCloudStorage : IPlatformStorage
    {
        private static string FileOf(string key) { return "mds_save_" + key + ".txt"; }

        public UniTask<string> GetItemAsync(string key)
        {
            string v;
            return UniTask.FromResult(TryRead(key, out v) ? v : null);
        }

        public UniTask<(bool ok, string value)> TryGetItemAsync(string key)
        {
            if (!SteamPlatform.IsReady) return UniTask.FromResult((false, (string)null));
            string v;
            bool exists = TryRead(key, out v);
            return UniTask.FromResult((true, exists ? v : null));
        }

        private static bool TryRead(string key, out string value)
        {
            value = null;
            if (!SteamPlatform.IsReady) return false;
            string file = FileOf(key);
            if (!SteamRemoteStorage.FileExists(file)) return false;
            int size = SteamRemoteStorage.GetFileSize(file);
            var data = new byte[size];
            int read = SteamRemoteStorage.FileRead(file, data, size);
            value = Encoding.UTF8.GetString(data, 0, read);
            return true;
        }

        public UniTask<bool> SetItemAsync(string key, string value)
        {
            if (!SteamPlatform.IsReady) return UniTask.FromResult(false);
            byte[] data = Encoding.UTF8.GetBytes(value ?? "");
            return UniTask.FromResult(SteamRemoteStorage.FileWrite(FileOf(key), data, data.Length));
        }

        public UniTask<bool> RemoveItemAsync(string key)
        {
            if (!SteamPlatform.IsReady) return UniTask.FromResult(false);
            string file = FileOf(key);
            return UniTask.FromResult(!SteamRemoteStorage.FileExists(file) || SteamRemoteStorage.FileDelete(file));
        }
    }
}
#endif
