using System;
using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 지각 변동 모드의 지진 이벤트 (기획서 9.2 / 9.3).
    ///  - 플레이 시간 구간별로 간격·강도·지속이 세진다 (0~60초 / 60~120초 / 120초 이후)
    ///  - 발생 2초 전 경보 → 발굴대가 한쪽으로 기울며 흔들림
    ///  - 그동안 라이프를 잃지 않으면 "버티기 성공" 보너스
    /// 흔들림(진동 성분)은 설정의 흔들림 강도(SuikaSettings.ShakeIntensity)를 따른다. 0%면 기울기만 남는다.
    /// </summary>
    public class SuikaQuakeDirector : MonoBehaviour
    {
        [Serializable]
        public class Stage
        {
            [Tooltip("이 플레이 시간(초) 이후 적용")]
            public float fromSeconds;
            public float interval;
            public float tiltDeg;
            public float duration;
            public int surviveBonus;
        }

        [SerializeField] private Stage[] stages =
        {
            new Stage { fromSeconds = 0f,   interval = 30f, tiltDeg = 5f,  duration = 2f, surviveBonus = 100 },
            new Stage { fromSeconds = 60f,  interval = 20f, tiltDeg = 10f, duration = 3f, surviveBonus = 200 },
            new Stage { fromSeconds = 120f, interval = 15f, tiltDeg = 15f, duration = 4f, surviveBonus = 300 },
        };
        [SerializeField] private float warningSeconds = 2f;
        [SerializeField] private float shakeFrequency = 3f;
        [Tooltip("흔들림(진동) 성분 크기 = 기울기 × 이 비율 × 흔들림 강도 설정")]
        [SerializeField] private float shakeRatio = 0.35f;

        /// <summary>경보 시작 (남은 초)</summary>
        public event Action<float> OnWarning;
        public event Action OnQuakeStart;
        /// <summary>지진 종료 (버티기 성공 여부, 보너스 점수)</summary>
        public event Action<bool, int> OnQuakeEnd;

        private bool _running;
        private float _playTime;
        private float _nextQuakeAt;
        private bool _warned;
        private float _quakeTimer;
        private Stage _active;
        private Vector3 _dir;
        private int _livesAtStart;
        private SuikaGameManager _manager;

        /// <summary>발굴대에 더할 (x축 각, z축 각)</summary>
        public Vector2 CurrentTiltEuler { get; private set; }
        public bool IsQuaking => _quakeTimer > 0f;
        public bool IsWarning => _running && !IsQuaking && _warned;

        public void Begin(SuikaGameManager manager)
        {
            _manager = manager;
            _running = true;
            _playTime = 0f;
            _warned = false;
            _quakeTimer = 0f;
            CurrentTiltEuler = Vector2.zero;
            _nextQuakeAt = StageAt(0f).interval;
        }

        public void Stop()
        {
            _running = false;
            _quakeTimer = 0f;
            _warned = false;
            CurrentTiltEuler = Vector2.zero;
        }

        /// <summary>판 진행 시간을 흘린다 (타이머가 도는 동안만 SuikaGameManager가 호출)</summary>
        public void Tick(float dt)
        {
            if (!_running) return;
            _playTime += dt;

            if (_quakeTimer > 0f)
            {
                _quakeTimer -= dt;
                float elapsed = _active.duration - _quakeTimer;
                // 천천히 기울었다가 끝에서 돌아옴 + 흔들림
                float envelope = Mathf.Sin(Mathf.Clamp01(elapsed / _active.duration) * Mathf.PI);
                float shake = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeRatio * SuikaSettings.ShakeIntensity;
                CurrentTiltEuler = SuikaPlate.DirToEuler(_dir, _active.tiltDeg * (envelope + shake * envelope));

                if (_quakeTimer <= 0f) EndQuake();
                return;
            }

            CurrentTiltEuler = Vector2.zero;

            if (!_warned && _playTime >= _nextQuakeAt - warningSeconds)
            {
                _warned = true;
                SuikaHaptics.Play(SuikaHaptics.QUAKE_WARNING);
                OnWarning?.Invoke(Mathf.Max(0f, _nextQuakeAt - _playTime));
            }

            if (_playTime >= _nextQuakeAt) StartQuake();
        }

        private void StartQuake()
        {
            _active = StageAt(_playTime);
            _quakeTimer = _active.duration;
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            _livesAtStart = _manager != null ? _manager.Lives : 0;
            _warned = false;
            SuikaHaptics.Play(SuikaHaptics.QUAKE);
            OnQuakeStart?.Invoke();
        }

        private void EndQuake()
        {
            _quakeTimer = 0f;
            CurrentTiltEuler = Vector2.zero;
            bool survived = _manager != null && _manager.Lives >= _livesAtStart;
            int bonus = survived ? _active.surviveBonus : 0;
            OnQuakeEnd?.Invoke(survived, bonus);
            _nextQuakeAt = _playTime + StageAt(_playTime).interval;
        }

        private Stage StageAt(float t)
        {
            Stage s = stages[0];
            for (int i = 0; i < stages.Length; i++)
                if (t >= stages[i].fromSeconds) s = stages[i];
            return s;
        }
    }
}
