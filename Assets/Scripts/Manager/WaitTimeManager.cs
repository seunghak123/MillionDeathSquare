using Seunghak;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaitTimeManager : UnitySingleton<WaitTimeManager>
{
    private const string WAITFORTIME = "wfts";
    private const string WAITFOR_REALTIME = "wfrts";
    private const string WAITFOR_ENDFLAME = "wfef";
    private static Dictionary<string, YieldInstruction> waitforTimeLists = new Dictionary<string, YieldInstruction>();
    private static Dictionary<string, CustomYieldInstruction> waitforRealTimeLists = new Dictionary<string, CustomYieldInstruction>();
    public static YieldInstruction WaitForTimeSeconds(float times)
    {
        string timeString = $"{WAITFORTIME}{times:F2}";
        if (waitforTimeLists.ContainsKey(timeString))
        {
            return waitforTimeLists[timeString];
        }
        else
        {
            WaitForSeconds wfs = new WaitForSeconds(times);
            waitforTimeLists[timeString] = wfs;
            return wfs;
        }
    }
    public static CustomYieldInstruction WaitForRealTimeSeconds(float times)
    {
        string timeString = $"{WAITFOR_REALTIME}{times:F2}";
        if (waitforRealTimeLists.ContainsKey(timeString))
        {
            return waitforRealTimeLists[timeString];
        }
        else
        {
            WaitForSecondsRealtime wfs = new WaitForSecondsRealtime(times);
            waitforRealTimeLists[timeString] = wfs;
            return wfs;
        }
    }
    public static YieldInstruction WaitForEndFrame()
    {
        if (waitforTimeLists.ContainsKey(WAITFOR_ENDFLAME))
        {
            return waitforTimeLists[WAITFOR_ENDFLAME];
        }
        else
        {
            waitforTimeLists[WAITFOR_ENDFLAME] = new WaitForEndOfFrame();
            return waitforTimeLists[WAITFOR_ENDFLAME];
        }
    }
}
