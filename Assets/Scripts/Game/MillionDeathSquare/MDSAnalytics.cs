using Seunghak.Platform;
using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 분석 이벤트 (이름은 항상 mds_ 접두사). 플랫폼(IPlatform.LogEvent)으로 보낸다 — Steam판은 기록만 남기고,
    /// 모바일(토스)판은 토스 이벤트 로그로 간다. 판 진행 중 핫패스에서는 부르지 않는다 (판 시작/끝/화면 전환만).
    /// </summary>
    public static class MDSAnalytics
    {
        public const string PREFIX = "mds_";

        public static void Screen(string screenName)
        {
            Log("screen", 0f, "{\"screen\":\"" + screenName + "\"}");
        }

        public static void Log(string eventName, float value = 0f, string paramsJson = "")
        {
            string name = eventName.StartsWith(PREFIX) ? eventName : PREFIX + eventName;
            IPlatform p = PlatformFactory.Current;
            if (p != null) p.LogEvent(name, value, paramsJson);
#if UNITY_EDITOR
            Debug.Log("[MDSAnalytics] " + name + " " + value + " " + paramsJson);
#endif
        }

        public static void StageStart(string mapKey) { Log("stage_start", 0f, "{\"map\":\"" + mapKey + "\"}"); }

        public static void StageClear(string mapKey, int deaths, int ticks)
        {
            Log("stage_clear", deaths, "{\"map\":\"" + mapKey + "\",\"deaths\":" + deaths + ",\"ticks\":" + ticks + "}");
        }

        public static void StageQuit(string mapKey, int deaths, int ticks)
        {
            Log("stage_quit", deaths, "{\"map\":\"" + mapKey + "\",\"deaths\":" + deaths + ",\"ticks\":" + ticks + "}");
        }
    }
}
