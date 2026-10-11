using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// 플랫폼별 기능 추상화 인터페이스.
    /// PlatformFactory.Create() 로 실제 구현체를 생성합니다.
    /// </summary>
    public interface IPlatform
    {
        /// <summary>플랫폼 SDK 초기화 (로그인 포함)</summary>
        UniTask InitAsync();

        /// <summary>플레이어 고유 ID</summary>
        string GetPlayerId();

        /// <summary>플레이어 표시 이름</summary>
        string GetPlayerName();

        /// <summary>게임 시작 시 플랫폼에 알림</summary>
        void OnGameStart();

        /// <summary>게임 종료 시 점수 전송</summary>
        void OnGameEnd(int score, int bestScore);

        /// <summary>리더보드 점수 제출</summary>
        UniTask<bool> SubmitScoreAsync(int score);

        /// <summary>친구 리더보드 점수 조회</summary>
        UniTask<List<FBLeaderboardEntry>> GetFriendScoresAsync(int count = 10);

        /// <summary>게임 결과 공유</summary>
        UniTask<bool> ShareResultAsync(int score, string imageUrl = "");

        /// <summary>뒤로가기 처리 요청 (Toss 앱 전용)</summary>
        void RequestBack();

        /// <summary>햅틱 피드백 (0=light, 1=medium, 2=heavy)</summary>
        void Vibrate(int type = 0);

        /// <summary>로딩 진행률 전송 (Facebook Instant Games 필수: 0~100)</summary>
        void SetLoadingProgress(float percent);

        /// <summary>이벤트 로깅 (Facebook Analytics 등)</summary>
        void LogEvent(string eventName, float value = 0f, string paramsJson = "");

        /// <summary>햅틱 피드백 named type (tickWeak/tap/success/error 등)</summary>
        void GenerateHaptic(string hapticType);

        // ── 플랫폼 핸들러 (contracts.md 6장) ─────────────────────────────

        /// <summary>플랫폼 측 원본 저장소. PlayerPrefs는 동기 로컬 캐시로만 씁니다.</summary>
        IPlatformStorage Storage { get; }

        /// <summary>플랫폼 광고 핸들러. 미지원이면 NullAds.</summary>
        IPlatformAds Ads { get; }

        /// <summary>플랫폼 인앱결제 핸들러. 미지원이면 NullIAP.</summary>
        IPlatformIAP IAP { get; }

        /// <summary>플랫폼이 자체 리더보드 화면을 제공하는지</summary>
        bool SupportsLeaderboardUI { get; }

        /// <summary>플랫폼 리더보드 화면 열기. 성공 여부 반환.</summary>
        UniTask<bool> OpenLeaderboardAsync();

        /// <summary>친구 초대(리워드) 기능을 쓸 수 있는지</summary>
        bool SupportsInvite { get; }

        /// <summary>친구 초대 화면 열기. 지급된 리워드 수량 합계 반환 (없으면 0).</summary>
        UniTask<int> InviteFriendsAsync();

        /// <summary>플랫폼 UI 여백 (캔버스 단위). 상단 내비게이션 바 등에 가리는 만큼 입력. PlatformSafeArea가 적용.</summary>
        SafeAreaInset SafeAreaInset { get; }
    }

    // ── 데이터 모델 ──────────────────────────────────────────────────────

    [Serializable]
    public class FBLeaderboardEntry
    {
        public string playerId;
        public string name;
        public int score;
        public int rank;
    }

    [Serializable]
    internal class FBLeaderboardEntryWrapper
    {
        public List<FBLeaderboardEntry> items;
    }

    [Serializable]
    public class TournamentData
    {
        public string id;
        public string contextID;
        public int endTime;
        public string title;
    }

    // ── 에디터 / 미지원 플랫폼 ──────────────────────────────────────────

    /// <summary>에디터 / 미지원 플랫폼용 no-op 구현체.</summary>
    public sealed class DefaultPlatform : IPlatform
    {
        public UniTask InitAsync() => UniTask.CompletedTask;
        public string GetPlayerId() => "guest";
        public string GetPlayerName() => "Guest";
        public void OnGameStart() { }
        public void OnGameEnd(int score, int bestScore) { }
        public UniTask<bool> SubmitScoreAsync(int score) => UniTask.FromResult(false);
        public UniTask<List<FBLeaderboardEntry>> GetFriendScoresAsync(int count = 10) => UniTask.FromResult(new List<FBLeaderboardEntry>());
        public UniTask<bool> ShareResultAsync(int score, string imageUrl = "") => UniTask.FromResult(false);
        public void RequestBack() => Debug.Log("[DefaultPlatform] RequestBack");
        public void Vibrate(int type = 0) { }
        public void SetLoadingProgress(float percent) { }
        public void LogEvent(string eventName, float value = 0f, string paramsJson = "") { }
        public void GenerateHaptic(string hapticType) { }
        public IPlatformStorage Storage => PlayerPrefsStorage.Instance;
        public IPlatformAds Ads => NullAds.Instance;
#if UNITY_EDITOR
        // 에디터에서는 TossIAP의 목 결제(즉시 지급)로 상점 UI를 테스트할 수 있게 한다
        private IPlatformIAP _editorIap;
        public IPlatformIAP IAP => _editorIap ??= new TossIAP();
#else
        public IPlatformIAP IAP => NullIAP.Instance;
#endif
        public bool SupportsLeaderboardUI => false;
        public UniTask<bool> OpenLeaderboardAsync() => UniTask.FromResult(false);
        public bool SupportsInvite => false;
        public UniTask<int> InviteFriendsAsync() => UniTask.FromResult(0);
        public SafeAreaInset SafeAreaInset => SafeAreaInset.Zero;
    }

    // ── 팩토리 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 런타임 환경을 감지해 알맞은 IPlatform 구현체를 생성합니다.
    /// PC: Steam(STEAMWORKS_NET) / WebGL: 토스 미니게임(모바일판) → Facebook / 그 밖: DefaultPlatform.
    /// ApplicationManager 초기화 시 한 번 Create()를 호출하세요.
    /// </summary>
    public static class PlatformFactory
    {
        /// <summary>현재 활성 플랫폼. Create() 호출 전에는 DefaultPlatform.</summary>
        public static IPlatform Current { get; private set; } = new DefaultPlatform();

        /// <summary>
        /// 환경을 감지하고 플랫폼 컴포넌트를 owner 오브젝트에 부착합니다.
        /// </summary>
        public static IPlatform Create(GameObject owner)
        {
#if STEAMWORKS_NET && (UNITY_STANDALONE || UNITY_EDITOR)
            // PC(Steam)판 — 주 플랫폼. Steam 초기화에 실패하면 오프라인(DefaultPlatform)으로 진행
            SteamPlatform steam = SteamPlatform.TryCreate(owner);
            if (steam != null)
            {
                Current = steam;
                Debug.Log("[PlatformFactory] SteamPlatform 생성");
                return Current;
            }
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            if (TossPlatform.CheckIsInsideTossApp())
            {
                Current = owner.AddComponent<TossPlatform>();
                Debug.Log("[PlatformFactory] TossPlatform 생성");
                return Current;
            }
            if (FacebookPlatform.CheckIsAvailable())
            {
                Current = owner.AddComponent<FacebookPlatform>();
                Debug.Log("[PlatformFactory] FacebookPlatform 생성");
                return Current;
            }
#endif
            Current = new DefaultPlatform();
            Debug.Log("[PlatformFactory] DefaultPlatform 생성");
            return Current;
        }
    }
}
