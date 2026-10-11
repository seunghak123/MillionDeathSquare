namespace Seunghak.Platform
{
    /// <summary>
    /// Steam 앱 ID — 항상 컴파일된다 (셋업 툴이 steam_appid.txt를 만들 때도 사용).
    /// TODO: Steamworks에서 앱을 등록하면 실제 ID로 교체 (Docs/TODO.md). 480 = Valve 테스트 앱 Spacewar.
    /// </summary>
    public static class MDSSteamAppId
    {
        public const uint Value = 480;
    }
}
