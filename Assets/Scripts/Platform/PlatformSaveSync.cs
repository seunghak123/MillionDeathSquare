using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Seunghak.Common;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// PlayerPrefs(동기 로컬 캐시) ↔ 플랫폼 Storage(원본) 동기화 계층.
    ///
    /// 유저 진행 데이터 키 전체를 JSON 스냅샷 하나로 묶어 플랫폼 Storage의 단일 키에 저장합니다.
    /// 게임 코드는 지금처럼 PlayerPrefs만 읽고 쓰고, 값을 바꾼 곳에서 MarkDirty()만 부르면 됩니다.
    ///
    ///   - 복원: 로그인 직전 RestoreAsync() — 원격 스냅샷이 있으면 원격 우선,
    ///           로컬 rev가 더 크면(푸시 못 하고 종료된 경우) 로컬 유지 후 푸시, 원격이 없으면 로컬 푸시
    ///   - 저장: MarkDirty() → 디바운스 후 푸시. 게임오버 / 백그라운드 전환 시 FlushNow()로 즉시 푸시
    ///   - PlayerPrefsStorage를 쓰는 플랫폼(에디터 등)에서는 아무것도 하지 않습니다.
    ///
    /// 새 플랫폼은 IPlatform.Storage만 구현하면 이 계층을 그대로 씁니다.
    /// </summary>
    public static class PlatformSaveSync
    {
        /// <summary>플랫폼 Storage에 스냅샷을 저장하는 단일 키.</summary>
        private const string SnapshotKey = "MillionDeathSquareSaveSnapshot";

        /// <summary>로컬 변경 카운터 (PlayerPrefs). 스냅샷의 rev와 비교해 어느 쪽이 최신인지 판단.</summary>
        private const string LocalRevKey = "PlatformSaveRev";

        private const int SnapshotVersion = 1;
        private const int DebounceMs = 1500;

        // 스냅샷에 포함하는 PlayerPrefs 키 (문자열로 저장된 것)
        private static readonly string[] StringKeys =
        {
            // CommonUtil.SavePlayerPref 경유 (LocalDataBase / UserDataManager)
            nameof(PlayerPrefKey.UserInfoData),
            nameof(PlayerPrefKey.UserOptionData),
            nameof(PlayerPrefKey.UserItemDatas),
            nameof(PlayerPrefKey.UserStoryDatas),
            nameof(PlayerPrefKey.UserLobbyDatas),
            nameof(PlayerPrefKey.BestScore),
            nameof(PlayerPrefKey.Gold),
            nameof(PlayerPrefKey.PlayCount),
            // CharacterManager.PREF_KEY_OWNED
            "OwnedCharacters",
            // CharacterShopWindow.DAILY_GIFT_KEY / AD_REWARD_KEY
            "DailyGiftDate",
            "AdRewardTime",
            // 인앱결제 지급 완료 기록 (중복 지급 방지)
            TossIAP.GrantedOrdersKey,
            // 백만 번 죽는 네모: 캠페인 진행·기록 (MDSSaveStore), 에디터 작업 중인 맵, 플레이한 맵 목록
            "MDSCampaignSave",
            "MDSEditorDraft",
            "MDSPlayedMaps",
        };

        // 스냅샷에 포함하는 PlayerPrefs 키 (SetInt로 저장된 것)
        private static readonly string[] IntKeys =
        {
            // CharacterManager.PREF_KEY_SELECTED
            "SelectedCharacter",
            // 백만 번 죽는 네모: 설정 (MDSSettings)
            "MDSShakePercent",
            "MDSRumbleEnabled",
            "MDSShowGhosts",
        };

        [Serializable]
        private class Snapshot
        {
            public int version = SnapshotVersion;
            /// <summary>증가 카운터. 큰 쪽이 최신.</summary>
            public int rev;
            /// <summary>저장 시각 (UTC unix ms). 진단용.</summary>
            public long savedAt;
            public List<string> keys = new List<string>();
            public List<string> values = new List<string>();
        }

        // 푸시할 때마다 재사용 (리스트 재할당 방지)
        private static readonly Snapshot _pushBuffer = new Snapshot();

        private static IPlatformStorage _storage;
        private static bool _enabled;
        private static int _rev;
        private static bool _dirty;
        private static bool _debouncing;
        private static bool _pushing;
        /// <summary>복원 때 원격을 읽지 못해 이번 세션 푸시를 막은 상태.</summary>
        private static bool _pushBlocked;

        /// <summary>
        /// 플레이 중 여부 (인게임 씬 컨트롤러가 게임 상태에 맞춰 설정).
        /// true인 동안에는 디바운스 푸시를 미뤄 플레이 프레임에 할당이 생기지 않게 합니다.
        /// </summary>
        public static bool GameplayActive { get; set; }

        /// <summary>
        /// 플랫폼 Storage에서 스냅샷을 읽어 PlayerPrefs에 반영합니다.
        /// PlatformFactory.Current.InitAsync() 이후, 로그인 전에 한 번 호출하세요.
        /// </summary>
        /// <returns>원격 스냅샷을 PlayerPrefs에 덮어썼으면 true — 호출부가 인메모리 상태를 다시 로드해야 합니다.</returns>
        public static async UniTask<bool> RestoreAsync()
        {
            _enabled = false;
            _storage = PlatformFactory.Current.Storage;

            // PlayerPrefs가 곧 원본인 플랫폼은 스냅샷 왕복이 무의미
            if (_storage == null || _storage is PlayerPrefsStorage)
                return false;

            _pushBlocked = false;
            _rev = PlayerPrefs.GetInt(LocalRevKey, 0);

            // 읽기 실패는 한 번 더 시도한다.
            var (ok, json) = await _storage.TryGetItemAsync(SnapshotKey);
            if (!ok)
                (ok, json) = await _storage.TryGetItemAsync(SnapshotKey);

            if (!ok)
            {
                // 원격 상태를 알 수 없다 — 이번 세션에는 절대 푸시하지 않는다 (원격 데이터 덮어쓰기 방지).
                // 로컬 캐시가 이전에 동기화된 적이 있으면(rev > 0) rev만 올려 두어, 다음 실행에서
                // 로컬이 최신으로 판정돼 이번 세션 진행분이 원격에 반영되게 한다.
                // 로컬이 빈 상태(rev 0)면 rev도 올리지 않아 다음 실행에서 원격이 그대로 복원된다.
                _pushBlocked = true;
                _enabled = _rev > 0;
                Debug.LogWarning($"[PlatformSaveSync] 원격 스냅샷 읽기 실패 → 이번 세션 푸시 중단 (localRev={_rev})");
                return false;
            }

            Snapshot remote = Parse(json);

            bool applied = false;
            if (remote == null)
            {
                // 첫 실행: 로컬을 원본으로 올린다
                Debug.Log("[PlatformSaveSync] 원격 스냅샷 없음 → 로컬 푸시");
                _dirty = true;
            }
            else if (_rev > remote.rev)
            {
                // 지난 세션에서 푸시하지 못한 변경이 로컬에 남아 있음
                Debug.Log($"[PlatformSaveSync] 로컬이 최신 (local={_rev}, remote={remote.rev}) → 로컬 유지 후 푸시");
                _dirty = true;
            }
            else
            {
                Apply(remote);
                _rev = remote.rev;
                PlayerPrefs.SetInt(LocalRevKey, _rev);
                PlayerPrefs.Save();
                applied = true;
                Debug.Log($"[PlatformSaveSync] 원격 스냅샷 복원 (rev={remote.rev})");
            }

            _enabled = true;
            if (_dirty)
                PushAsync().Forget();
            return applied;
        }

        /// <summary>
        /// 스냅샷 대상 PlayerPrefs 값을 바꾼 직후 호출합니다 (PlayerPrefs.Save() 전에 불러도 됩니다).
        /// 복원 전이거나 동기화가 필요 없는 플랫폼이면 무시됩니다.
        /// </summary>
        public static void MarkDirty()
        {
            if (!_enabled) return;

            // 푸시 전에 앱이 종료돼도 다음 실행에서 로컬이 최신임을 알 수 있게 rev를 바로 기록
            _rev++;
            PlayerPrefs.SetInt(LocalRevKey, _rev);
            _dirty = true;

            if (_debouncing) return;
            _debouncing = true;
            DebouncedPushAsync().Forget();
        }

        /// <summary>변경분이 있으면 디바운스를 기다리지 않고 즉시 푸시합니다 (게임오버 / 백그라운드 전환).</summary>
        public static void FlushNow()
        {
            if (!_enabled || !_dirty) return;
            PushAsync().Forget();
        }

        private static async UniTaskVoid DebouncedPushAsync()
        {
            await UniTask.Delay(DebounceMs, ignoreTimeScale: true);
            _debouncing = false;

            // 플레이 중에는 푸시(JSON 직렬화 + 브릿지 호출)를 미룬다.
            // 게임오버 / 백그라운드 전환 시 FlushNow()가 처리한다.
            if (GameplayActive) return;

            if (_dirty)
                PushAsync().Forget();
        }

        private static async UniTaskVoid PushAsync()
        {
            // 진행 중인 푸시가 끝나면서 _dirty를 다시 확인하므로 겹쳐 보내지 않는다
            if (_pushing || _pushBlocked) return;
            _pushing = true;
            try
            {
                while (_dirty)
                {
                    _dirty = false;
                    // 원격 rev는 항상 1 이상 — 캐시가 지워진 기기(rev 0)가 원격을 이기지 못하게 한다
                    if (_rev < 1)
                    {
                        _rev = 1;
                        PlayerPrefs.SetInt(LocalRevKey, _rev);
                        PlayerPrefs.Save();
                    }

                    bool ok = await _storage.SetItemAsync(SnapshotKey, BuildJson());
                    if (!ok)
                    {
                        // 실패분은 다음 MarkDirty / FlushNow 때 다시 시도
                        _dirty = true;
                        Debug.LogWarning("[PlatformSaveSync] 스냅샷 푸시 실패");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _dirty = true;
                Debug.LogWarning($"[PlatformSaveSync] 스냅샷 푸시 오류: {e.Message}");
            }
            finally
            {
                _pushing = false;
            }
        }

        private static string BuildJson()
        {
            _pushBuffer.rev = _rev;
            _pushBuffer.savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _pushBuffer.keys.Clear();
            _pushBuffer.values.Clear();

            for (int i = 0; i < StringKeys.Length; i++)
            {
                if (!PlayerPrefs.HasKey(StringKeys[i])) continue;
                _pushBuffer.keys.Add(StringKeys[i]);
                _pushBuffer.values.Add(PlayerPrefs.GetString(StringKeys[i]));
            }
            for (int i = 0; i < IntKeys.Length; i++)
            {
                if (!PlayerPrefs.HasKey(IntKeys[i])) continue;
                _pushBuffer.keys.Add(IntKeys[i]);
                _pushBuffer.values.Add(PlayerPrefs.GetInt(IntKeys[i]).ToString());
            }
            return JsonUtility.ToJson(_pushBuffer);
        }

        private static Snapshot Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var snapshot = JsonUtility.FromJson<Snapshot>(json);
                if (snapshot == null || snapshot.keys == null || snapshot.values == null) return null;
                if (snapshot.keys.Count != snapshot.values.Count) return null;
                return snapshot;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlatformSaveSync] 스냅샷 파싱 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 스냅샷을 PlayerPrefs에 그대로 반영. 스냅샷은 "전체 상태"이므로
        /// 스냅샷에 없는 대상 키는 로컬에서도 지운다.
        /// </summary>
        private static void Apply(Snapshot snapshot)
        {
            for (int i = 0; i < StringKeys.Length; i++)
            {
                int index = snapshot.keys.IndexOf(StringKeys[i]);
                if (index >= 0)
                    PlayerPrefs.SetString(StringKeys[i], snapshot.values[index]);
                else
                    PlayerPrefs.DeleteKey(StringKeys[i]);
            }
            for (int i = 0; i < IntKeys.Length; i++)
            {
                int index = snapshot.keys.IndexOf(IntKeys[i]);
                if (index >= 0 && int.TryParse(snapshot.values[index], out int value))
                    PlayerPrefs.SetInt(IntKeys[i], value);
                else
                    PlayerPrefs.DeleteKey(IntKeys[i]);
            }
        }
    }
}
