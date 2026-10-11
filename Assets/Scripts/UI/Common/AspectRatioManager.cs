using UnityEngine;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 종횡비 대응 매니저.
    /// 기준 종횡비(16:9 세로 = 9:16)를 벗어나는 화면에서
    /// 카메라 Viewport를 조정하여 레터박스/필러박스를 적용합니다.
    ///
    /// 사용법: Main Camera에 이 컴포넌트를 추가.
    /// WebGL 브라우저 리사이즈에도 실시간 대응.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class AspectRatioManager : MonoBehaviour
    {
        [Header("기준 종횡비")]
        [SerializeField] private float targetAspectWidth = 9f;
        [SerializeField] private float targetAspectHeight = 16f;

        [Header("레터박스 색상")]
        [SerializeField] private Color letterboxColor = Color.black;

        [Header("모드")]
        [SerializeField] private bool enableLetterbox = true;

        [Header("레터박스 카메라 (프리팹/씬에서 미리 세팅)")]
        [SerializeField] private Camera letterboxCamera;

        private Camera _cam;
        private float _lastAspect;

        private float TargetAspect => targetAspectWidth / targetAspectHeight;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            SetupLetterboxCamera();
        }

        private void OnEnable()
        {
            UpdateViewport();
        }

        private void Update()
        {
            float currentAspect = (float)Screen.width / Screen.height;
            if (!Mathf.Approximately(currentAspect, _lastAspect))
            {
                UpdateViewport();
            }
        }

        private void SetupLetterboxCamera()
        {
            if (!enableLetterbox || letterboxCamera == null) return;

            letterboxCamera.depth = _cam.depth - 1;
            letterboxCamera.cullingMask = 0;
            letterboxCamera.clearFlags = CameraClearFlags.SolidColor;
            letterboxCamera.backgroundColor = letterboxColor;
            letterboxCamera.orthographic = true;
        }

        private void UpdateViewport()
        {
            if (!enableLetterbox)
            {
                _cam.rect = new Rect(0, 0, 1, 1);
                _lastAspect = (float)Screen.width / Screen.height;
                return;
            }

            float screenAspect = (float)Screen.width / Screen.height;
            float targetAspect = TargetAspect;
            _lastAspect = screenAspect;

            // 세로로 긴 화면(폰/토스 웹뷰)은 위아래 레터박스 없이 화면 전체를 채운다.
            // 가로로 넓은 화면(PC 브라우저)만 좌우 필러박스 유지.
            if (screenAspect <= targetAspect || Mathf.Approximately(screenAspect, targetAspect))
            {
                _cam.rect = new Rect(0, 0, 1, 1);
            }
            else if (screenAspect > targetAspect)
            {
                float viewportWidth = targetAspect / screenAspect;
                float offsetX = (1f - viewportWidth) * 0.5f;
                _cam.rect = new Rect(offsetX, 0, viewportWidth, 1);
            }
            else
            {
                float viewportHeight = screenAspect / targetAspect;
                float offsetY = (1f - viewportHeight) * 0.5f;
                _cam.rect = new Rect(0, offsetY, 1, viewportHeight);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (letterboxCamera != null)
                letterboxCamera.backgroundColor = letterboxColor;
        }
#endif
    }
}
