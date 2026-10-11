using System;
using UnityEngine;
using Seunghak.Common;
using Seunghak.Platform;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 판 밖에서 유지되는 진행 데이터 (기획서 8장 로비 장착, 9장 모드 해금, 10장 코인).
    ///  - 코인 = UserDataManager Gold (공용 재화)
    ///  - 일반 모드 최고점 = UserDataManager BestScore, 지각 변동 최고점 = 별도 키
    ///  - 로비 장착 슬롯 2칸 (판 시작 시 인벤토리로 옮겨지고 비워진다)
    ///  - 로비 광고 스킬 하루 10회
    /// 모든 키는 PlatformSaveSync 스냅샷에 포함된다.
    /// 지각 변동 최고점은 순위표가 없으므로 결과 화면에 크게 표시한다. 자체 서버가 없어 토스 저장소(기기)에만 저장된다.
    /// </summary>
    public static class SuikaProgress
    {
        public const string PREF_TECTONIC_BEST = "FossilTectonicBest";
        public const string PREF_TECTONIC_UNLOCKED = "FossilTectonicUnlocked";
        public const string PREF_TECTONIC_UNLOCK_SHOWN = "FossilTectonicUnlockShown";
        public const string PREF_LOADOUT = "FossilLoadout";
        public const string PREF_AD_SKILL_DATE = "FossilAdSkillDate";
        public const string PREF_AD_SKILL_COUNT = "FossilAdSkillCount";

        public const int LOADOUT_SLOTS = 2;
        public const int SKILL_PRICE = 150;
        public const int AD_SKILL_DAILY_LIMIT = 10;
        public const int SKILL_COUNT = 5;

        public static event Action OnChanged;

        // ── 모드 해금 / 최고점 ─────────────────────────────────────────

        public static bool IsTectonicUnlocked => PlayerPrefs.GetInt(PREF_TECTONIC_UNLOCKED, 0) != 0;

        /// <summary>해금은 됐지만 안내 팝업을 아직 안 보여줬는지 (로비에서 1회 표시)</summary>
        public static bool ShouldShowUnlockNotice =>
            IsTectonicUnlocked && PlayerPrefs.GetInt(PREF_TECTONIC_UNLOCK_SHOWN, 0) == 0;

        public static void MarkUnlockNoticeShown()
        {
            PlayerPrefs.SetInt(PREF_TECTONIC_UNLOCK_SHOWN, 1);
            PlatformSaveSync.MarkDirty();
        }

        public static int GetBest(E_SUIKA_DIFFICULTY mode)
        {
            if (mode == E_SUIKA_DIFFICULTY.TECTONIC) return PlayerPrefs.GetInt(PREF_TECTONIC_BEST, 0);
            return UserDataManager.Instance != null ? UserDataManager.Instance.GetBestScore() : 0;
        }

        /// <summary>판 결과 기록. 일반 모드에서 해금 점수를 처음 넘으면 true</summary>
        public static bool RecordResult(E_SUIKA_DIFFICULTY mode, int score, int unlockScore)
        {
            bool newlyUnlocked = false;
            if (mode == E_SUIKA_DIFFICULTY.TECTONIC)
            {
                if (score > PlayerPrefs.GetInt(PREF_TECTONIC_BEST, 0))
                {
                    PlayerPrefs.SetInt(PREF_TECTONIC_BEST, score);
                    PlatformSaveSync.MarkDirty();
                }
            }
            else
            {
                UserDataManager.Instance?.UpdateBestScore(score);
                if (!IsTectonicUnlocked && score >= unlockScore)
                {
                    PlayerPrefs.SetInt(PREF_TECTONIC_UNLOCKED, 1);
                    PlatformSaveSync.MarkDirty();
                    newlyUnlocked = true;
                }
            }
            OnChanged?.Invoke();
            return newlyUnlocked;
        }

        // ── 로비 장착 ─────────────────────────────────────────────────

        private static readonly E_FOSSIL_SKILL[] _loadout = { E_FOSSIL_SKILL.NONE, E_FOSSIL_SKILL.NONE };
        private static bool _loadoutLoaded;

        public static E_FOSSIL_SKILL GetLoadout(int slot)
        {
            EnsureLoadout();
            return slot >= 0 && slot < LOADOUT_SLOTS ? _loadout[slot] : E_FOSSIL_SKILL.NONE;
        }

        public static int EmptyLoadoutSlot()
        {
            EnsureLoadout();
            for (int i = 0; i < LOADOUT_SLOTS; i++)
                if (_loadout[i] == E_FOSSIL_SKILL.NONE) return i;
            return -1;
        }

        public static bool HasEmptyLoadoutSlot => EmptyLoadoutSlot() >= 0;

        /// <summary>코인으로 원하는 스킬 확정 구매 → 빈 장착 슬롯에 넣는다</summary>
        public static bool TryBuySkill(E_FOSSIL_SKILL skill)
        {
            int slot = EmptyLoadoutSlot();
            if (slot < 0 || skill == E_FOSSIL_SKILL.NONE) return false;
            if (UserDataManager.Instance == null || !UserDataManager.Instance.SpendGold(SKILL_PRICE)) return false;
            SetLoadout(slot, skill);
            SuikaAnalytics.SkillAcquiredInLobby(skill, "coin");
            return true;
        }

        /// <summary>광고 보상: 랜덤 스킬 1개를 빈 장착 슬롯에 넣는다. 받은 스킬 반환 (실패 시 NONE)</summary>
        public static E_FOSSIL_SKILL GrantRandomSkill()
        {
            int slot = EmptyLoadoutSlot();
            if (slot < 0) return E_FOSSIL_SKILL.NONE;
            var skill = (E_FOSSIL_SKILL)UnityEngine.Random.Range(0, SKILL_COUNT);
            SetLoadout(slot, skill);
            ConsumeAdSkill();
            SuikaAnalytics.SkillAcquiredInLobby(skill, "ad");
            return skill;
        }

        public static void ClearLoadoutSlot(int slot)
        {
            if (slot < 0 || slot >= LOADOUT_SLOTS) return;
            SetLoadout(slot, E_FOSSIL_SKILL.NONE);
        }

        /// <summary>판 시작: 장착 스킬을 꺼내고 슬롯을 비운다 (1회용)</summary>
        public static int TakeLoadout(E_FOSSIL_SKILL[] buffer)
        {
            EnsureLoadout();
            int n = 0;
            for (int i = 0; i < LOADOUT_SLOTS; i++)
            {
                if (_loadout[i] == E_FOSSIL_SKILL.NONE) continue;
                if (n < buffer.Length) buffer[n++] = _loadout[i];
                _loadout[i] = E_FOSSIL_SKILL.NONE;
            }
            if (n > 0) SaveLoadout();
            return n;
        }

        private static void SetLoadout(int slot, E_FOSSIL_SKILL skill)
        {
            EnsureLoadout();
            _loadout[slot] = skill;
            SaveLoadout();
        }

        private static void EnsureLoadout()
        {
            if (_loadoutLoaded) return;
            _loadoutLoaded = true;
            string s = PlayerPrefs.GetString(PREF_LOADOUT, string.Empty);
            string[] parts = s.Split(',');
            for (int i = 0; i < LOADOUT_SLOTS; i++)
            {
                _loadout[i] = E_FOSSIL_SKILL.NONE;
                if (i < parts.Length && int.TryParse(parts[i], out int v) && v >= 0 && v < SKILL_COUNT)
                    _loadout[i] = (E_FOSSIL_SKILL)v;
            }
        }

        private static void SaveLoadout()
        {
            PlayerPrefs.SetString(PREF_LOADOUT, $"{(int)_loadout[0]},{(int)_loadout[1]}");
            PlatformSaveSync.MarkDirty();
            OnChanged?.Invoke();
        }

        // ── 로비 광고 스킬 일일 한도 ──────────────────────────────────

        public static int AdSkillsLeftToday
        {
            get
            {
                string today = DateTime.Now.ToString("yyyyMMdd");
                if (PlayerPrefs.GetString(PREF_AD_SKILL_DATE, string.Empty) != today) return AD_SKILL_DAILY_LIMIT;
                return Mathf.Max(0, AD_SKILL_DAILY_LIMIT - PlayerPrefs.GetInt(PREF_AD_SKILL_COUNT, 0));
            }
        }

        private static void ConsumeAdSkill()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            int used = PlayerPrefs.GetString(PREF_AD_SKILL_DATE, string.Empty) == today
                ? PlayerPrefs.GetInt(PREF_AD_SKILL_COUNT, 0) : 0;
            PlayerPrefs.SetString(PREF_AD_SKILL_DATE, today);
            PlayerPrefs.SetInt(PREF_AD_SKILL_COUNT, used + 1);
            PlatformSaveSync.MarkDirty();
        }

        /// <summary>플랫폼 저장소 복원 후 캐시 무효화</summary>
        public static void Reload()
        {
            _loadoutLoaded = false;
            OnChanged?.Invoke();
        }

        /// <summary>복원 직후 Fossil Merge 관련 캐시 전부 다시 읽기 (진행/설정/햅틱/최고점)</summary>
        public static void ReloadAllAfterRestore()
        {
            Reload();
            SuikaSettings.Reload();
            SuikaHaptics.Reload();
            var gm = UnityEngine.Object.FindFirstObjectByType<SuikaGameManager>();
            if (gm != null) gm.ReloadBestScore();
        }
    }
}
