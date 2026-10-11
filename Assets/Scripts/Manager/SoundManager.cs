using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Seunghak.Common;

namespace Seunghak.Manager
{
    /// <summary>
    /// 사운드 매니저 — BGM / SFX / Voice 3채널 관리.
    ///
    /// [BGM 크로스페이드]
    ///   AudioSource A / B 두 개를 교차하며 동시에 fade-out/in (0.4초).
    ///
    /// [광고 대응]
    ///   PauseForAd()  → 모든 사운드 일시정지, 게임 Pause/Resume이 개입 불가
    ///   ResumeFromAd() → 광고 종료 후 원래 상태로 복귀
    ///
    /// 사용법:
    ///   SoundManager.Instance.PlayBGM("Lobby");
    ///   SoundManager.Instance.PlaySFX("Jump");
    ///   SoundManager.Instance.PauseForAd();
    ///   SoundManager.Instance.ResumeFromAd();
    /// </summary>
    public class SoundManager : UnitySingleton<SoundManager>
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("BGM AudioSource (2개 필수 — 크로스페이드용)")]
        [SerializeField] private AudioSource bgmAudioSource;   // Source A
        [SerializeField] private AudioSource bgmAudioSourceB;  // Source B

        [Header("Voice")]
        [SerializeField] private AudioSource voiceAudioSource;

        [Header("SFX 풀 (에디터에서 AudioSource 미리 세팅)")]
        [SerializeField] private List<AudioSource> sfxAudioSources = new List<AudioSource>();

        [Header("BGM 클립 목록")]
        [SerializeField] private SoundEntry[] bgmClips;

        [Header("SFX 클립 목록")]
        [SerializeField] private SoundEntry[] sfxClips;

        [Header("BGM 페이드 시간 (초)")]
        [SerializeField] private float bgmFadeDuration = 0.4f;

        // ── 내부 상태 ──────────────────────────────────────────────────────

        private readonly Dictionary<string, AudioClip> _bgmDict = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioClip> _sfxDict = new Dictionary<string, AudioClip>();

        // SetClipVolume()으로 등록된 클립별 개별 볼륨 배수 (key → 0~1). 미등록 키는 1f.
        private readonly Dictionary<string, float> _clipVolumeMultipliers = new Dictionary<string, float>();

        private string _currentBgmKey = string.Empty;
        private bool _bgmUsingA = true;   // 현재 재생 중인 소스가 A인지 B인지

        private Coroutine _bgmCrossFadeCoroutine;

        // 광고 중 뮤트 플래그 — true이면 ResumeAll()이 무시됨
        private bool _adMuted;

        // ── 프로퍼티 ───────────────────────────────────────────────────────

        private AudioSource ActiveBgm   => _bgmUsingA ? bgmAudioSource  : bgmAudioSourceB;
        private AudioSource InactiveBgm => _bgmUsingA ? bgmAudioSourceB : bgmAudioSource;

        // ── 초기화 ────────────────────────────────────────────────────────

        protected override void InitSingleton()
        {
            base.InitSingleton();
            BuildDictionaries();
            EnsureSfxPool();

            // 저장된 볼륨 옵션은 여기서 1회 반영한다. 예전엔 AudioListener.volume(마스터)을
            // 설정창(SettingsWindow/SettingPopup)의 LoadValues()에서만 세팅해서, 설정창을
            // 한 번도 열지 않으면 지난 번에 저장한 볼륨이 무시되고 항상 100%로 재생됐다.
            ApplyUserVolumeSettings();
        }

        private void BuildDictionaries()
        {
            _bgmDict.Clear();
            _sfxDict.Clear();

            if (bgmClips != null)
                foreach (var e in bgmClips)
                    if (e.clip != null && !string.IsNullOrEmpty(e.key))
                        _bgmDict[e.key] = e.clip;

            if (sfxClips != null)
                foreach (var e in sfxClips)
                    if (e.clip != null && !string.IsNullOrEmpty(e.key))
                        _sfxDict[e.key] = e.clip;
        }

        private void EnsureSfxPool()
        {
            for (int i = sfxAudioSources.Count - 1; i >= 0; i--)
                if (sfxAudioSources[i] == null) sfxAudioSources.RemoveAt(i);

            if (sfxAudioSources.Count == 0)
                Debug.LogWarning("[SoundManager] SFX AudioSource가 비어있습니다. Inspector에서 세팅하세요.");
        }

        // ── 볼륨 헬퍼 ─────────────────────────────────────────────────────

        // 마스터 볼륨은 AudioListener.volume으로 한 번만 적용한다(ApplyMasterVolumeToListener).
        // 채널 볼륨에까지 곱하면 master가 제곱으로 먹어서(예: 50% → 실제 25%) 슬라이더가
        // 표시하는 값과 실제 음량이 어긋난다.
        private float GetBgmVolume()
        {
            if (UserDataManager.Instance == null) return 1f;
            var opt = UserDataManager.Instance.GetUserOptionData();
            if (opt == null || opt.IsMute) return 0f;
            return opt.SoundVolume / 100f;
        }

        private float GetSfxVolume()
        {
            if (UserDataManager.Instance == null) return 1f;
            var opt = UserDataManager.Instance.GetUserOptionData();
            if (opt == null || opt.IsMute) return 0f;
            return opt.sfxVolume / 100f;
        }

        private float GetVoiceVolume()
        {
            if (UserDataManager.Instance == null) return 1f;
            var opt = UserDataManager.Instance.GetUserOptionData();
            if (opt == null || opt.IsMute) return 0f;
            return opt.FBXVolume / 100f;
        }

        private bool IsMuted()
        {
            if (UserDataManager.Instance == null) return false;
            var opt = UserDataManager.Instance.GetUserOptionData();
            return opt != null && opt.IsMute;
        }

        /// <summary>사운드 테이블에 등록된 키별 개별 볼륨 배수. 미등록 키는 1f (배수 없음).</summary>
        private float GetClipVolumeMultiplier(string key)
        {
            if (!string.IsNullOrEmpty(key) && _clipVolumeMultipliers.TryGetValue(key, out float v)) return v;
            return 1f;
        }

        // ── BGM ───────────────────────────────────────────────────────────

        /// <summary>등록된 키로 BGM 재생 (크로스페이드). 같은 곡이면 무시.</summary>
        public void PlayBGM(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            // 크로스페이드 진행 중에는 _bgmUsingA가 아직 flip되지 않아 ActiveBgm(이전 소스)이
            // 이미 Stop된 상태일 수 있다. InactiveBgm(새 소스, fade-in 중)도 함께 확인해야
            // 같은 키로 이중 크로스페이드가 시작되는 것을 방지할 수 있다.
            bool activeIsPlaying   = ActiveBgm   != null && ActiveBgm.isPlaying;
            bool inactiveIsPlaying = InactiveBgm != null && InactiveBgm.isPlaying;
            if (_currentBgmKey == key && (activeIsPlaying || inactiveIsPlaying)) return;
            if (IsMuted()) return;

            AudioClip clip = GetBgmClip(key);
            if (clip == null)
            {
                Debug.LogWarning($"[SoundManager] BGM 클립 없음: {key}");
                return;
            }

            // 같은 곡이 Pause된 채 남아있으면(Stop과 달리 time이 유지됨) 새로 크로스페이드하지 않고 그대로 재개.
            // 크로스페이드를 걸면 기존 소스는 볼륨 0으로 꺼지고, Pause 플래그가 남은 반대 소스에
            // Play()가 걸려 WebGL에서 BGM이 무음이 되는 경우가 있다. 광고 중이면 재개는 ResumeFromAd()에 맡긴다.
            if (_currentBgmKey == key && ActiveBgm != null && ActiveBgm.clip == clip && ActiveBgm.time > 0f)
            {
                if (!_adMuted) ActiveBgm.UnPause();
                return;
            }

            _currentBgmKey = key;
            StartCrossFade(clip, GetClipVolumeMultiplier(key));
        }

        /// <summary>AudioClip을 직접 전달하여 BGM 재생.</summary>
        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || IsMuted()) return;
            if (ActiveBgm != null && ActiveBgm.clip == clip && ActiveBgm.isPlaying) return;

            _currentBgmKey = clip.name;
            StartCrossFade(clip, GetClipVolumeMultiplier(clip.name));
        }

        /// <summary>BGM 페이드아웃 후 정지.</summary>
        public void StopBGM()
        {
            _currentBgmKey = string.Empty;

            if (_bgmCrossFadeCoroutine != null)
                StopCoroutine(_bgmCrossFadeCoroutine);

            _bgmCrossFadeCoroutine = StartCoroutine(FadeOutSource(ActiveBgm, bgmFadeDuration, stopAfter: true));
        }

        private void StartCrossFade(AudioClip newClip, float clipVolumeMultiplier = 1f)
        {
            if (_bgmCrossFadeCoroutine != null)
                StopCoroutine(_bgmCrossFadeCoroutine);

            _bgmCrossFadeCoroutine = StartCoroutine(CrossFadeRoutine(newClip, clipVolumeMultiplier));
        }

        /// <summary>
        /// 진짜 크로스페이드: Active(이전 곡) fade-out과 Inactive(새 곡) fade-in이 동시에 진행.
        /// </summary>
        private IEnumerator CrossFadeRoutine(AudioClip newClip, float clipVolumeMultiplier)
        {
            if (bgmAudioSource == null || bgmAudioSourceB == null)
            {
                // Source B가 없으면 단순 sequential fade
                float directVolume = GetBgmVolume() * clipVolumeMultiplier;
                yield return FadeOutSource(ActiveBgm, bgmFadeDuration, stopAfter: true);
                PlayDirect(ActiveBgm, newClip, directVolume);
                yield return FadeInSource(ActiveBgm, bgmFadeDuration, directVolume);
                _bgmCrossFadeCoroutine = null;
                yield break;
            }

            float targetVolume = GetBgmVolume() * clipVolumeMultiplier;
            AudioSource outSource = ActiveBgm;
            AudioSource inSource  = InactiveBgm;

            // 새 곡을 Inactive 소스에 세팅하고 볼륨 0으로 시작
            inSource.clip   = newClip;
            inSource.loop   = true;
            inSource.volume = 0f;
            inSource.Play();

            // Active 소스 초기 볼륨 캡처
            float outStartVol = outSource.isPlaying ? outSource.volume : 0f;

            // fade-out(기존) + fade-in(신규) 동시 진행
            float elapsed = 0f;
            while (elapsed < bgmFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / bgmFadeDuration);

                outSource.volume = Mathf.Lerp(outStartVol, 0f, t);
                inSource.volume  = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            // 정리
            outSource.volume = 0f;
            outSource.Stop();
            outSource.clip = null;

            inSource.volume = targetVolume;

            // 역할 교체 (다음 크로스페이드를 위해)
            _bgmUsingA = !_bgmUsingA;

            _bgmCrossFadeCoroutine = null;
        }

        // ── SFX ───────────────────────────────────────────────────────────

        /// <summary>등록된 키로 효과음 재생.</summary>
        public void PlaySFX(string key)
        {
            if (string.IsNullOrEmpty(key) || IsMuted()) return;

            AudioClip clip = GetSfxClip(key);
            if (clip == null)
            {
                Debug.LogWarning($"[SoundManager] SFX 클립 없음: {key}");
                return;
            }

            PlaySfxInternal(clip, GetSfxVolume() * GetClipVolumeMultiplier(key));
        }

        /// <summary>AudioClip을 직접 전달하여 효과음 재생.</summary>
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || IsMuted()) return;
            PlaySfxInternal(clip, GetSfxVolume());
        }

        /// <summary>볼륨을 지정하여 효과음 재생.</summary>
        public void PlaySFX(string key, float volumeScale)
        {
            if (string.IsNullOrEmpty(key) || IsMuted()) return;
            AudioClip clip = GetSfxClip(key);
            if (clip == null) return;
            PlaySfxInternal(clip, GetSfxVolume() * GetClipVolumeMultiplier(key) * volumeScale);
        }

        private void PlaySfxInternal(AudioClip clip, float volume)
        {
            // 빈 소스 우선 탐색
            for (int i = 0; i < sfxAudioSources.Count; i++)
            {
                if (sfxAudioSources[i] != null && !sfxAudioSources[i].isPlaying)
                {
                    sfxAudioSources[i].clip   = clip;
                    sfxAudioSources[i].volume = volume;
                    sfxAudioSources[i].loop   = false;
                    sfxAudioSources[i].Play();
                    return;
                }
            }

            // 모두 사용 중 → 가장 오래된 소스 덮어쓰기
            if (sfxAudioSources.Count > 0)
            {
                sfxAudioSources[0].clip   = clip;
                sfxAudioSources[0].volume = volume;
                sfxAudioSources[0].loop   = false;
                sfxAudioSources[0].Play();
            }
        }

        // ── Voice ─────────────────────────────────────────────────────────

        /// <summary>캐릭터 보이스 재생.</summary>
        public void PlayVoice(AudioClip clip, bool loop = false)
        {
            if (clip == null || voiceAudioSource == null || IsMuted()) return;
            voiceAudioSource.clip   = clip;
            voiceAudioSource.volume = GetVoiceVolume();
            voiceAudioSource.loop   = loop;
            voiceAudioSource.Play();
        }

        /// <summary>보이스 정지.</summary>
        public void StopVoice()
        {
            if (voiceAudioSource != null) voiceAudioSource.Stop();
        }

        // ── 전체 제어 ─────────────────────────────────────────────────────

        /// <summary>모든 사운드 일시정지 (게임 일시정지 등).</summary>
        public void PauseAll()
        {
            PauseAllSources();
        }

        /// <summary>모든 사운드 재개. 광고 중이면 무시.</summary>
        public void ResumeAll()
        {
            if (_adMuted) return;
            ResumeAllSources();
        }

        /// <summary>모든 사운드 정지.</summary>
        public void StopAll()
        {
            if (bgmAudioSource != null) bgmAudioSource.Stop();
            if (bgmAudioSourceB != null) bgmAudioSourceB.Stop();
            if (voiceAudioSource != null) voiceAudioSource.Stop();
            for (int i = 0; i < sfxAudioSources.Count; i++)
                if (sfxAudioSources[i] != null) sfxAudioSources[i].Stop();
            _currentBgmKey = string.Empty;
        }

        // ── 광고 제어 ─────────────────────────────────────────────────────

        /// <summary>
        /// 광고 시작 시 호출. 모든 사운드 즉시 정지.
        /// ResumeAll() 호출이 들어와도 광고가 끝날 때까지 재개되지 않음.
        /// </summary>
        public void PauseForAd()
        {
            _adMuted = true;
            PauseAllSources();
        }

        /// <summary>
        /// 광고 종료 시 호출. 모든 사운드 원래 상태로 복귀.
        /// </summary>
        public void ResumeFromAd()
        {
            _adMuted = false;
            ResumeAllSources();
        }

        // ── 볼륨 갱신 ─────────────────────────────────────────────────────

        /// <summary>
        /// 저장된 유저 사운드 옵션(마스터/음악/캐릭터/효과음)을 실제 오디오에 반영한다.
        /// 게임 시작 시(InitSingleton)와 설정창에서 값을 바꿀 때 모두 이 경로를 탄다.
        /// </summary>
        public void ApplyUserVolumeSettings()
        {
            RefreshVolumes();
        }

        /// <summary>마스터 볼륨을 AudioListener에 반영 (게임 전체 음량).</summary>
        private void ApplyMasterVolumeToListener()
        {
            if (UserDataManager.Instance == null) return;
            var opt = UserDataManager.Instance.GetUserOptionData();
            if (opt == null) return;

            AudioListener.volume = opt.IsMute ? 0f : Mathf.Clamp01(opt.MasterVolume / 100f);
        }

        /// <summary>볼륨 설정 변경 시 호출. 실시간 반영.</summary>
        public void RefreshVolumes()
        {
            ApplyMasterVolumeToListener();

            if (ActiveBgm != null && ActiveBgm.isPlaying)
                ActiveBgm.volume = GetBgmVolume() * GetClipVolumeMultiplier(_currentBgmKey);

            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
                voiceAudioSource.volume = GetVoiceVolume();
        }

        // ── 클립 등록/조회 ────────────────────────────────────────────────

        public void RegisterBGM(string key, AudioClip clip)
        {
            if (!string.IsNullOrEmpty(key) && clip != null) _bgmDict[key] = clip;
        }

        public void RegisterSFX(string key, AudioClip clip)
        {
            if (!string.IsNullOrEmpty(key) && clip != null) _sfxDict[key] = clip;
        }

        private AudioClip GetBgmClip(string key)
        {
            if (_bgmDict.TryGetValue(key, out AudioClip clip)) return clip;
            var loaded = GameResourceManager.Instance.LoadObject(key) as AudioClip;
            if (loaded != null) _bgmDict[key] = loaded;
            return loaded;
        }

        private AudioClip GetSfxClip(string key)
        {
            if (_sfxDict.TryGetValue(key, out AudioClip clip)) return clip;
            var loaded = GameResourceManager.Instance.LoadObject(key) as AudioClip;
            if (loaded != null) _sfxDict[key] = loaded;
            return loaded;
        }

        // ── 레거시 호환 ───────────────────────────────────────────────────

        /// <summary>[레거시] 문자열 + 볼륨 + 루프로 사운드 재생.</summary>
        public void PlaySound(string soundName, float soundVolume, bool isLoop)
        {
            if (isLoop)
            {
                AudioClip clip = GameResourceManager.Instance.LoadObject(soundName) as AudioClip;
                PlayVoice(clip, true);
            }
            else
            {
                PlaySFX(soundName);
            }
        }

        /// <summary>[레거시] FX 사운드 재생.</summary>
        public void PlayFxSound(string soundName, float soundVolume)
        {
            PlaySFX(soundName);
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────────────────

        private void PauseAllSources()
        {
            if (bgmAudioSource != null) bgmAudioSource.Pause();
            if (bgmAudioSourceB != null) bgmAudioSourceB.Pause();
            if (voiceAudioSource != null) voiceAudioSource.Pause();
            for (int i = 0; i < sfxAudioSources.Count; i++)
                if (sfxAudioSources[i] != null) sfxAudioSources[i].Pause();
        }

        private void ResumeAllSources()
        {
            if (bgmAudioSource != null) bgmAudioSource.UnPause();
            if (bgmAudioSourceB != null) bgmAudioSourceB.UnPause();
            if (voiceAudioSource != null) voiceAudioSource.UnPause();
            for (int i = 0; i < sfxAudioSources.Count; i++)
                if (sfxAudioSources[i] != null) sfxAudioSources[i].UnPause();
        }

        private static void PlayDirect(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null) return;
            source.clip   = clip;
            source.loop   = true;
            source.volume = volume;
            source.Play();
        }

        private IEnumerator FadeOutSource(AudioSource source, float duration, bool stopAfter)
        {
            if (source == null || !source.isPlaying) yield break;

            float startVol = source.volume;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            if (stopAfter) { source.Stop(); source.clip = null; }
        }

        private IEnumerator FadeInSource(AudioSource source, float duration, float targetVolume)
        {
            if (source == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
                yield return null;
            }
            source.volume = targetVolume;
        }

        // ── 런타임 클립 등록 ──────────────────────────────────────────────

        /// <summary>
        /// 키-클립 쌍을 사운드 딕셔너리에 병합 (같은 키는 덮어씀).
        /// 게임별 사운드 테이블 에셋 등에서 Inspector 등록분 외의 클립을 추가할 때 사용.
        /// </summary>
        public void RegisterClips(IEnumerable<SoundEntry> bgm, IEnumerable<SoundEntry> sfx)
        {
            if (bgm != null) foreach (var e in bgm) if (!string.IsNullOrEmpty(e.key)) _bgmDict[e.key] = e.clip;
            if (sfx != null) foreach (var e in sfx) if (!string.IsNullOrEmpty(e.key)) _sfxDict[e.key] = e.clip;
        }

        /// <summary>클립별 개별 볼륨 배수(0~1) 지정. 미지정 키는 1.</summary>
        public void SetClipVolume(string key, float volume)
        {
            if (string.IsNullOrEmpty(key)) return;
            _clipVolumeMultipliers[key] = Mathf.Clamp01(volume);
        }
    }

    /// <summary>Inspector에서 키-클립 쌍을 등록하기 위한 직렬화 구조체.</summary>
    [Serializable]
    public struct SoundEntry
    {
        public string key;
        public AudioClip clip;
    }
}
