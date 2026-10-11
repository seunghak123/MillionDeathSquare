using UnityEngine;

namespace Seunghak
{
    internal static class UnitySingletonRuntime
    {
        public static bool IsQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsQuitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting()
        {
            IsQuitting = true;
        }
    }

    public class UnitySingleton<T> : MonoBehaviour where T : UnityEngine.Component
    {
        private static T s_Instance = null;

        // Awake() 실행 순서는 보장되지 않아서, 다른 스크립트의 Awake()가 먼저 .Instance를
        // 참조하면 Instance getter의 FindFirstObjectByType 경로로 s_Instance가 채워지고
        // InitSingleton()이 한 번도 호출되지 않은 채로 끝날 수 있었음 — 이후 실제 이 오브젝트의
        // Awake()가 실행돼도 s_Instance가 이미 non-null이라 다시 건너뛰어져 영구히 초기화가
        // 누락됨 (예: 오브젝트 풀 딕셔너리가 끝까지 비어있는 문제).
        // 인스턴스별 플래그로 어느 경로로 들어오든 정확히 한 번만 InitSingleton()이 실행되게 보장한다.
        private bool _singletonInited = false;

        protected virtual void InitSingleton() { }

        private void EnsureInited()
        {
            if (_singletonInited) return;
            _singletonInited = true;
            InitSingleton();
        }

        private void Awake()
        {
            if (s_Instance == null)
            {
                s_Instance = this.gameObject.GetComponent<T>();
                if (s_Instance != null && transform.parent == null)
                    DontDestroyOnLoad(s_Instance.gameObject);
            }

            // 이 오브젝트가 등록된 인스턴스든 아니든, 각자 자기 자신의 InitSingleton은
            // 정확히 한 번만 실행되도록 보장 (중복 인스턴스가 있어도 안전).
            EnsureInited();
        }

        private void OnDestroy()
        {
            if (s_Instance != null && s_Instance.gameObject == gameObject)
                s_Instance = null;
        }

        public static T Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = Object.FindFirstObjectByType<T>();
                    if (s_Instance == null)
                    {
                        if (UnitySingletonRuntime.IsQuitting) return null;
                        string objectName = (typeof(T)).ToString();
                        s_Instance = new GameObject(objectName).AddComponent<T>();
                        DontDestroyOnLoad(s_Instance.gameObject);
                    }
                }

                s_Instance.GetComponent<UnitySingleton<T>>()?.EnsureInited();
                return s_Instance;
            }
        }
    }
}