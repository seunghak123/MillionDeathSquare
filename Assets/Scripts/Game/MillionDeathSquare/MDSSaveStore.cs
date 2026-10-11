using System;
using System.Collections.Generic;
using System.Text;
using Seunghak.Platform;
using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 백만 번 죽는 네모의 기기 저장 (PlayerPrefs, 키 접두사 MDS). PlatformSaveSync 스냅샷에 포함되어
    /// 플랫폼 저장소(Steam 클라우드/토스 저장소)와 동기화된다.
    ///   - MDSCampaignSave : 캠페인 진행 (CampaignProgress 직렬화)
    ///   - MDSEditorDraft  : 에디터에서 작업 중인 맵 (공유 코드)
    ///   - MDSPlayedMaps   : 내가 플레이한 유저 맵 목록 (맵 찾기 화면)
    /// </summary>
    public static class MDSSaveStore
    {
        public const string KEY_CAMPAIGN = "MDSCampaignSave";
        public const string KEY_EDITOR_DRAFT = "MDSEditorDraft";
        public const string KEY_PLAYED = "MDSPlayedMaps";
        public const int MAX_PLAYED = 50;

        private static CampaignProgress _campaign;
        private static List<PlayedMap> _played;

        [Serializable]
        public sealed class PlayedMap
        {
            /// <summary>맵 참조: 맵 코드, 공유 코드, 또는 "campaign:1-3"</summary>
            public string MapRef;
            public string Name;
            public bool Cleared;
            public int BestDeaths = -1;
            public long LastPlayedUnix;
        }

        public static CampaignProgress Campaign
        {
            get
            {
                if (_campaign == null) _campaign = CampaignProgress.Parse(PlayerPrefs.GetString(KEY_CAMPAIGN, ""));
                return _campaign;
            }
        }

        public static void SaveCampaign()
        {
            if (_campaign == null) return;
            PlayerPrefs.SetString(KEY_CAMPAIGN, _campaign.Serialize());
            PlayerPrefs.Save();
            PlatformSaveSync.MarkDirty();
        }

        // ── 에디터 초안 ──

        public static MapData LoadEditorDraft()
        {
            string code = PlayerPrefs.GetString(KEY_EDITOR_DRAFT, "");
            MapData map;
            string error;
            if (!string.IsNullOrEmpty(code) && MapSerializer.TryParseShareCode(code, out map, out error)) return map;
            return null;
        }

        public static void SaveEditorDraft(MapData map)
        {
            if (map == null) return;
            PlayerPrefs.SetString(KEY_EDITOR_DRAFT, MapSerializer.ToShareCode(map));
            PlatformSaveSync.MarkDirty();
        }

        // ── 플레이한 맵 ──

        public static List<PlayedMap> PlayedMaps
        {
            get
            {
                if (_played == null) _played = ParsePlayed(PlayerPrefs.GetString(KEY_PLAYED, ""));
                return _played;
            }
        }

        public static void RecordPlayed(string mapRef, string name, bool cleared, int deaths)
        {
            if (string.IsNullOrEmpty(mapRef)) return;
            List<PlayedMap> list = PlayedMaps;
            PlayedMap entry = null;
            for (int i = 0; i < list.Count; i++) if (list[i].MapRef == mapRef) { entry = list[i]; list.RemoveAt(i); break; }
            if (entry == null) entry = new PlayedMap { MapRef = mapRef };
            entry.Name = name ?? "";
            if (cleared)
            {
                entry.Cleared = true;
                if (entry.BestDeaths < 0 || deaths < entry.BestDeaths) entry.BestDeaths = deaths;
            }
            entry.LastPlayedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            list.Insert(0, entry);
            while (list.Count > MAX_PLAYED) list.RemoveAt(list.Count - 1);
            PlayerPrefs.SetString(KEY_PLAYED, SerializePlayed(list));
            PlatformSaveSync.MarkDirty();
        }

        private static string SerializePlayed(List<PlayedMap> list)
        {
            // 한 줄에 하나: 참조 \t 이름 \t 클리어 \t 최소데스 \t 시각 (공유 코드에는 탭/줄바꿈이 없음)
            var sb = new StringBuilder("P1");
            foreach (PlayedMap p in list)
            {
                sb.Append('\n').Append(Clean(p.MapRef)).Append('\t').Append(Clean(p.Name)).Append('\t')
                  .Append(p.Cleared ? 1 : 0).Append('\t').Append(p.BestDeaths).Append('\t').Append(p.LastPlayedUnix);
            }
            return sb.ToString();
        }

        private static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('\n', ' '); }

        private static List<PlayedMap> ParsePlayed(string s)
        {
            var list = new List<PlayedMap>();
            if (string.IsNullOrEmpty(s)) return list;
            string[] lines = s.Split('\n');
            if (lines[0] != "P1") return list;
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].Split('\t');
                if (f.Length < 5) continue;
                int deaths;
                long t;
                int.TryParse(f[3], out deaths);
                long.TryParse(f[4], out t);
                list.Add(new PlayedMap { MapRef = f[0], Name = f[1], Cleared = f[2] == "1", BestDeaths = deaths, LastPlayedUnix = t });
            }
            return list;
        }

        /// <summary>플랫폼 저장소 스냅샷이 PlayerPrefs를 덮어쓴 직후 (ApplicationManager가 호출)</summary>
        public static void ReloadAfterRestore()
        {
            _campaign = null;
            _played = null;
        }
    }
}
